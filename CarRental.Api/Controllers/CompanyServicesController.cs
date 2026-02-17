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
using Microsoft.Extensions.Caching.Memory;
using CarRental.Api.Data;
using CarRental.Api.DTOs;
using CarRental.Api.Models;

namespace CarRental.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CompanyServicesController : ControllerBase
{
    private readonly CarRentalDbContext _context;
    private readonly ILogger<CompanyServicesController> _logger;
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(30);

    public CompanyServicesController(
        CarRentalDbContext context,
        ILogger<CompanyServicesController> logger,
        IMemoryCache cache)
    {
        _context = context;
        _logger = logger;
        _cache = cache;
    }

    private static string GetCacheKey(Guid companyId, bool? isActive) =>
        $"company_services_{companyId}_{isActive?.ToString() ?? "all"}";

    /// <summary>
    /// Get all services for a specific company
    /// </summary>
    [HttpGet("company/{companyId}")]
    [ProducesResponseType(typeof(IEnumerable<CompanyServiceDto>), 200)]
    public async Task<ActionResult<IEnumerable<CompanyServiceDto>>> GetCompanyServices(
        Guid companyId,
        [FromQuery] bool? isActive = null)
    {
        try
        {
            var cacheKey = GetCacheKey(companyId, isActive);
            if (_cache.TryGetValue(cacheKey, out List<CompanyServiceDto>? cached))
            {
                _logger.LogDebug("CompanyServices cache HIT for {CompanyId}", companyId);
                return Ok(cached);
            }

            _logger.LogDebug("CompanyServices cache MISS for {CompanyId}, loading from DB", companyId);

            var query = _context.CompanyServices
                .AsNoTracking()
                .Include(cs => cs.Company)
                .Include(cs => cs.AdditionalService)
                .Where(cs => cs.CompanyId == companyId);

            if (isActive.HasValue)
                query = query.Where(cs => cs.IsActive == isActive.Value);

            var companyServices = await query
                .OrderBy(cs => cs.AdditionalService.ServiceType)
                .ThenBy(cs => cs.AdditionalService.Name)
                .Select(cs => new CompanyServiceDto
                {
                    CompanyId = cs.CompanyId,
                    AdditionalServiceId = cs.AdditionalServiceId,
                    Price = cs.Price,
                    IsMandatory = cs.IsMandatory,
                    IsActive = cs.IsActive,
                    CreatedAt = cs.CreatedAt,
                    CompanyName = cs.Company.CompanyName,
                    ServiceName = cs.AdditionalService.Name,
                    ServiceDescription = cs.AdditionalService.Description,
                    ServicePrice = cs.Price ?? cs.AdditionalService.Price,
                    ServiceType = cs.AdditionalService.ServiceType,
                    ServiceIsMandatory = cs.IsMandatory ?? cs.AdditionalService.IsMandatory,
                    ServiceMaxQuantity = cs.AdditionalService.MaxQuantity
                })
                .ToListAsync();

            _cache.Set(cacheKey, companyServices, CacheExpiration);

            return Ok(companyServices);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving services for company {CompanyId}", companyId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get all companies that offer a specific service
    /// </summary>
    [HttpGet("service/{serviceId}")]
    [ProducesResponseType(typeof(IEnumerable<CompanyServiceDto>), 200)]
    public async Task<ActionResult<IEnumerable<CompanyServiceDto>>> GetServiceCompanies(
        Guid serviceId,
        [FromQuery] bool? isActive = null)
    {
        try
        {
            var query = _context.CompanyServices
                .Include(cs => cs.Company)
                .Include(cs => cs.AdditionalService)
                .Where(cs => cs.AdditionalServiceId == serviceId);

            if (isActive.HasValue)
                query = query.Where(cs => cs.IsActive == isActive.Value);

            var companyServices = await query
                .OrderBy(cs => cs.Company.CompanyName)
                .Select(cs => new CompanyServiceDto
                {
                    CompanyId = cs.CompanyId,
                    AdditionalServiceId = cs.AdditionalServiceId,
                    Price = cs.Price,
                    IsMandatory = cs.IsMandatory,
                    IsActive = cs.IsActive,
                    CreatedAt = cs.CreatedAt,
                    CompanyName = cs.Company.CompanyName,
                    ServiceName = cs.AdditionalService.Name,
                    ServiceDescription = cs.AdditionalService.Description,
                    ServicePrice = cs.Price ?? cs.AdditionalService.Price, // Use custom price if set, otherwise use service price
                    ServiceType = cs.AdditionalService.ServiceType,
                    ServiceIsMandatory = cs.IsMandatory ?? cs.AdditionalService.IsMandatory, // Use custom mandatory if set, otherwise use service mandatory
                    ServiceMaxQuantity = cs.AdditionalService.MaxQuantity
                })
                .ToListAsync();

            return Ok(companyServices);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving companies for service {ServiceId}", serviceId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get a specific company-service relationship
    /// </summary>
    [HttpGet("{companyId}/{serviceId}")]
    [ProducesResponseType(typeof(CompanyServiceDto), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<CompanyServiceDto>> GetCompanyService(Guid companyId, Guid serviceId)
    {
        try
        {
            var companyService = await _context.CompanyServices
                .Include(cs => cs.Company)
                .Include(cs => cs.AdditionalService)
                .FirstOrDefaultAsync(cs => cs.CompanyId == companyId && cs.AdditionalServiceId == serviceId);

            if (companyService == null)
                return NotFound();

            var dto = new CompanyServiceDto
            {
                CompanyId = companyService.CompanyId,
                AdditionalServiceId = companyService.AdditionalServiceId,
                Price = companyService.Price,
                IsMandatory = companyService.IsMandatory,
                IsActive = companyService.IsActive,
                CreatedAt = companyService.CreatedAt,
                CompanyName = companyService.Company.CompanyName,
                ServiceName = companyService.AdditionalService.Name,
                ServiceDescription = companyService.AdditionalService.Description,
                ServicePrice = companyService.Price ?? companyService.AdditionalService.Price, // Use custom price if set, otherwise use service price
                ServiceType = companyService.AdditionalService.ServiceType,
                ServiceIsMandatory = companyService.IsMandatory ?? companyService.AdditionalService.IsMandatory, // Use custom mandatory if set, otherwise use service mandatory
                ServiceMaxQuantity = companyService.AdditionalService.MaxQuantity
            };

            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving company service {CompanyId}/{ServiceId}", companyId, serviceId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Add a service to a company
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CompanyServiceDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(409)]
    public async Task<ActionResult<CompanyServiceDto>> AddServiceToCompany([FromBody] CreateCompanyServiceDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // Check if company exists
            var companyExists = await _context.Companies
                .AnyAsync(c => c.Id == createDto.CompanyId);

            if (!companyExists)
                return BadRequest("Company not found");

            // Check if service exists
            var serviceExists = await _context.AdditionalServices
                .AnyAsync(s => s.Id == createDto.AdditionalServiceId);

            if (!serviceExists)
                return BadRequest("Service not found");

            // Check if relationship already exists
            var existingRelation = await _context.CompanyServices
                .AnyAsync(cs => cs.CompanyId == createDto.CompanyId && 
                               cs.AdditionalServiceId == createDto.AdditionalServiceId);

            if (existingRelation)
                return Conflict("This service is already added to the company");

            var companyService = new CompanyServiceLink
            {
                CompanyId = createDto.CompanyId,
                AdditionalServiceId = createDto.AdditionalServiceId,
                Price = createDto.Price,
                IsMandatory = createDto.IsMandatory,
                IsActive = createDto.IsActive
            };

            _context.CompanyServices.Add(companyService);
            await _context.SaveChangesAsync();

            // Invalidate cache for this company
            InvalidateCompanyCache(createDto.CompanyId);

            // Load related data for response
            await _context.Entry(companyService)
                .Reference(cs => cs.Company)
                .LoadAsync();
            await _context.Entry(companyService)
                .Reference(cs => cs.AdditionalService)
                .LoadAsync();

            var dto = new CompanyServiceDto
            {
                CompanyId = companyService.CompanyId,
                AdditionalServiceId = companyService.AdditionalServiceId,
                Price = companyService.Price,
                IsMandatory = companyService.IsMandatory,
                IsActive = companyService.IsActive,
                CreatedAt = companyService.CreatedAt,
                CompanyName = companyService.Company.CompanyName,
                ServiceName = companyService.AdditionalService.Name,
                ServiceDescription = companyService.AdditionalService.Description,
                ServicePrice = companyService.Price ?? companyService.AdditionalService.Price, // Use custom price if set, otherwise use service price
                ServiceType = companyService.AdditionalService.ServiceType,
                ServiceIsMandatory = companyService.IsMandatory ?? companyService.AdditionalService.IsMandatory, // Use custom mandatory if set, otherwise use service mandatory
                ServiceMaxQuantity = companyService.AdditionalService.MaxQuantity
            };

            return CreatedAtAction(
                nameof(GetCompanyService),
                new { companyId = companyService.CompanyId, serviceId = companyService.AdditionalServiceId },
                dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding service to company");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Update a company-service relationship (e.g., activate/deactivate)
    /// </summary>
    [HttpPut("{companyId}/{serviceId}")]
    [ProducesResponseType(typeof(CompanyServiceDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<CompanyServiceDto>> UpdateCompanyService(
        Guid companyId,
        Guid serviceId,
        [FromBody] UpdateCompanyServiceDto updateDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var companyService = await _context.CompanyServices
                .Include(cs => cs.Company)
                .Include(cs => cs.AdditionalService)
                .FirstOrDefaultAsync(cs => cs.CompanyId == companyId && cs.AdditionalServiceId == serviceId);

            if (companyService == null)
                return NotFound();

            if (updateDto.Price.HasValue)
                companyService.Price = updateDto.Price.Value;

            if (updateDto.IsMandatory.HasValue)
                companyService.IsMandatory = updateDto.IsMandatory.Value;

            if (updateDto.IsActive.HasValue)
                companyService.IsActive = updateDto.IsActive.Value;

            await _context.SaveChangesAsync();

            // Invalidate cache for this company
            InvalidateCompanyCache(companyId);

            var dto = new CompanyServiceDto
            {
                CompanyId = companyService.CompanyId,
                AdditionalServiceId = companyService.AdditionalServiceId,
                Price = companyService.Price,
                IsMandatory = companyService.IsMandatory,
                IsActive = companyService.IsActive,
                CreatedAt = companyService.CreatedAt,
                CompanyName = companyService.Company.CompanyName,
                ServiceName = companyService.AdditionalService.Name,
                ServiceDescription = companyService.AdditionalService.Description,
                ServicePrice = companyService.Price ?? companyService.AdditionalService.Price, // Use custom price if set, otherwise use service price
                ServiceType = companyService.AdditionalService.ServiceType,
                ServiceIsMandatory = companyService.IsMandatory ?? companyService.AdditionalService.IsMandatory, // Use custom mandatory if set, otherwise use service mandatory
                ServiceMaxQuantity = companyService.AdditionalService.MaxQuantity
            };

            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating company service {CompanyId}/{ServiceId}", companyId, serviceId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Remove a service from a company
    /// </summary>
    [HttpDelete("{companyId}/{serviceId}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> RemoveServiceFromCompany(Guid companyId, Guid serviceId)
    {
        try
        {
            var companyService = await _context.CompanyServices
                .FirstOrDefaultAsync(cs => cs.CompanyId == companyId && cs.AdditionalServiceId == serviceId);

            if (companyService == null)
                return NotFound();

            _context.CompanyServices.Remove(companyService);
            await _context.SaveChangesAsync();

            // Invalidate cache for this company
            InvalidateCompanyCache(companyId);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing service from company {CompanyId}/{ServiceId}", companyId, serviceId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Invalidate all cached service data for a company (both "active only" and "all" variants)
    /// </summary>
    private void InvalidateCompanyCache(Guid companyId)
    {
        _cache.Remove(GetCacheKey(companyId, true));
        _cache.Remove(GetCacheKey(companyId, false));
        _cache.Remove(GetCacheKey(companyId, null));
        _cache.Remove($"booking_info_{companyId}"); // Also invalidate combined booking info
        _logger.LogInformation("Invalidated CompanyServices + BookingInfo cache for company {CompanyId}", companyId);
    }
}

