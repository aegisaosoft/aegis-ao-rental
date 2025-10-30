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
    /// <returns>List of vehicle categories</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<object>), 200)]
    public async Task<IActionResult> GetVehicleCategories()
    {
        try
        {
            var categories = await _context.VehicleCategories
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
