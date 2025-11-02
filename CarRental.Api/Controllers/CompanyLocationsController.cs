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
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using CarRental.Api.Data;
using CarRental.Api.DTOs;
using CarRental.Api.Models;

namespace CarRental.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CompanyLocationsController : ControllerBase
{
    private readonly CarRentalDbContext _context;
    private readonly ILogger<CompanyLocationsController> _logger;

    public CompanyLocationsController(CarRentalDbContext context, ILogger<CompanyLocationsController> logger)
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
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? User.FindFirst("role")?.Value;
        return role == "admin" || role == "mainadmin";
    }

    /// <summary>
    /// Check if the current user can edit locations for a specific company
    /// </summary>
    /// <param name="companyId">Company ID to check</param>
    /// <returns>True if user can edit locations for this company</returns>
    private bool CanEditCompanyLocations(Guid companyId)
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? User.FindFirst("role")?.Value;
        var userCompanyId = User.FindFirst("company_id")?.Value;

        // Mainadmin can edit all locations
        if (role == "mainadmin")
            return true;

        // Admin can only edit locations from their own company
        if (role == "admin" && userCompanyId != null)
            return Guid.TryParse(userCompanyId, out var parsedCompanyId) && parsedCompanyId == companyId;

        return false;
    }

    // GET: api/CompanyLocations
    [HttpGet]
    public async Task<ActionResult<IEnumerable<CompanyLocation>>> GetCompanyLocations(
        [FromQuery] Guid? companyId = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] bool? isPickupLocation = null,
        [FromQuery] bool? isReturnLocation = null)
    {
        try
        {
            var query = _context.CompanyLocations
                .Include(cl => cl.Company)
                .AsQueryable();

            // Apply filters
            if (companyId.HasValue)
                query = query.Where(cl => cl.CompanyId == companyId.Value);

            if (isActive.HasValue)
                query = query.Where(cl => cl.IsActive == isActive.Value);

            if (isPickupLocation.HasValue)
                query = query.Where(cl => cl.IsPickupLocation == isPickupLocation.Value);

            if (isReturnLocation.HasValue)
                query = query.Where(cl => cl.IsReturnLocation == isReturnLocation.Value);

            var locations = await query
                .OrderBy(cl => cl.LocationName)
                .ToListAsync();

            return Ok(locations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving company locations");
            return StatusCode(500, "Internal server error while retrieving company locations");
        }
    }

    // GET: api/CompanyLocations/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<CompanyLocation>> GetCompanyLocation(Guid id)
    {
        try
        {
            var location = await _context.CompanyLocations
                .Include(cl => cl.Company)
                .FirstOrDefaultAsync(cl => cl.Id == id);

            if (location == null)
                return NotFound(new { message = "Company location not found" });

            return Ok(location);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving company location {LocationId}", id);
            return StatusCode(500, new { message = "Internal server error while retrieving company location" });
        }
    }

    // POST: api/CompanyLocations
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<LocationDto>> CreateCompanyLocation([FromBody] CreateLocationDto dto)
    {
        try
        {
            // Check if user has admin privileges
            if (!HasAdminPrivileges())
                return Forbid();

            // Check if user can edit locations for this company
            if (!CanEditCompanyLocations(dto.CompanyId))
                return Forbid();

            // Verify company exists
            var companyExists = await _context.Companies.AnyAsync(c => c.Id == dto.CompanyId);
            if (!companyExists)
            {
                return BadRequest(new { message = "Company not found" });
            }

            var location = new CompanyLocation
            {
                CompanyId = dto.CompanyId,
                LocationName = dto.LocationName,
                Address = dto.Address,
                City = dto.City,
                State = dto.State,
                Country = dto.Country,
                PostalCode = dto.PostalCode,
                Phone = dto.Phone,
                Email = dto.Email,
                Latitude = dto.Latitude,
                Longitude = dto.Longitude,
                IsActive = dto.IsActive,
                IsPickupLocation = dto.IsPickupLocation,
                IsReturnLocation = dto.IsReturnLocation,
                OpeningHours = dto.OpeningHours,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.CompanyLocations.Add(location);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created company location {LocationId} for company {CompanyId}", location.Id, dto.CompanyId);

            var locationDto = new LocationDto
            {
                LocationId = location.Id,
                CompanyId = location.CompanyId,
                LocationName = location.LocationName,
                Address = location.Address,
                City = location.City,
                State = location.State,
                Country = location.Country,
                PostalCode = location.PostalCode,
                Phone = location.Phone,
                Email = location.Email,
                Latitude = location.Latitude,
                Longitude = location.Longitude,
                IsActive = location.IsActive,
                IsPickupLocation = location.IsPickupLocation,
                IsReturnLocation = location.IsReturnLocation,
                OpeningHours = location.OpeningHours,
                CreatedAt = location.CreatedAt,
                UpdatedAt = location.UpdatedAt
            };

            return CreatedAtAction(nameof(GetCompanyLocation), new { id = location.Id }, locationDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating company location");
            return StatusCode(500, new { message = "Internal server error while creating company location" });
        }
    }

    // PUT: api/CompanyLocations/{id}
    [HttpPut("{id}")]
    [Authorize]
    public async Task<IActionResult> UpdateCompanyLocation(Guid id, [FromBody] UpdateLocationDto dto)
    {
        try
        {
            // Check if user has admin privileges
            if (!HasAdminPrivileges())
                return Forbid();

            var location = await _context.CompanyLocations.FindAsync(id);
            if (location == null)
            {
                return NotFound(new { message = "Company location not found" });
            }

            // Check if user can edit locations for this company
            if (!CanEditCompanyLocations(location.CompanyId))
                return Forbid();

            // Verify company exists if changed
            if (location.CompanyId != dto.CompanyId)
            {
                var companyExists = await _context.Companies.AnyAsync(c => c.Id == dto.CompanyId);
                if (!companyExists)
                {
                    return BadRequest(new { message = "Company not found" });
                }
            }

            // Update location properties
            location.CompanyId = dto.CompanyId;
            location.LocationName = dto.LocationName;
            location.Address = dto.Address;
            location.City = dto.City;
            location.State = dto.State;
            location.Country = dto.Country;
            location.PostalCode = dto.PostalCode;
            location.Phone = dto.Phone;
            location.Email = dto.Email;
            location.Latitude = dto.Latitude;
            location.Longitude = dto.Longitude;
            location.IsActive = dto.IsActive;
            location.IsPickupLocation = dto.IsPickupLocation;
            location.IsReturnLocation = dto.IsReturnLocation;
            location.OpeningHours = dto.OpeningHours;
            location.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Updated company location {LocationId}", id);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating company location {LocationId}", id);
            return StatusCode(500, new { message = "Internal server error while updating company location" });
        }
    }

    // DELETE: api/CompanyLocations/{id}
    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> DeleteCompanyLocation(Guid id)
    {
        try
        {
            // Check if user has admin privileges
            if (!HasAdminPrivileges())
                return Forbid();

            var location = await _context.CompanyLocations.FindAsync(id);
            if (location == null)
            {
                return NotFound(new { message = "Company location not found" });
            }

            // Check if user can edit locations for this company
            if (!CanEditCompanyLocations(location.CompanyId))
                return Forbid();

            // Count vehicles that will be affected (for logging)
            var vehicleCount = await _context.Vehicles.CountAsync(v => v.LocationId == id);
            _logger.LogInformation("Deleting company location {LocationId} with {VehicleCount} vehicles assigned", id, vehicleCount);

            // Delete the location - database will automatically set vehicles.location_id to NULL
            // due to OnDelete(DeleteBehavior.SetNull) foreign key constraint
            _context.CompanyLocations.Remove(location);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Deleted company location {LocationId}, {VehicleCount} vehicles updated (location_id set to NULL)", id, vehicleCount);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting company location {LocationId}", id);
            return StatusCode(500, new { message = "Internal server error while deleting company location" });
        }
    }
}

