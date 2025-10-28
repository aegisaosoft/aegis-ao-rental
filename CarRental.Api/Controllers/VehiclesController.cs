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
    /// Get all vehicles with optional filtering
    /// </summary>
    /// <param name="companyId">Filter by company ID</param>
    /// <param name="categoryId">Filter by category ID</param>
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
                .Include(v => v.Category)
                .Where(v => v.IsActive)
                .AsQueryable();

            if (companyId.HasValue)
                query = query.Where(v => v.CompanyId == companyId.Value);

            if (categoryId.HasValue)
                query = query.Where(v => v.CategoryId == categoryId.Value);

            if (!string.IsNullOrEmpty(status))
                query = query.Where(v => v.Status == status);

            if (!string.IsNullOrEmpty(location))
                query = query.Where(v => v.Location != null && v.Location.Contains(location));

            if (minPrice.HasValue)
                query = query.Where(v => v.DailyRate >= minPrice.Value);

            if (maxPrice.HasValue)
                query = query.Where(v => v.DailyRate <= maxPrice.Value);

            var totalCount = await query.CountAsync();

            var vehicles = await query
                .OrderBy(v => v.DailyRate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(v => new VehicleDto
                {
                    VehicleId = v.VehicleId,
                    CompanyId = v.CompanyId,
                    CompanyName = v.Company.CompanyName,
                    CategoryId = v.CategoryId,
                    CategoryName = v.Category != null ? v.Category.CategoryName : null,
                    Make = v.Make,
                    Model = v.Model,
                    Year = v.Year,
                    Color = v.Color,
                    LicensePlate = v.LicensePlate,
                    Vin = v.Vin,
                    Mileage = v.Mileage,
                    FuelType = v.FuelType,
                    Transmission = v.Transmission,
                    Seats = v.Seats,
                    DailyRate = v.DailyRate,
                    Status = v.Status,
                    State = v.State,
                    Location = v.Location,
                    ImageUrl = v.ImageUrl,
                    Features = v.Features,
                    IsActive = v.IsActive,
                    CreatedAt = v.CreatedAt,
                    UpdatedAt = v.UpdatedAt
                })
                .ToListAsync();

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
                .Include(v => v.Category)
                .FirstOrDefaultAsync(v => v.VehicleId == id);

            if (vehicle == null)
                return NotFound();

            var vehicleDto = new VehicleDto
            {
                VehicleId = vehicle.VehicleId,
                CompanyId = vehicle.CompanyId,
                CompanyName = vehicle.Company.CompanyName,
                CategoryId = vehicle.CategoryId,
                CategoryName = vehicle.Category?.CategoryName,
                Make = vehicle.Make,
                Model = vehicle.Model,
                Year = vehicle.Year,
                Color = vehicle.Color,
                LicensePlate = vehicle.LicensePlate,
                Vin = vehicle.Vin,
                Mileage = vehicle.Mileage,
                FuelType = vehicle.FuelType,
                Transmission = vehicle.Transmission,
                Seats = vehicle.Seats,
                DailyRate = vehicle.DailyRate,
                Status = vehicle.Status,
                State = vehicle.State,
                Location = vehicle.Location,
                ImageUrl = vehicle.ImageUrl,
                Features = vehicle.Features,
                IsActive = vehicle.IsActive,
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
            var company = await _context.RentalCompanies.FindAsync(createVehicleDto.CompanyId);
            if (company == null)
                return BadRequest("Company not found");

            // Check if category exists (if provided)
            if (createVehicleDto.CategoryId.HasValue)
            {
                var category = await _context.VehicleCategories.FindAsync(createVehicleDto.CategoryId.Value);
                if (category == null)
                    return BadRequest("Vehicle category not found");
            }

            // Check if license plate is unique
            var existingVehicle = await _context.Vehicles
                .FirstOrDefaultAsync(v => v.LicensePlate == createVehicleDto.LicensePlate);
            if (existingVehicle != null)
                return BadRequest("License plate already exists");

            var vehicle = new Vehicle
            {
                CompanyId = createVehicleDto.CompanyId,
                CategoryId = createVehicleDto.CategoryId,
                Make = createVehicleDto.Make,
                Model = createVehicleDto.Model,
                Year = createVehicleDto.Year,
                Color = createVehicleDto.Color,
                LicensePlate = createVehicleDto.LicensePlate,
                Vin = createVehicleDto.Vin,
                Mileage = createVehicleDto.Mileage,
                FuelType = createVehicleDto.FuelType,
                Transmission = createVehicleDto.Transmission,
                Seats = createVehicleDto.Seats,
                DailyRate = createVehicleDto.DailyRate,
                Status = createVehicleDto.Status,
                State = createVehicleDto.State,
                Location = createVehicleDto.Location,
                ImageUrl = createVehicleDto.ImageUrl,
                Features = createVehicleDto.Features,
                IsActive = createVehicleDto.IsActive
            };

            _context.Vehicles.Add(vehicle);
            await _context.SaveChangesAsync();

            // Load related data for response
            await _context.Entry(vehicle)
                .Reference(v => v.Company)
                .LoadAsync();
            await _context.Entry(vehicle)
                .Reference(v => v.Category)
                .LoadAsync();

            var vehicleDto = new VehicleDto
            {
                VehicleId = vehicle.VehicleId,
                CompanyId = vehicle.CompanyId,
                CompanyName = vehicle.Company.CompanyName,
                CategoryId = vehicle.CategoryId,
                CategoryName = vehicle.Category?.CategoryName,
                Make = vehicle.Make,
                Model = vehicle.Model,
                Year = vehicle.Year,
                Color = vehicle.Color,
                LicensePlate = vehicle.LicensePlate,
                Vin = vehicle.Vin,
                Mileage = vehicle.Mileage,
                FuelType = vehicle.FuelType,
                Transmission = vehicle.Transmission,
                Seats = vehicle.Seats,
                DailyRate = vehicle.DailyRate,
                Status = vehicle.Status,
                State = vehicle.State,
                Location = vehicle.Location,
                ImageUrl = vehicle.ImageUrl,
                Features = vehicle.Features,
                IsActive = vehicle.IsActive,
                CreatedAt = vehicle.CreatedAt,
                UpdatedAt = vehicle.UpdatedAt
            };

            return CreatedAtAction(nameof(GetVehicle), new { id = vehicle.VehicleId }, vehicleDto);
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

            // Check if category is being changed and bulk update is requested
            bool shouldBulkUpdateCategory = false;
            Guid? newCategoryId = null;
            if (updateVehicleDto.CategoryId.HasValue && 
                updateVehicleDto.CategoryId.Value != vehicle.CategoryId &&
                !string.IsNullOrEmpty(updateVehicleDto.Make) && 
                !string.IsNullOrEmpty(updateVehicleDto.Model))
            {
                shouldBulkUpdateCategory = true;
                newCategoryId = updateVehicleDto.CategoryId.Value;
            }

            // Update properties if provided
            if (updateVehicleDto.CategoryId.HasValue)
                vehicle.CategoryId = updateVehicleDto.CategoryId.Value;

            if (updateVehicleDto.Year.HasValue)
                vehicle.Year = updateVehicleDto.Year.Value;

            if (updateVehicleDto.Color != null)
                vehicle.Color = updateVehicleDto.Color;

            if (updateVehicleDto.Vin != null)
                vehicle.Vin = updateVehicleDto.Vin;

            if (updateVehicleDto.Mileage.HasValue)
                vehicle.Mileage = updateVehicleDto.Mileage.Value;

            if (updateVehicleDto.FuelType != null)
                vehicle.FuelType = updateVehicleDto.FuelType;

            if (updateVehicleDto.Transmission != null)
                vehicle.Transmission = updateVehicleDto.Transmission;

            if (updateVehicleDto.Seats.HasValue)
                vehicle.Seats = updateVehicleDto.Seats.Value;

            if (updateVehicleDto.DailyRate.HasValue)
                vehicle.DailyRate = updateVehicleDto.DailyRate.Value;

            if (updateVehicleDto.Status != null)
                vehicle.Status = updateVehicleDto.Status;

            if (updateVehicleDto.Location != null)
                vehicle.Location = updateVehicleDto.Location;

            if (updateVehicleDto.ImageUrl != null)
                vehicle.ImageUrl = updateVehicleDto.ImageUrl;

            if (updateVehicleDto.Features != null)
                vehicle.Features = updateVehicleDto.Features;

            if (updateVehicleDto.IsActive.HasValue)
                vehicle.IsActive = updateVehicleDto.IsActive.Value;

            vehicle.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // If category changed and make/model provided, update all matching vehicles
            if (shouldBulkUpdateCategory && newCategoryId.HasValue)
            {
                var matchingVehicles = await _context.Vehicles
                    .Where(v => v.Make == vehicle.Make && v.Model == vehicle.Model && v.VehicleId != id)
                    .ToListAsync();

                foreach (var v in matchingVehicles)
                {
                    v.CategoryId = newCategoryId.Value;
                    v.UpdatedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                
                _logger.LogInformation(
                    "Bulk updated category for {Count} vehicles with make '{Make}' and model '{Model}' to category {CategoryId}",
                    matchingVehicles.Count, vehicle.Make, vehicle.Model, newCategoryId.Value);
            }

            // Load related data for response
            await _context.Entry(vehicle)
                .Reference(v => v.Company)
                .LoadAsync();
            await _context.Entry(vehicle)
                .Reference(v => v.Category)
                .LoadAsync();

            var vehicleDto = new VehicleDto
            {
                VehicleId = vehicle.VehicleId,
                CompanyId = vehicle.CompanyId,
                CompanyName = vehicle.Company.CompanyName,
                CategoryId = vehicle.CategoryId,
                CategoryName = vehicle.Category?.CategoryName,
                Make = vehicle.Make,
                Model = vehicle.Model,
                Year = vehicle.Year,
                Color = vehicle.Color,
                LicensePlate = vehicle.LicensePlate,
                Vin = vehicle.Vin,
                Mileage = vehicle.Mileage,
                FuelType = vehicle.FuelType,
                Transmission = vehicle.Transmission,
                Seats = vehicle.Seats,
                DailyRate = vehicle.DailyRate,
                Status = vehicle.Status,
                State = vehicle.State,
                Location = vehicle.Location,
                ImageUrl = vehicle.ImageUrl,
                Features = vehicle.Features,
                IsActive = vehicle.IsActive,
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
                .Where(v => v.IsActive && !string.IsNullOrEmpty(v.Make))
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
                .Where(v => v.IsActive && !string.IsNullOrEmpty(v.Location))
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
