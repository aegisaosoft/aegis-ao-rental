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
public class AdditionalServicesController : ControllerBase
{
    private readonly CarRentalDbContext _context;
    private readonly ILogger<AdditionalServicesController> _logger;

    public AdditionalServicesController(
        CarRentalDbContext context,
        ILogger<AdditionalServicesController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Get all additional services with optional filtering
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<AdditionalServiceDto>), 200)]
    public async Task<ActionResult<IEnumerable<AdditionalServiceDto>>> GetAdditionalServices(
        [FromQuery] Guid? companyId = null,
        [FromQuery] string? serviceType = null,
        [FromQuery] bool? isMandatory = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var query = _context.AdditionalServices
                .Include(s => s.Company)
                .AsQueryable();

            if (companyId.HasValue)
                query = query.Where(s => s.CompanyId == companyId.Value);

            if (!string.IsNullOrEmpty(serviceType))
                query = query.Where(s => s.ServiceType == serviceType);

            if (isMandatory.HasValue)
                query = query.Where(s => s.IsMandatory == isMandatory.Value);

            if (isActive.HasValue)
                query = query.Where(s => s.IsActive == isActive.Value);

            var totalCount = await query.CountAsync();

            var services = await query
                .OrderBy(s => s.CompanyId)
                .ThenBy(s => s.ServiceType)
                .ThenBy(s => s.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(s => new AdditionalServiceDto
                {
                    Id = s.Id,
                    CompanyId = s.CompanyId,
                    CompanyName = s.Company.CompanyName,
                    Name = s.Name,
                    Description = s.Description,
                    Price = s.Price,
                    ServiceType = s.ServiceType,
                    IsMandatory = s.IsMandatory,
                    MaxQuantity = s.MaxQuantity,
                    IsActive = s.IsActive,
                    CreatedAt = s.CreatedAt,
                    UpdatedAt = s.UpdatedAt
                })
                .ToListAsync();

            Response.Headers["X-Total-Count"] = totalCount.ToString();
            Response.Headers["X-Page-Number"] = page.ToString();
            Response.Headers["X-Page-Size"] = pageSize.ToString();

            return Ok(services);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving additional services");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get a specific additional service by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(AdditionalServiceDto), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<AdditionalServiceDto>> GetAdditionalService(Guid id)
    {
        try
        {
            var service = await _context.AdditionalServices
                .Include(s => s.Company)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (service == null)
                return NotFound();

            var serviceDto = new AdditionalServiceDto
            {
                Id = service.Id,
                CompanyId = service.CompanyId,
                CompanyName = service.Company.CompanyName,
                Name = service.Name,
                Description = service.Description,
                Price = service.Price,
                ServiceType = service.ServiceType,
                IsMandatory = service.IsMandatory,
                MaxQuantity = service.MaxQuantity,
                IsActive = service.IsActive,
                CreatedAt = service.CreatedAt,
                UpdatedAt = service.UpdatedAt
            };

            return Ok(serviceDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving additional service {ServiceId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Create a new additional service
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(AdditionalServiceDto), 201)]
    [ProducesResponseType(400)]
    public async Task<ActionResult<AdditionalServiceDto>> CreateAdditionalService(
        [FromBody] CreateAdditionalServiceDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // Validate service type
            if (!ServiceTypeConstants.AllTypes.Contains(createDto.ServiceType))
                return BadRequest($"Invalid service type. Valid types: {string.Join(", ", ServiceTypeConstants.AllTypes)}");

            // Check if company exists
            var companyExists = await _context.Companies
                .AnyAsync(c => c.Id == createDto.CompanyId);

            if (!companyExists)
                return BadRequest("Company not found");

            var service = new AdditionalService
            {
                CompanyId = createDto.CompanyId,
                Name = createDto.Name,
                Description = createDto.Description,
                Price = createDto.Price,
                ServiceType = createDto.ServiceType,
                IsMandatory = createDto.IsMandatory,
                MaxQuantity = createDto.MaxQuantity,
                IsActive = createDto.IsActive
            };

            _context.AdditionalServices.Add(service);
            await _context.SaveChangesAsync();

            // Load company for response
            await _context.Entry(service)
                .Reference(s => s.Company)
                .LoadAsync();

            var serviceDto = new AdditionalServiceDto
            {
                Id = service.Id,
                CompanyId = service.CompanyId,
                CompanyName = service.Company.CompanyName,
                Name = service.Name,
                Description = service.Description,
                Price = service.Price,
                ServiceType = service.ServiceType,
                IsMandatory = service.IsMandatory,
                MaxQuantity = service.MaxQuantity,
                IsActive = service.IsActive,
                CreatedAt = service.CreatedAt,
                UpdatedAt = service.UpdatedAt
            };

            return CreatedAtAction(
                nameof(GetAdditionalService),
                new { id = service.Id },
                serviceDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating additional service");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Update an existing additional service
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(AdditionalServiceDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<AdditionalServiceDto>> UpdateAdditionalService(
        Guid id,
        [FromBody] UpdateAdditionalServiceDto updateDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var service = await _context.AdditionalServices
                .Include(s => s.Company)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (service == null)
                return NotFound();

            // Validate service type if provided
            if (!string.IsNullOrEmpty(updateDto.ServiceType) &&
                !ServiceTypeConstants.AllTypes.Contains(updateDto.ServiceType))
                return BadRequest($"Invalid service type. Valid types: {string.Join(", ", ServiceTypeConstants.AllTypes)}");

            // Update fields
            if (!string.IsNullOrEmpty(updateDto.Name))
                service.Name = updateDto.Name;

            if (updateDto.Description != null)
                service.Description = updateDto.Description;

            if (updateDto.Price.HasValue)
                service.Price = updateDto.Price.Value;

            if (!string.IsNullOrEmpty(updateDto.ServiceType))
                service.ServiceType = updateDto.ServiceType;

            if (updateDto.IsMandatory.HasValue)
                service.IsMandatory = updateDto.IsMandatory.Value;

            if (updateDto.MaxQuantity.HasValue)
                service.MaxQuantity = updateDto.MaxQuantity.Value;

            if (updateDto.IsActive.HasValue)
                service.IsActive = updateDto.IsActive.Value;

            service.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var serviceDto = new AdditionalServiceDto
            {
                Id = service.Id,
                CompanyId = service.CompanyId,
                CompanyName = service.Company.CompanyName,
                Name = service.Name,
                Description = service.Description,
                Price = service.Price,
                ServiceType = service.ServiceType,
                IsMandatory = service.IsMandatory,
                MaxQuantity = service.MaxQuantity,
                IsActive = service.IsActive,
                CreatedAt = service.CreatedAt,
                UpdatedAt = service.UpdatedAt
            };

            return Ok(serviceDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating additional service {ServiceId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Delete an additional service
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeleteAdditionalService(Guid id)
    {
        try
        {
            var service = await _context.AdditionalServices.FindAsync(id);

            if (service == null)
                return NotFound();

            _context.AdditionalServices.Remove(service);
            await _context.SaveChangesAsync();

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting additional service {ServiceId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get available service types
    /// </summary>
    [HttpGet("service-types")]
    [ProducesResponseType(typeof(string[]), 200)]
    public ActionResult<string[]> GetServiceTypes()
    {
        return Ok(ServiceTypeConstants.AllTypes);
    }
}

