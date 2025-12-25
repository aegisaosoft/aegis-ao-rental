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
using CarRental.Api.Services;
using System.Security.Claims;
using System.IO;
using System.Linq;

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
    /// Translate category name from Spanish, Portuguese, French, or German to English
    /// </summary>
    /// <param name="categoryName">Category name in any language</param>
    /// <returns>English category name</returns>
    private string TranslateCategoryToEnglish(string categoryName)
    {
        if (string.IsNullOrWhiteSpace(categoryName))
            return categoryName;

        var normalized = categoryName.Trim();
        
        // Dictionary mapping common vehicle category translations to English
        // Note: Using TryAdd to avoid duplicate key errors - each key appears only once
        var translations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        
        // Spanish (es/sp) - with and without accents
        translations.TryAdd("económico", "Economy");
        translations.TryAdd("economico", "Economy");
        translations.TryAdd("compacto", "Compact");
        translations.TryAdd("intermedio", "Intermediate");
        translations.TryAdd("intermedia", "Intermediate");
        translations.TryAdd("estándar", "Standard");
        translations.TryAdd("estandar", "Standard");
        translations.TryAdd("full size", "Full Size");
        translations.TryAdd("tamaño completo", "Full Size");
        translations.TryAdd("tamano completo", "Full Size");
        translations.TryAdd("premium", "Premium");
        translations.TryAdd("lujo", "Luxury");
        translations.TryAdd("suv", "SUV");
        translations.TryAdd("todoterreno", "SUV");
        translations.TryAdd("van", "Van");
        translations.TryAdd("furgoneta", "Van");
        translations.TryAdd("pickup", "Pickup");
        translations.TryAdd("camioneta", "Pickup");
        translations.TryAdd("convertible", "Convertible");
        translations.TryAdd("descapotable", "Convertible");
        translations.TryAdd("deportivo", "Sports");
        translations.TryAdd("sports", "Sports");
        translations.TryAdd("médio", "Intermediate");
        translations.TryAdd("medio", "Intermediate");
        translations.TryAdd("grande", "Full Size");
        translations.TryAdd("esportivo", "Sports");
        
        // Portuguese (pt/pg) - with and without accents
        translations.TryAdd("econômico", "Economy"); // May already exist from Spanish, but TryAdd handles it
        translations.TryAdd("intermediário", "Intermediate");
        translations.TryAdd("intermediario", "Intermediate");
        translations.TryAdd("padrão", "Standard");
        translations.TryAdd("padrao", "Standard");
        translations.TryAdd("tamanho completo", "Full Size");
        translations.TryAdd("luxo", "Luxury");
        translations.TryAdd("utilitário esportivo", "SUV");
        translations.TryAdd("utilitario esportivo", "SUV");
        translations.TryAdd("furgão", "Van");
        translations.TryAdd("furgao", "Van");
        translations.TryAdd("caminhonete", "Pickup");
        translations.TryAdd("conversível", "Convertible");
        translations.TryAdd("conversivel", "Convertible");
        
        // French (fr) - with and without accents
        translations.TryAdd("économique", "Economy");
        translations.TryAdd("economique", "Economy");
        translations.TryAdd("compact", "Compact");
        translations.TryAdd("intermédiaire", "Intermediate");
        translations.TryAdd("intermediaire", "Intermediate");
        translations.TryAdd("standard", "Standard");
        translations.TryAdd("pleine grandeur", "Full Size");
        translations.TryAdd("luxe", "Luxury");
        translations.TryAdd("vus", "SUV");
        translations.TryAdd("fourgonnette", "Van");
        translations.TryAdd("camionnette", "Pickup");
        translations.TryAdd("décapotable", "Convertible");
        translations.TryAdd("decapotable", "Convertible");
        translations.TryAdd("sport", "Sports");
        translations.TryAdd("sportif", "Sports");
        
        // German (de)
        translations.TryAdd("wirtschaftlich", "Economy");
        translations.TryAdd("kompakt", "Compact");
        translations.TryAdd("mittelklasse", "Intermediate");
        translations.TryAdd("vollgröße", "Full Size");
        translations.TryAdd("vollgroesse", "Full Size");
        translations.TryAdd("luxus", "Luxury");
        translations.TryAdd("geländewagen", "SUV");
        translations.TryAdd("gelaendewagen", "SUV");
        translations.TryAdd("transporter", "Van");
        translations.TryAdd("pick-up", "Pickup");
        translations.TryAdd("cabriolet", "Convertible");
        translations.TryAdd("sportwagen", "Sports");

        // Check if translation exists
        if (translations.TryGetValue(normalized, out var translated))
        {
            return translated;
        }

        // If no translation found, return original (might already be in English)
        return normalized;
    }

    /// <summary>
    /// Parse a CSV line handling quoted fields that may contain commas
    /// </summary>
    private string[] ParseCsvLine(string line)
    {
        var values = new List<string>();
        var currentValue = new System.Text.StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    // Escaped quote (double quote)
                    currentValue.Append('"');
                    i++; // Skip next quote
                }
                else
                {
                    // Toggle quote state
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                // End of field
                values.Add(currentValue.ToString().Trim());
                currentValue.Clear();
            }
            else
            {
                currentValue.Append(c);
            }
        }

        // Add the last field
        values.Add(currentValue.ToString().Trim());

        return values.ToArray();
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
    /// <param name="make">Filter by make</param>
    /// <param name="model">Filter by model</param>
    /// <param name="year">Filter by year</param>
    /// <param name="licensePlate">Filter by license plate</param>
    /// <param name="minPrice">Minimum daily rate</param>
    /// <param name="maxPrice">Maximum daily rate</param>
    /// <param name="availableFrom">Start date for availability check (pickup date)</param>
    /// <param name="availableTo">End date for availability check (return date)</param>
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
        [FromQuery] int? year = null,
        [FromQuery] string? licensePlate = null,
        [FromQuery] string? status = null,
        [FromQuery] string? location = null,
        [FromQuery] Guid? locationId = null,
        [FromQuery] decimal? minPrice = null,
        [FromQuery] decimal? maxPrice = null,
        [FromQuery] DateTime? availableFrom = null,
        [FromQuery] DateTime? availableTo = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            _logger.LogInformation("GetVehicles called with companyId={CompanyId}, status={Status}, availableFrom={AvailableFrom}, availableTo={AvailableTo}, pageSize={PageSize}", 
                companyId, status, availableFrom, availableTo, pageSize);

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

            if (locationId.HasValue)
                query = query.Where(v => v.LocationId == locationId.Value);

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

            // Filter by year if provided
            if (year.HasValue)
            {
                query = query.Where(v => v.VehicleModel != null && v.VehicleModel.Model != null && 
                    v.VehicleModel.Model.Year == year.Value);
            }

            // Filter by license plate if provided (case-insensitive using ILIKE for PostgreSQL)
            if (!string.IsNullOrEmpty(licensePlate))
            {
                query = query.Where(v => v.LicensePlate != null && 
                    EF.Functions.ILike(v.LicensePlate, $"%{licensePlate}%"));
            }

            // Filter out vehicles that are booked for the requested dates
            // A vehicle is unavailable if it has a booking that overlaps with the requested date range
            // and the booking status is one that makes the vehicle unavailable
            if (availableFrom.HasValue && availableTo.HasValue)
            {
                var unavailableStatuses = new[] { BookingStatus.Pending, BookingStatus.Confirmed, BookingStatus.PickedUp };
                
                // Normalize request dates: pickup at start of day, return at end of day
                // This ensures we catch all bookings that overlap with the requested dates
                var requestPickupDateStart = availableFrom.Value.Date; // Start of pickup day (00:00:00)
                var requestReturnDateEnd = availableTo.Value.Date.AddDays(1); // Start of day after return (00:00:00, exclusive)
                
                _logger.LogInformation("Checking vehicle availability for dates: Pickup={RequestPickupDateStart:yyyy-MM-dd} to Return={RequestReturnDateEnd:yyyy-MM-dd}", 
                    requestPickupDateStart, requestReturnDateEnd.AddDays(-1));
                
                // Get vehicle IDs that have overlapping bookings
                // Standard overlap logic: Two date ranges [A_start, A_end] and [B_start, B_end] overlap if:
                // A_start <= B_end AND A_end >= B_start
                // For car rentals: A booking from Jan 14-15 means vehicle is unavailable from Jan 14 00:00:00 to Jan 15 23:59:59
                // So we need to check if booking's date range overlaps with requested date range
                // Request: Jan 14-21 (pickup Jan 14 00:00:00, return Jan 21 23:59:59)
                // Booking: Jan 14-15 (pickup Jan 14 00:00:00, return Jan 15 23:59:59)
                // Overlap check: requestPickupDateStart <= bookingReturnDate AND bookingPickupDate <= requestReturnDateEnd
                // Since ReturnDate might be stored as start of day (Jan 15 00:00:00), we need to treat it as end of day
                // So we check: requestPickupDateStart <= (bookingReturnDate + 1 day) AND bookingPickupDate < (requestReturnDateEnd)
                // Which simplifies to: requestPickupDateStart <= bookingReturnDate.AddDays(1) AND bookingPickupDate < requestReturnDateEnd
                // But we can't use AddDays in EF Core easily, so we'll use:
                // bookingReturnDate >= requestPickupDateStart AND bookingPickupDate < requestReturnDateEnd
                // This works because if ReturnDate is Jan 15 00:00:00, and requestPickupDateStart is Jan 14 00:00:00,
                // then bookingReturnDate (Jan 15) >= requestPickupDateStart (Jan 14) is true
                var unavailableVehicleIds = await _context.Bookings
                    .Where(b => unavailableStatuses.Contains(b.Status) &&
                                // Booking's pickup date is before the end of requested return date
                                b.PickupDate < requestReturnDateEnd &&
                                // Booking's return date is on or after the start of requested pickup date
                                // Note: Even if ReturnDate is stored as start of day (e.g., Jan 15 00:00:00),
                                // this comparison works because Jan 15 00:00:00 >= Jan 14 00:00:00 is true
                                b.ReturnDate >= requestPickupDateStart)
                    .Select(b => new { b.VehicleId, b.PickupDate, b.ReturnDate, b.Status })
                    .ToListAsync();

                var vehicleIds = unavailableVehicleIds.Select(b => b.VehicleId).Distinct().ToList();

                _logger.LogInformation("Found {Count} bookings with overlapping dates. Vehicle IDs: {VehicleIds}", 
                    unavailableVehicleIds.Count, string.Join(", ", vehicleIds));
                
                if (unavailableVehicleIds.Any())
                {
                    _logger.LogInformation("Overlapping bookings details: {Details}", 
                        string.Join("; ", unavailableVehicleIds.Select(b => 
                            $"VehicleId={b.VehicleId}, Pickup={b.PickupDate:yyyy-MM-dd}, Return={b.ReturnDate:yyyy-MM-dd}, Status={b.Status}")));
                }

                // Exclude vehicles with overlapping bookings
                if (vehicleIds.Any())
                {
                    query = query.Where(v => !vehicleIds.Contains(v.Id));
                    _logger.LogInformation("Filtered out {Count} vehicles due to date conflicts for dates {AvailableFrom:yyyy-MM-dd} to {AvailableTo:yyyy-MM-dd}", 
                        vehicleIds.Count, requestPickupDateStart, availableTo.Value.Date);
                }
                else
                {
                    _logger.LogInformation("No vehicles filtered out - no overlapping bookings found for dates {AvailableFrom:yyyy-MM-dd} to {AvailableTo:yyyy-MM-dd}", 
                        requestPickupDateStart, availableTo.Value.Date);
                }
            }

            // Note: categoryId filtering will be done after fetching vehicles
            // to avoid EF Core translation issues with ToUpper() in subqueries

            var totalCount = await query.CountAsync();

            // Get paginated vehicles first (before loading relations)
            // Order by Make, then Model, then Year
            var vehiclesData = await query
                .OrderBy(v => v.VehicleModel != null && v.VehicleModel.Model != null ? v.VehicleModel.Model.Make : "")
                .ThenBy(v => v.VehicleModel != null && v.VehicleModel.Model != null ? v.VehicleModel.Model.ModelName : "")
                .ThenBy(v => v.VehicleModel != null && v.VehicleModel.Model != null ? v.VehicleModel.Model.Year : 0)
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
                    LocationId = v.LocationId,
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
                LocationId = vehicle.LocationId,
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
                LocationId = vehicle.LocationId,
                ImageUrl = vehicle.ImageUrl,
                Features = vehicle.Features,
                CreatedAt = vehicle.CreatedAt,
                UpdatedAt = vehicle.UpdatedAt
            };

            // Auto-publish to social media if enabled (fire and forget)
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = HttpContext.RequestServices.CreateScope();
                    var autoPublishService = scope.ServiceProvider.GetService<IAutoPublishService>();
                    if (autoPublishService != null)
                    {
                        await autoPublishService.PublishVehicleAsync(vehicle.CompanyId, vehicle.Id);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Auto-publish failed for vehicle {VehicleId}", vehicle.Id);
                }
            });

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
        _logger.LogInformation("========== UpdateVehicle called ==========");
        _logger.LogInformation("UpdateVehicle: Vehicle ID = {VehicleId}", id);
        _logger.LogInformation("UpdateVehicle: DTO LocationId = {LocationId}", updateVehicleDto.LocationId);
        _logger.LogInformation("UpdateVehicle: DTO Location = {Location}", updateVehicleDto.Location);
        
        try
        {
            var vehicle = await _context.Vehicles.FindAsync(id);
            if (vehicle == null)
            {
                _logger.LogWarning("UpdateVehicle: Vehicle {VehicleId} not found", id);
                return NotFound();
            }
            
            _logger.LogInformation("UpdateVehicle: Found vehicle, current LocationId = {CurrentLocationId}", vehicle.LocationId);

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

            _logger.LogInformation("UpdateVehicle: LocationId from DTO - HasValue: {HasValue}, Value: {Value}", 
                updateVehicleDto.LocationId.HasValue, 
                updateVehicleDto.LocationId.HasValue ? updateVehicleDto.LocationId.Value : "null");
            
            if (updateVehicleDto.LocationId.HasValue)
            {
                _logger.LogInformation("UpdateVehicle: Setting vehicle.LocationId to {LocationId}", updateVehicleDto.LocationId.Value);
                vehicle.LocationId = updateVehicleDto.LocationId.Value;
            }
            else
            {
                _logger.LogInformation("UpdateVehicle: LocationId.HasValue is false, setting LocationId to null");
                vehicle.LocationId = null; // Explicitly set to null to unassign
            }

            if (updateVehicleDto.ImageUrl != null)
                vehicle.ImageUrl = updateVehicleDto.ImageUrl;

            if (updateVehicleDto.Features != null)
                vehicle.Features = updateVehicleDto.Features;

            // Note: IsActive has been replaced with Status field
            // Use Status = OutOfService instead of IsActive = false

            vehicle.UpdatedAt = DateTime.UtcNow;

            _logger.LogInformation("UpdateVehicle: About to save changes. Final LocationId = {LocationId}", vehicle.LocationId);
            await _context.SaveChangesAsync();
            _logger.LogInformation("UpdateVehicle: Changes saved successfully");

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
                LocationId = vehicle.LocationId,
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

    /// <summary>
    /// Import vehicles from CSV file
    /// </summary>
    /// <param name="file">CSV file containing vehicle data</param>
    /// <param name="companyId">Company ID to associate vehicles with</param>
    /// <returns>Import result with count of imported vehicles</returns>
    [HttpPost("import")]
    [Authorize]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> ImportVehicles(IFormFile file, [FromForm] string? companyId = null, [FromForm] string? fieldMapping = null)
    {
        _logger.LogInformation("=== VEHICLE IMPORT STARTED ===");
        _logger.LogInformation("File name: {FileName}, Size: {Size} bytes, ContentType: {ContentType}", 
            file?.FileName, file?.Length, file?.ContentType);
        _logger.LogInformation("CompanyId from form: {CompanyId}", companyId);
        _logger.LogInformation("FieldMapping from form parameter: {FieldMapping}", fieldMapping ?? "null");
        
        // Also try to get from Request.Form directly in case FromForm binding didn't work
        var fieldMappingFromForm = Request.Form["fieldMapping"].FirstOrDefault();
        _logger.LogInformation("FieldMapping from Request.Form: {FieldMapping}", fieldMappingFromForm ?? "null");
        
        // Use Request.Form value if parameter is null
        if (string.IsNullOrEmpty(fieldMapping) && !string.IsNullOrEmpty(fieldMappingFromForm))
        {
            fieldMapping = fieldMappingFromForm;
            _logger.LogInformation("Using fieldMapping from Request.Form");
        }
        
        try
        {
            // Check admin privileges
            _logger.LogInformation("[Import] Checking admin privileges...");
            if (!HasAdminPrivileges())
            {
                _logger.LogWarning("[Import] Admin check failed - user does not have admin privileges");
                return Forbid("Only admin and mainadmin users can import vehicles");
            }
            _logger.LogInformation("[Import] Admin privileges confirmed");

            // Get company ID from form, query, or context
            Guid parsedCompanyId;
            if (!string.IsNullOrEmpty(companyId))
            {
                _logger.LogInformation("[Import] Parsing companyId from form parameter: {CompanyId}", companyId);
                if (!Guid.TryParse(companyId, out parsedCompanyId))
                {
                    _logger.LogError("[Import] Invalid company ID format: {CompanyId}", companyId);
                    return BadRequest("Invalid company ID format");
                }
            }
            else
            {
                // Try to get from HttpContext (set by CompanyMiddleware)
                var contextCompanyId = HttpContext.Items["CompanyId"]?.ToString();
                _logger.LogInformation("[Import] CompanyId from HttpContext: {ContextCompanyId}", contextCompanyId);
                if (string.IsNullOrEmpty(contextCompanyId) || !Guid.TryParse(contextCompanyId, out parsedCompanyId))
                {
                    _logger.LogError("[Import] Company ID is required but not found in form or context");
                    return BadRequest("Company ID is required");
                }
            }
            _logger.LogInformation("[Import] Using CompanyId: {CompanyId}", parsedCompanyId);

            // Verify company exists
            _logger.LogInformation("[Import] Verifying company exists...");
            var company = await _context.Companies.FindAsync(parsedCompanyId);
            if (company == null)
            {
                _logger.LogError("[Import] Company not found: {CompanyId}", parsedCompanyId);
                return BadRequest("Company not found");
            }
            _logger.LogInformation("[Import] Company found: {CompanyName} (ID: {CompanyId})", company.CompanyName, parsedCompanyId);

            // Verify user can edit vehicles for this company
            _logger.LogInformation("[Import] Checking if user can edit vehicles for company...");
            if (!CanEditCompanyVehicles(parsedCompanyId))
            {
                _logger.LogWarning("[Import] User cannot edit vehicles for company {CompanyId}", parsedCompanyId);
                return Forbid("You can only import vehicles for your own company");
            }
            _logger.LogInformation("[Import] User has permission to edit vehicles for this company");

            // Validate file
            _logger.LogInformation("[Import] Validating file...");
            if (file == null || file.Length == 0)
            {
                _logger.LogError("[Import] No file uploaded or file is empty");
                return BadRequest("No file uploaded");
            }

            if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError("[Import] File is not a CSV: {FileName}", file.FileName);
                return BadRequest("File must be a CSV file");
            }
            _logger.LogInformation("[Import] File validation passed: {FileName}, {Size} bytes", file.FileName, file.Length);

            // Read CSV file
            var vehicles = new List<object>();
            var importedCount = 0;
            var loadedCount = 0; // New vehicles created
            var updatedCount = 0; // Existing vehicles updated
            var errors = new List<string>();

            _logger.LogInformation("[Import] Starting CSV file reading...");
            using (var reader = new StreamReader(file.OpenReadStream()))
            {
                // Read header line
                var headerLine = await reader.ReadLineAsync();
                if (string.IsNullOrEmpty(headerLine))
                {
                    _logger.LogError("[Import] CSV file is empty - no header line found");
                    return BadRequest("CSV file is empty");
                }

                _logger.LogInformation("[Import] Header line: {HeaderLine}", headerLine);
                var headerValues = ParseCsvLine(headerLine);
                var headers = headerValues.Select(h => h.Trim()).ToArray(); // Keep original case for display
                var headersLower = headers.Select(h => h.ToLowerInvariant()).ToArray();
                _logger.LogInformation("[Import] Parsed {Count} headers: {Headers}", headers.Length, string.Join(", ", headers));

                // Parse field mapping if provided
                Dictionary<string, int>? columnMapping = null;
                if (!string.IsNullOrEmpty(fieldMapping))
                {
                    try
                    {
                        _logger.LogInformation("[Import] Received field mapping string: {Mapping}", fieldMapping);
                        columnMapping = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, int>>(fieldMapping);
                        if (columnMapping != null)
                        {
                            _logger.LogInformation("[Import] Successfully parsed field mapping with {Count} fields", columnMapping.Count);
                            foreach (var kvp in columnMapping)
                            {
                                _logger.LogInformation("[Import] Mapping: {Field} -> Column {Index}", kvp.Key, kvp.Value);
                            }
                        }
                        else
                        {
                            _logger.LogWarning("[Import] Field mapping deserialized to null");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[Import] Failed to parse field mapping: {Error}", ex.Message);
                        _logger.LogError("[Import] Field mapping string was: {Mapping}", fieldMapping);
                    }
                }
                else
                {
                    _logger.LogInformation("[Import] No field mapping provided");
                }

                // Find required column indices - use mapping if provided, otherwise try to find by name
                _logger.LogInformation("[Import] Finding column indices...");
                int makeIndex, modelIndex, yearIndex, licensePlateIndex, stateIndex, categoryIndex;
                
                if (columnMapping != null)
                {
                    // Use provided mapping
                    makeIndex = columnMapping.GetValueOrDefault("make", -1);
                    modelIndex = columnMapping.GetValueOrDefault("model", -1);
                    yearIndex = columnMapping.GetValueOrDefault("year", -1);
                    licensePlateIndex = columnMapping.GetValueOrDefault("license_plate", -1);
                    stateIndex = columnMapping.GetValueOrDefault("state", -1);
                    categoryIndex = columnMapping.GetValueOrDefault("category", -1);
                }
                else
                {
                    // Try to find by name (case-insensitive)
                    makeIndex = Array.IndexOf(headersLower, "make");
                    modelIndex = Array.IndexOf(headersLower, "model");
                    yearIndex = Array.IndexOf(headersLower, "year");
                    licensePlateIndex = Array.IndexOf(headersLower, "licenseplate") != -1 
                        ? Array.IndexOf(headersLower, "licenseplate") 
                        : Array.IndexOf(headersLower, "license_plate");
                    stateIndex = Array.IndexOf(headersLower, "state");
                    categoryIndex = Array.IndexOf(headersLower, "category");
                }

                _logger.LogInformation("[Import] Required columns - Make: {MakeIndex}, Model: {ModelIndex}, Year: {YearIndex}, LicensePlate: {LicensePlateIndex}, State: {StateIndex}, Category: {CategoryIndex}",
                    makeIndex, modelIndex, yearIndex, licensePlateIndex, stateIndex, categoryIndex);

                // Check if mandatory fields are missing
                bool missingMandatory = makeIndex == -1 || modelIndex == -1 || yearIndex == -1 || licensePlateIndex == -1 || stateIndex == -1 || categoryIndex == -1;
                
                // Mandatory fields: make, model, year, license_plate, state, category
                int mandatoryFieldCount = 6;
                
                if (missingMandatory && headers.Length >= mandatoryFieldCount)
                {
                    // Enough columns but wrong names - return headers for mapping
                    _logger.LogInformation("[Import] Missing mandatory fields but enough columns ({ColumnCount} >= {RequiredCount}). Returning headers for mapping.",
                        headers.Length, mandatoryFieldCount);
                    
                    return Ok(new
                    {
                        requiresMapping = true,
                        headers = headers,
                        availableFields = new[]
                        {
                            new { field = "make", label = "Make", mandatory = true, defaultValue = (string?)null },
                            new { field = "model", label = "Model", mandatory = true, defaultValue = (string?)null },
                            new { field = "year", label = "Year", mandatory = true, defaultValue = (string?)null },
                            new { field = "license_plate", label = "License Plate", mandatory = true, defaultValue = (string?)null },
                            new { field = "state", label = "State", mandatory = true, defaultValue = (string?)null },
                            new { field = "category", label = "Category", mandatory = true, defaultValue = (string?)null },
                            new { field = "color", label = "Color", mandatory = false, defaultValue = (string?)"Silver" },
                            new { field = "number_of_seats", label = "Number of Seats", mandatory = false, defaultValue = (string?)null },
                            new { field = "fuel_type", label = "Fuel Type", mandatory = false, defaultValue = (string?)"Gasoline" }
                        }
                    });
                }
                
                if (missingMandatory)
                {
                    _logger.LogError("[Import] Missing required columns. Make: {MakeIndex}, Model: {ModelIndex}, Year: {YearIndex}, LicensePlate: {LicensePlateIndex}, State: {StateIndex}, Category: {CategoryIndex}",
                        makeIndex, modelIndex, yearIndex, licensePlateIndex, stateIndex, categoryIndex);
                    return BadRequest("CSV must contain columns: make, model, year, license_plate, state, category");
                }

                // Optional columns - use mapping if provided
                int idIndex, colorIndex, vinIndex, mileageIndex, transmissionIndex, seatsIndex, fuelTypeIndex, locationIndex, dailyRateIndex;
                
                if (columnMapping != null)
                {
                    idIndex = columnMapping.GetValueOrDefault("id", -1);
                    colorIndex = columnMapping.GetValueOrDefault("color", -1);
                    vinIndex = columnMapping.GetValueOrDefault("vin", -1);
                    mileageIndex = columnMapping.GetValueOrDefault("mileage", -1);
                    transmissionIndex = columnMapping.GetValueOrDefault("transmission", -1);
                    seatsIndex = columnMapping.GetValueOrDefault("number_of_seats", columnMapping.GetValueOrDefault("seats", -1));
                    fuelTypeIndex = columnMapping.GetValueOrDefault("fuel_type", columnMapping.GetValueOrDefault("fueltype", -1));
                    locationIndex = columnMapping.GetValueOrDefault("location", -1);
                    dailyRateIndex = columnMapping.GetValueOrDefault("daily_rate", columnMapping.GetValueOrDefault("dailyrate", -1));
                }
                else
                {
                    idIndex = Array.IndexOf(headersLower, "id");
                    colorIndex = Array.IndexOf(headersLower, "color");
                    vinIndex = Array.IndexOf(headersLower, "vin");
                    mileageIndex = Array.IndexOf(headersLower, "mileage");
                    transmissionIndex = Array.IndexOf(headersLower, "transmission");
                    seatsIndex = Array.IndexOf(headersLower, "number_of_seats") != -1 
                        ? Array.IndexOf(headersLower, "number_of_seats") 
                        : Array.IndexOf(headersLower, "seats");
                    fuelTypeIndex = Array.IndexOf(headersLower, "fueltype") != -1 
                        ? Array.IndexOf(headersLower, "fueltype") 
                        : Array.IndexOf(headersLower, "fuel_type");
                    locationIndex = Array.IndexOf(headersLower, "location");
                    dailyRateIndex = Array.IndexOf(headersLower, "dailyrate") != -1 
                        ? Array.IndexOf(headersLower, "dailyrate") 
                        : Array.IndexOf(headersLower, "daily_rate");
                }

                _logger.LogInformation("[Import] Optional columns - ID: {IdIndex}, Color: {ColorIndex}, VIN: {VinIndex}, Mileage: {MileageIndex}, Transmission: {TransmissionIndex}, Seats: {SeatsIndex}, FuelType: {FuelTypeIndex}, State: {StateIndex}, Location: {LocationIndex}, Category: {CategoryIndex}, DailyRate: {DailyRateIndex}",
                    idIndex, colorIndex, vinIndex, mileageIndex, transmissionIndex, seatsIndex, fuelTypeIndex, stateIndex, locationIndex, categoryIndex, dailyRateIndex);

                // Process each line
                int lineNumber = 1;
                int totalLines = 0;
                _logger.LogInformation("[Import] Starting to process CSV rows...");
                
                while (!reader.EndOfStream)
                {
                    lineNumber++;
                    var line = await reader.ReadLineAsync();
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        _logger.LogDebug("[Import] Line {LineNumber}: Skipping empty line", lineNumber);
                        continue;
                    }

                    totalLines++;
                    _logger.LogInformation("[Import] Processing line {LineNumber}: {Line}", lineNumber, line);

                    // Declare licensePlate outside try block so it's accessible in catch
                    string? licensePlate = null;
                    
                    try
                    {
                        // Parse CSV line handling quoted fields
                        var values = ParseCsvLine(line);
                        _logger.LogDebug("[Import] Line {LineNumber}: Parsed {Count} values", lineNumber, values.Length);

                        if (values.Length <= Math.Max(makeIndex, Math.Max(modelIndex, Math.Max(yearIndex, licensePlateIndex))))
                        {
                            errors.Add($"Line {lineNumber}: Not enough columns");
                            continue;
                        }

                        // Get and uppercase make and model
                        var make = values[makeIndex]?.ToUpperInvariant().Trim();
                        var modelName = values[modelIndex]?.ToUpperInvariant().Trim();
                        var yearStr = values[yearIndex]?.Trim();
                        licensePlate = values[licensePlateIndex]?.Trim();

                        _logger.LogDebug("[Import] Line {LineNumber}: Extracted - Make: {Make}, Model: {Model}, Year: {Year}, LicensePlate: {LicensePlate}",
                            lineNumber, make, modelName, yearStr, licensePlate);

                        if (string.IsNullOrEmpty(make) || string.IsNullOrEmpty(modelName) || string.IsNullOrEmpty(licensePlate))
                        {
                            var errorMsg = $"Line {lineNumber}: Missing required fields (make: {make}, model: {modelName}, licenseplate: {licensePlate})";
                            _logger.LogWarning("[Import] {Error}", errorMsg);
                            errors.Add(errorMsg);
                            continue;
                        }

                        // Parse year - use current year if empty
                        int year;
                        if (string.IsNullOrWhiteSpace(yearStr))
                        {
                            year = DateTime.Now.Year;
                            _logger.LogInformation("[Import] Line {LineNumber}: Year is empty, using current year: {Year}", lineNumber, year);
                        }
                        else if (!int.TryParse(yearStr, out year) || year < 1900 || year > DateTime.Now.Year + 1)
                        {
                            var errorMsg = $"Line {lineNumber}: Invalid year: {yearStr}";
                            _logger.LogWarning("[Import] {Error}", errorMsg);
                            errors.Add(errorMsg);
                            continue;
                        }
                        _logger.LogDebug("[Import] Line {LineNumber}: Parsed year: {Year}", lineNumber, year);

                        // Parse state early (needed for duplicate check) - ignore if column doesn't exist or is null/empty
                        string? state = null;
                        if (stateIndex != -1 && values.Length > stateIndex && !string.IsNullOrWhiteSpace(values[stateIndex]))
                        {
                            state = values[stateIndex].Trim();
                        }

                        // Parse optional ID column - if provided, check if vehicle exists and update it
                        Guid? vehicleIdFromCsv = null;
                        if (idIndex != -1 && values.Length > idIndex && !string.IsNullOrWhiteSpace(values[idIndex]))
                        {
                            if (Guid.TryParse(values[idIndex].Trim(), out var parsedId))
                            {
                                vehicleIdFromCsv = parsedId;
                                _logger.LogDebug("[Import] Line {LineNumber}: Parsed vehicle ID from CSV: {VehicleId}", lineNumber, vehicleIdFromCsv);
                            }
                            else
                            {
                                _logger.LogWarning("[Import] Line {LineNumber}: Invalid vehicle ID format in CSV: {IdValue}", lineNumber, values[idIndex]);
                            }
                        }

                        // Parse optional fields
                        int? seats = null;
                        if (seatsIndex != -1 && values.Length > seatsIndex && int.TryParse(values[seatsIndex], out var parsedSeats))
                        {
                            seats = parsedSeats;
                            _logger.LogDebug("[Import] Line {LineNumber}: Parsed seats: {Seats}", lineNumber, seats);
                        }

                        // Parse fuel type - default to "Gasoline" if not provided
                        string? fuelType = null;
                        if (fuelTypeIndex != -1 && values.Length > fuelTypeIndex && !string.IsNullOrWhiteSpace(values[fuelTypeIndex]))
                        {
                            fuelType = values[fuelTypeIndex].Trim();
                            _logger.LogDebug("[Import] Line {LineNumber}: Parsed fuel type: {FuelType}", lineNumber, fuelType);
                        }
                        else
                        {
                            // Default to Gasoline if not provided
                            fuelType = "Gasoline";
                            _logger.LogDebug("[Import] Line {LineNumber}: Using default fuel type: Gasoline", lineNumber);
                        }

                        // Parse and translate category (needed for model creation/update)
                        Guid? categoryId = null;
                        if (categoryIndex != -1 && values.Length > categoryIndex && !string.IsNullOrWhiteSpace(values[categoryIndex]))
                        {
                            var categoryName = values[categoryIndex].Trim();
                            _logger.LogDebug("[Import] Line {LineNumber}: Processing category: {CategoryName}", lineNumber, categoryName);
                            var englishCategoryName = TranslateCategoryToEnglish(categoryName);
                            _logger.LogDebug("[Import] Line {LineNumber}: Translated category to English: {EnglishCategoryName}", lineNumber, englishCategoryName);
                            
                            // Find or create category
                            var category = await _context.VehicleCategories
                                .FirstOrDefaultAsync(c => c.CategoryName.ToUpper() == englishCategoryName.ToUpper());
                            
                            if (category == null)
                            {
                                _logger.LogInformation("[Import] Line {LineNumber}: Creating new category: {CategoryName} (translated from: {Original})", 
                                    lineNumber, englishCategoryName, categoryName);
                                category = new VehicleCategory
                                {
                                    CategoryName = englishCategoryName
                                };
                                _context.VehicleCategories.Add(category);
                                await _context.SaveChangesAsync();
                                _logger.LogInformation("[Import] Line {LineNumber}: Created category with ID: {CategoryId}", lineNumber, category.Id);
                            }
                            else
                            {
                                _logger.LogDebug("[Import] Line {LineNumber}: Found existing category: {CategoryName} (ID: {CategoryId})", 
                                    lineNumber, englishCategoryName, category.Id);
                            }
                            
                            categoryId = category.Id;
                        }
                        else
                        {
                            _logger.LogDebug("[Import] Line {LineNumber}: No category provided", lineNumber);
                        }

                        // Find or create model and update if needed (do this BEFORE duplicate check so model is updated even if vehicle exists)
                        _logger.LogDebug("[Import] Line {LineNumber}: Looking for model - Make: {Make}, Model: {Model}, Year: {Year}", 
                            lineNumber, make, modelName, year);
                        var model = await _context.Models
                            .FirstOrDefaultAsync(m => 
                                m.Make.ToUpper() == make && 
                                m.ModelName.ToUpper() == modelName && 
                                m.Year == year);

                        if (model == null)
                        {
                            // Create new model
                            _logger.LogInformation("[Import] Line {LineNumber}: Model not found, creating new model - {Make} {Model} {Year}", 
                                lineNumber, make, modelName, year);
                            model = new Model
                            {
                                Make = make,
                                ModelName = modelName,
                                Year = year,
                                FuelType = fuelType,
                                Transmission = transmissionIndex != -1 && values.Length > transmissionIndex ? values[transmissionIndex] : null,
                                Seats = seats,
                                CategoryId = categoryId
                            };
                            _logger.LogInformation("[Import] Line {LineNumber}: Creating model with FuelType: {FuelType}, Seats: {Seats}", 
                                lineNumber, fuelType, seats);
                            _context.Models.Add(model);
                            await _context.SaveChangesAsync();
                            _logger.LogInformation("[Import] Line {LineNumber}: Created new model with ID: {ModelId}, Make: {Make}, Model: {Model}, Year: {Year}, CategoryId: {CategoryId}", 
                                lineNumber, model.Id, make, modelName, year, categoryId);
                        }
                        else
                        {
                            _logger.LogDebug("[Import] Line {LineNumber}: Found existing model with ID: {ModelId}, Make: {Make}, Model: {Model}, Year: {Year}", 
                                lineNumber, model.Id, make, modelName, year);
                            
                            bool modelUpdated = false;
                            
                            // Update category if provided and different
                            if (categoryId.HasValue && model.CategoryId != categoryId)
                            {
                                _logger.LogInformation("[Import] Line {LineNumber}: Updating model category from {OldCategoryId} to {NewCategoryId}", 
                                    lineNumber, model.CategoryId, categoryId);
                                model.CategoryId = categoryId;
                                modelUpdated = true;
                            }
                            
                            // Update seats if model has null seats and CSV has seats value
                            if (model.Seats == null && seats.HasValue)
                            {
                                _logger.LogInformation("[Import] Line {LineNumber}: Updating model seats from null to {Seats}", 
                                    lineNumber, seats);
                                model.Seats = seats;
                                modelUpdated = true;
                            }
                            
                            // Update fuel type if model has null fuel type and CSV has fuel type value
                            if (string.IsNullOrEmpty(model.FuelType) && !string.IsNullOrEmpty(fuelType))
                            {
                                _logger.LogInformation("[Import] Line {LineNumber}: Updating model fuel type from null to {FuelType}", 
                                    lineNumber, fuelType);
                                model.FuelType = fuelType;
                                modelUpdated = true;
                            }
                            
                            // Save changes if model was updated
                            if (modelUpdated)
                            {
                                await _context.SaveChangesAsync();
                                _logger.LogInformation("[Import] Line {LineNumber}: Updated existing model - Make: {Make}, Model: {ModelName}, Year: {Year}", 
                                    lineNumber, make, modelName, year);
                            }
                        }

                        // Check if vehicle exists by ID (if ID column is provided in CSV)
                        Vehicle? existingVehicle = null;
                        if (vehicleIdFromCsv.HasValue)
                        {
                            _logger.LogDebug("[Import] Line {LineNumber}: Checking if vehicle with ID {VehicleId} exists...", 
                                lineNumber, vehicleIdFromCsv);
                            existingVehicle = await _context.Vehicles
                                .FirstOrDefaultAsync(v => v.Id == vehicleIdFromCsv.Value);
                            
                            if (existingVehicle != null)
                            {
                                _logger.LogInformation("[Import] Line {LineNumber}: Vehicle with ID {VehicleId} exists, will update it", 
                                    lineNumber, vehicleIdFromCsv);
                                // Skip license plate check if updating by ID
                            }
                            else
                            {
                                _logger.LogDebug("[Import] Line {LineNumber}: Vehicle with ID {VehicleId} does not exist, will create new vehicle with this ID", 
                                    lineNumber, vehicleIdFromCsv);
                            }
                        }
                        
                        // If no ID provided or vehicle doesn't exist by ID, check by license plate and state
                        if (existingVehicle == null)
                        {
                            _logger.LogDebug("[Import] Line {LineNumber}: Checking if vehicle with license plate {LicensePlate} and state {State} already exists...", 
                                lineNumber, licensePlate, state ?? "null");
                            
                            // Build query to check for duplicate: license plate AND state must match
                            var existingVehicleQuery = _context.Vehicles
                                .Where(v => v.LicensePlate == licensePlate);
                            
                            // If state is provided, check for matching state; if null, check for null state
                            if (state != null)
                            {
                                existingVehicleQuery = existingVehicleQuery.Where(v => v.State == state);
                            }
                            else
                            {
                                existingVehicleQuery = existingVehicleQuery.Where(v => v.State == null);
                            }
                            
                            existingVehicle = await existingVehicleQuery.FirstOrDefaultAsync();
                            
                            if (existingVehicle != null)
                            {
                                var errorMsg = $"Line {lineNumber}: Vehicle with license plate {licensePlate} and state {(state ?? "null")} already exists";
                                _logger.LogWarning("[Import] {Error}", errorMsg);
                                errors.Add(errorMsg);
                                continue;
                            }
                            _logger.LogDebug("[Import] Line {LineNumber}: Vehicle with license plate {LicensePlate} and state {State} is unique", 
                                lineNumber, licensePlate, state ?? "null");
                        }

                        // Note: Model and category were already found/created/updated above (before duplicate check)
                        // This ensures model data is updated even if vehicle is a duplicate

                        // Find or create vehicle_model catalog entry (required for binding vehicle to model)
                        _logger.LogDebug("[Import] Line {LineNumber}: Looking for vehicle_model entry - ModelId: {ModelId}, CompanyId: {CompanyId}", 
                            lineNumber, model.Id, parsedCompanyId);
                        var vehicleModel = await _context.VehicleModels
                            .FirstOrDefaultAsync(vm => vm.ModelId == model.Id && vm.CompanyId == parsedCompanyId);

                        if (vehicleModel == null)
                        {
                            var dailyRate = dailyRateIndex != -1 && values.Length > dailyRateIndex && decimal.TryParse(values[dailyRateIndex], out var rate) ? rate : 0m;
                            _logger.LogInformation("[Import] Line {LineNumber}: Vehicle_model not found, creating new entry - ModelId: {ModelId}, CompanyId: {CompanyId}, DailyRate: {DailyRate}", 
                                lineNumber, model.Id, parsedCompanyId, dailyRate);
                            vehicleModel = new VehicleModel
                            {
                                CompanyId = parsedCompanyId,
                                ModelId = model.Id,
                                DailyRate = dailyRate
                            };
                            _context.VehicleModels.Add(vehicleModel);
                            await _context.SaveChangesAsync();
                            _logger.LogInformation("[Import] Line {LineNumber}: Created vehicle_model entry with ID: {VehicleModelId}, ModelId: {ModelId}, CompanyId: {CompanyId}, DailyRate: {DailyRate}", 
                                lineNumber, vehicleModel.Id, model.Id, parsedCompanyId, dailyRate);
                        }
                        else
                        {
                            _logger.LogDebug("[Import] Line {LineNumber}: Found existing vehicle_model entry with ID: {VehicleModelId}, ModelId: {ModelId}, CompanyId: {CompanyId}", 
                                lineNumber, vehicleModel.Id, model.Id, parsedCompanyId);
                        }

                        // Parse VIN (optional - only for model recognition, not required for vehicle)
                        string? vin = null;
                        if (vinIndex != -1 && values.Length > vinIndex && !string.IsNullOrWhiteSpace(values[vinIndex]))
                        {
                            vin = values[vinIndex].Trim();
                            // Only set VIN if it's not empty after trimming
                            if (string.IsNullOrEmpty(vin))
                            {
                                vin = null;
                            }
                            _logger.LogDebug("[Import] Line {LineNumber}: VIN provided: {Vin}", lineNumber, vin);
                        }
                        else
                        {
                            _logger.LogDebug("[Import] Line {LineNumber}: No VIN provided (optional)", lineNumber);
                        }

                        // Parse other optional fields - ignore if column doesn't exist or value is null/empty
                        // Color defaults to "Silver" if not provided
                        string? color = null;
                        if (colorIndex != -1 && values.Length > colorIndex && !string.IsNullOrWhiteSpace(values[colorIndex]))
                        {
                            color = values[colorIndex].Trim();
                        }
                        else
                        {
                            // Default to Silver if not provided
                            color = "Silver";
                            _logger.LogDebug("[Import] Line {LineNumber}: Using default color: Silver", lineNumber);
                        }
                        
                        int mileage = 0;
                        if (mileageIndex != -1 && values.Length > mileageIndex && !string.IsNullOrWhiteSpace(values[mileageIndex]))
                        {
                            if (int.TryParse(values[mileageIndex], out var parsedMileage))
                            {
                                mileage = parsedMileage;
                            }
                        }
                        
                        string? transmission = null;
                        if (transmissionIndex != -1 && values.Length > transmissionIndex && !string.IsNullOrWhiteSpace(values[transmissionIndex]))
                        {
                            transmission = values[transmissionIndex].Trim();
                        }
                        
                        string? location = null;
                        if (locationIndex != -1 && values.Length > locationIndex && !string.IsNullOrWhiteSpace(values[locationIndex]))
                        {
                            location = values[locationIndex].Trim();
                        }
                        // Note: state was already parsed earlier for duplicate check

                        _logger.LogDebug("[Import] Line {LineNumber}: Optional fields - Color: {Color}, Mileage: {Mileage}, Transmission: {Transmission}, Seats: {Seats}, State: {State}, Location: {Location}",
                            lineNumber, color, mileage, transmission, seats, state, location);

                        // Update existing vehicle or create new one
                        Vehicle vehicle;
                        if (existingVehicle != null)
                        {
                            // Update existing vehicle - only update non-null values from CSV
                            _logger.LogInformation("[Import] Line {LineNumber}: Updating existing vehicle - VehicleId: {VehicleId}, LicensePlate: {LicensePlate}, Make: {Make}, Model: {Model}, Year: {Year}",
                                lineNumber, existingVehicle.Id, licensePlate, make, modelName, year);
                            
                            existingVehicle.CompanyId = parsedCompanyId;
                            
                            // Only update fields if they have non-null/non-empty values from CSV
                            // This preserves existing values when CSV has null/empty or column doesn't exist
                            if (color != null && !string.IsNullOrWhiteSpace(color)) existingVehicle.Color = color;
                            if (!string.IsNullOrEmpty(licensePlate)) existingVehicle.LicensePlate = licensePlate;
                            if (vin != null && !string.IsNullOrWhiteSpace(vin)) existingVehicle.Vin = vin;
                            if (mileageIndex != -1 && mileage > 0) existingVehicle.Mileage = mileage; // Only update if column exists and value > 0
                            if (transmission != null && !string.IsNullOrWhiteSpace(transmission)) existingVehicle.Transmission = transmission;
                            if (seatsIndex != -1 && seats.HasValue) existingVehicle.Seats = seats; // Only update if column exists
                            if (vehicleModel != null) existingVehicle.VehicleModelId = vehicleModel.Id;
                            if (stateIndex != -1 && state != null && !string.IsNullOrWhiteSpace(state)) existingVehicle.State = state; // Only update if column exists
                            if (location != null && !string.IsNullOrWhiteSpace(location)) existingVehicle.Location = location;
                            
                            existingVehicle.UpdatedAt = DateTime.UtcNow;
                            
                            vehicle = existingVehicle;
                            importedCount++;
                            updatedCount++;
                            _logger.LogInformation("[Import] Line {LineNumber}: Vehicle updated successfully - VehicleId: {VehicleId}, LicensePlate: {LicensePlate}, VehicleModelId: {VehicleModelId}",
                                lineNumber, vehicle.Id, licensePlate, vehicleModel!.Id);
                        }
                        else
                        {
                            // Create new vehicle
                            _logger.LogInformation("[Import] Line {LineNumber}: Creating new vehicle - LicensePlate: {LicensePlate}, Make: {Make}, Model: {Model}, Year: {Year}, VehicleModelId: {VehicleModelId}",
                                lineNumber, licensePlate, make, modelName, year, vehicleModel!.Id);
                            
                            vehicle = new Vehicle
                            {
                                Id = vehicleIdFromCsv ?? Guid.NewGuid(), // Use ID from CSV if provided, otherwise generate new
                                CompanyId = parsedCompanyId,
                                Color = color,
                                LicensePlate = licensePlate,
                                Vin = vin, // VIN is optional - only used for model recognition
                                Mileage = mileage,
                                Transmission = transmission,
                                Seats = seats,
                                VehicleModelId = vehicleModel!.Id, // Bind vehicle to model through vehicle_model table
                                Status = VehicleStatus.Available,
                                State = state,
                                Location = location
                            };

                            _context.Vehicles.Add(vehicle);
                            importedCount++;
                            loadedCount++;
                            _logger.LogInformation("[Import] Line {LineNumber}: Vehicle created successfully - LicensePlate: {LicensePlate}, VehicleId: {VehicleId}, VehicleModelId: {VehicleModelId}",
                                lineNumber, licensePlate, vehicle.Id, vehicleModel!.Id);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Include license plate in error message if available
                        var errorMsg = string.IsNullOrEmpty(licensePlate)
                            ? $"Line {lineNumber}: Error processing - {ex.Message}"
                            : $"Line {lineNumber}: Error processing {licensePlate} - {ex.Message}";
                        _logger.LogError(ex, "[Import] {Error}", errorMsg);
                        errors.Add(errorMsg);
                    }
                }

                _logger.LogInformation("[Import] Finished processing {TotalLines} lines. Loaded: {LoadedCount}, Updated: {UpdatedCount}, Total: {ImportedCount}, Errors: {ErrorCount}", 
                    totalLines, loadedCount, updatedCount, importedCount, errors.Count);

                // Save all vehicles
                _logger.LogInformation("[Import] Saving all vehicles to database...");
                await _context.SaveChangesAsync();
                _logger.LogInformation("[Import] All vehicles saved successfully");

                _logger.LogInformation("=== VEHICLE IMPORT COMPLETED ===");
                _logger.LogInformation("Total lines processed: {TotalLines}", totalLines);
                _logger.LogInformation("Loaded (new): {LoadedCount} vehicles", loadedCount);
                _logger.LogInformation("Updated (existing): {UpdatedCount} vehicles", updatedCount);
                _logger.LogInformation("Total imported: {ImportedCount} vehicles", importedCount);
                _logger.LogInformation("Ignored (errors): {ErrorCount} lines", errors.Count);
                if (errors.Count > 0)
                {
                    _logger.LogWarning("[Import] Error details: {Errors}", string.Join("; ", errors));
                }

                return Ok(new
                {
                    count = importedCount,
                    loadedCount = loadedCount,
                    updatedCount = updatedCount,
                    ignoredCount = errors.Count,
                    errors = errors,
                    totalLines = totalLines,
                    message = $"Successfully imported {importedCount} vehicle(s)" + (errors.Count > 0 ? $" with {errors.Count} error(s)" : "")
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "=== VEHICLE IMPORT FAILED ===");
            _logger.LogError(ex, "[Import] Fatal error importing vehicles: {Message}", ex.Message);
            _logger.LogError(ex, "[Import] Inner exception: {InnerException}", ex.InnerException?.Message ?? "None");
            _logger.LogError(ex, "[Import] Stack trace: {StackTrace}", ex.StackTrace);
            
            // Return more detailed error information for debugging
            var errorDetails = new
            {
                message = ex.Message,
                innerException = ex.InnerException?.Message,
                stackTrace = ex.StackTrace
            };
            
            return StatusCode(500, new
            {
                message = "Internal server error during vehicle import",
                error = errorDetails
            });
        }
    }
}
