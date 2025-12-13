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

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CarRental.Api.Data;
using CarRental.Api.DTOs;
using CarRental.Api.Models;
using CarRental.Api.Extensions;

namespace CarRental.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FindersListController : ControllerBase
{
    private readonly CarRentalDbContext _context;
    private readonly ILogger<FindersListController> _logger;

    public FindersListController(
        CarRentalDbContext context,
        ILogger<FindersListController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Get finders list configuration for a company
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(FindersListDto), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GetFindersList([FromQuery] Guid? companyId = null)
    {
        try
        {
            // Get company ID from context (set by CompanyMiddleware) or from query parameter
            var resolvedCompanyId = HttpContext.GetCompanyIdAsGuid() ?? companyId;

            if (resolvedCompanyId == null || resolvedCompanyId == Guid.Empty)
            {
                return BadRequest(new { message = "Company ID is required" });
            }

            var findersList = await _context.FindersLists
                .FirstOrDefaultAsync(f => f.CompanyId == resolvedCompanyId.Value);

            if (findersList == null)
            {
                // Return empty list if not found
                return Ok(new FindersListDto
                {
                    Id = Guid.Empty,
                    CompanyId = resolvedCompanyId.Value,
                    FindersList = new List<string>(),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            var result = new FindersListDto
            {
                Id = findersList.Id,
                CompanyId = findersList.CompanyId,
                FindersList = findersList.StateCodes,
                CreatedAt = findersList.CreatedAt,
                UpdatedAt = findersList.UpdatedAt
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving finders list for company {CompanyId}", companyId);
            return StatusCode(500, new { message = "An error occurred while retrieving finders list", error = ex.Message });
        }
    }

    /// <summary>
    /// Create or update finders list configuration for a company
    /// </summary>
    [HttpPost]
    [HttpPut]
    [ProducesResponseType(typeof(FindersListDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> UpsertFindersList([FromBody] UpdateFindersListDto dto, [FromQuery] Guid? companyId = null)
    {
        try
        {
            if (dto == null || dto.FindersList == null)
            {
                return BadRequest(new { message = "Finders list is required" });
            }

            // Get company ID from context (set by CompanyMiddleware) or from query parameter
            var resolvedCompanyId = HttpContext.GetCompanyIdAsGuid() ?? companyId;

            if (resolvedCompanyId == null || resolvedCompanyId == Guid.Empty)
            {
                return BadRequest(new { message = "Company ID is required" });
            }

            // Get company to check country
            var company = await _context.Companies
                .FirstOrDefaultAsync(c => c.Id == resolvedCompanyId.Value);

            if (company == null)
            {
                return BadRequest(new { message = "Company not found" });
            }

            // Prepare the finders list - add "USA" if company country is United States
            var findersListToSave = new List<string>(dto.FindersList ?? new List<string>());
            var companyCountry = company.Country?.Trim() ?? "";
            
            if (string.Equals(companyCountry, "United States", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(companyCountry, "USA", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(companyCountry, "US", StringComparison.OrdinalIgnoreCase))
            {
                // Add "USA" if not already in the list
                if (!findersListToSave.Contains("USA", StringComparer.OrdinalIgnoreCase))
                {
                    findersListToSave.Add("USA");
                }
            }

            var existing = await _context.FindersLists
                .FirstOrDefaultAsync(f => f.CompanyId == resolvedCompanyId.Value);

            if (existing != null)
            {
                // Update existing
                existing.StateCodes = findersListToSave;
                existing.UpdatedAt = DateTime.UtcNow;
                _context.FindersLists.Update(existing);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Updated finders list for company {CompanyId} with {Count} states (country: {Country})", 
                    resolvedCompanyId.Value, findersListToSave.Count, companyCountry);

                return Ok(new FindersListDto
                {
                    Id = existing.Id,
                    CompanyId = existing.CompanyId,
                    FindersList = existing.StateCodes,
                    CreatedAt = existing.CreatedAt,
                    UpdatedAt = existing.UpdatedAt
                });
            }
            else
            {
                // Create new
                var newFindersList = new FindersList
                {
                    CompanyId = resolvedCompanyId.Value,
                    StateCodes = findersListToSave,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.FindersLists.Add(newFindersList);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Created finders list for company {CompanyId} with {Count} states (country: {Country})", 
                    resolvedCompanyId.Value, findersListToSave.Count, companyCountry);

                return Ok(new FindersListDto
                {
                    Id = newFindersList.Id,
                    CompanyId = newFindersList.CompanyId,
                    FindersList = newFindersList.StateCodes,
                    CreatedAt = newFindersList.CreatedAt,
                    UpdatedAt = newFindersList.UpdatedAt
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving finders list for company {CompanyId}", companyId);
            return StatusCode(500, new { message = "An error occurred while saving finders list", error = ex.Message });
        }
    }
}
