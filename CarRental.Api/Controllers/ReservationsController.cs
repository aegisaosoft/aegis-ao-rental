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
                .Include(r => r.Company)
                .AsQueryable();

            if (customerId.HasValue)
                query = query.Where(r => r.CustomerId == customerId.Value);

            if (companyId.HasValue)
                query = query.Where(r => r.CompanyId == companyId.Value);

            if (!string.IsNullOrEmpty(status))
                query = query.Where(r => r.Status == status);

            var totalCount = await query.CountAsync();

            var reservations = await query
                .OrderByDescending(r => r.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new ReservationDto
                {
                    ReservationId = r.ReservationId,
                    CustomerId = r.CustomerId,
                    CustomerName = r.Customer.FirstName + " " + r.Customer.LastName,
                    CustomerEmail = r.Customer.Email,
                    VehicleId = r.VehicleId,
                    VehicleName = r.Vehicle.Make + " " + r.Vehicle.Model + " (" + r.Vehicle.Year + ")",
                    LicensePlate = r.Vehicle.LicensePlate,
                    CompanyId = r.CompanyId,
                    CompanyName = r.Company.CompanyName,
                    ReservationNumber = r.ReservationNumber,
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
                })
                .ToListAsync();

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
                .Include(r => r.Company)
                .FirstOrDefaultAsync(r => r.ReservationId == id);

            if (reservation == null)
                return NotFound();

            var reservationDto = new ReservationDto
            {
                ReservationId = reservation.ReservationId,
                CustomerId = reservation.CustomerId,
                CustomerName = reservation.Customer.FirstName + " " + reservation.Customer.LastName,
                CustomerEmail = reservation.Customer.Email,
                VehicleId = reservation.VehicleId,
                VehicleName = reservation.Vehicle.Make + " " + reservation.Vehicle.Model + " (" + reservation.Vehicle.Year + ")",
                LicensePlate = reservation.Vehicle.LicensePlate,
                CompanyId = reservation.CompanyId,
                CompanyName = reservation.Company.CompanyName,
                ReservationNumber = reservation.ReservationNumber,
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

            if (vehicle.Status != "available")
                return BadRequest("Vehicle is not available");

            // Check if company exists
            var company = await _context.RentalCompanies.FindAsync(createReservationDto.CompanyId);
            if (company == null)
                return BadRequest("Company not found");

            // Calculate total days and amounts
            var totalDays = (int)(createReservationDto.ReturnDate - createReservationDto.PickupDate).TotalDays;
            var subtotal = createReservationDto.DailyRate * totalDays;
            var totalAmount = subtotal + createReservationDto.TaxAmount + createReservationDto.InsuranceAmount + createReservationDto.AdditionalFees;

            // Generate unique reservation number
            var reservationNumber = $"RES-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";

            var reservation = new Reservation
            {
                CustomerId = createReservationDto.CustomerId,
                VehicleId = createReservationDto.VehicleId,
                CompanyId = createReservationDto.CompanyId,
                ReservationNumber = reservationNumber,
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
                ReservationId = reservation.ReservationId,
                CustomerId = reservation.CustomerId,
                CustomerName = reservation.Customer.FirstName + " " + reservation.Customer.LastName,
                CustomerEmail = reservation.Customer.Email,
                VehicleId = reservation.VehicleId,
                VehicleName = reservation.Vehicle.Make + " " + reservation.Vehicle.Model + " (" + reservation.Vehicle.Year + ")",
                LicensePlate = reservation.Vehicle.LicensePlate,
                CompanyId = reservation.CompanyId,
                CompanyName = reservation.Company.CompanyName,
                ReservationNumber = reservation.ReservationNumber,
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

            return CreatedAtAction(nameof(GetReservation), new { id = reservation.ReservationId }, reservationDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating reservation");
            return StatusCode(500, "Internal server error");
        }
    }
}
