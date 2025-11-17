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
public class LocationsController : ControllerBase
{
    private readonly CarRentalDbContext _context;
    private readonly ILogger<LocationsController> _logger;

    public LocationsController(CarRentalDbContext context, ILogger<LocationsController> logger)
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
    /// <param name="companyId">Company ID to check (nullable for regular locations)</param>
    /// <returns>True if user can edit locations for this company</returns>
    private bool CanEditCompanyLocations(Guid? companyId)
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? User.FindFirst("role")?.Value;
        
        // If companyId is null (regular location), only mainadmin can edit
        if (!companyId.HasValue)
        {
            return role == "mainadmin";
        }

        var userCompanyId = User.FindFirst("company_id")?.Value;

        // Mainadmin can edit all locations
        if (role == "mainadmin")
            return true;

        // Admin can only edit locations from their own company
        if (role == "admin" && userCompanyId != null)
            return Guid.TryParse(userCompanyId, out var parsedCompanyId) && parsedCompanyId == companyId.Value;

        return false;
    }

    // GET: api/Locations
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Location>>> GetLocations(
        [FromQuery] Guid? companyId = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] bool? isPickupLocation = null,
        [FromQuery] bool? isReturnLocation = null,
        [FromQuery] string? state = null,
        [FromQuery] string? city = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var query = _context.Locations
                .Include(l => l.Company)
                .AsQueryable();

            // Apply filters
            if (companyId.HasValue)
                query = query.Where(l => l.CompanyId == companyId.Value);

            if (isActive.HasValue)
                query = query.Where(l => l.IsActive == isActive.Value);

            if (isPickupLocation.HasValue)
                query = query.Where(l => l.IsPickupLocation == isPickupLocation.Value);

            if (isReturnLocation.HasValue)
                query = query.Where(l => l.IsReturnLocation == isReturnLocation.Value);

            if (!string.IsNullOrEmpty(state))
                query = query.Where(l => l.State == state);

            if (!string.IsNullOrEmpty(city))
                query = query.Where(l => l.City != null && l.City.Contains(city));

            // Get total count before pagination
            var totalCount = await query.CountAsync();

            // Apply pagination
            var locations = await query
                .OrderBy(l => l.LocationName)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Add pagination headers
            Response.Headers["X-Total-Count"] = totalCount.ToString();
            Response.Headers["X-Page-Number"] = pageNumber.ToString();
            Response.Headers["X-Page-Size"] = pageSize.ToString();

            return Ok(locations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving locations");
            return StatusCode(500, new { message = "Internal server error while retrieving locations" });
        }
    }

    // GET: api/Locations/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<Location>> GetLocation(Guid id)
    {
        try
        {
            var location = await _context.Locations
                .Include(l => l.Company)
                .Include(l => l.Vehicles)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (location == null)
            {
                return NotFound(new { message = $"Location with ID {id} not found" });
            }

            return Ok(location);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving location {LocationId}", id);
            return StatusCode(500, new { message = "Internal server error while retrieving location" });
        }
    }

    // GET: api/Locations/company/{companyId}
    [HttpGet("company/{companyId}")]
    public async Task<ActionResult<IEnumerable<Location>>> GetLocationsByCompany(Guid companyId)
    {
        try
        {
            var locations = await _context.Locations
                .Where(l => l.CompanyId == companyId && l.IsActive)
                .OrderBy(l => l.LocationName)
                .ToListAsync();

            return Ok(locations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving locations for company {CompanyId}", companyId);
            return StatusCode(500, new { message = "Internal server error while retrieving company locations" });
        }
    }

    // GET: api/Locations/pickup
    [HttpGet("pickup")]
    [AllowAnonymous] // Allow anonymous access for public pickup locations
    public async Task<ActionResult<IEnumerable<LocationDto>>> GetPickupLocations([FromQuery] Guid? companyId = null)
    {
        try
        {
            var query = _context.Locations
                .AsNoTracking() // Don't track entities to avoid circular references
                .Where(l => l.IsActive && l.IsPickupLocation); // Show all active pickup locations
            
            // Filter by companyId if provided
            if (companyId.HasValue)
            {
                query = query.Where(l => l.CompanyId == companyId.Value);
            }

            var locations = await query
                .OrderBy(l => l.LocationName)
                .ToListAsync();

            // Map to DTOs to avoid circular references
            var locationDtos = locations.Select(l => new LocationDto
            {
                LocationId = l.Id,
                CompanyId = l.CompanyId, // This will be null for regular locations - it should be included in JSON
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
                OpeningHours = l.OpeningHours,
                CreatedAt = l.CreatedAt,
                UpdatedAt = l.UpdatedAt
            }).ToList();

            return Ok(locationDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pickup locations");
            return StatusCode(500, new { message = "Internal server error while retrieving pickup locations" });
        }
    }

    // GET: api/Locations/return
    [HttpGet("return")]
    [AllowAnonymous] // Allow anonymous access for public return locations
    public async Task<ActionResult<IEnumerable<LocationDto>>> GetReturnLocations([FromQuery] Guid? companyId = null)
    {
        try
        {
            var query = _context.Locations
                .AsNoTracking() // Don't track entities to avoid circular references
                .Where(l => l.IsReturnLocation && l.IsActive);

            // Only filter by companyId if it's explicitly provided (not null)
            // If companyId is null/not provided, don't add any companyId filter - show all return locations
            if (companyId.HasValue && companyId.Value != Guid.Empty)
            {
                query = query.Where(l => l.CompanyId == companyId.Value);
            }
            // When companyId is not provided, don't filter by companyId at all - show all return locations

            var locations = await query
                .OrderBy(l => l.LocationName)
                .ToListAsync();

            // Map to DTOs to avoid circular references
            var locationDtos = locations.Select(l => new LocationDto
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
                OpeningHours = l.OpeningHours,
                CreatedAt = l.CreatedAt,
                UpdatedAt = l.UpdatedAt
            }).ToList();

            return Ok(locationDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving return locations");
            return StatusCode(500, new { message = "Internal server error while retrieving return locations" });
        }
    }

    // POST: api/Locations
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<LocationDto>> CreateLocation([FromBody] CreateLocationDto dto)
    {
        try
        {
            // Check if user has admin privileges
            if (!HasAdminPrivileges())
                return Forbid();

            // Check if user can edit locations for this company
            if (!CanEditCompanyLocations(dto.CompanyId))
                return Forbid();

            // If companyId is provided, verify it exists
            if (dto.CompanyId.HasValue)
            {
                var companyExists = await _context.Companies.AnyAsync(c => c.Id == dto.CompanyId.Value);
                if (!companyExists)
                {
                    return BadRequest(new { message = "Company not found" });
                }
            }

            var locationId = Guid.NewGuid();
            var location = new Location
            {
                Id = locationId,
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

            _context.Locations.Add(location);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created location {LocationId} for company {CompanyId}", location.Id, dto.CompanyId);

            // Map to DTO to avoid circular references
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

            return CreatedAtAction(nameof(GetLocation), new { id = location.Id }, locationDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating location");
            return StatusCode(500, new { message = "Internal server error while creating location" });
        }
    }

    // PUT: api/Locations/{id}
    [HttpPut("{id}")]
    [Authorize]
    public async Task<IActionResult> UpdateLocation(Guid id, [FromBody] UpdateLocationDto dto)
    {
        try
        {
            // Check if user has admin privileges
            if (!HasAdminPrivileges())
                return Forbid();

            var existingLocation = await _context.Locations.FindAsync(id);
            if (existingLocation == null)
            {
                return NotFound(new { message = $"Location with ID {id} not found" });
            }

            // Prohibit editing locations that have a CompanyId - these must be managed via CompanyLocations endpoint
            if (existingLocation.CompanyId.HasValue)
            {
                return BadRequest(new { message = "Cannot edit company locations via this endpoint. Use the CompanyLocations endpoint instead." });
            }

            // Check if user can edit locations for this company (should be null for regular locations)
            if (!CanEditCompanyLocations(existingLocation.CompanyId))
                return Forbid();

            // Verify company exists if companyId is provided and changed (should not happen for regular locations)
            if (dto.CompanyId.HasValue && existingLocation.CompanyId != dto.CompanyId)
            {
                var companyExists = await _context.Companies.AnyAsync(c => c.Id == dto.CompanyId.Value);
                if (!companyExists)
                {
                    return BadRequest(new { message = "Company not found" });
                }
            }

            // Prohibit setting CompanyId on regular locations via this endpoint
            if (dto.CompanyId.HasValue)
            {
                return BadRequest(new { message = "Cannot assign company to regular locations via this endpoint. Use the CompanyLocations endpoint instead." });
            }

            // Update properties
            // Keep companyId as null for regular locations (already verified above)
            existingLocation.CompanyId = null; // Always null for regular locations
            existingLocation.LocationName = dto.LocationName;
            existingLocation.Address = dto.Address;
            existingLocation.City = dto.City;
            existingLocation.State = dto.State;
            existingLocation.Country = dto.Country;
            existingLocation.PostalCode = dto.PostalCode;
            existingLocation.Phone = dto.Phone;
            existingLocation.Email = dto.Email;
            existingLocation.Latitude = dto.Latitude;
            existingLocation.Longitude = dto.Longitude;
            existingLocation.IsActive = dto.IsActive;
            existingLocation.IsPickupLocation = dto.IsPickupLocation;
            existingLocation.IsReturnLocation = dto.IsReturnLocation;
            existingLocation.OpeningHours = dto.OpeningHours;
            existingLocation.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Updated location {LocationId}", id);

            return NoContent();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await LocationExists(id))
            {
                return NotFound(new { message = $"Location with ID {id} not found" });
            }
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating location {LocationId}", id);
            return StatusCode(500, new { message = "Internal server error while updating location" });
        }
    }

    // DELETE: api/Locations/{id}
    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> DeleteLocation(Guid id)
    {
        try
        {
            // Check if user has admin privileges
            if (!HasAdminPrivileges())
                return Forbid();

            var location = await _context.Locations
                .Include(l => l.Vehicles)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (location == null)
            {
                return NotFound(new { message = $"Location with ID {id} not found" });
            }

            // Prohibit deleting locations that have a CompanyId - these must be managed via CompanyLocations endpoint
            if (location.CompanyId.HasValue)
            {
                return BadRequest(new { message = "Cannot delete company locations via this endpoint. Use the CompanyLocations endpoint instead." });
            }

            // Check if user can edit locations for this company (should be null for regular locations)
            if (!CanEditCompanyLocations(location.CompanyId))
                return Forbid();

            // Count vehicles that will be affected (for logging)
            var vehicleCount = location.Vehicles.Count();
            _logger.LogInformation("Deleting location {LocationId} with {VehicleCount} vehicles assigned", id, vehicleCount);

            // Delete the location - database will automatically set vehicles.current_location_id to NULL
            // due to OnDelete(DeleteBehavior.SetNull) foreign key constraint
            _context.Locations.Remove(location);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Deleted location {LocationId}, {VehicleCount} vehicles updated (current_location_id set to NULL)", id, vehicleCount);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting location {LocationId}", id);
            return StatusCode(500, new { message = "Internal server error while deleting location" });
        }
    }

    // PATCH: api/Locations/{id}/deactivate
    [HttpPatch("{id}/deactivate")]
    [Authorize]
    public async Task<IActionResult> DeactivateLocation(Guid id)
    {
        try
        {
            // Check if user has admin privileges
            if (!HasAdminPrivileges())
                return Forbid();

            var location = await _context.Locations.FindAsync(id);
            if (location == null)
            {
                return NotFound(new { message = $"Location with ID {id} not found" });
            }

            // Check if user can edit locations for this company
            if (!CanEditCompanyLocations(location.CompanyId))
                return Forbid();

            location.IsActive = false;
            location.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Deactivated location {LocationId}", id);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deactivating location {LocationId}", id);
            return StatusCode(500, new { message = "Internal server error while deactivating location" });
        }
    }

    // PATCH: api/Locations/{id}/activate
    [HttpPatch("{id}/activate")]
    [Authorize]
    public async Task<IActionResult> ActivateLocation(Guid id)
    {
        try
        {
            // Check if user has admin privileges
            if (!HasAdminPrivileges())
                return Forbid();

            var location = await _context.Locations.FindAsync(id);
            if (location == null)
            {
                return NotFound(new { message = $"Location with ID {id} not found" });
            }

            // Check if user can edit locations for this company
            if (!CanEditCompanyLocations(location.CompanyId))
                return Forbid();

            location.IsActive = true;
            location.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Activated location {LocationId}", id);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error activating location {LocationId}", id);
            return StatusCode(500, new { message = "Internal server error while activating location" });
        }
    }

    // GET: api/Locations/states
    [HttpGet("states")]
    public async Task<ActionResult<IEnumerable<string>>> GetStates([FromQuery] Guid? companyId = null)
    {
        try
        {
            var query = _context.Locations.Where(l => l.IsActive && l.State != null);

            if (companyId.HasValue)
                query = query.Where(l => l.CompanyId == companyId.Value);

            var states = await query
                .Select(l => l.State!)
                .Distinct()
                .OrderBy(s => s)
                .ToListAsync();

            return Ok(states);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving states");
            return StatusCode(500, new { message = "Internal server error while retrieving states" });
        }
    }

    // GET: api/Locations/cities
    [HttpGet("cities")]
    public async Task<ActionResult<IEnumerable<string>>> GetCities(
        [FromQuery] Guid? companyId = null,
        [FromQuery] string? state = null)
    {
        try
        {
            var query = _context.Locations.Where(l => l.IsActive && l.City != null);

            if (companyId.HasValue)
                query = query.Where(l => l.CompanyId == companyId.Value);

            if (!string.IsNullOrEmpty(state))
                query = query.Where(l => l.State == state);

            var cities = await query
                .Select(l => l.City!)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();

            return Ok(cities);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving cities");
            return StatusCode(500, new { message = "Internal server error while retrieving cities" });
        }
    }

    private async Task<bool> LocationExists(Guid id)
    {
        return await _context.Locations.AnyAsync(e => e.Id == id);
    }
}

