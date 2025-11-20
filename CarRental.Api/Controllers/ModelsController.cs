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
using CarRental.Api.Extensions;

namespace CarRental.Api.Controllers;

public class VehicleMakeModelYear
{
    public string Make { get; set; } = "";
    public string Model { get; set; } = "";
    public int Year { get; set; }
}

[ApiController]
[Route("api/[controller]")]
public class ModelsController : ControllerBase
{
    private readonly CarRentalDbContext _context;
    private readonly ILogger<ModelsController> _logger;

    public ModelsController(
        CarRentalDbContext context,
        ILogger<ModelsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Get all models grouped by category using stored procedure for better performance
    /// </summary>
    /// <param name="companyId">Optional company ID to filter models by vehicles in company fleet. If not provided, uses company from domain context.</param>
    /// <param name="locationId">Optional location ID to filter models by vehicles at specific location</param>
    /// <param name="pickupDate">Pickup date for availability check</param>
    /// <param name="returnDate">Return date for availability check</param>
    [HttpGet("grouped-by-category")]
    [ProducesResponseType(typeof(IEnumerable<ModelsGroupedByCategoryDto>), 200)]
    public async Task<IActionResult> GetModelsGroupedByCategory(
        [FromQuery] Guid? companyId = null, 
        [FromQuery] Guid? locationId = null,
        [FromQuery] DateTime? pickupDate = null,
        [FromQuery] DateTime? returnDate = null)
    {
        try
        {
            // If companyId not provided, try to get from HttpContext (set by CompanyMiddleware)
            if (!companyId.HasValue)
            {
                companyId = HttpContext.GetCompanyIdAsGuid();
                if (companyId.HasValue)
                {
                    _logger.LogInformation("GetModelsGroupedByCategory: Using company ID from domain context: {CompanyId}", companyId);
                }
            }
            
            // If dates not provided, use default range (today to 7 days from now)
            if (!pickupDate.HasValue)
            {
                pickupDate = DateTime.UtcNow;
            }
            if (!returnDate.HasValue)
            {
                returnDate = pickupDate.Value.AddDays(7);
            }
            
            _logger.LogInformation("GetModelsGroupedByCategory called with companyId={CompanyId}, locationId={LocationId}, pickupDate={PickupDate}, returnDate={ReturnDate}", 
                companyId, locationId, pickupDate, returnDate);
            
            // Early return if no company ID
            if (!companyId.HasValue)
            {
                _logger.LogWarning("No company ID provided for GetModelsGroupedByCategory");
                return Ok(new List<ModelsGroupedByCategoryDto>());
            }

            // Log location filtering behavior
            if (locationId.HasValue)
            {
                _logger.LogInformation("Filtering vehicles by specific locationId: {LocationId}", locationId.Value);
            }
            else
            {
                _logger.LogInformation("No locationId specified - searching all locations for company {CompanyId}", companyId.Value);
            }

            // Call stored procedure to get available vehicles
            // Note: If locationId is NULL, the stored procedure will return vehicles from all company locations
            var sql = @"SELECT * FROM get_available_vehicles_by_company(@p0, @p1, @p2, @p3)";
            
            var availableVehicles = await _context.Database
                .SqlQueryRaw<AvailableVehicleDto>(sql, 
                    companyId.Value, 
                    pickupDate.Value, 
                    returnDate.Value, 
                    locationId.HasValue ? (object)locationId.Value : DBNull.Value)
                .ToListAsync();

            _logger.LogInformation("Stored procedure returned {Count} available vehicle models", availableVehicles.Count);

            // Get actual daily rates from vehicle_model for each model (same logic as GetModels)
            var modelIds = availableVehicles.Select(v => v.ModelId).Distinct().ToList();
            var vehicleModelQuery = _context.VehicleModels
                .Where(vm => modelIds.Contains(vm.ModelId) && vm.CompanyId == companyId.Value)
                .AsQueryable();
            
            var vehicleModelsDict = await vehicleModelQuery
                .OrderBy(vm => vm.CreatedAt)
                .GroupBy(vm => vm.ModelId)
                .Select(g => g.First())
                .ToDictionaryAsync(vm => vm.ModelId);

            _logger.LogInformation("GetModelsGroupedByCategory: Found rates for {Count} vehicle_model entries", vehicleModelsDict.Count);

            // Group by category
            var grouped = availableVehicles
                .Where(v => !string.IsNullOrEmpty(v.CategoryName))
                .GroupBy(v => new { v.CategoryId, v.CategoryName })
                .Select(g => new ModelsGroupedByCategoryDto
                {
                    CategoryId = g.Key.CategoryId ?? Guid.Empty,
                    CategoryName = g.Key.CategoryName ?? "Uncategorized",
                    CategoryDescription = null,
                    Models = g.Select(v => new ModelDto
                    {
                        Id = v.ModelId,
                        Make = v.Make,
                        ModelName = v.Model,
                        Year = ParseFirstYear(v.YearsAvailable),
                        FuelType = v.FuelType,
                        Transmission = v.Transmission,
                        Seats = v.Seats,
                        DailyRate = vehicleModelsDict.ContainsKey(v.ModelId) ? vehicleModelsDict[v.ModelId].DailyRate : v.AvgDailyRate, // Use actual rate from vehicle_model, fallback to average
                        Features = v.ModelFeatures,
                        Description = null,
                        CategoryId = v.CategoryId,
                        CategoryName = v.CategoryName ?? "Uncategorized",
                        VehicleCount = (int)v.AllVehiclesCount, // Total vehicles for this model
                        AvailableCount = (int)v.AvailableCount // Available during selected dates
                    }).ToList()
                })
                .OrderBy(g => g.CategoryName)
                .ToList();

            return Ok(grouped);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching models grouped by category. Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}", 
                ex.GetType().Name, ex.Message, ex.StackTrace);
            return StatusCode(500, new { 
                message = "An error occurred while fetching models",
                error = ex.Message,
                exceptionType = ex.GetType().Name
            });
        }
    }

    /// <summary>
    /// Get all models
    /// </summary>
    /// <param name="categoryId">Filter by category ID</param>
    /// <param name="make">Filter by make</param>
    /// <param name="modelName">Filter by model name</param>
    /// <param name="year">Filter by year</param>
    /// <param name="companyId">Optional company ID to filter models by vehicles in company fleet</param>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ModelDto>), 200)]
    public async Task<IActionResult> GetModels(
        [FromQuery] Guid? categoryId = null,
        [FromQuery] string? make = null,
        [FromQuery] string? modelName = null,
        [FromQuery] int? year = null,
        [FromQuery] Guid? companyId = null)
    {
        try
        {
            var query = _context.Models
                .Include(m => m.Category)
                .AsQueryable();

            List<Model> modelsList;

            // If companyId is provided, filter models to only those that exist in company vehicles
            if (companyId.HasValue)
            {
                // Get distinct make/model combinations from vehicles for this company via vehicle_model
                var companyVehiclesData = await _context.Vehicles
                    .Include(v => v.VehicleModel)
                    .Where(v => v.CompanyId == companyId.Value)
                    .ToListAsync();
                
                // Load Model for each VehicleModel that has one
                foreach (var vehicle in companyVehiclesData.Where(v => v.VehicleModel != null))
                {
                    await _context.Entry(vehicle.VehicleModel!)
                        .Reference(vm => vm.Model)
                        .LoadAsync();
                }
                
                // Project to distinct make/model/year combinations
                var companyVehicles = companyVehiclesData
                    .Where(v => v.VehicleModel?.Model != null)
                    .Select(v => new 
                    { 
                        Make = v.VehicleModel!.Model!.Make ?? "", 
                        Model = v.VehicleModel!.Model!.ModelName ?? "", 
                        Year = v.VehicleModel!.Model!.Year 
                    })
                    .Distinct()
                    .ToList();

                _logger.LogInformation("Company {CompanyId} has {Count} vehicles", companyId.Value, companyVehicles.Count);

                if (companyVehicles.Any())
                {
                    // Create a HashSet of normalized make|model|year tuples for exact matching
                    var vehicleTriplets = new HashSet<string>(
                        companyVehicles
                            .Where(v => !string.IsNullOrEmpty(v.Make) && !string.IsNullOrEmpty(v.Model) && v.Year > 0)
                            .Select(v => 
                                $"{v.Make.ToUpperInvariant().Trim()}|{v.Model.ToUpperInvariant().Trim()}|{v.Year}"),
                        StringComparer.OrdinalIgnoreCase);

                    _logger.LogInformation("Company vehicles make/model/year triplets: {Count} unique", vehicleTriplets.Count);

                    // Get all models matching other filters first
                    var filteredQuery = query;
                    
                    if (categoryId.HasValue)
                        filteredQuery = filteredQuery.Where(m => m.CategoryId == categoryId.Value);

                    if (!string.IsNullOrEmpty(make))
                        filteredQuery = filteredQuery.Where(m => m.Make.Contains(make));

                    if (!string.IsNullOrEmpty(modelName))
                        filteredQuery = filteredQuery.Where(m => m.ModelName.Contains(modelName));

                    if (year.HasValue)
                        filteredQuery = filteredQuery.Where(m => m.Year == year.Value);

                    // Filter models in memory - match by normalized make|model|year (exact match)
                    var allModels = await filteredQuery.ToListAsync();
                    
                    _logger.LogInformation("Total models matching filters: {Count}", allModels.Count);

                    modelsList = allModels
                        .Where(m => 
                            m.Make != null && 
                            m.ModelName != null &&
                            m.Year > 0 &&
                            vehicleTriplets.Contains(
                                $"{m.Make.ToUpperInvariant().Trim()}|{m.ModelName.ToUpperInvariant().Trim()}|{m.Year}"))
                        .OrderBy(m => m.Make)
                        .ThenBy(m => m.ModelName)
                        .ThenByDescending(m => m.Year)
                        .ToList();

                    _logger.LogInformation("Filtered models for company: {Count} (matched by make/model/year)", modelsList.Count);
                }
                else
                {
                    // No vehicles for this company
                    _logger.LogWarning("No vehicles found for company {CompanyId}", companyId.Value);
                    modelsList = new List<Model>();
                }
            }
            else
            {
                // No company filter - apply other filters
                if (categoryId.HasValue)
                    query = query.Where(m => m.CategoryId == categoryId.Value);

                if (!string.IsNullOrEmpty(make))
                    query = query.Where(m => m.Make.Contains(make));

                if (!string.IsNullOrEmpty(modelName))
                    query = query.Where(m => m.ModelName.Contains(modelName));

                if (year.HasValue)
                    query = query.Where(m => m.Year == year.Value);

                modelsList = await query
                    .OrderBy(m => m.Make)
                    .ThenBy(m => m.ModelName)
                    .ThenByDescending(m => m.Year)
                    .Take(1000) // Limit to 1000 models max
                    .ToListAsync();
            }

            // Get vehicle_model daily rates for these models (filtered by company if provided)
            var modelIds = modelsList.Select(m => m.Id).ToList();
            _logger.LogInformation("GetModels: Getting rates for {Count} models", modelIds.Count);
            IQueryable<VehicleModel> vehicleModelQuery2 = _context.VehicleModels.Where(vm => modelIds.Contains(vm.ModelId));
            
            if (companyId.HasValue)
            {
                vehicleModelQuery2 = vehicleModelQuery2.Where(vm => vm.CompanyId == companyId.Value);
                _logger.LogInformation("GetModels: Filtering vehicle_model by companyId: {CompanyId}", companyId.Value);
            }
            
            // Group by ModelId to handle potential duplicates, then take the first one (ordered by CreatedAt)
            var vehicleModelsDict = await vehicleModelQuery2
                .OrderBy(vm => vm.CreatedAt) // Order by creation date to get the most recent entry
                .GroupBy(vm => vm.ModelId)
                .Select(g => g.First())
                .ToDictionaryAsync(vm => vm.ModelId);
            _logger.LogInformation("GetModels: Found rates for {Count} vehicle_model entries", vehicleModelsDict.Count);

            // Get vehicle counts per model
            Dictionary<Guid, int> modelVehicleCountDict = new();
            Dictionary<Guid, int> modelAvailableCountDict = new();
            
            if (companyId.HasValue)
            {
                // Count all vehicles per model for this specific company
                var vehicleCounts = await _context.Vehicles
                    .Include(v => v.VehicleModel)
                    .Where(v => v.CompanyId == companyId.Value && v.VehicleModel != null && modelIds.Contains(v.VehicleModel.ModelId))
                    .GroupBy(v => v.VehicleModel!.ModelId)
                    .Select(g => new { ModelId = g.Key, Count = g.Count() })
                    .ToListAsync();
                
                modelVehicleCountDict = vehicleCounts.ToDictionary(vc => vc.ModelId, vc => vc.Count);
                
                // Count available vehicles per model for this specific company
                var availableCounts = await _context.Vehicles
                    .Include(v => v.VehicleModel)
                    .Where(v => v.CompanyId == companyId.Value && v.VehicleModel != null && modelIds.Contains(v.VehicleModel.ModelId) && v.Status == VehicleStatus.Available)
                    .GroupBy(v => v.VehicleModel!.ModelId)
                    .Select(g => new { ModelId = g.Key, Count = g.Count() })
                    .ToListAsync();
                
                modelAvailableCountDict = availableCounts.ToDictionary(vc => vc.ModelId, vc => vc.Count);
            }
            else
            {
                // Count all vehicles per model across all companies
                var vehicleCounts = await _context.Vehicles
                    .Include(v => v.VehicleModel)
                    .Where(v => v.VehicleModel != null && modelIds.Contains(v.VehicleModel.ModelId))
                    .GroupBy(v => v.VehicleModel!.ModelId)
                    .Select(g => new { ModelId = g.Key, Count = g.Count() })
                    .ToListAsync();
                
                modelVehicleCountDict = vehicleCounts.ToDictionary(vc => vc.ModelId, vc => vc.Count);
                
                // Count available vehicles per model across all companies
                var availableCounts = await _context.Vehicles
                    .Include(v => v.VehicleModel)
                    .Where(v => v.VehicleModel != null && modelIds.Contains(v.VehicleModel.ModelId) && v.Status == VehicleStatus.Available)
                    .GroupBy(v => v.VehicleModel!.ModelId)
                    .Select(g => new { ModelId = g.Key, Count = g.Count() })
                    .ToListAsync();
                
                modelAvailableCountDict = availableCounts.ToDictionary(vc => vc.ModelId, vc => vc.Count);
            }

            var models = modelsList.Select(m => new ModelDto
            {
                Id = m.Id,
                Make = m.Make,
                ModelName = m.ModelName,
                Year = m.Year,
                FuelType = m.FuelType,
                Transmission = m.Transmission,
                Seats = m.Seats,
                DailyRate = vehicleModelsDict.ContainsKey(m.Id) ? vehicleModelsDict[m.Id].DailyRate : null,
                Features = m.Features,
                Description = m.Description,
                CategoryId = m.CategoryId,
                CategoryName = m.Category != null ? m.Category.CategoryName : null,
                VehicleCount = modelVehicleCountDict.ContainsKey(m.Id) ? modelVehicleCountDict[m.Id] : 0,
                AvailableCount = modelAvailableCountDict.ContainsKey(m.Id) ? modelAvailableCountDict[m.Id] : 0
            }).ToList();

            return Ok(models);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching models");
            return StatusCode(500, "An error occurred while fetching models");
        }
    }

    /// <summary>
    /// Bulk update daily rates for models matching specific criteria
    /// </summary>
    /// <param name="request">Bulk update request with filters and new daily rate</param>
    /// <returns>Number of models updated</returns>
    [HttpPut("bulk-update-daily-rate")]
    [Authorize]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> BulkUpdateDailyRate([FromBody] BulkUpdateModelDailyRateDto request)
    {
        try
        {
            _logger.LogInformation("BulkUpdateDailyRate called with CompanyId={CompanyId}, CategoryId={CategoryId}, DailyRate={DailyRate}", 
                request.CompanyId, request.CategoryId, request.DailyRate);

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // CompanyId is required for vehicle_model updates
            if (!request.CompanyId.HasValue)
            {
                return BadRequest(new { message = "CompanyId is required for bulk daily rate updates" });
            }

            // Use simple raw SQL UPDATE with JOIN for efficiency
            var sql = new System.Text.StringBuilder();
            sql.Append("UPDATE vehicle_model vm ");
            sql.Append("SET daily_rate = {0} ");
            sql.Append("FROM models m ");
            sql.Append("WHERE vm.model_id = m.id AND vm.company_id = {1}");

            var parameters = new List<object> { request.DailyRate, request.CompanyId.Value };
            var paramIndex = 2;

            // Add category filter if provided
            if (request.CategoryId.HasValue)
            {
                sql.Append($" AND m.category_id = {{{paramIndex}}}");
                parameters.Add(request.CategoryId.Value);
                paramIndex++;
            }

            // Add make filter if provided
            if (!string.IsNullOrEmpty(request.Make))
            {
                sql.Append($" AND UPPER(TRIM(m.make)) = UPPER(TRIM({{{paramIndex}}}))");
                parameters.Add(request.Make);
                paramIndex++;
            }

            // Add model filter if provided
            if (!string.IsNullOrEmpty(request.ModelName))
            {
                sql.Append($" AND UPPER(TRIM(m.model)) = UPPER(TRIM({{{paramIndex}}}))");
                parameters.Add(request.ModelName);
                paramIndex++;
            }

            // Add year filter if provided
            if (request.Year.HasValue)
            {
                sql.Append($" AND m.year = {{{paramIndex}}}");
                parameters.Add(request.Year.Value);
            }

            _logger.LogInformation("Executing SQL: {Sql}", sql.ToString());

            var rowsAffected = await _context.Database.ExecuteSqlRawAsync(sql.ToString(), parameters.ToArray());

            _logger.LogInformation("Bulk updated {Count} vehicle_model catalog entries daily rate to {Rate}", 
                rowsAffected, request.DailyRate);

            // Note: Vehicles get their daily_rate from vehicle_model through the relationship
            // No need to update vehicles table directly - they reference vehicle_model via vehicle_model_id
            // All vehicles using the updated vehicle_model entries will automatically use the new rate

            return Ok(new { 
                CatalogEntriesUpdated = (int)rowsAffected,
                Message = $"Successfully updated {rowsAffected} catalog entries. All vehicles using these models will use the new rate." 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error bulk updating models daily rate");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Parses the first year from a comma-separated string of years
    /// </summary>
    /// <param name="yearsString">Comma-separated years string (e.g., "2020, 2021, 2022")</param>
    /// <returns>The first year as an integer, or 0 if parsing fails</returns>
    private static int ParseFirstYear(string? yearsString)
    {
        if (string.IsNullOrWhiteSpace(yearsString))
            return 0;

        var firstYear = yearsString.Split(',')[0].Trim();
        return int.TryParse(firstYear, out var year) ? year : 0;
    }
}

