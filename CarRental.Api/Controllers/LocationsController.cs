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
            Response.Headers.Add("X-Total-Count", totalCount.ToString());
            Response.Headers.Add("X-Page-Number", pageNumber.ToString());
            Response.Headers.Add("X-Page-Size", pageSize.ToString());

            return Ok(locations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving locations");
            return StatusCode(500, "Internal server error while retrieving locations");
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
                .FirstOrDefaultAsync(l => l.LocationId == id);

            if (location == null)
            {
                return NotFound($"Location with ID {id} not found");
            }

            return Ok(location);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving location {LocationId}", id);
            return StatusCode(500, "Internal server error while retrieving location");
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
            return StatusCode(500, "Internal server error while retrieving company locations");
        }
    }

    // GET: api/Locations/pickup
    [HttpGet("pickup")]
    public async Task<ActionResult<IEnumerable<Location>>> GetPickupLocations([FromQuery] Guid? companyId = null)
    {
        try
        {
            var query = _context.Locations
                .Where(l => l.IsPickupLocation && l.IsActive);

            if (companyId.HasValue)
                query = query.Where(l => l.CompanyId == companyId.Value);

            var locations = await query
                .OrderBy(l => l.LocationName)
                .ToListAsync();

            return Ok(locations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pickup locations");
            return StatusCode(500, "Internal server error while retrieving pickup locations");
        }
    }

    // GET: api/Locations/return
    [HttpGet("return")]
    public async Task<ActionResult<IEnumerable<Location>>> GetReturnLocations([FromQuery] Guid? companyId = null)
    {
        try
        {
            var query = _context.Locations
                .Where(l => l.IsReturnLocation && l.IsActive);

            if (companyId.HasValue)
                query = query.Where(l => l.CompanyId == companyId.Value);

            var locations = await query
                .OrderBy(l => l.LocationName)
                .ToListAsync();

            return Ok(locations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving return locations");
            return StatusCode(500, "Internal server error while retrieving return locations");
        }
    }

    // POST: api/Locations
    [HttpPost]
    public async Task<ActionResult<Location>> CreateLocation([FromBody] Location location)
    {
        try
        {
            // Validate company exists
            var company = await _context.RentalCompanies.FindAsync(location.CompanyId);
            if (company == null)
            {
                return BadRequest($"Company with ID {location.CompanyId} not found");
            }

            // Set timestamps
            location.LocationId = Guid.NewGuid();
            location.CreatedAt = DateTime.UtcNow;
            location.UpdatedAt = DateTime.UtcNow;

            _context.Locations.Add(location);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created location {LocationId} for company {CompanyId}", 
                location.LocationId, location.CompanyId);

            return CreatedAtAction(nameof(GetLocation), new { id = location.LocationId }, location);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating location");
            return StatusCode(500, "Internal server error while creating location");
        }
    }

    // PUT: api/Locations/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateLocation(Guid id, [FromBody] Location location)
    {
        if (id != location.LocationId)
        {
            return BadRequest("Location ID mismatch");
        }

        try
        {
            var existingLocation = await _context.Locations.FindAsync(id);
            if (existingLocation == null)
            {
                return NotFound($"Location with ID {id} not found");
            }

            // Validate company exists if it's being changed
            if (existingLocation.CompanyId != location.CompanyId)
            {
                var company = await _context.RentalCompanies.FindAsync(location.CompanyId);
                if (company == null)
                {
                    return BadRequest($"Company with ID {location.CompanyId} not found");
                }
            }

            // Update properties
            existingLocation.CompanyId = location.CompanyId;
            existingLocation.LocationName = location.LocationName;
            existingLocation.Address = location.Address;
            existingLocation.City = location.City;
            existingLocation.State = location.State;
            existingLocation.Country = location.Country;
            existingLocation.PostalCode = location.PostalCode;
            existingLocation.Phone = location.Phone;
            existingLocation.Email = location.Email;
            existingLocation.Latitude = location.Latitude;
            existingLocation.Longitude = location.Longitude;
            existingLocation.IsActive = location.IsActive;
            existingLocation.IsPickupLocation = location.IsPickupLocation;
            existingLocation.IsReturnLocation = location.IsReturnLocation;
            existingLocation.OpeningHours = location.OpeningHours;
            existingLocation.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Updated location {LocationId}", id);

            return NoContent();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await LocationExists(id))
            {
                return NotFound($"Location with ID {id} not found");
            }
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating location {LocationId}", id);
            return StatusCode(500, "Internal server error while updating location");
        }
    }

    // DELETE: api/Locations/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteLocation(Guid id)
    {
        try
        {
            var location = await _context.Locations
                .Include(l => l.Vehicles)
                .FirstOrDefaultAsync(l => l.LocationId == id);

            if (location == null)
            {
                return NotFound($"Location with ID {id} not found");
            }

            // Check if location has vehicles
            if (location.Vehicles.Any())
            {
                return BadRequest($"Cannot delete location. {location.Vehicles.Count} vehicle(s) are assigned to this location. " +
                    "Please reassign vehicles before deleting.");
            }

            _context.Locations.Remove(location);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Deleted location {LocationId}", id);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting location {LocationId}", id);
            return StatusCode(500, "Internal server error while deleting location");
        }
    }

    // PATCH: api/Locations/{id}/deactivate
    [HttpPatch("{id}/deactivate")]
    public async Task<IActionResult> DeactivateLocation(Guid id)
    {
        try
        {
            var location = await _context.Locations.FindAsync(id);
            if (location == null)
            {
                return NotFound($"Location with ID {id} not found");
            }

            location.IsActive = false;
            location.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Deactivated location {LocationId}", id);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deactivating location {LocationId}", id);
            return StatusCode(500, "Internal server error while deactivating location");
        }
    }

    // PATCH: api/Locations/{id}/activate
    [HttpPatch("{id}/activate")]
    public async Task<IActionResult> ActivateLocation(Guid id)
    {
        try
        {
            var location = await _context.Locations.FindAsync(id);
            if (location == null)
            {
                return NotFound($"Location with ID {id} not found");
            }

            location.IsActive = true;
            location.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Activated location {LocationId}", id);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error activating location {LocationId}", id);
            return StatusCode(500, "Internal server error while activating location");
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
            return StatusCode(500, "Internal server error while retrieving states");
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
            return StatusCode(500, "Internal server error while retrieving cities");
        }
    }

    private async Task<bool> LocationExists(Guid id)
    {
        return await _context.Locations.AnyAsync(e => e.LocationId == id);
    }
}

