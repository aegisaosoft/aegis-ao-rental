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
using CarRental.Api.DTOs;
using CarRental.Api.Models;

namespace CarRental.Api.Controllers;

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
    /// Get all models grouped by category
    /// </summary>
    /// <param name="companyId">Optional company ID to filter models by vehicles in company fleet</param>
    [HttpGet("grouped-by-category")]
    [ProducesResponseType(typeof(IEnumerable<ModelsGroupedByCategoryDto>), 200)]
    public async Task<IActionResult> GetModelsGroupedByCategory([FromQuery] Guid? companyId = null)
    {
        try
        {
            var query = _context.Models
                .Include(m => m.Category)
                .AsQueryable();

            // If companyId is provided, only return models that exist in the company's vehicle fleet
            if (companyId.HasValue)
            {
                // Use a subquery that EF Core can translate to SQL
                // Filter models to only those that have matching vehicles in the company fleet
                query = query.Where(m => _context.Vehicles
                    .Any(v => v.CompanyId == companyId.Value &&
                             v.Status != VehicleStatus.OutOfService &&
                             v.Make.ToUpper() == m.Make.ToUpper() &&
                             v.Model.ToUpper() == m.ModelName.ToUpper()));
            }

            var models = await query
                .OrderBy(m => m.Category != null ? m.Category.CategoryName : "")
                .ThenBy(m => m.Make)
                .ThenBy(m => m.ModelName)
                .ThenByDescending(m => m.Year)
                .ToListAsync();

            var grouped = models
                .Where(m => m.Category != null)
                .GroupBy(m => new 
                { 
                    CategoryId = m.Category!.Id, 
                    CategoryName = m.Category.CategoryName,
                    CategoryDescription = m.Category.Description
                })
                .Select(g => new ModelsGroupedByCategoryDto
                {
                    CategoryId = g.Key.CategoryId,
                    CategoryName = g.Key.CategoryName,
                    CategoryDescription = g.Key.CategoryDescription,
                    Models = g.Select(m => new ModelDto
                    {
                        Id = m.Id,
                        Make = m.Make,
                        ModelName = m.ModelName,
                        Year = m.Year,
                        FuelType = m.FuelType,
                        Transmission = m.Transmission,
                        Seats = m.Seats,
                        DailyRate = m.DailyRate,
                        Features = m.Features,
                        Description = m.Description,
                        CategoryId = m.CategoryId,
                        CategoryName = m.Category!.CategoryName
                    }).ToList()
                })
                .OrderBy(g => g.CategoryName)
                .ToList();

            return Ok(grouped);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching models grouped by category");
            return StatusCode(500, "An error occurred while fetching models");
        }
    }

    /// <summary>
    /// Get all models
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ModelDto>), 200)]
    public async Task<IActionResult> GetModels(
        [FromQuery] Guid? categoryId = null,
        [FromQuery] string? make = null,
        [FromQuery] string? modelName = null,
        [FromQuery] int? year = null)
    {
        try
        {
            var query = _context.Models
                .Include(m => m.Category)
                .AsQueryable();

            if (categoryId.HasValue)
                query = query.Where(m => m.CategoryId == categoryId.Value);

            if (!string.IsNullOrEmpty(make))
                query = query.Where(m => m.Make.Contains(make));

            if (!string.IsNullOrEmpty(modelName))
                query = query.Where(m => m.ModelName.Contains(modelName));

            if (year.HasValue)
                query = query.Where(m => m.Year == year.Value);

            var models = await query
                .OrderBy(m => m.Make)
                .ThenBy(m => m.ModelName)
                .ThenByDescending(m => m.Year)
                .Select(m => new ModelDto
                {
                    Id = m.Id,
                    Make = m.Make,
                    ModelName = m.ModelName,
                    Year = m.Year,
                    FuelType = m.FuelType,
                    Transmission = m.Transmission,
                    Seats = m.Seats,
                    DailyRate = m.DailyRate,
                    Features = m.Features,
                    Description = m.Description,
                    CategoryId = m.CategoryId,
                    CategoryName = m.Category != null ? m.Category.CategoryName : null
                })
                .ToListAsync();

            return Ok(models);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching models");
            return StatusCode(500, "An error occurred while fetching models");
        }
    }
}

