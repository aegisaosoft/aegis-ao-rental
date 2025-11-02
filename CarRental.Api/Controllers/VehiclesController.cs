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
            _logger.LogInformation("GetVehicles called with companyId={CompanyId}, status={Status}, pageSize={PageSize}", 
                companyId, status, pageSize);

            // Query vehicles with joins to vehicle_model (Model loaded separately)
            var query = _context.Vehicles
                .Include(v => v.VehicleModel)
                .Where(v => v.Status != VehicleStatus.OutOfService)
                .AsQueryable();

            if (companyId.HasValue)
                query = query.Where(v => v.CompanyId == companyId.Value);

            if (!string.IsNullOrEmpty(status))
                query = query.Where(v => v.Status.ToString() == status);

            if (!string.IsNullOrEmpty(location))
                query = query.Where(v => v.Location != null && v.Location.Contains(location));

            // Filter by daily_rate from vehicle_model
            if (minPrice.HasValue)
                query = query.Where(v => v.VehicleModel != null && v.VehicleModel.DailyRate >= minPrice.Value);

            if (maxPrice.HasValue)
                query = query.Where(v => v.VehicleModel != null && v.VehicleModel.DailyRate <= maxPrice.Value);

            // Filter by make/model if provided (case-insensitive using ILIKE for PostgreSQL)
            if (!string.IsNullOrEmpty(make))
            {
                query = query.Where(v => v.VehicleModel != null && v.VehicleModel.Model != null && 
                    EF.Functions.ILike(v.VehicleModel.Model.Make, $"%{make}%"));
            }

            if (!string.IsNullOrEmpty(model))
            {
                query = query.Where(v => v.VehicleModel != null && v.VehicleModel.Model != null && 
                    EF.Functions.ILike(v.VehicleModel.Model.ModelName, $"%{model}%"));
            }

            // Note: categoryId filtering will be done after fetching vehicles
            // to avoid EF Core translation issues with ToUpper() in subqueries

            var totalCount = await query.CountAsync();

            // Get paginated vehicles first (before loading relations)
            var vehiclesData = await query
                .OrderBy(v => v.VehicleModel != null ? v.VehicleModel.DailyRate : 0)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
            
            // Load Model and Category for each VehicleModel that has one
            foreach (var vehicle in vehiclesData.Where(v => v.VehicleModel != null))
            {
                var vm = vehicle.VehicleModel!;
                await _context.Entry(vm)
                    .Reference(v => v.Model)
                    .LoadAsync();
                
                if (vm.Model != null)
                {
                    await _context.Entry(vm.Model)
                        .Reference(m => m.Category)
                        .LoadAsync();
                }
            }
            
            // Fetch company names separately to avoid navigation property issues
            var companyIds = vehiclesData.Select(v => v.CompanyId).Distinct().ToList();
            var companies = await _context.Companies
                .Where(c => companyIds.Contains(c.Id))
                .Select(c => new { c.Id, c.CompanyName })
                .ToDictionaryAsync(c => c.Id, c => c.CompanyName);

            // Map vehicles to DTOs using vehicle_model and models data
            var vehicles = vehiclesData.Select(v =>
            {
                // Get company name from dictionary
                companies.TryGetValue(v.CompanyId, out var companyName);

                // Get model info from vehicle_model catalog
                var vm = v.VehicleModel;
                var model = vm?.Model;

                return new VehicleDto
                {
                    VehicleId = v.Id,
                    CompanyId = v.CompanyId,
                    CompanyName = companyName ?? "",
                    CategoryId = model?.CategoryId,
                    CategoryName = model?.Category?.CategoryName,
                    Make = model?.Make ?? "",
                    Model = model?.ModelName ?? "",
                    Year = model?.Year ?? 0,
                    Color = v.Color,
                    LicensePlate = v.LicensePlate,
                    Vin = v.Vin,
                    Mileage = v.Mileage,
                    FuelType = model?.FuelType,
                    Transmission = v.Transmission,
                    Seats = v.Seats,
                    DailyRate = vm?.DailyRate ?? 0, // Rate from catalog
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
                .Include(v => v.VehicleModel)
                .FirstOrDefaultAsync(v => v.Id == id);

            if (vehicle == null)
                return NotFound();
            
            // Load Model and Category if VehicleModel has one
            if (vehicle.VehicleModel != null)
            {
                await _context.Entry(vehicle.VehicleModel)
                    .Reference(vm => vm.Model)
                    .LoadAsync();
                
                if (vehicle.VehicleModel.Model != null)
                {
                    await _context.Entry(vehicle.VehicleModel.Model)
                        .Reference(m => m.Category)
                        .LoadAsync();
                }
            }

            // Get model info from vehicle_model catalog
            var vm = vehicle.VehicleModel;
            var model = vm?.Model;

            var vehicleDto = new VehicleDto
            {
                VehicleId = vehicle.Id,
                CompanyId = vehicle.CompanyId,
                CompanyName = vehicle.Company.CompanyName,
                CategoryId = model?.CategoryId,
                CategoryName = model?.Category?.CategoryName,
                Make = model?.Make ?? "",
                Model = model?.ModelName ?? "",
                Year = model?.Year ?? 0,
                Color = vehicle.Color,
                LicensePlate = vehicle.LicensePlate,
                Vin = vehicle.Vin,
                Mileage = vehicle.Mileage,
                FuelType = model?.FuelType,
                Transmission = vehicle.Transmission,
                Seats = vehicle.Seats,
                DailyRate = vm?.DailyRate ?? 0, // Rate from catalog
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

            // Find or create the model
            var model = await _context.Models
                .Include(m => m.Category)
                .FirstOrDefaultAsync(m => 
                    m.Make.ToUpper() == createVehicleDto.Make.ToUpper() && 
                    m.ModelName.ToUpper() == createVehicleDto.Model.ToUpper() && 
                    m.Year == createVehicleDto.Year);

            if (model == null)
                return BadRequest($"Model not found: {createVehicleDto.Make} {createVehicleDto.Model} {createVehicleDto.Year}");

            // Find or create vehicle_model catalog entry
            var vehicleModel = await _context.VehicleModels
                .FirstOrDefaultAsync(vm => vm.ModelId == model.Id && vm.CompanyId == createVehicleDto.CompanyId);
            if (vehicleModel == null)
            {
                vehicleModel = new VehicleModel
                {
                    CompanyId = createVehicleDto.CompanyId,
                    ModelId = model.Id,
                    DailyRate = createVehicleDto.DailyRate
                };
                _context.VehicleModels.Add(vehicleModel);
                await _context.SaveChangesAsync();
            }

            var vehicle = new Vehicle
            {
                CompanyId = createVehicleDto.CompanyId,
                Color = createVehicleDto.Color,
                LicensePlate = createVehicleDto.LicensePlate,
                Vin = createVehicleDto.Vin,
                Mileage = createVehicleDto.Mileage,
                Transmission = createVehicleDto.Transmission,
                Seats = createVehicleDto.Seats,
                VehicleModelId = vehicleModel.Id,
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

            await _context.Entry(vehicle)
                .Reference(v => v.VehicleModel)
                .LoadAsync();

            var vehicleDto = new VehicleDto
            {
                VehicleId = vehicle.Id,
                CompanyId = vehicle.CompanyId,
                CompanyName = vehicle.Company.CompanyName,
                CategoryId = model.CategoryId,
                CategoryName = model.Category?.CategoryName,
                Make = model.Make,
                Model = model.ModelName,
                Year = model.Year,
                Color = vehicle.Color,
                LicensePlate = vehicle.LicensePlate,
                Vin = vehicle.Vin,
                Mileage = vehicle.Mileage,
                FuelType = model.FuelType,
                Transmission = vehicle.Transmission,
                Seats = vehicle.Seats,
                DailyRate = vehicleModel.DailyRate ?? 0,
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

            // Note: DailyRate is stored in vehicle_model catalog, not on individual vehicles

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

            await _context.Entry(vehicle)
                .Reference(v => v.VehicleModel)
                .LoadAsync();

            // Get model info from vehicle_model catalog
            var vm = vehicle.VehicleModel;
            Model? model = null;
            
            if (vm != null)
            {
                await _context.Entry(vm)
                    .Reference(v => v.Model)
                    .LoadAsync();
                
                model = vm.Model;
                if (model != null)
                {
                    await _context.Entry(model)
                        .Reference(m => m.Category)
                        .LoadAsync();
                }
            }

            var vehicleDto = new VehicleDto
            {
                VehicleId = vehicle.Id,
                CompanyId = vehicle.CompanyId,
                CompanyName = vehicle.Company.CompanyName,
                CategoryId = model?.CategoryId,
                CategoryName = model?.Category?.CategoryName,
                Make = model?.Make ?? "",
                Model = model?.ModelName ?? "",
                Year = model?.Year ?? 0,
                Color = vehicle.Color,
                LicensePlate = vehicle.LicensePlate,
                Vin = vehicle.Vin,
                Mileage = vehicle.Mileage,
                FuelType = model?.FuelType,
                Transmission = vehicle.Transmission,
                Seats = vehicle.Seats,
                DailyRate = vm?.DailyRate ?? 0, // Rate from catalog
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
            var makes = await _context.Models
                .Where(m => !string.IsNullOrEmpty(m.Make))
                .Select(m => m.Make)
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

    /// <summary>
    /// Bulk update daily rates for vehicles matching specific criteria
    /// </summary>
    /// <param name="request">Bulk update request with filters and new daily rate</param>
    /// <returns>Number of vehicles updated</returns>
    [HttpPut("bulk-update-daily-rate")]
    [Authorize]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> BulkUpdateDailyRate([FromBody] BulkUpdateDailyRateDto request)
    {
        try
        {
            _logger.LogInformation("VehiclesController.BulkUpdateDailyRate called with CompanyId={CompanyId}, CategoryId={CategoryId}, DailyRate={DailyRate}", 
                request.CompanyId, request.CategoryId, request.DailyRate);

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // Check if user has admin privileges
            if (!HasAdminPrivileges())
                return Forbid("Only admin and mainadmin users can bulk update vehicles");

            // Check if user can edit vehicles for this company
            if (request.CompanyId.HasValue && !CanEditCompanyVehicles(request.CompanyId.Value))
                return Forbid("You can only update vehicles from your own company");

            // Use simple raw SQL UPDATE with JOIN for efficiency
            var sql = new System.Text.StringBuilder();
            sql.Append("UPDATE vehicle_model vm ");
            sql.Append("SET daily_rate = {0} ");
            sql.Append("FROM vehicles v ");
            sql.Append("INNER JOIN models m ON m.id = vm.model_id ");
            sql.Append("WHERE vm.id = v.vehicle_model_id ");

            var parameters = new List<object> { request.DailyRate };

            var paramIndex = 1;

            // Add company filter if provided
            if (request.CompanyId.HasValue)
            {
                sql.Append(" AND vm.company_id = {" + paramIndex + "}");
                parameters.Add(request.CompanyId.Value);
                paramIndex++;
            }

            // Add category filter if provided
            if (request.CategoryId.HasValue)
            {
                sql.Append(" AND m.category_id = {" + paramIndex + "}");
                parameters.Add(request.CategoryId.Value);
                paramIndex++;
            }

            // Add make filter if provided
            if (!string.IsNullOrEmpty(request.Make))
            {
                sql.Append(" AND UPPER(TRIM(m.make)) = UPPER(TRIM({" + paramIndex + "}))");
                parameters.Add(request.Make);
                paramIndex++;
            }

            // Add model filter if provided
            if (!string.IsNullOrEmpty(request.Model))
            {
                sql.Append(" AND UPPER(TRIM(m.model)) = UPPER(TRIM({" + paramIndex + "}))");
                parameters.Add(request.Model);
                paramIndex++;
            }

            // Add year filter if provided
            if (request.Year.HasValue)
            {
                sql.Append(" AND m.year = {" + paramIndex + "}");
                parameters.Add(request.Year.Value);
            }

            _logger.LogInformation("Executing SQL: {Sql}", sql.ToString());

            var rowsAffected = await _context.Database.ExecuteSqlRawAsync(sql.ToString(), parameters.ToArray());

            _logger.LogInformation("Bulk updated {Count} vehicle_model catalog entries daily rate to {Rate}", 
                rowsAffected, request.DailyRate);

            return Ok(new { 
                Count = (int)rowsAffected, 
                Message = $"Successfully updated {rowsAffected} vehicle catalog entries" 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error bulk updating vehicles daily rate");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get vehicle count for a company
    /// </summary>
    /// <param name="companyId">Company ID to count vehicles for</param>
    /// <returns>Total vehicle count</returns>
    [HttpGet("count")]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<IActionResult> GetVehicleCount([FromQuery] Guid? companyId = null)
    {
        try
        {
            _logger.LogInformation("GetVehicleCount called with companyId={CompanyId}", companyId);
            var count = await _context.Vehicles
                .Where(v => companyId == null || v.CompanyId == companyId)
                .CountAsync();
            
            _logger.LogInformation("Vehicle count result: {Count} for companyId={CompanyId}", count, companyId);
            return Ok(new { Count = count, CompanyId = companyId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting vehicle count");
            return StatusCode(500, "Internal server error");
        }
    }
}
