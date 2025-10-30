/*
 *
 * Copyright (c) 2025 Alexander Orlov.
 * 34 Middletown Ave Atlantic Highlands NJ 07716
 *
 * THIS SOFTWARE IS THE CONFIDENTIAL AND PROPRIETARY INFORMATION OF
 * Alexander Orlov. ("CONFIDENTIAL INFORMATION"). YOU SHALL NOT DISCLOSE
 * SUCH CONFIDENTIAL INFORMATION AND SHALL USE IT ONLY IN ACCORDANCE
 * WITH THE TERMS OF THE LICENSE AGREEMENT YOU ENTERED INTO WITH
 * Alexander Orlov.
 *
 * Author: Alexander Orlov
 *
 */

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using CarRental.Api.Data;
using CarRental.Api.DTOs;
using CarRental.Api.Models;
using System.Security.Claims;

namespace CarRental.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VehiclesController : ControllerBase
{
    private readonly CarRentalDbContext _context;
    private readonly ILogger<VehiclesController> _logger;

    public VehiclesController(CarRentalDbContext context, ILogger<VehiclesController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Check if the current user has admin privileges (admin or mainadmin role)
    /// </summary>
    /// <returns>True if user has admin privileges</returns>
    private bool HasAdminPrivileges()
    {
        // Try both ClaimTypes.Role and "role" claim names
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? User.FindFirst("role")?.Value;
        
        _logger.LogInformation("Checking admin privileges. Role: {Role}, User: {UserIdentity}", role, User.Identity?.Name);
        _logger.LogInformation("All claims: {Claims}", string.Join(", ", User.Claims.Select(c => $"{c.Type}={c.Value}")));
        
        return role == "admin" || role == "mainadmin";
    }

    /// <summary>
    /// Check if the current user can edit vehicles for a specific company
    /// </summary>
    /// <param name="companyId">Company ID to check</param>
    /// <returns>True if user can edit vehicles for this company</returns>
    private bool CanEditCompanyVehicles(Guid companyId)
    {
        // Try both ClaimTypes.Role and "role" claim names
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? User.FindFirst("role")?.Value;
        var userCompanyId = User.FindFirst("company_id")?.Value;

        _logger.LogInformation("Checking company edit permission. Role: {Role}, UserCompanyId: {UserCompanyId}, VehicleCompanyId: {CompanyId}", role, userCompanyId, companyId);

        // Mainadmin can edit all vehicles
        if (role == "mainadmin")
            return true;

        // Admin can only edit vehicles from their own company
        if (role == "admin" && userCompanyId != null)
            return Guid.TryParse(userCompanyId, out var parsedCompanyId) && parsedCompanyId == companyId;

        return false;
    }

    /// <summary>
    /// Get vehicle status constants
    /// </summary>
    /// <returns>List of available vehicle status values</returns>
    [HttpGet("status-constants")]
    [ProducesResponseType(typeof(object), 200)]
    public IActionResult GetVehicleStatusConstants()
    {
        var statusConstants = new
        {
            Available = VehicleStatusConstants.Available,
            Rented = VehicleStatusConstants.Rented,
            Maintenance = VehicleStatusConstants.Maintenance,
            OutOfService = VehicleStatusConstants.OutOfService,
            Cleaning = VehicleStatusConstants.Cleaning
        };

        return Ok(statusConstants);
    }

    /// <summary>
    /// Get all vehicles with optional filtering
    /// </summary>
    /// <param name="companyId">Filter by company ID</param>
    /// <param name="status">Filter by status</param>
    /// <param name="location">Filter by location</param>
    /// <param name="minPrice">Minimum daily rate</param>
    /// <param name="maxPrice">Maximum daily rate</param>
    /// <param name="page">Page number</param>
    /// <param name="pageSize">Page size</param>
    /// <returns>List of vehicles</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<VehicleDto>), 200)]
    [ProducesResponseType(401)] // Unauthorized
    public async Task<IActionResult> GetVehicles(
        [FromQuery] Guid? companyId = null,
        [FromQuery] Guid? categoryId = null,
        [FromQuery] string? make = null,
        [FromQuery] string? model = null,
        [FromQuery] string? status = null,
        [FromQuery] string? location = null,
        [FromQuery] decimal? minPrice = null,
        [FromQuery] decimal? maxPrice = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            var query = _context.Vehicles
                .Include(v => v.Company)
                .Where(v => v.Status != VehicleStatus.OutOfService)
                .AsQueryable();

            if (companyId.HasValue)
                query = query.Where(v => v.CompanyId == companyId.Value);

            if (!string.IsNullOrEmpty(status))
                query = query.Where(v => v.Status.ToString() == status);

            if (!string.IsNullOrEmpty(location))
                query = query.Where(v => v.Location != null && v.Location.Contains(location));

            if (minPrice.HasValue)
                query = query.Where(v => v.DailyRate >= minPrice.Value);

            if (maxPrice.HasValue)
                query = query.Where(v => v.DailyRate <= maxPrice.Value);

            // Filter by make/model if provided
            if (!string.IsNullOrEmpty(make))
                query = query.Where(v => v.Make.ToUpper() == make.ToUpper());

            if (!string.IsNullOrEmpty(model))
                query = query.Where(v => v.Model.ToUpper() == model.ToUpper());

            // Filter by categoryId through models table
            if (categoryId.HasValue)
            {
                // Use a join to filter vehicles that match models with this category
                // This approach can be translated to SQL by EF Core
                query = query.Where(v => _context.Models
                    .Any(m => m.CategoryId == categoryId.Value &&
                             m.Make.ToUpper() == v.Make.ToUpper() &&
                             m.ModelName.ToUpper() == v.Model.ToUpper() &&
                             m.Year == v.Year));
            }

            var totalCount = await query.CountAsync();

            var vehiclesList = await query
                .OrderBy(v => v.DailyRate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Get all models needed for category and fuel type lookup
            // Since we can't use client-side collections in LINQ, we'll query models separately
            // and match in memory
            List<Model> modelsDict;
            
            if (vehiclesList.Any())
            {
                // Get all unique make/model/year combinations from vehicles
                var vehicleCombos = vehiclesList
                    .Select(v => new { Make = v.Make.ToUpper(), Model = v.Model.ToUpper(), v.Year })
                    .Distinct()
                    .ToList();

                // Fetch all models that might match (we'll filter in memory)
                // This is more efficient than querying for each combination separately
                var allMakeModelYears = vehicleCombos.Select(c => c.Year).Distinct().ToList();
                var allMakes = vehicleCombos.Select(c => c.Make).Distinct().ToList();
                var allModels = vehicleCombos.Select(c => c.Model).Distinct().ToList();
                
                var potentialModels = await _context.Models
                    .Include(m => m.Category)
                    .Where(m => allMakes.Contains(m.Make.ToUpper()) && 
                               allModels.Contains(m.ModelName.ToUpper()) && 
                               allMakeModelYears.Contains(m.Year))
                    .ToListAsync();

                // Filter modelsDict in memory to match vehicle combinations
                modelsDict = potentialModels
                    .Where(m => vehicleCombos.Any(v => 
                        v.Make == m.Make.ToUpper() && 
                        v.Model == m.ModelName.ToUpper() && 
                        v.Year == m.Year))
                    .ToList();
            }
            else
            {
                modelsDict = new List<Model>();
            }

            var vehicles = vehiclesList.Select(v =>
            {
                var matchingModel = modelsDict.FirstOrDefault(m => 
                    m.Make.ToUpper() == v.Make.ToUpper() && 
                    m.ModelName.ToUpper() == v.Model.ToUpper() && 
                    m.Year == v.Year);

                return new VehicleDto
                {
                    VehicleId = v.Id,
                    CompanyId = v.CompanyId,
                    CompanyName = v.Company.CompanyName,
                    CategoryId = matchingModel?.CategoryId,
                    CategoryName = matchingModel?.Category?.CategoryName,
                    Make = v.Make,
                    Model = v.Model,
                    Year = v.Year,
                    Color = v.Color,
                    LicensePlate = v.LicensePlate,
                    Vin = v.Vin,
                    Mileage = v.Mileage,
                    FuelType = matchingModel?.FuelType,
                    Transmission = v.Transmission,
                    Seats = v.Seats,
                    DailyRate = v.DailyRate,
                    Status = v.Status.ToString(),
                    State = v.State,
                    Location = v.Location,
                    ImageUrl = v.ImageUrl,
                    Features = v.Features,
                    CreatedAt = v.CreatedAt,
                    UpdatedAt = v.UpdatedAt
                };
            }).ToList();

            return Ok(new
            {
                Vehicles = vehicles,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving vehicles");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get a specific vehicle by ID
    /// </summary>
    /// <param name="id">Vehicle ID</param>
    /// <returns>Vehicle details</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(VehicleDto), 200)]
    [ProducesResponseType(401)] // Unauthorized
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetVehicle(Guid id)
    {
        try
        {
            var vehicle = await _context.Vehicles
                .Include(v => v.Company)
                .FirstOrDefaultAsync(v => v.Id == id);

            if (vehicle == null)
                return NotFound();

            // Get category and fuel type from models table
            var matchingModel = await _context.Models
                .Include(m => m.Category)
                .FirstOrDefaultAsync(m => 
                    m.Make.ToUpper() == vehicle.Make.ToUpper() && 
                    m.ModelName.ToUpper() == vehicle.Model.ToUpper() && 
                    m.Year == vehicle.Year);

            var vehicleDto = new VehicleDto
            {
                VehicleId = vehicle.Id,
                CompanyId = vehicle.CompanyId,
                CompanyName = vehicle.Company.CompanyName,
                CategoryId = matchingModel?.CategoryId,
                CategoryName = matchingModel?.Category?.CategoryName,
                Make = vehicle.Make,
                Model = vehicle.Model,
                Year = vehicle.Year,
                Color = vehicle.Color,
                LicensePlate = vehicle.LicensePlate,
                Vin = vehicle.Vin,
                Mileage = vehicle.Mileage,
                FuelType = matchingModel?.FuelType,
                Transmission = vehicle.Transmission,
                Seats = vehicle.Seats,
                DailyRate = vehicle.DailyRate,
                Status = vehicle.Status.ToString(),
                State = vehicle.State,
                Location = vehicle.Location,
                ImageUrl = vehicle.ImageUrl,
                Features = vehicle.Features,
                CreatedAt = vehicle.CreatedAt,
                UpdatedAt = vehicle.UpdatedAt
            };

            return Ok(vehicleDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving vehicle {VehicleId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Create a new vehicle (Admin only)
    /// </summary>
    /// <param name="createVehicleDto">Vehicle creation data</param>
    /// <returns>Created vehicle</returns>
    [HttpPost]
    [Authorize] // Require authentication for creating vehicles
    [ProducesResponseType(typeof(VehicleDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)] // Unauthorized
    [ProducesResponseType(403)] // Forbidden
    public async Task<IActionResult> CreateVehicle([FromBody] CreateVehicleDto createVehicleDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // Check if user has admin privileges
            if (!HasAdminPrivileges())
                return Forbid("Only admin and mainadmin users can create vehicles");

            // Check if user can edit vehicles for this company
            if (!CanEditCompanyVehicles(createVehicleDto.CompanyId))
                return Forbid("You can only create vehicles for your own company");

            // Check if company exists
            var company = await _context.Companies.FindAsync(createVehicleDto.CompanyId);
            if (company == null)
                return BadRequest("Company not found");

            // Check if license plate is unique
            var existingVehicle = await _context.Vehicles
                .FirstOrDefaultAsync(v => v.LicensePlate == createVehicleDto.LicensePlate);
            if (existingVehicle != null)
                return BadRequest("License plate already exists");

            var vehicle = new Vehicle
            {
                CompanyId = createVehicleDto.CompanyId,
                Make = createVehicleDto.Make,
                Model = createVehicleDto.Model,
                Year = createVehicleDto.Year,
                Color = createVehicleDto.Color,
                LicensePlate = createVehicleDto.LicensePlate,
                Vin = createVehicleDto.Vin,
                Mileage = createVehicleDto.Mileage,
                Transmission = createVehicleDto.Transmission,
                Seats = createVehicleDto.Seats,
                DailyRate = createVehicleDto.DailyRate,
                Status = Enum.TryParse<VehicleStatus>(createVehicleDto.Status, out var statusEnum) ? statusEnum : VehicleStatus.Available,
                State = createVehicleDto.State,
                Location = createVehicleDto.Location,
                ImageUrl = createVehicleDto.ImageUrl,
                Features = createVehicleDto.Features
            };

            _context.Vehicles.Add(vehicle);
            await _context.SaveChangesAsync();

            // Load related data for response
            await _context.Entry(vehicle)
                .Reference(v => v.Company)
                .LoadAsync();

            // Get category and fuel type from models table
            var matchingModel = await _context.Models
                .Include(m => m.Category)
                .FirstOrDefaultAsync(m => 
                    m.Make.ToUpper() == vehicle.Make.ToUpper() && 
                    m.ModelName.ToUpper() == vehicle.Model.ToUpper() && 
                    m.Year == vehicle.Year);

            var vehicleDto = new VehicleDto
            {
                VehicleId = vehicle.Id,
                CompanyId = vehicle.CompanyId,
                CompanyName = vehicle.Company.CompanyName,
                CategoryId = matchingModel?.CategoryId,
                CategoryName = matchingModel?.Category?.CategoryName,
                Make = vehicle.Make,
                Model = vehicle.Model,
                Year = vehicle.Year,
                Color = vehicle.Color,
                LicensePlate = vehicle.LicensePlate,
                Vin = vehicle.Vin,
                Mileage = vehicle.Mileage,
                FuelType = matchingModel?.FuelType,
                Transmission = vehicle.Transmission,
                Seats = vehicle.Seats,
                DailyRate = vehicle.DailyRate,
                Status = vehicle.Status.ToString(),
                State = vehicle.State,
                Location = vehicle.Location,
                ImageUrl = vehicle.ImageUrl,
                Features = vehicle.Features,
                CreatedAt = vehicle.CreatedAt,
                UpdatedAt = vehicle.UpdatedAt
            };

            return CreatedAtAction(nameof(GetVehicle), new { id = vehicle.Id }, vehicleDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating vehicle");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Update an existing vehicle (Admin only)
    /// </summary>
    /// <param name="id">Vehicle ID</param>
    /// <param name="updateVehicleDto">Vehicle update data</param>
    /// <returns>Updated vehicle</returns>
    [HttpPut("{id}")]
    [Authorize] // Require authentication for updating vehicles
    [ProducesResponseType(typeof(VehicleDto), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)] // Unauthorized
    [ProducesResponseType(403)] // Forbidden
    public async Task<IActionResult> UpdateVehicle(Guid id, [FromBody] UpdateVehicleDto updateVehicleDto)
    {
        try
        {
            var vehicle = await _context.Vehicles.FindAsync(id);
            if (vehicle == null)
                return NotFound();

            // Check if user has admin privileges
            if (!HasAdminPrivileges())
                return Forbid("Only admin and mainadmin users can update vehicles");

            // Check if user can edit vehicles for this company
            if (!CanEditCompanyVehicles(vehicle.CompanyId))
                return Forbid("You can only update vehicles from your own company");

            // Update properties if provided
            if (updateVehicleDto.Year.HasValue)
                vehicle.Year = updateVehicleDto.Year.Value;

            if (updateVehicleDto.Color != null)
                vehicle.Color = updateVehicleDto.Color;

            if (updateVehicleDto.Vin != null)
                vehicle.Vin = updateVehicleDto.Vin;

            if (updateVehicleDto.Mileage.HasValue)
                vehicle.Mileage = updateVehicleDto.Mileage.Value;

            if (updateVehicleDto.Transmission != null)
                vehicle.Transmission = updateVehicleDto.Transmission;

            if (updateVehicleDto.Seats.HasValue)
                vehicle.Seats = updateVehicleDto.Seats.Value;

            if (updateVehicleDto.DailyRate.HasValue)
                vehicle.DailyRate = updateVehicleDto.DailyRate.Value;

            if (updateVehicleDto.Status != null)
            {
                if (Enum.TryParse<VehicleStatus>(updateVehicleDto.Status, out var statusEnum))
                    vehicle.Status = statusEnum;
            }

            if (updateVehicleDto.Location != null)
                vehicle.Location = updateVehicleDto.Location;

            if (updateVehicleDto.ImageUrl != null)
                vehicle.ImageUrl = updateVehicleDto.ImageUrl;

            if (updateVehicleDto.Features != null)
                vehicle.Features = updateVehicleDto.Features;

            // Note: IsActive has been replaced with Status field
            // Use Status = OutOfService instead of IsActive = false

            vehicle.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Load related data for response
            await _context.Entry(vehicle)
                .Reference(v => v.Company)
                .LoadAsync();

            // Get category and fuel type from models table
            var matchingModel = await _context.Models
                .Include(m => m.Category)
                .FirstOrDefaultAsync(m => 
                    m.Make.ToUpper() == vehicle.Make.ToUpper() && 
                    m.ModelName.ToUpper() == vehicle.Model.ToUpper() && 
                    m.Year == vehicle.Year);

            var vehicleDto = new VehicleDto
            {
                VehicleId = vehicle.Id,
                CompanyId = vehicle.CompanyId,
                CompanyName = vehicle.Company.CompanyName,
                CategoryId = matchingModel?.CategoryId,
                CategoryName = matchingModel?.Category?.CategoryName,
                Make = vehicle.Make,
                Model = vehicle.Model,
                Year = vehicle.Year,
                Color = vehicle.Color,
                LicensePlate = vehicle.LicensePlate,
                Vin = vehicle.Vin,
                Mileage = vehicle.Mileage,
                FuelType = matchingModel?.FuelType,
                Transmission = vehicle.Transmission,
                Seats = vehicle.Seats,
                DailyRate = vehicle.DailyRate,
                Status = vehicle.Status.ToString(),
                State = vehicle.State,
                Location = vehicle.Location,
                ImageUrl = vehicle.ImageUrl,
                Features = vehicle.Features,
                CreatedAt = vehicle.CreatedAt,
                UpdatedAt = vehicle.UpdatedAt
            };

            return Ok(vehicleDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating vehicle {VehicleId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Delete a vehicle (Admin only)
    /// </summary>
    /// <param name="id">Vehicle ID</param>
    /// <returns>No content</returns>
    [HttpDelete("{id}")]
    [Authorize] // Require authentication for deleting vehicles
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    [ProducesResponseType(401)] // Unauthorized
    [ProducesResponseType(403)] // Forbidden
    public async Task<IActionResult> DeleteVehicle(Guid id)
    {
        try
        {
            var vehicle = await _context.Vehicles.FindAsync(id);
            if (vehicle == null)
                return NotFound();

            // Check if user has admin privileges
            if (!HasAdminPrivileges())
                return Forbid("Only admin and mainadmin users can delete vehicles");

            // Check if user can edit vehicles for this company
            if (!CanEditCompanyVehicles(vehicle.CompanyId))
                return Forbid("You can only delete vehicles from your own company");

            _context.Vehicles.Remove(vehicle);
            await _context.SaveChangesAsync();

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting vehicle {VehicleId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get list of unique vehicle makes
    /// </summary>
    /// <returns>List of unique vehicle makes</returns>
    [HttpGet("makes")]
    [ProducesResponseType(typeof(IEnumerable<string>), 200)]
    public async Task<IActionResult> GetVehicleMakes()
    {
        try
        {
            var makes = await _context.Vehicles
                .Where(v => v.Status != VehicleStatus.OutOfService && !string.IsNullOrEmpty(v.Make))
                .Select(v => v.Make)
                .Distinct()
                .OrderBy(m => m)
                .ToListAsync();

            return Ok(makes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving vehicle makes");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get list of unique vehicle locations
    /// </summary>
    /// <returns>List of unique vehicle locations</returns>
    [HttpGet("locations")]
    [ProducesResponseType(typeof(IEnumerable<string>), 200)]
    public async Task<IActionResult> GetVehicleLocations()
    {
        try
        {
            var locations = await _context.Vehicles
                .Where(v => v.Status != VehicleStatus.OutOfService && !string.IsNullOrEmpty(v.Location))
                .Select(v => v.Location!)
                .Distinct()
                .OrderBy(l => l)
                .ToListAsync();

            return Ok(locations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving vehicle locations");
            return StatusCode(500, "Internal server error");
        }
    }
}
