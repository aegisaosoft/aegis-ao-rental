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
using Microsoft.EntityFrameworkCore;
using CarRental.Api.Data;
using CarRental.Api.Models;

namespace CarRental.Api.Controllers;

[ApiController]
[Route("api/vehicles/categories")]
public class VehicleCategoriesController : ControllerBase
{
    private readonly CarRentalDbContext _context;
    private readonly ILogger<VehicleCategoriesController> _logger;

    public VehicleCategoriesController(CarRentalDbContext context, ILogger<VehicleCategoriesController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Get all vehicle categories
    /// </summary>
    /// <param name="companyId">Optional company ID to filter categories by vehicles in company fleet</param>
    /// <returns>List of vehicle categories</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<object>), 200)]
    public async Task<IActionResult> GetVehicleCategories([FromQuery] Guid? companyId = null)
    {
        try
        {
            var query = _context.VehicleCategories.AsQueryable();

            // If companyId provided, filter to only categories that have vehicles in that company
            if (companyId.HasValue)
            {
                // Get distinct category IDs from vehicles of this company via vehicle_model -> models
                var companyVehicles = await _context.Vehicles
                    .Include(v => v.VehicleModel)
                    .Where(v => v.CompanyId == companyId.Value && v.VehicleModel != null)
                    .ToListAsync();
                
                // Load Model for each VehicleModel that has one
                foreach (var vehicle in companyVehicles)
                {
                    await _context.Entry(vehicle.VehicleModel!)
                        .Reference(vm => vm.Model)
                        .LoadAsync();
                }
                
                // Project to distinct category IDs
                var companyCategoryIds = companyVehicles
                    .Where(v => v.VehicleModel?.Model?.CategoryId != null)
                    .Select(v => v.VehicleModel!.Model!.CategoryId!.Value)
                    .Distinct()
                    .ToList();

                _logger.LogInformation("Company {CompanyId} has categories: {Count}", companyId.Value, companyCategoryIds.Count);

                if (companyCategoryIds.Any())
                {
                    query = query.Where(c => companyCategoryIds.Contains(c.Id));
                }
                else
                {
                    // No categories for this company
                    _logger.LogWarning("No categories found for company {CompanyId}", companyId.Value);
                    return Ok(new List<object>());
                }
            }

            var categories = await query
                .Select(c => new
                {
                    c.Id,
                    c.CategoryName,
                    c.Description,
                    c.CreatedAt
                })
                .OrderBy(c => c.CategoryName)
                .ToListAsync();

            return Ok(categories);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving vehicle categories");
            return StatusCode(500, "Internal server error");
        }
    }
}
