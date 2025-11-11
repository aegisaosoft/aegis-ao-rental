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
public class BookingServicesController : ControllerBase
{
    private readonly CarRentalDbContext _context;
    private readonly ILogger<BookingServicesController> _logger;

    public BookingServicesController(
        CarRentalDbContext context,
        ILogger<BookingServicesController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Get all services for a specific booking
    /// </summary>
    [HttpGet("booking/{bookingId}")]
    [ProducesResponseType(typeof(IEnumerable<BookingServiceDto>), 200)]
    public async Task<ActionResult<IEnumerable<BookingServiceDto>>> GetBookingServices(Guid bookingId)
    {
        try
        {
            var bookingServices = await _context.BookingServices
                .Include(bs => bs.Booking)
                .Include(bs => bs.AdditionalService)
                .Where(bs => bs.BookingId == bookingId)
                .OrderBy(bs => bs.AdditionalService.ServiceType)
                .ThenBy(bs => bs.AdditionalService.Name)
                .Select(bs => new BookingServiceDto
                {
                    BookingId = bs.BookingId,
                    AdditionalServiceId = bs.AdditionalServiceId,
                    Quantity = bs.Quantity,
                    PriceAtBooking = bs.PriceAtBooking,
                    Subtotal = bs.Subtotal,
                    CreatedAt = bs.CreatedAt,
                    BookingNumber = bs.Booking.BookingNumber,
                    ServiceName = bs.AdditionalService.Name,
                    ServiceDescription = bs.AdditionalService.Description,
                    ServiceType = bs.AdditionalService.ServiceType
                })
                .ToListAsync();

            return Ok(bookingServices);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving services for booking {BookingId}", bookingId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get a specific booking-service relationship
    /// </summary>
    [HttpGet("{bookingId}/{serviceId}")]
    [ProducesResponseType(typeof(BookingServiceDto), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<BookingServiceDto>> GetBookingService(Guid bookingId, Guid serviceId)
    {
        try
        {
            var bookingService = await _context.BookingServices
                .Include(bs => bs.Booking)
                .Include(bs => bs.AdditionalService)
                .FirstOrDefaultAsync(bs => bs.BookingId == bookingId && bs.AdditionalServiceId == serviceId);

            if (bookingService == null)
                return NotFound();

            var dto = new BookingServiceDto
            {
                BookingId = bookingService.BookingId,
                AdditionalServiceId = bookingService.AdditionalServiceId,
                Quantity = bookingService.Quantity,
                PriceAtBooking = bookingService.PriceAtBooking,
                Subtotal = bookingService.Subtotal,
                CreatedAt = bookingService.CreatedAt,
                BookingNumber = bookingService.Booking.BookingNumber,
                ServiceName = bookingService.AdditionalService.Name,
                ServiceDescription = bookingService.AdditionalService.Description,
                ServiceType = bookingService.AdditionalService.ServiceType
            };

            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving booking service {BookingId}/{ServiceId}", bookingId, serviceId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Add a service to a booking
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(BookingServiceDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(409)]
    public async Task<ActionResult<BookingServiceDto>> AddServiceToBooking([FromBody] CreateBookingServiceDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // Check if booking exists
            var booking = await _context.Bookings
                .FirstOrDefaultAsync(r => r.Id == createDto.BookingId);

            if (booking == null)
                return BadRequest("Booking not found");

            // Check if service exists and is active
            var service = await _context.AdditionalServices
                .FirstOrDefaultAsync(s => s.Id == createDto.AdditionalServiceId && s.IsActive);

            if (service == null)
                return BadRequest("Service not found or not active");

            // Validate quantity against max quantity
            if (createDto.Quantity > service.MaxQuantity)
                return BadRequest($"Quantity exceeds maximum allowed ({service.MaxQuantity}) for this service");

            // Check if relationship already exists
            var existingRelation = await _context.BookingServices
                .AnyAsync(bs => bs.BookingId == createDto.BookingId && 
                               bs.AdditionalServiceId == createDto.AdditionalServiceId);

            if (existingRelation)
                return Conflict("This service is already added to the booking");

            // Calculate subtotal
            var subtotal = service.Price * createDto.Quantity;

            var bookingService = new BookingService
            {
                BookingId = createDto.BookingId,
                AdditionalServiceId = createDto.AdditionalServiceId,
                Quantity = createDto.Quantity,
                PriceAtBooking = service.Price,
                Subtotal = subtotal
            };

            _context.BookingServices.Add(bookingService);
            await _context.SaveChangesAsync();

            // Load related data for response
            await _context.Entry(bookingService)
                .Reference(bs => bs.Booking)
                .LoadAsync();
            await _context.Entry(bookingService)
                .Reference(bs => bs.AdditionalService)
                .LoadAsync();

            var dto = new BookingServiceDto
            {
                BookingId = bookingService.BookingId,
                AdditionalServiceId = bookingService.AdditionalServiceId,
                Quantity = bookingService.Quantity,
                PriceAtBooking = bookingService.PriceAtBooking,
                Subtotal = bookingService.Subtotal,
                CreatedAt = bookingService.CreatedAt,
                BookingNumber = bookingService.Booking.BookingNumber,
                ServiceName = bookingService.AdditionalService.Name,
                ServiceDescription = bookingService.AdditionalService.Description,
                ServiceType = bookingService.AdditionalService.ServiceType
            };

            return CreatedAtAction(
                nameof(GetBookingService),
                new { bookingId = bookingService.BookingId, serviceId = bookingService.AdditionalServiceId },
                dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding service to booking");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Update a booking-service relationship (e.g., change quantity)
    /// </summary>
    [HttpPut("{bookingId}/{serviceId}")]
    [ProducesResponseType(typeof(BookingServiceDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<BookingServiceDto>> UpdateBookingService(
        Guid bookingId,
        Guid serviceId,
        [FromBody] UpdateBookingServiceDto updateDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var bookingService = await _context.BookingServices
                .Include(bs => bs.Booking)
                .Include(bs => bs.AdditionalService)
                .FirstOrDefaultAsync(bs => bs.BookingId == bookingId && bs.AdditionalServiceId == serviceId);

            if (bookingService == null)
                return NotFound();

            if (updateDto.Quantity.HasValue)
            {
                // Validate quantity against max quantity
                if (updateDto.Quantity.Value > bookingService.AdditionalService.MaxQuantity)
                    return BadRequest($"Quantity exceeds maximum allowed ({bookingService.AdditionalService.MaxQuantity}) for this service");

                bookingService.Quantity = updateDto.Quantity.Value;
                // Recalculate subtotal
                bookingService.Subtotal = bookingService.PriceAtBooking * bookingService.Quantity;
            }

            await _context.SaveChangesAsync();

            var dto = new BookingServiceDto
            {
                BookingId = bookingService.BookingId,
                AdditionalServiceId = bookingService.AdditionalServiceId,
                Quantity = bookingService.Quantity,
                PriceAtBooking = bookingService.PriceAtBooking,
                Subtotal = bookingService.Subtotal,
                CreatedAt = bookingService.CreatedAt,
                BookingNumber = bookingService.Booking.BookingNumber,
                ServiceName = bookingService.AdditionalService.Name,
                ServiceDescription = bookingService.AdditionalService.Description,
                ServiceType = bookingService.AdditionalService.ServiceType
            };

            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating booking service {BookingId}/{ServiceId}", bookingId, serviceId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Remove a service from a booking
    /// </summary>
    [HttpDelete("{bookingId}/{serviceId}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> RemoveServiceFromBooking(Guid bookingId, Guid serviceId)
    {
        try
        {
            var bookingService = await _context.BookingServices
                .FirstOrDefaultAsync(bs => bs.BookingId == bookingId && bs.AdditionalServiceId == serviceId);

            if (bookingService == null)
                return NotFound();

            _context.BookingServices.Remove(bookingService);
            await _context.SaveChangesAsync();

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing service from booking {BookingId}/{ServiceId}", bookingId, serviceId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get total cost of all services for a booking
    /// </summary>
    [HttpGet("booking/{bookingId}/total")]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<ActionResult<object>> GetBookingServicesTotal(Guid bookingId)
    {
        try
        {
            var total = await _context.BookingServices
                .Where(bs => bs.BookingId == bookingId)
                .SumAsync(bs => bs.Subtotal);

            var count = await _context.BookingServices
                .CountAsync(bs => bs.BookingId == bookingId);

            return Ok(new
            {
                BookingId = bookingId,
                ServiceCount = count,
                TotalCost = total
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating services total for booking {BookingId}", bookingId);
            return StatusCode(500, "Internal server error");
        }
    }
}

