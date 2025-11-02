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
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using CarRental.Api.Data;
using CarRental.Api.DTOs;
using CarRental.Api.Models;

namespace CarRental.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize] // Require Bearer token authentication for all endpoints
public class ReservationsController : ControllerBase
{
    private readonly CarRentalDbContext _context;
    private readonly ILogger<ReservationsController> _logger;

    public ReservationsController(CarRentalDbContext context, ILogger<ReservationsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Get all reservations with optional filtering
    /// </summary>
    /// <param name="customerId">Filter by customer ID</param>
    /// <param name="companyId">Filter by company ID</param>
    /// <param name="status">Filter by status</param>
    /// <param name="page">Page number</param>
    /// <param name="pageSize">Page size</param>
    /// <returns>List of reservations</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ReservationDto>), 200)]
    [ProducesResponseType(401)] // Unauthorized
    public async Task<IActionResult> GetReservations(
        [FromQuery] Guid? customerId = null,
        [FromQuery] Guid? companyId = null,
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            var query = _context.Reservations
                .Include(r => r.Customer)
                .Include(r => r.Vehicle)
                    .ThenInclude(v => v.VehicleModel)
                .Include(r => r.Company)
                .AsQueryable();

            if (customerId.HasValue)
                query = query.Where(r => r.CustomerId == customerId.Value);

            if (companyId.HasValue)
                query = query.Where(r => r.CompanyId == companyId.Value);

            if (!string.IsNullOrEmpty(status))
                query = query.Where(r => r.Status == status);

            var totalCount = await query.CountAsync();

            var allReservations = await query
                .OrderByDescending(r => r.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
            
            // Load Model for each VehicleModel that has one
            foreach (var reservation in allReservations.Where(r => r.Vehicle?.VehicleModel != null))
            {
                await _context.Entry(reservation.Vehicle.VehicleModel!)
                    .Reference(vm => vm.Model)
                    .LoadAsync();
            }
            
            var reservations = allReservations.Select(r => new ReservationDto
            {
                Id = r.Id,
                CustomerId = r.CustomerId,
                CustomerName = r.Customer.FirstName + " " + r.Customer.LastName,
                CustomerEmail = r.Customer.Email,
                VehicleId = r.VehicleId,
                VehicleName = (r.Vehicle?.VehicleModel?.Model != null) ? 
                    r.Vehicle.VehicleModel.Model.Make + " " + r.Vehicle.VehicleModel.Model.ModelName + " (" + r.Vehicle.VehicleModel.Model.Year + ")" : 
                    "Unknown Vehicle",
                LicensePlate = r.Vehicle?.LicensePlate ?? "",
                CompanyId = r.CompanyId,
                CompanyName = r.Company.CompanyName,
                BookingNumber = r.BookingNumber,
                AltBookingNumber = r.AltBookingNumber,
                PickupDate = r.PickupDate,
                ReturnDate = r.ReturnDate,
                PickupLocation = r.PickupLocation,
                ReturnLocation = r.ReturnLocation,
                DailyRate = r.DailyRate,
                TotalDays = r.TotalDays,
                Subtotal = r.Subtotal,
                TaxAmount = r.TaxAmount,
                InsuranceAmount = r.InsuranceAmount,
                AdditionalFees = r.AdditionalFees,
                TotalAmount = r.TotalAmount,
                Status = r.Status,
                Notes = r.Notes,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt
            }).ToList();

            return Ok(new
            {
                Reservations = reservations,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving reservations");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get a specific reservation by ID
    /// </summary>
    /// <param name="id">Reservation ID</param>
    /// <returns>Reservation details</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ReservationDto), 200)]
    [ProducesResponseType(401)] // Unauthorized
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetReservation(Guid id)
    {
        try
        {
            var reservation = await _context.Reservations
                .Include(r => r.Customer)
                .Include(r => r.Vehicle)
                    .ThenInclude(v => v.VehicleModel)
                .Include(r => r.Company)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reservation == null)
                return NotFound();
            
            // Load Model for VehicleModel if it has one
            if (reservation.Vehicle?.VehicleModel != null)
            {
                await _context.Entry(reservation.Vehicle.VehicleModel)
                    .Reference(vm => vm.Model)
                    .LoadAsync();
            }

            var reservationDto = new ReservationDto
            {
                Id = reservation.Id,
                CustomerId = reservation.CustomerId,
                CustomerName = reservation.Customer.FirstName + " " + reservation.Customer.LastName,
                CustomerEmail = reservation.Customer.Email,
                VehicleId = reservation.VehicleId,
                VehicleName = (reservation.Vehicle?.VehicleModel?.Model != null) ? 
                    reservation.Vehicle.VehicleModel.Model.Make + " " + reservation.Vehicle.VehicleModel.Model.ModelName + " (" + reservation.Vehicle.VehicleModel.Model.Year + ")" : 
                    "Unknown Vehicle",
                LicensePlate = reservation.Vehicle?.LicensePlate ?? "",
                CompanyId = reservation.CompanyId,
                CompanyName = reservation.Company.CompanyName,
                BookingNumber = reservation.BookingNumber,
                AltBookingNumber = reservation.AltBookingNumber,
                PickupDate = reservation.PickupDate,
                ReturnDate = reservation.ReturnDate,
                PickupLocation = reservation.PickupLocation,
                ReturnLocation = reservation.ReturnLocation,
                DailyRate = reservation.DailyRate,
                TotalDays = reservation.TotalDays,
                Subtotal = reservation.Subtotal,
                TaxAmount = reservation.TaxAmount,
                InsuranceAmount = reservation.InsuranceAmount,
                AdditionalFees = reservation.AdditionalFees,
                TotalAmount = reservation.TotalAmount,
                Status = reservation.Status,
                Notes = reservation.Notes,
                CreatedAt = reservation.CreatedAt,
                UpdatedAt = reservation.UpdatedAt
            };

            return Ok(reservationDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving reservation {ReservationId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Create a new reservation
    /// </summary>
    /// <param name="createReservationDto">Reservation creation data</param>
    /// <returns>Created reservation</returns>
    [HttpPost]
    [ProducesResponseType(typeof(ReservationDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)] // Unauthorized
    public async Task<IActionResult> CreateReservation([FromBody] CreateReservationDto createReservationDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // Check if customer exists
            var customer = await _context.Customers.FindAsync(createReservationDto.CustomerId);
            if (customer == null)
                return BadRequest("Customer not found");

            // Check if vehicle exists and is available
            var vehicle = await _context.Vehicles.FindAsync(createReservationDto.VehicleId);
            if (vehicle == null)
                return BadRequest("Vehicle not found");

            if (vehicle.Status != VehicleStatus.Available)
                return BadRequest("Vehicle is not available");

            // Check if company exists
            var company = await _context.Companies.FindAsync(createReservationDto.CompanyId);
            if (company == null)
                return BadRequest("Company not found");

            // Calculate total days and amounts
            var totalDays = (int)(createReservationDto.ReturnDate - createReservationDto.PickupDate).TotalDays;
            var subtotal = createReservationDto.DailyRate * totalDays;
            var totalAmount = subtotal + createReservationDto.TaxAmount + createReservationDto.InsuranceAmount + createReservationDto.AdditionalFees;

            // Generate unique booking number
            var bookingNumber = $"BK-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";

            var reservation = new Reservation
            {
                CustomerId = createReservationDto.CustomerId,
                VehicleId = createReservationDto.VehicleId,
                CompanyId = createReservationDto.CompanyId,
                BookingNumber = bookingNumber,
                AltBookingNumber = createReservationDto.AltBookingNumber,
                PickupDate = createReservationDto.PickupDate,
                ReturnDate = createReservationDto.ReturnDate,
                PickupLocation = createReservationDto.PickupLocation,
                ReturnLocation = createReservationDto.ReturnLocation,
                DailyRate = createReservationDto.DailyRate,
                TotalDays = totalDays,
                Subtotal = subtotal,
                TaxAmount = createReservationDto.TaxAmount,
                InsuranceAmount = createReservationDto.InsuranceAmount,
                AdditionalFees = createReservationDto.AdditionalFees,
                TotalAmount = totalAmount,
                Notes = createReservationDto.Notes
            };

            _context.Reservations.Add(reservation);
            await _context.SaveChangesAsync();

            // Load related data for response
            await _context.Entry(reservation)
                .Reference(r => r.Customer)
                .LoadAsync();
            await _context.Entry(reservation)
                .Reference(r => r.Vehicle)
                .LoadAsync();
            await _context.Entry(reservation)
                .Reference(r => r.Company)
                .LoadAsync();

            var reservationDto = new ReservationDto
            {
                Id = reservation.Id,
                CustomerId = reservation.CustomerId,
                CustomerName = reservation.Customer.FirstName + " " + reservation.Customer.LastName,
                CustomerEmail = reservation.Customer.Email,
                VehicleId = reservation.VehicleId,
                VehicleName = (reservation.Vehicle?.VehicleModel?.Model != null) ? 
                    reservation.Vehicle.VehicleModel.Model.Make + " " + reservation.Vehicle.VehicleModel.Model.ModelName + " (" + reservation.Vehicle.VehicleModel.Model.Year + ")" : 
                    "Unknown Vehicle",
                LicensePlate = reservation.Vehicle?.LicensePlate ?? "",
                CompanyId = reservation.CompanyId,
                CompanyName = reservation.Company.CompanyName,
                BookingNumber = reservation.BookingNumber,
                AltBookingNumber = reservation.AltBookingNumber,
                PickupDate = reservation.PickupDate,
                ReturnDate = reservation.ReturnDate,
                PickupLocation = reservation.PickupLocation,
                ReturnLocation = reservation.ReturnLocation,
                DailyRate = reservation.DailyRate,
                TotalDays = reservation.TotalDays,
                Subtotal = reservation.Subtotal,
                TaxAmount = reservation.TaxAmount,
                InsuranceAmount = reservation.InsuranceAmount,
                AdditionalFees = reservation.AdditionalFees,
                TotalAmount = reservation.TotalAmount,
                Status = reservation.Status,
                Notes = reservation.Notes,
                CreatedAt = reservation.CreatedAt,
                UpdatedAt = reservation.UpdatedAt
            };

            return CreatedAtAction(nameof(GetReservation), new { id = reservation.Id }, reservationDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating reservation");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Update an existing reservation
    /// </summary>
    /// <param name="id">Reservation ID</param>
    /// <param name="updateReservationDto">Updated reservation data</param>
    /// <returns>Updated reservation</returns>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ReservationDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)] // Unauthorized
    [ProducesResponseType(404)]
    public async Task<IActionResult> UpdateReservation(Guid id, [FromBody] UpdateReservationDto updateReservationDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var reservation = await _context.Reservations
                .Include(r => r.Customer)
                .Include(r => r.Vehicle)
                    .ThenInclude(v => v.VehicleModel)
                .Include(r => r.Company)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reservation == null)
                return NotFound();
            
            // Load Model for VehicleModel if it has one
            if (reservation.Vehicle?.VehicleModel != null)
            {
                await _context.Entry(reservation.Vehicle.VehicleModel)
                    .Reference(vm => vm.Model)
                    .LoadAsync();
            }

            // Update fields if provided
            if (!string.IsNullOrEmpty(updateReservationDto.AltBookingNumber))
                reservation.AltBookingNumber = updateReservationDto.AltBookingNumber;

            if (updateReservationDto.PickupDate.HasValue)
                reservation.PickupDate = updateReservationDto.PickupDate.Value;

            if (updateReservationDto.ReturnDate.HasValue)
                reservation.ReturnDate = updateReservationDto.ReturnDate.Value;

            if (!string.IsNullOrEmpty(updateReservationDto.PickupLocation))
                reservation.PickupLocation = updateReservationDto.PickupLocation;

            if (!string.IsNullOrEmpty(updateReservationDto.ReturnLocation))
                reservation.ReturnLocation = updateReservationDto.ReturnLocation;

            if (updateReservationDto.TaxAmount.HasValue)
                reservation.TaxAmount = updateReservationDto.TaxAmount.Value;

            if (updateReservationDto.InsuranceAmount.HasValue)
                reservation.InsuranceAmount = updateReservationDto.InsuranceAmount.Value;

            if (updateReservationDto.AdditionalFees.HasValue)
                reservation.AdditionalFees = updateReservationDto.AdditionalFees.Value;

            if (!string.IsNullOrEmpty(updateReservationDto.Status))
                reservation.Status = updateReservationDto.Status;

            if (updateReservationDto.Notes != null)
                reservation.Notes = updateReservationDto.Notes;

            // Recalculate totals if dates changed
            if (updateReservationDto.PickupDate.HasValue || updateReservationDto.ReturnDate.HasValue)
            {
                reservation.TotalDays = (int)(reservation.ReturnDate - reservation.PickupDate).TotalDays;
                reservation.Subtotal = reservation.DailyRate * reservation.TotalDays;
            }

            // Recalculate total amount
            reservation.TotalAmount = reservation.Subtotal + reservation.TaxAmount + 
                                     reservation.InsuranceAmount + reservation.AdditionalFees;
            
            reservation.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var reservationDto = new ReservationDto
            {
                Id = reservation.Id,
                CustomerId = reservation.CustomerId,
                CustomerName = reservation.Customer.FirstName + " " + reservation.Customer.LastName,
                CustomerEmail = reservation.Customer.Email,
                VehicleId = reservation.VehicleId,
                VehicleName = (reservation.Vehicle?.VehicleModel?.Model != null) ? 
                    reservation.Vehicle.VehicleModel.Model.Make + " " + reservation.Vehicle.VehicleModel.Model.ModelName + " (" + reservation.Vehicle.VehicleModel.Model.Year + ")" : 
                    "Unknown Vehicle",
                LicensePlate = reservation.Vehicle?.LicensePlate ?? "",
                CompanyId = reservation.CompanyId,
                CompanyName = reservation.Company.CompanyName,
                BookingNumber = reservation.BookingNumber,
                AltBookingNumber = reservation.AltBookingNumber,
                PickupDate = reservation.PickupDate,
                ReturnDate = reservation.ReturnDate,
                PickupLocation = reservation.PickupLocation,
                ReturnLocation = reservation.ReturnLocation,
                DailyRate = reservation.DailyRate,
                TotalDays = reservation.TotalDays,
                Subtotal = reservation.Subtotal,
                TaxAmount = reservation.TaxAmount,
                InsuranceAmount = reservation.InsuranceAmount,
                AdditionalFees = reservation.AdditionalFees,
                TotalAmount = reservation.TotalAmount,
                Status = reservation.Status,
                Notes = reservation.Notes,
                CreatedAt = reservation.CreatedAt,
                UpdatedAt = reservation.UpdatedAt
            };

            return Ok(reservationDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating reservation {ReservationId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Update reservation status
    /// </summary>
    /// <param name="id">Reservation ID</param>
    /// <param name="status">New status (Pending, Confirmed, PickedUp, Returned, Cancelled, NoShow)</param>
    /// <returns>Updated reservation</returns>
    [HttpPatch("{id}/status")]
    [ProducesResponseType(typeof(ReservationDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)] // Unauthorized
    [ProducesResponseType(404)]
    public async Task<IActionResult> UpdateReservationStatus(Guid id, [FromBody] string status)
    {
        try
        {
            var validStatuses = new[] { "Pending", "Confirmed", "PickedUp", "Returned", "Cancelled", "NoShow" };
            if (!validStatuses.Contains(status))
                return BadRequest($"Invalid status. Valid values: {string.Join(", ", validStatuses)}");

            var reservation = await _context.Reservations
                .Include(r => r.Customer)
                .Include(r => r.Vehicle)
                    .ThenInclude(v => v.VehicleModel)
                .Include(r => r.Company)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reservation == null)
                return NotFound();
            
            // Load Model for VehicleModel if it has one
            if (reservation.Vehicle?.VehicleModel != null)
            {
                await _context.Entry(reservation.Vehicle.VehicleModel)
                    .Reference(vm => vm.Model)
                    .LoadAsync();
            }

            reservation.Status = status;
            reservation.UpdatedAt = DateTime.UtcNow;

            // Update vehicle status based on booking status
            if (status == "PickedUp")
            {
                var vehicle = await _context.Vehicles.FindAsync(reservation.VehicleId);
                if (vehicle != null)
                    vehicle.Status = VehicleStatus.Rented;
            }
            else if (status == "Returned" || status == "Cancelled")
            {
                var vehicle = await _context.Vehicles.FindAsync(reservation.VehicleId);
                if (vehicle != null)
                    vehicle.Status = VehicleStatus.Available;
            }

            await _context.SaveChangesAsync();

            var reservationDto = new ReservationDto
            {
                Id = reservation.Id,
                CustomerId = reservation.CustomerId,
                CustomerName = reservation.Customer.FirstName + " " + reservation.Customer.LastName,
                CustomerEmail = reservation.Customer.Email,
                VehicleId = reservation.VehicleId,
                VehicleName = (reservation.Vehicle?.VehicleModel?.Model != null) ? 
                    reservation.Vehicle.VehicleModel.Model.Make + " " + reservation.Vehicle.VehicleModel.Model.ModelName + " (" + reservation.Vehicle.VehicleModel.Model.Year + ")" : 
                    "Unknown Vehicle",
                LicensePlate = reservation.Vehicle?.LicensePlate ?? "",
                CompanyId = reservation.CompanyId,
                CompanyName = reservation.Company.CompanyName,
                BookingNumber = reservation.BookingNumber,
                AltBookingNumber = reservation.AltBookingNumber,
                PickupDate = reservation.PickupDate,
                ReturnDate = reservation.ReturnDate,
                PickupLocation = reservation.PickupLocation,
                ReturnLocation = reservation.ReturnLocation,
                DailyRate = reservation.DailyRate,
                TotalDays = reservation.TotalDays,
                Subtotal = reservation.Subtotal,
                TaxAmount = reservation.TaxAmount,
                InsuranceAmount = reservation.InsuranceAmount,
                AdditionalFees = reservation.AdditionalFees,
                TotalAmount = reservation.TotalAmount,
                Status = reservation.Status,
                Notes = reservation.Notes,
                CreatedAt = reservation.CreatedAt,
                UpdatedAt = reservation.UpdatedAt
            };

            return Ok(reservationDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating reservation status {ReservationId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Cancel a reservation
    /// </summary>
    /// <param name="id">Reservation ID</param>
    /// <returns>Cancelled reservation</returns>
    [HttpPost("{id}/cancel")]
    [ProducesResponseType(typeof(ReservationDto), 200)]
    [ProducesResponseType(401)] // Unauthorized
    [ProducesResponseType(404)]
    public async Task<IActionResult> CancelReservation(Guid id)
    {
        try
        {
            var reservation = await _context.Reservations
                .Include(r => r.Customer)
                .Include(r => r.Vehicle)
                    .ThenInclude(v => v.VehicleModel)
                .Include(r => r.Company)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reservation == null)
                return NotFound();
            
            // Load Model for VehicleModel if it has one
            if (reservation.Vehicle?.VehicleModel != null)
            {
                await _context.Entry(reservation.Vehicle.VehicleModel)
                    .Reference(vm => vm.Model)
                    .LoadAsync();
            }

            reservation.Status = "Cancelled";
            reservation.UpdatedAt = DateTime.UtcNow;

            // Make vehicle available again
            var vehicle = await _context.Vehicles.FindAsync(reservation.VehicleId);
            if (vehicle != null)
                vehicle.Status = VehicleStatus.Available;

            await _context.SaveChangesAsync();

            var reservationDto = new ReservationDto
            {
                Id = reservation.Id,
                CustomerId = reservation.CustomerId,
                CustomerName = reservation.Customer.FirstName + " " + reservation.Customer.LastName,
                CustomerEmail = reservation.Customer.Email,
                VehicleId = reservation.VehicleId,
                VehicleName = (reservation.Vehicle?.VehicleModel?.Model != null) ? 
                    reservation.Vehicle.VehicleModel.Model.Make + " " + reservation.Vehicle.VehicleModel.Model.ModelName + " (" + reservation.Vehicle.VehicleModel.Model.Year + ")" : 
                    "Unknown Vehicle",
                LicensePlate = reservation.Vehicle?.LicensePlate ?? "",
                CompanyId = reservation.CompanyId,
                CompanyName = reservation.Company.CompanyName,
                BookingNumber = reservation.BookingNumber,
                AltBookingNumber = reservation.AltBookingNumber,
                PickupDate = reservation.PickupDate,
                ReturnDate = reservation.ReturnDate,
                PickupLocation = reservation.PickupLocation,
                ReturnLocation = reservation.ReturnLocation,
                DailyRate = reservation.DailyRate,
                TotalDays = reservation.TotalDays,
                Subtotal = reservation.Subtotal,
                TaxAmount = reservation.TaxAmount,
                InsuranceAmount = reservation.InsuranceAmount,
                AdditionalFees = reservation.AdditionalFees,
                TotalAmount = reservation.TotalAmount,
                Status = reservation.Status,
                Notes = reservation.Notes,
                CreatedAt = reservation.CreatedAt,
                UpdatedAt = reservation.UpdatedAt
            };

            return Ok(reservationDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling reservation {ReservationId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get reservation by booking number
    /// </summary>
    /// <param name="bookingNumber">Booking number</param>
    /// <returns>Reservation details</returns>
    [HttpGet("booking-number/{bookingNumber}")]
    [ProducesResponseType(typeof(ReservationDto), 200)]
    [ProducesResponseType(401)] // Unauthorized
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetReservationByBookingNumber(string bookingNumber)
    {
        try
        {
            var reservation = await _context.Reservations
                .Include(r => r.Customer)
                .Include(r => r.Vehicle)
                .Include(r => r.Company)
                .FirstOrDefaultAsync(r => r.BookingNumber == bookingNumber);

            if (reservation == null)
                return NotFound();

            var reservationDto = new ReservationDto
            {
                Id = reservation.Id,
                CustomerId = reservation.CustomerId,
                CustomerName = reservation.Customer.FirstName + " " + reservation.Customer.LastName,
                CustomerEmail = reservation.Customer.Email,
                VehicleId = reservation.VehicleId,
                VehicleName = (reservation.Vehicle?.VehicleModel?.Model != null) ? 
                    reservation.Vehicle.VehicleModel.Model.Make + " " + reservation.Vehicle.VehicleModel.Model.ModelName + " (" + reservation.Vehicle.VehicleModel.Model.Year + ")" : 
                    "Unknown Vehicle",
                LicensePlate = reservation.Vehicle?.LicensePlate ?? "",
                CompanyId = reservation.CompanyId,
                CompanyName = reservation.Company.CompanyName,
                BookingNumber = reservation.BookingNumber,
                AltBookingNumber = reservation.AltBookingNumber,
                PickupDate = reservation.PickupDate,
                ReturnDate = reservation.ReturnDate,
                PickupLocation = reservation.PickupLocation,
                ReturnLocation = reservation.ReturnLocation,
                DailyRate = reservation.DailyRate,
                TotalDays = reservation.TotalDays,
                Subtotal = reservation.Subtotal,
                TaxAmount = reservation.TaxAmount,
                InsuranceAmount = reservation.InsuranceAmount,
                AdditionalFees = reservation.AdditionalFees,
                TotalAmount = reservation.TotalAmount,
                Status = reservation.Status,
                Notes = reservation.Notes,
                CreatedAt = reservation.CreatedAt,
                UpdatedAt = reservation.UpdatedAt
            };

            return Ok(reservationDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving reservation by booking number {BookingNumber}", bookingNumber);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Delete a reservation
    /// </summary>
    /// <param name="id">Reservation ID</param>
    [HttpDelete("{id}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)] // Unauthorized
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeleteReservation(Guid id)
    {
        try
        {
            var reservation = await _context.Reservations.FindAsync(id);

            if (reservation == null)
                return NotFound();

            // Make vehicle available if it was reserved
            var vehicle = await _context.Vehicles.FindAsync(reservation.VehicleId);
            if (vehicle != null && vehicle.Status == VehicleStatus.Rented)
                vehicle.Status = VehicleStatus.Available;

            _context.Reservations.Remove(reservation);
            await _context.SaveChangesAsync();

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting reservation {ReservationId}", id);
            return StatusCode(500, "Internal server error");
        }
    }
}
