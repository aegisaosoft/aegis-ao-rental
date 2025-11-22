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
using CarRental.Api.Helpers;

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
    [AllowAnonymous] // Allow anonymous access for public company locations
    public async Task<ActionResult<IEnumerable<CompanyLocationDto>>> GetCompanyLocations(
        [FromQuery] Guid? companyId = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] bool? isPickupLocation = null,
        [FromQuery] bool? isReturnLocation = null)
    {
        try
        {
            var query = _context.CompanyLocations
                .AsNoTracking() // Don't track entities to avoid circular references
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

            // Map to DTOs to avoid circular references
            var locationDtos = locations.Select(l => new CompanyLocationDto
            {
                LocationId = l.Id,
                CompanyId = l.CompanyId,
                LocationName = l.LocationName,
                Address = l.Address,
                City = l.City,
                State = l.State,
                Country = l.Country,
                PostalCode = l.PostalCode,
                Phone = l.Phone,
                Email = l.Email,
                Latitude = l.Latitude,
                Longitude = l.Longitude,
                IsActive = l.IsActive,
                IsPickupLocation = l.IsPickupLocation,
                IsReturnLocation = l.IsReturnLocation,
                IsOffice = l.IsOffice,
                OpeningHours = l.OpeningHours,
                CreatedAt = l.CreatedAt,
                UpdatedAt = l.UpdatedAt
            }).ToList();

            return Ok(locationDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving company locations");
            return StatusCode(500, "Internal server error while retrieving company locations");
        }
    }

    // GET: api/CompanyLocations/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<CompanyLocationDto>> GetCompanyLocation(Guid id)
    {
        try
        {
            var location = await _context.CompanyLocations
                .AsNoTracking() // Don't track entities to avoid circular references
                .FirstOrDefaultAsync(cl => cl.Id == id);

            if (location == null)
                return NotFound(new { message = "Company location not found" });

            // Map to DTO to avoid circular references
            var locationDto = new CompanyLocationDto
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
                IsOffice = location.IsOffice,
                OpeningHours = location.OpeningHours,
                CreatedAt = location.CreatedAt,
                UpdatedAt = location.UpdatedAt
            };

            return Ok(locationDto);
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
    public async Task<ActionResult<CompanyLocationDto>> CreateCompanyLocation([FromBody] CreateCompanyLocationDto dto)
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

            var locationId = Guid.NewGuid();
            var location = new CompanyLocation
            {
                Id = locationId,
                CompanyId = dto.CompanyId,
                LocationName = dto.LocationName,
                Address = dto.Address,
                City = dto.City,
                State = dto.State,
                Country = string.IsNullOrWhiteSpace(dto.Country) 
                    ? "US" 
                    : CountryHelper.NormalizeToIsoCode(dto.Country),
                PostalCode = dto.PostalCode,
                Phone = dto.Phone,
                Email = dto.Email,
                Latitude = dto.Latitude,
                Longitude = dto.Longitude,
                IsActive = dto.IsActive,
                IsPickupLocation = dto.IsPickupLocation,
                IsReturnLocation = dto.IsReturnLocation,
                IsOffice = dto.IsOffice,
                OpeningHours = dto.OpeningHours,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.CompanyLocations.Add(location);

            // Also create corresponding Location record with the same ID
            var locationRecord = new Location
            {
                Id = locationId,
                CompanyId = dto.CompanyId,
                LocationName = dto.LocationName,
                Address = dto.Address,
                City = dto.City,
                State = dto.State,
                Country = string.IsNullOrWhiteSpace(dto.Country) 
                    ? "US" 
                    : CountryHelper.NormalizeToIsoCode(dto.Country),
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

            _context.Locations.Add(locationRecord);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created company location {LocationId} for company {CompanyId} and synced to locations table", location.Id, dto.CompanyId);

            var locationDto = new CompanyLocationDto
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
                IsOffice = location.IsOffice,
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
    public async Task<IActionResult> UpdateCompanyLocation(Guid id, [FromBody] UpdateCompanyLocationDto dto)
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
            location.Country = string.IsNullOrWhiteSpace(dto.Country) 
                ? "US" 
                : CountryHelper.NormalizeToIsoCode(dto.Country);
            location.PostalCode = dto.PostalCode;
            location.Phone = dto.Phone;
            location.Email = dto.Email;
            location.Latitude = dto.Latitude;
            location.Longitude = dto.Longitude;
            location.IsActive = dto.IsActive;
            location.IsPickupLocation = dto.IsPickupLocation;
            location.IsReturnLocation = dto.IsReturnLocation;
            location.IsOffice = dto.IsOffice;
            location.OpeningHours = dto.OpeningHours;
            location.UpdatedAt = DateTime.UtcNow;

            // Also update corresponding Location record with the same ID
            var locationRecord = await _context.Locations.FindAsync(id);
            if (locationRecord != null)
            {
                locationRecord.CompanyId = dto.CompanyId;
                locationRecord.LocationName = dto.LocationName;
                locationRecord.Address = dto.Address;
                locationRecord.City = dto.City;
                locationRecord.State = dto.State;
                locationRecord.Country = string.IsNullOrWhiteSpace(dto.Country) 
                    ? "US" 
                    : CountryHelper.NormalizeToIsoCode(dto.Country);
                locationRecord.PostalCode = dto.PostalCode;
                locationRecord.Phone = dto.Phone;
                locationRecord.Email = dto.Email;
                locationRecord.Latitude = dto.Latitude;
                locationRecord.Longitude = dto.Longitude;
                locationRecord.IsActive = dto.IsActive;
                locationRecord.IsPickupLocation = dto.IsPickupLocation;
                locationRecord.IsReturnLocation = dto.IsReturnLocation;
                locationRecord.OpeningHours = dto.OpeningHours;
                locationRecord.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                // If Location record doesn't exist, create it
                locationRecord = new Location
                {
                    Id = id,
                    CompanyId = dto.CompanyId,
                    LocationName = dto.LocationName,
                    Address = dto.Address,
                    City = dto.City,
                    State = dto.State,
                    Country = string.IsNullOrWhiteSpace(dto.Country) 
                    ? "US" 
                    : CountryHelper.NormalizeToIsoCode(dto.Country),
                    PostalCode = dto.PostalCode,
                    Phone = dto.Phone,
                    Email = dto.Email,
                    Latitude = dto.Latitude,
                    Longitude = dto.Longitude,
                    IsActive = dto.IsActive,
                    IsPickupLocation = dto.IsPickupLocation,
                    IsReturnLocation = dto.IsReturnLocation,
                    OpeningHours = dto.OpeningHours,
                    CreatedAt = location.CreatedAt,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.Locations.Add(locationRecord);
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Updated company location {LocationId} and synced to locations table", id);

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

            // Update corresponding Location record with the same ID to set companyId to null
            // This converts it from a company location to a regular pickup location
            var locationRecord = await _context.Locations.FindAsync(id);
            if (locationRecord != null)
            {
                locationRecord.CompanyId = null;
                locationRecord.UpdatedAt = DateTime.UtcNow;
                _logger.LogInformation("Updated location {LocationId} companyId set to null (converted to regular location)", id);
            }

            // Delete the company location - database will automatically set vehicles.location_id to NULL
            // due to OnDelete(DeleteBehavior.SetNull) foreign key constraint
            _context.CompanyLocations.Remove(location);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Deleted company location {LocationId} and updated corresponding location record (companyId set to null), {VehicleCount} vehicles updated (location_id set to NULL)", id, vehicleCount);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting company location {LocationId}", id);
            return StatusCode(500, new { message = "Internal server error while deleting company location" });
        }
    }
}

