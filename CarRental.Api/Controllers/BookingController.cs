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
using CarRental.Api.Services;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Linq;

namespace CarRental.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BookingController : ControllerBase
{
    private readonly CarRentalDbContext _context;
    private readonly IEmailService _emailService;
    private readonly IStripeService _stripeService;
    private readonly ILogger<BookingController> _logger;

    public BookingController(
        CarRentalDbContext context,
        IEmailService emailService,
        IStripeService stripeService,
        ILogger<BookingController> logger)
    {
        _context = context;
        _emailService = emailService;
        _stripeService = stripeService;
        _logger = logger;
    }

    /// <summary>
    /// Create a booking token and send booking link to customer
    /// </summary>
    [HttpPost("create-token")]
    public async Task<ActionResult<BookingTokenDto>> CreateBookingToken(CreateBookingTokenDto createDto)
    {
        try
        {
            // Validate company and vehicle exist
            var company = await _context.Companies.FindAsync(createDto.CompanyId);
            if (company == null)
                return NotFound("Company not found");

            var vehicle = await _context.Vehicles
                .Include(v => v.Company)
                .Include(v => v.VehicleModel)
                .FirstOrDefaultAsync(v => v.Id == createDto.VehicleId && v.CompanyId == createDto.CompanyId);
            
            if (vehicle?.VehicleModel != null)
            {
                await _context.Entry(vehicle.VehicleModel)
                    .Reference(vm => vm.Model)
                    .LoadAsync();
            }

            if (vehicle == null)
                return NotFound("Vehicle not found or not available");

            if (vehicle.Status != VehicleStatus.Available)
                return BadRequest("Vehicle is not available for booking");

            // Fetch location details for pickup and return locations
            LocationInfo? pickupLocationInfo = null;
            LocationInfo? returnLocationInfo = null;

            if (!string.IsNullOrEmpty(createDto.BookingData.PickupLocation))
            {
                var pickupLocation = await _context.Locations
                    .Where(l => l.CompanyId == createDto.CompanyId && 
                               l.LocationName == createDto.BookingData.PickupLocation &&
                               l.IsActive && l.IsPickupLocation)
                    .FirstOrDefaultAsync();

                if (pickupLocation != null)
                {
                    pickupLocationInfo = new LocationInfo
                    {
                        LocationName = pickupLocation.LocationName,
                        Address = pickupLocation.Address,
                        City = pickupLocation.City,
                        State = pickupLocation.State,
                        Country = pickupLocation.Country,
                        PostalCode = pickupLocation.PostalCode,
                        Phone = pickupLocation.Phone,
                        Email = pickupLocation.Email,
                        OpeningHours = pickupLocation.OpeningHours
                    };
                }
            }

            if (!string.IsNullOrEmpty(createDto.BookingData.ReturnLocation))
            {
                var returnLocation = await _context.Locations
                    .Where(l => l.CompanyId == createDto.CompanyId && 
                               l.LocationName == createDto.BookingData.ReturnLocation &&
                               l.IsActive && l.IsReturnLocation)
                    .FirstOrDefaultAsync();

                if (returnLocation != null)
                {
                    returnLocationInfo = new LocationInfo
                    {
                        LocationName = returnLocation.LocationName,
                        Address = returnLocation.Address,
                        City = returnLocation.City,
                        State = returnLocation.State,
                        Country = returnLocation.Country,
                        PostalCode = returnLocation.PostalCode,
                        Phone = returnLocation.Phone,
                        Email = returnLocation.Email,
                        OpeningHours = returnLocation.OpeningHours
                    };
                }
            }

            // Generate secure token
            var token = GenerateSecureToken();

            // Create booking token
            var bookingToken = new BookingToken
            {
                CompanyId = createDto.CompanyId,
                CustomerEmail = createDto.CustomerEmail,
                VehicleId = createDto.VehicleId,
                Token = token,
                BookingData = new BookingData
                {
                    PickupDate = createDto.BookingData.PickupDate,
                    ReturnDate = createDto.BookingData.ReturnDate,
                    PickupLocation = createDto.BookingData.PickupLocation,
                    ReturnLocation = createDto.BookingData.ReturnLocation,
                    DailyRate = createDto.BookingData.DailyRate,
                    TotalDays = createDto.BookingData.TotalDays,
                    Subtotal = createDto.BookingData.Subtotal,
                    TaxAmount = createDto.BookingData.TaxAmount,
                    InsuranceAmount = createDto.BookingData.InsuranceAmount,
                    AdditionalFees = createDto.BookingData.AdditionalFees,
                    TotalAmount = createDto.BookingData.TotalAmount,
                SecurityDeposit = createDto.BookingData.SecurityDeposit > 0 ? createDto.BookingData.SecurityDeposit : company.SecurityDeposit,
                    VehicleInfo = new VehicleInfo
                    {
                        Make = vehicle.VehicleModel?.Model?.Make ?? "",
                        Model = vehicle.VehicleModel?.Model?.ModelName ?? "",
                        Year = vehicle.VehicleModel?.Model?.Year ?? 0,
                        Color = vehicle.Color,
                        LicensePlate = vehicle.LicensePlate,
                        ImageUrl = vehicle.ImageUrl,
                        Features = vehicle.Features
                    },
                    CompanyInfo = new CompanyInfo
                    {
                        Name = company.CompanyName,
                        Email = company.Email
                    },
                    PickupLocationInfo = pickupLocationInfo,
                    ReturnLocationInfo = returnLocationInfo,
                    Notes = createDto.BookingData.Notes
                },
                ExpiresAt = DateTime.UtcNow.AddHours(createDto.ExpirationHours)
            };

            _context.BookingTokens.Add(bookingToken);
            // We'll persist the status change after all related entities are added

            // Send booking link email
            var bookingUrl = $"{Request.Scheme}://{Request.Host}/booking/{token}";
            await _emailService.SendBookingLinkAsync(bookingToken, bookingUrl);

            var bookingTokenDto = new BookingTokenDto
            {
                TokenId = bookingToken.Id,
                CompanyId = bookingToken.CompanyId,
                CustomerEmail = bookingToken.CustomerEmail,
                VehicleId = bookingToken.VehicleId,
                Token = bookingToken.Token,
                BookingData = new BookingDataDto
                {
                    PickupDate = bookingToken.BookingData.PickupDate,
                    ReturnDate = bookingToken.BookingData.ReturnDate,
                    PickupLocation = bookingToken.BookingData.PickupLocation,
                    ReturnLocation = bookingToken.BookingData.ReturnLocation,
                    DailyRate = bookingToken.BookingData.DailyRate,
                    TotalDays = bookingToken.BookingData.TotalDays,
                    Subtotal = bookingToken.BookingData.Subtotal,
                    TaxAmount = bookingToken.BookingData.TaxAmount,
                    InsuranceAmount = bookingToken.BookingData.InsuranceAmount,
                    AdditionalFees = bookingToken.BookingData.AdditionalFees,
                    TotalAmount = bookingToken.BookingData.TotalAmount,
                SecurityDeposit = bookingToken.BookingData.SecurityDeposit,
                    VehicleInfo = new VehicleInfoDto
                    {
                        Make = bookingToken.BookingData.VehicleInfo?.Make ?? "",
                        Model = bookingToken.BookingData.VehicleInfo?.Model ?? "",
                        Year = bookingToken.BookingData.VehicleInfo?.Year ?? 0,
                        Color = bookingToken.BookingData.VehicleInfo?.Color,
                        LicensePlate = bookingToken.BookingData.VehicleInfo?.LicensePlate ?? "",
                        ImageUrl = bookingToken.BookingData.VehicleInfo?.ImageUrl,
                        Features = bookingToken.BookingData.VehicleInfo?.Features
                    },
                    CompanyInfo = new CompanyInfoDto
                    {
                        Name = bookingToken.BookingData.CompanyInfo?.Name ?? "",
                        Email = bookingToken.BookingData.CompanyInfo?.Email ?? ""
                    },
                    PickupLocationInfo = bookingToken.BookingData.PickupLocationInfo != null ? new LocationInfoDto
                    {
                        LocationName = bookingToken.BookingData.PickupLocationInfo.LocationName,
                        Address = bookingToken.BookingData.PickupLocationInfo.Address,
                        City = bookingToken.BookingData.PickupLocationInfo.City,
                        State = bookingToken.BookingData.PickupLocationInfo.State,
                        Country = bookingToken.BookingData.PickupLocationInfo.Country,
                        PostalCode = bookingToken.BookingData.PickupLocationInfo.PostalCode,
                        Phone = bookingToken.BookingData.PickupLocationInfo.Phone,
                        Email = bookingToken.BookingData.PickupLocationInfo.Email,
                        OpeningHours = bookingToken.BookingData.PickupLocationInfo.OpeningHours
                    } : null,
                    ReturnLocationInfo = bookingToken.BookingData.ReturnLocationInfo != null ? new LocationInfoDto
                    {
                        LocationName = bookingToken.BookingData.ReturnLocationInfo.LocationName,
                        Address = bookingToken.BookingData.ReturnLocationInfo.Address,
                        City = bookingToken.BookingData.ReturnLocationInfo.City,
                        State = bookingToken.BookingData.ReturnLocationInfo.State,
                        Country = bookingToken.BookingData.ReturnLocationInfo.Country,
                        PostalCode = bookingToken.BookingData.ReturnLocationInfo.PostalCode,
                        Phone = bookingToken.BookingData.ReturnLocationInfo.Phone,
                        Email = bookingToken.BookingData.ReturnLocationInfo.Email,
                        OpeningHours = bookingToken.BookingData.ReturnLocationInfo.OpeningHours
                    } : null,
                    Notes = bookingToken.BookingData.Notes
                },
                ExpiresAt = bookingToken.ExpiresAt,
                IsUsed = bookingToken.IsUsed,
                UsedAt = bookingToken.UsedAt,
                CreatedAt = bookingToken.CreatedAt,
                UpdatedAt = bookingToken.UpdatedAt,
                CompanyName = company.CompanyName,
                VehicleName = $"{vehicle.VehicleModel?.Model?.Make ?? ""} {vehicle.VehicleModel?.Model?.ModelName ?? ""} ({vehicle.VehicleModel?.Model?.Year ?? 0})"
            };

            return CreatedAtAction(nameof(GetBookingToken), new { token = bookingToken.Token }, bookingTokenDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating booking token");
            return BadRequest("Error creating booking token");
        }
    }

    /// <summary>
    /// Get booking token details by token
    /// </summary>
    [HttpGet("token/{token}")]
    public async Task<ActionResult<BookingTokenDto>> GetBookingToken(string token)
    {
        var bookingToken = await _context.BookingTokens
            .Include(bt => bt.Company)
            .Include(bt => bt.Vehicle)
                .ThenInclude(v => v.VehicleModel)
            .FirstOrDefaultAsync(bt => bt.Token == token);

        if (bookingToken == null)
            return NotFound("Booking token not found");
        
        if (bookingToken.Vehicle?.VehicleModel != null)
        {
            await _context.Entry(bookingToken.Vehicle.VehicleModel)
                .Reference(vm => vm.Model)
                .LoadAsync();
        }

        if (bookingToken.IsUsed)
            return BadRequest("Booking token has already been used");

        if (bookingToken.ExpiresAt < DateTime.UtcNow)
            return BadRequest("Booking token has expired");

        var bookingTokenDto = new BookingTokenDto
        {
            TokenId = bookingToken.Id,
            CompanyId = bookingToken.CompanyId,
            CustomerEmail = bookingToken.CustomerEmail,
            VehicleId = bookingToken.VehicleId,
            Token = bookingToken.Token,
            BookingData = new BookingDataDto
            {
                PickupDate = bookingToken.BookingData.PickupDate,
                ReturnDate = bookingToken.BookingData.ReturnDate,
                PickupLocation = bookingToken.BookingData.PickupLocation,
                ReturnLocation = bookingToken.BookingData.ReturnLocation,
                DailyRate = bookingToken.BookingData.DailyRate,
                TotalDays = bookingToken.BookingData.TotalDays,
                Subtotal = bookingToken.BookingData.Subtotal,
                TaxAmount = bookingToken.BookingData.TaxAmount,
                InsuranceAmount = bookingToken.BookingData.InsuranceAmount,
                AdditionalFees = bookingToken.BookingData.AdditionalFees,
                TotalAmount = bookingToken.BookingData.TotalAmount,
                SecurityDeposit = bookingToken.BookingData.SecurityDeposit,
                VehicleInfo = new VehicleInfoDto
                {
                    Make = bookingToken.BookingData.VehicleInfo?.Make ?? "",
                    Model = bookingToken.BookingData.VehicleInfo?.Model ?? "",
                    Year = bookingToken.BookingData.VehicleInfo?.Year ?? 0,
                    Color = bookingToken.BookingData.VehicleInfo?.Color,
                    LicensePlate = bookingToken.BookingData.VehicleInfo?.LicensePlate ?? "",
                    ImageUrl = bookingToken.BookingData.VehicleInfo?.ImageUrl,
                    Features = bookingToken.BookingData.VehicleInfo?.Features
                },
                CompanyInfo = new CompanyInfoDto
                {
                    Name = bookingToken.BookingData.CompanyInfo?.Name ?? "",
                    Email = bookingToken.BookingData.CompanyInfo?.Email ?? ""
                },
                PickupLocationInfo = bookingToken.BookingData.PickupLocationInfo != null ? new LocationInfoDto
                {
                    LocationName = bookingToken.BookingData.PickupLocationInfo.LocationName,
                    Address = bookingToken.BookingData.PickupLocationInfo.Address,
                    City = bookingToken.BookingData.PickupLocationInfo.City,
                    State = bookingToken.BookingData.PickupLocationInfo.State,
                    Country = bookingToken.BookingData.PickupLocationInfo.Country,
                    PostalCode = bookingToken.BookingData.PickupLocationInfo.PostalCode,
                    Phone = bookingToken.BookingData.PickupLocationInfo.Phone,
                    Email = bookingToken.BookingData.PickupLocationInfo.Email,
                    OpeningHours = bookingToken.BookingData.PickupLocationInfo.OpeningHours
                } : null,
                ReturnLocationInfo = bookingToken.BookingData.ReturnLocationInfo != null ? new LocationInfoDto
                {
                    LocationName = bookingToken.BookingData.ReturnLocationInfo.LocationName,
                    Address = bookingToken.BookingData.ReturnLocationInfo.Address,
                    City = bookingToken.BookingData.ReturnLocationInfo.City,
                    State = bookingToken.BookingData.ReturnLocationInfo.State,
                    Country = bookingToken.BookingData.ReturnLocationInfo.Country,
                    PostalCode = bookingToken.BookingData.ReturnLocationInfo.PostalCode,
                    Phone = bookingToken.BookingData.ReturnLocationInfo.Phone,
                    Email = bookingToken.BookingData.ReturnLocationInfo.Email,
                    OpeningHours = bookingToken.BookingData.ReturnLocationInfo.OpeningHours
                } : null,
                Notes = bookingToken.BookingData.Notes
            },
            ExpiresAt = bookingToken.ExpiresAt,
            IsUsed = bookingToken.IsUsed,
            UsedAt = bookingToken.UsedAt,
            CreatedAt = bookingToken.CreatedAt,
            UpdatedAt = bookingToken.UpdatedAt,
            CompanyName = bookingToken.Company.CompanyName,
            VehicleName = (bookingToken.Vehicle?.VehicleModel?.Model != null) ? 
                $"{bookingToken.Vehicle.VehicleModel.Model.Make} {bookingToken.Vehicle.VehicleModel.Model.ModelName} ({bookingToken.Vehicle.VehicleModel.Model.Year})" : 
                "Unknown Vehicle"
        };

        return Ok(bookingTokenDto);
    }

    /// <summary>
    /// Process booking with payment
    /// </summary>
    [HttpPost("process")]
    public async Task<ActionResult<BookingConfirmationDto>> ProcessBooking(ProcessBookingDto processDto)
    {
        try
        {
            var bookingToken = await _context.BookingTokens
                .Include(bt => bt.Company)
                .Include(bt => bt.Vehicle)
                .FirstOrDefaultAsync(bt => bt.Token == processDto.Token);

            if (bookingToken == null)
                return NotFound("Booking token not found");

            if (bookingToken.IsUsed)
                return BadRequest("Booking token has already been used");

            if (bookingToken.ExpiresAt < DateTime.UtcNow)
                return BadRequest("Booking token has expired");

            // Check if customer exists, create if not
            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.Email == bookingToken.CustomerEmail);

            if (customer == null)
            {
                // Create new customer
                customer = new Customer
                {
                    Email = bookingToken.CustomerEmail,
                    FirstName = "Customer", // You might want to collect this information
                    LastName = "User",
                    IsVerified = true
                };

                // Create Stripe customer
                try
                {
                    customer = await _stripeService.CreateCustomerAsync(customer);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to create Stripe customer for {Email}", customer.Email);
                }

                _context.Customers.Add(customer);
                await _context.SaveChangesAsync();
            }

            var bookingData = bookingToken.BookingData;
            if (bookingData == null)
            {
                _logger.LogWarning("[Booking] Booking token {BookingTokenId} is missing booking data", bookingToken.Id);
                return BadRequest("Booking data is incomplete for this token.");
            }

            // Debug info before payment processing
            _logger.LogInformation(
                "[Booking] Preparing payment. BookingTokenId: {BookingTokenId}, Amount: {Amount}, CompanyId: {CompanyId}, VehicleId: {VehicleId}",
                bookingToken.Id,
                bookingData.TotalAmount,
                bookingToken.CompanyId,
                bookingToken.VehicleId);

            // Process payment with Stripe
            var paymentIntent = await _stripeService.CreatePaymentIntentAsync(
                bookingData.TotalAmount,
                "USD",
                customer.StripeCustomerId ?? "",
                processDto.PaymentMethodId);

            // Confirm payment
            var confirmedPayment = await _stripeService.ConfirmPaymentIntentAsync(paymentIntent.Id);

            if (confirmedPayment.Status != "succeeded")
                return BadRequest("Payment failed");

            // Create reservation in pending status
            var bookingNumber = $"BK-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";
            var reservation = new Reservation
            {
                CustomerId = customer.Id,
                VehicleId = bookingToken.VehicleId,
                CompanyId = bookingToken.CompanyId,
                BookingNumber = bookingNumber,
                PickupDate = bookingData.PickupDate,
                ReturnDate = bookingData.ReturnDate,
                PickupLocation = bookingData.PickupLocation,
                ReturnLocation = bookingData.ReturnLocation,
                DailyRate = bookingData.DailyRate,
                TotalDays = bookingData.TotalDays,
                Subtotal = bookingData.Subtotal,
                TaxAmount = bookingData.TaxAmount,
                InsuranceAmount = bookingData.InsuranceAmount,
                AdditionalFees = bookingData.AdditionalFees,
                TotalAmount = bookingData.TotalAmount,
                SecurityDeposit = 0m,
                Status = BookingStatus.Pending,
                Notes = processDto.CustomerNotes
            };

            _context.Bookings.Add(reservation);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "[Booking] Reservation created with pending status. ReservationId: {ReservationId}, Status: {Status}",
                reservation.Id,
                reservation.Status);

            // Update reservation status to confirmed after successful payment
            var rowsUpdated = await _context.Bookings
                .Where(r => r.Id == reservation.Id)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(r => r.Status, BookingStatus.Confirmed)
                    .SetProperty(r => r.UpdatedAt, DateTime.UtcNow));

            if (rowsUpdated == 0)
            {
                _logger.LogWarning("[Booking] Failed to update reservation {ReservationId} to confirmed status", reservation.Id);
            }
            else
            {
                reservation.Status = BookingStatus.Confirmed;
                reservation.UpdatedAt = DateTime.UtcNow;
            }

            _logger.LogInformation(
                "[Booking] Payment confirmed. ReservationId: {ReservationId}, Amount: {Amount}, CompanyId: {CompanyId}, CustomerId: {CustomerId}, Status changed to: {Status}",
                reservation.Id,
                reservation.TotalAmount,
                reservation.CompanyId,
                reservation.CustomerId,
                reservation.Status);

            // Create payment record
            var payment = new Payment
            {
                CustomerId = customer.Id,
                CompanyId = bookingToken.CompanyId,
                ReservationId = reservation.Id,
                Amount = bookingData.TotalAmount,
                Currency = "USD",
                PaymentType = "full_payment",
                PaymentMethod = "card",
                StripePaymentIntentId = paymentIntent.Id,
                StripePaymentMethodId = processDto.PaymentMethodId,
                Status = "succeeded",
                ProcessedAt = DateTime.UtcNow
            };

            _context.Payments.Add(payment);

            // Create booking confirmation
            var confirmationNumber = $"CONF-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";
            var bookingConfirmation = new BookingConfirmation
            {
                BookingTokenId = bookingToken.Id,
                ReservationId = reservation.Id,
                CustomerEmail = bookingToken.CustomerEmail,
                ConfirmationNumber = confirmationNumber,
                BookingDetails = bookingToken.BookingData,
                PaymentStatus = "completed",
                StripePaymentIntentId = paymentIntent.Id,
                ConfirmationSent = false
            };

            _context.BookingConfirmations.Add(bookingConfirmation);

            // Mark booking token as used
            bookingToken.IsUsed = true;
            bookingToken.UsedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Send confirmation email
            var confirmationUrl = $"{Request.Scheme}://{Request.Host}/booking/confirmation/{confirmationNumber}";
            await _emailService.SendBookingConfirmationAsync(bookingConfirmation, confirmationUrl);

            // Send payment success notification
            await _emailService.SendPaymentSuccessNotificationAsync(
                bookingToken.CustomerEmail,
                new BookingDataDto
                {
                    PickupDate = bookingToken.BookingData.PickupDate,
                    ReturnDate = bookingToken.BookingData.ReturnDate,
                    TotalAmount = bookingToken.BookingData.TotalAmount,
                    SecurityDeposit = bookingToken.BookingData.SecurityDeposit,
                    VehicleInfo = new VehicleInfoDto
                    {
                        Make = bookingToken.BookingData.VehicleInfo?.Make ?? "",
                        Model = bookingToken.BookingData.VehicleInfo?.Model ?? "",
                        Year = bookingToken.BookingData.VehicleInfo?.Year ?? 0
                    },
                    CompanyInfo = new CompanyInfoDto
                    {
                        Name = bookingToken.BookingData.CompanyInfo?.Name ?? "",
                        Email = bookingToken.BookingData.CompanyInfo?.Email ?? ""
                    }
                });

            var confirmationDto = new BookingConfirmationDto
            {
                ConfirmationId = bookingConfirmation.Id,
                BookingTokenId = bookingConfirmation.BookingTokenId,
                ReservationId = bookingConfirmation.ReservationId,
                CustomerEmail = bookingConfirmation.CustomerEmail,
                ConfirmationNumber = bookingConfirmation.ConfirmationNumber,
                BookingDetails = new BookingDataDto
                {
                    PickupDate = bookingConfirmation.BookingDetails.PickupDate,
                    ReturnDate = bookingConfirmation.BookingDetails.ReturnDate,
                    PickupLocation = bookingConfirmation.BookingDetails.PickupLocation,
                    ReturnLocation = bookingConfirmation.BookingDetails.ReturnLocation,
                    DailyRate = bookingConfirmation.BookingDetails.DailyRate,
                    TotalDays = bookingConfirmation.BookingDetails.TotalDays,
                    Subtotal = bookingConfirmation.BookingDetails.Subtotal,
                    TaxAmount = bookingConfirmation.BookingDetails.TaxAmount,
                    InsuranceAmount = bookingConfirmation.BookingDetails.InsuranceAmount,
                    AdditionalFees = bookingConfirmation.BookingDetails.AdditionalFees,
                    TotalAmount = bookingConfirmation.BookingDetails.TotalAmount,
                SecurityDeposit = bookingConfirmation.BookingDetails.SecurityDeposit,
                    VehicleInfo = new VehicleInfoDto
                    {
                        Make = bookingConfirmation.BookingDetails.VehicleInfo?.Make ?? "",
                        Model = bookingConfirmation.BookingDetails.VehicleInfo?.Model ?? "",
                        Year = bookingConfirmation.BookingDetails.VehicleInfo?.Year ?? 0,
                        Color = bookingConfirmation.BookingDetails.VehicleInfo?.Color,
                        LicensePlate = bookingConfirmation.BookingDetails.VehicleInfo?.LicensePlate ?? "",
                        ImageUrl = bookingConfirmation.BookingDetails.VehicleInfo?.ImageUrl,
                        Features = bookingConfirmation.BookingDetails.VehicleInfo?.Features
                    },
                    CompanyInfo = new CompanyInfoDto
                    {
                        Name = bookingConfirmation.BookingDetails.CompanyInfo?.Name ?? "",
                        Email = bookingConfirmation.BookingDetails.CompanyInfo?.Email ?? ""
                    },
                    PickupLocationInfo = bookingConfirmation.BookingDetails.PickupLocationInfo != null ? new LocationInfoDto
                    {
                        LocationName = bookingConfirmation.BookingDetails.PickupLocationInfo.LocationName,
                        Address = bookingConfirmation.BookingDetails.PickupLocationInfo.Address,
                        City = bookingConfirmation.BookingDetails.PickupLocationInfo.City,
                        State = bookingConfirmation.BookingDetails.PickupLocationInfo.State,
                        Country = bookingConfirmation.BookingDetails.PickupLocationInfo.Country,
                        PostalCode = bookingConfirmation.BookingDetails.PickupLocationInfo.PostalCode,
                        Phone = bookingConfirmation.BookingDetails.PickupLocationInfo.Phone,
                        Email = bookingConfirmation.BookingDetails.PickupLocationInfo.Email,
                        OpeningHours = bookingConfirmation.BookingDetails.PickupLocationInfo.OpeningHours
                    } : null,
                    ReturnLocationInfo = bookingConfirmation.BookingDetails.ReturnLocationInfo != null ? new LocationInfoDto
                    {
                        LocationName = bookingConfirmation.BookingDetails.ReturnLocationInfo.LocationName,
                        Address = bookingConfirmation.BookingDetails.ReturnLocationInfo.Address,
                        City = bookingConfirmation.BookingDetails.ReturnLocationInfo.City,
                        State = bookingConfirmation.BookingDetails.ReturnLocationInfo.State,
                        Country = bookingConfirmation.BookingDetails.ReturnLocationInfo.Country,
                        PostalCode = bookingConfirmation.BookingDetails.ReturnLocationInfo.PostalCode,
                        Phone = bookingConfirmation.BookingDetails.ReturnLocationInfo.Phone,
                        Email = bookingConfirmation.BookingDetails.ReturnLocationInfo.Email,
                        OpeningHours = bookingConfirmation.BookingDetails.ReturnLocationInfo.OpeningHours
                    } : null,
                    Notes = bookingConfirmation.BookingDetails.Notes
                },
                PaymentStatus = bookingConfirmation.PaymentStatus,
                StripePaymentIntentId = bookingConfirmation.StripePaymentIntentId,
                ConfirmationSent = bookingConfirmation.ConfirmationSent,
                CreatedAt = bookingConfirmation.CreatedAt,
                UpdatedAt = bookingConfirmation.UpdatedAt
            };

            return Ok(confirmationDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing booking for token {Token}", processDto.Token);
            return BadRequest("Error processing booking");
        }
    }

    /// <summary>
    /// Get booking confirmation by confirmation number
    /// </summary>
    [HttpGet("confirmation/{confirmationNumber}")]
    public async Task<ActionResult<BookingConfirmationDto>> GetBookingConfirmation(string confirmationNumber)
    {
        var confirmation = await _context.BookingConfirmations
            .Include(bc => bc.BookingToken)
            .Include(bc => bc.Reservation)
            .FirstOrDefaultAsync(bc => bc.ConfirmationNumber == confirmationNumber);

        if (confirmation == null)
            return NotFound("Booking confirmation not found");

        var confirmationDto = new BookingConfirmationDto
        {
            ConfirmationId = confirmation.Id,
            BookingTokenId = confirmation.BookingTokenId,
            ReservationId = confirmation.ReservationId,
            CustomerEmail = confirmation.CustomerEmail,
            ConfirmationNumber = confirmation.ConfirmationNumber,
            BookingDetails = new BookingDataDto
            {
                PickupDate = confirmation.BookingDetails.PickupDate,
                ReturnDate = confirmation.BookingDetails.ReturnDate,
                PickupLocation = confirmation.BookingDetails.PickupLocation,
                ReturnLocation = confirmation.BookingDetails.ReturnLocation,
                DailyRate = confirmation.BookingDetails.DailyRate,
                TotalDays = confirmation.BookingDetails.TotalDays,
                Subtotal = confirmation.BookingDetails.Subtotal,
                TaxAmount = confirmation.BookingDetails.TaxAmount,
                InsuranceAmount = confirmation.BookingDetails.InsuranceAmount,
                AdditionalFees = confirmation.BookingDetails.AdditionalFees,
                TotalAmount = confirmation.BookingDetails.TotalAmount,
                SecurityDeposit = confirmation.BookingDetails.SecurityDeposit,
                VehicleInfo = new VehicleInfoDto
                {
                    Make = confirmation.BookingDetails.VehicleInfo?.Make ?? "",
                    Model = confirmation.BookingDetails.VehicleInfo?.Model ?? "",
                    Year = confirmation.BookingDetails.VehicleInfo?.Year ?? 0,
                    Color = confirmation.BookingDetails.VehicleInfo?.Color,
                    LicensePlate = confirmation.BookingDetails.VehicleInfo?.LicensePlate ?? "",
                    ImageUrl = confirmation.BookingDetails.VehicleInfo?.ImageUrl,
                    Features = confirmation.BookingDetails.VehicleInfo?.Features
                },
                CompanyInfo = new CompanyInfoDto
                {
                    Name = confirmation.BookingDetails.CompanyInfo?.Name ?? "",
                    Email = confirmation.BookingDetails.CompanyInfo?.Email ?? ""
                },
                PickupLocationInfo = confirmation.BookingDetails.PickupLocationInfo != null ? new LocationInfoDto
                {
                    LocationName = confirmation.BookingDetails.PickupLocationInfo.LocationName,
                    Address = confirmation.BookingDetails.PickupLocationInfo.Address,
                    City = confirmation.BookingDetails.PickupLocationInfo.City,
                    State = confirmation.BookingDetails.PickupLocationInfo.State,
                    Country = confirmation.BookingDetails.PickupLocationInfo.Country,
                    PostalCode = confirmation.BookingDetails.PickupLocationInfo.PostalCode,
                    Phone = confirmation.BookingDetails.PickupLocationInfo.Phone,
                    Email = confirmation.BookingDetails.PickupLocationInfo.Email,
                    OpeningHours = confirmation.BookingDetails.PickupLocationInfo.OpeningHours
                } : null,
                ReturnLocationInfo = confirmation.BookingDetails.ReturnLocationInfo != null ? new LocationInfoDto
                {
                    LocationName = confirmation.BookingDetails.ReturnLocationInfo.LocationName,
                    Address = confirmation.BookingDetails.ReturnLocationInfo.Address,
                    City = confirmation.BookingDetails.ReturnLocationInfo.City,
                    State = confirmation.BookingDetails.ReturnLocationInfo.State,
                    Country = confirmation.BookingDetails.ReturnLocationInfo.Country,
                    PostalCode = confirmation.BookingDetails.ReturnLocationInfo.PostalCode,
                    Phone = confirmation.BookingDetails.ReturnLocationInfo.Phone,
                    Email = confirmation.BookingDetails.ReturnLocationInfo.Email,
                    OpeningHours = confirmation.BookingDetails.ReturnLocationInfo.OpeningHours
                } : null,
                Notes = confirmation.BookingDetails.Notes
            },
            PaymentStatus = confirmation.PaymentStatus,
            StripePaymentIntentId = confirmation.StripePaymentIntentId,
            ConfirmationSent = confirmation.ConfirmationSent,
            CreatedAt = confirmation.CreatedAt,
            UpdatedAt = confirmation.UpdatedAt
        };

        return Ok(confirmationDto);
    }

    #region Booking Management

    /// <summary>
    /// Get all reservations with optional filtering
    /// </summary>
    /// <param name="customerId">Filter by customer ID</param>
    /// <param name="companyId">Filter by company ID</param>
    /// <param name="status">Filter by status</param>
    /// <param name="page">Page number</param>
    /// <param name="pageSize">Page size</param>
    /// <returns>List of reservations</returns>
    [HttpGet("bookings")]
    [Authorize]
    [ProducesResponseType(typeof(IEnumerable<BookingDto>), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GetBookings(
        [FromQuery] Guid? customerId = null,
        [FromQuery] Guid? companyId = null,
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            var query = _context.Bookings
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

            foreach (var reservation in allReservations.Where(r => r.Vehicle?.VehicleModel != null))
            {
                await _context.Entry(reservation.Vehicle!.VehicleModel!)
                    .Reference(vm => vm.Model)
                    .LoadAsync();
            }

            var reservations = allReservations.Select(r => new BookingDto
            {
                Id = r.Id,
                CustomerId = r.CustomerId,
                CustomerName = r.Customer.FirstName + " " + r.Customer.LastName,
                CustomerEmail = r.Customer.Email,
                VehicleId = r.VehicleId,
                VehicleName = (r.Vehicle?.VehicleModel?.Model != null)
                    ? r.Vehicle.VehicleModel.Model.Make + " " + r.Vehicle.VehicleModel.Model.ModelName + " (" + r.Vehicle.VehicleModel.Model.Year + ")"
                    : "Unknown Vehicle",
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
                SecurityDeposit = r.SecurityDeposit,
                Status = r.Status,
                Notes = r.Notes,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt
            }).ToList();

            return Ok(new
            {
                Bookings = reservations,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving bookings");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get bookings for a specific company with pagination and optional filters
    /// </summary>
    [HttpGet("companies/{companyId:guid}/bookings")]
    [Authorize]
    [ProducesResponseType(typeof(PaginatedResult<BookingDto>), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GetBookingsForCompany(
        Guid companyId,
        [FromQuery] Guid? customerId = null,
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        [FromQuery] string? customer = null,
        [FromQuery] DateTime? pickupStart = null,
        [FromQuery] DateTime? pickupEnd = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page <= 0) page = 1;
        if (pageSize <= 0) pageSize = 20;

        try
        {
            var query = _context.Bookings
                .Include(r => r.Customer)
                .Include(r => r.Vehicle)
                    .ThenInclude(v => v.VehicleModel)
                .Include(r => r.Company)
                .Where(r => r.CompanyId == companyId)
                .AsQueryable();

            if (customerId.HasValue)
                query = query.Where(r => r.CustomerId == customerId.Value);

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(r => r.Status == status);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var pattern = $"%{search.Trim()}%";
                query = query.Where(r =>
                    EF.Functions.ILike(r.BookingNumber ?? string.Empty, pattern) ||
                    EF.Functions.ILike(r.AltBookingNumber ?? string.Empty, pattern) ||
                    EF.Functions.ILike((r.Customer.FirstName + " " + r.Customer.LastName).Trim(), pattern) ||
                    EF.Functions.ILike(r.Customer.Email ?? string.Empty, pattern));
            }

            if (!string.IsNullOrWhiteSpace(customer))
            {
                var trimmed = customer.Trim().ToLower();
                query = query.Where(r =>
                    (r.Customer.FirstName + " " + r.Customer.LastName).ToLower().Contains(trimmed) ||
                    r.Customer.Email.ToLower().Contains(trimmed));
            }

            if (pickupStart.HasValue)
                query = query.Where(r => r.PickupDate >= pickupStart.Value);

            if (pickupEnd.HasValue)
                query = query.Where(r => r.ReturnDate <= pickupEnd.Value);

            var totalCount = await query.CountAsync();

            var bookings = await query
                .OrderByDescending(r => r.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            foreach (var booking in bookings.Where(r => r.Vehicle?.VehicleModel != null))
            {
                await _context.Entry(booking.Vehicle!.VehicleModel!)
                    .Reference(vm => vm.Model)
                    .LoadAsync();
            }

            var result = bookings.Select(r => new BookingDto
            {
                Id = r.Id,
                CustomerId = r.CustomerId,
                CustomerName = $"{r.Customer.FirstName} {r.Customer.LastName}",
                CustomerEmail = r.Customer.Email,
                VehicleId = r.VehicleId,
                VehicleName = r.Vehicle?.VehicleModel?.Model != null
                    ? $"{r.Vehicle.VehicleModel.Model.Make} {r.Vehicle.VehicleModel.Model.ModelName} ({r.Vehicle.VehicleModel.Model.Year})"
                    : "Unknown Vehicle",
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
                SecurityDeposit = r.SecurityDeposit,
                Status = r.Status,
                Notes = r.Notes,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt
            }).ToList();

            return Ok(new PaginatedResult<BookingDto>(result, totalCount, page, pageSize));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving company bookings CompanyId={CompanyId}", companyId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get a specific reservation by ID
    /// </summary>
    /// <param name="id">Reservation ID</param>
    /// <returns>Reservation details</returns>
    [HttpGet("bookings/{id}")]
    [Authorize]
    [ProducesResponseType(typeof(BookingDto), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [AllowAnonymous]
    public async Task<IActionResult> GetBooking(Guid id)
    {
        try
        {
            var reservation = await _context.Bookings
                .Include(r => r.Customer)
                .Include(r => r.Vehicle)
                    .ThenInclude(v => v.VehicleModel)
                .Include(r => r.Company)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reservation == null)
                return NotFound();

            if (reservation.Vehicle?.VehicleModel != null)
            {
                await _context.Entry(reservation.Vehicle.VehicleModel)
                    .Reference(vm => vm.Model)
                    .LoadAsync();
            }

            var reservationDto = new BookingDto
            {
                Id = reservation.Id,
                CustomerId = reservation.CustomerId,
                CustomerName = reservation.Customer.FirstName + " " + reservation.Customer.LastName,
                CustomerEmail = reservation.Customer.Email,
                VehicleId = reservation.VehicleId,
                VehicleName = (reservation.Vehicle?.VehicleModel?.Model != null)
                    ? reservation.Vehicle.VehicleModel.Model.Make + " " + reservation.Vehicle.VehicleModel.Model.ModelName + " (" + reservation.Vehicle.VehicleModel.Model.Year + ")"
                    : "Unknown Vehicle",
                LicensePlate = reservation.Vehicle?.LicensePlate ?? "",
                CompanyId = reservation.CompanyId,
                CompanyName = reservation.Company?.CompanyName ?? "",
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
                SecurityDeposit = reservation.SecurityDeposit,
                Status = reservation.Status,
                Notes = reservation.Notes,
                CreatedAt = reservation.CreatedAt,
                UpdatedAt = reservation.UpdatedAt
            };

            return Ok(reservationDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving booking {BookingId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Create a new reservation
    /// </summary>
    /// <param name="createReservationDto">Booking creation data</param>
    /// <returns>Created reservation</returns>
    [HttpPost("bookings")]
    [Authorize]
    [ProducesResponseType(typeof(BookingDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> CreateBooking([FromBody] CreateBookingDto createReservationDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var customer = await _context.Customers.FindAsync(createReservationDto.CustomerId);
            if (customer == null)
                return BadRequest("Customer not found");

            var vehicle = await _context.Vehicles.FindAsync(createReservationDto.VehicleId);
            if (vehicle == null)
                return BadRequest("Vehicle not found");

            if (vehicle.Status != VehicleStatus.Available)
                return BadRequest("Vehicle is not available");

            var company = await _context.Companies.FindAsync(createReservationDto.CompanyId);
            if (company == null)
                return BadRequest("Company not found");

            var totalDays = (int)(createReservationDto.ReturnDate - createReservationDto.PickupDate).TotalDays;
            var subtotal = createReservationDto.DailyRate * totalDays;
            var totalAmount = subtotal + createReservationDto.TaxAmount + createReservationDto.InsuranceAmount + createReservationDto.AdditionalFees;

            var bookingNumber = $"BK-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";

            var reservation = new Booking
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
                SecurityDeposit = 0m,
                Notes = createReservationDto.Notes
            };

            _context.Bookings.Add(reservation);
            await _context.SaveChangesAsync();

            await _context.Entry(reservation)
                .Reference(r => r.Customer)
                .LoadAsync();
            await _context.Entry(reservation)
                .Reference(r => r.Vehicle)
                .LoadAsync();
            await _context.Entry(reservation)
                .Reference(r => r.Company)
                .LoadAsync();

            var reservationDto = new BookingDto
            {
                Id = reservation.Id,
                CustomerId = reservation.CustomerId,
                CustomerName = reservation.Customer.FirstName + " " + reservation.Customer.LastName,
                CustomerEmail = reservation.Customer.Email,
                VehicleId = reservation.VehicleId,
                VehicleName = (reservation.Vehicle?.VehicleModel?.Model != null)
                    ? reservation.Vehicle.VehicleModel.Model.Make + " " + reservation.Vehicle.VehicleModel.Model.ModelName + " (" + reservation.Vehicle.VehicleModel.Model.Year + ")"
                    : "Unknown Vehicle",
                LicensePlate = reservation.Vehicle?.LicensePlate ?? "",
                CompanyId = reservation.CompanyId,
                CompanyName = reservation.Company?.CompanyName ?? "",
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
                SecurityDeposit = reservation.SecurityDeposit,
                Status = reservation.Status,
                Notes = reservation.Notes,
                CreatedAt = reservation.CreatedAt,
                UpdatedAt = reservation.UpdatedAt
            };

            return CreatedAtAction(nameof(GetBooking), new { id = reservation.Id }, reservationDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating booking");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Update an existing reservation
    /// </summary>
    /// <param name="id">Reservation ID</param>
    /// <param name="updateReservationDto">Updated booking data</param>
    /// <returns>Updated reservation</returns>
    [HttpPut("bookings/{id}")]
    [Authorize]
    [ProducesResponseType(typeof(BookingDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UpdateBooking(Guid id, [FromBody] UpdateBookingDto updateReservationDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var reservation = await _context.Bookings
                .Include(r => r.Customer)
                .Include(r => r.Vehicle)
                    .ThenInclude(v => v.VehicleModel)
                .Include(r => r.Company)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reservation == null)
                return NotFound();

            if (reservation.Vehicle?.VehicleModel != null)
            {
                await _context.Entry(reservation.Vehicle.VehicleModel)
                    .Reference(vm => vm.Model)
                    .LoadAsync();
            }

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

            if (updateReservationDto.SecurityDeposit.HasValue)
                reservation.SecurityDeposit = updateReservationDto.SecurityDeposit.Value;

            if (!string.IsNullOrEmpty(updateReservationDto.Status))
                reservation.Status = updateReservationDto.Status;

            if (updateReservationDto.Notes != null)
                reservation.Notes = updateReservationDto.Notes;

            if (updateReservationDto.PickupDate.HasValue || updateReservationDto.ReturnDate.HasValue)
            {
                reservation.TotalDays = (int)(reservation.ReturnDate - reservation.PickupDate).TotalDays;
                reservation.Subtotal = reservation.DailyRate * reservation.TotalDays;
            }

            reservation.TotalAmount = reservation.Subtotal + reservation.TaxAmount +
                                     reservation.InsuranceAmount + reservation.AdditionalFees;

            reservation.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var reservationDto = new BookingDto
            {
                Id = reservation.Id,
                CustomerId = reservation.CustomerId,
                CustomerName = reservation.Customer.FirstName + " " + reservation.Customer.LastName,
                CustomerEmail = reservation.Customer.Email,
                VehicleId = reservation.VehicleId,
                VehicleName = (reservation.Vehicle?.VehicleModel?.Model != null)
                    ? reservation.Vehicle.VehicleModel.Model.Make + " " + reservation.Vehicle.VehicleModel.Model.ModelName + " (" + reservation.Vehicle.VehicleModel.Model.Year + ")"
                    : "Unknown Vehicle",
                LicensePlate = reservation.Vehicle?.LicensePlate ?? "",
                CompanyId = reservation.CompanyId,
                CompanyName = reservation.Company?.CompanyName ?? "",
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
                SecurityDeposit = reservation.SecurityDeposit,
                Status = reservation.Status,
                Notes = reservation.Notes,
                CreatedAt = reservation.CreatedAt,
                UpdatedAt = reservation.UpdatedAt
            };

            return Ok(reservationDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating booking {BookingId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Update reservation status
    /// </summary>
    /// <param name="id">Reservation ID</param>
    /// <param name="status">New status</param>
    /// <returns>Updated reservation</returns>
    [HttpPatch("bookings/{id}/status")]
    [Authorize]
    [ProducesResponseType(typeof(BookingDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UpdateReservationStatus(Guid id, [FromBody] string status)
    {
        try
        {
            var validStatuses = new[] { "Pending", "Confirmed", "PickedUp", "Returned", "Cancelled", "NoShow" };
            if (!validStatuses.Contains(status))
                return BadRequest($"Invalid status. Valid values: {string.Join(", ", validStatuses)}");

            var reservation = await _context.Bookings
                .Include(r => r.Customer)
                .Include(r => r.Vehicle)
                    .ThenInclude(v => v.VehicleModel)
                .Include(r => r.Company)
                .Include(r => r.Payments)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reservation == null)
                return NotFound();

            if (reservation.Vehicle?.VehicleModel != null)
            {
                await _context.Entry(reservation.Vehicle.VehicleModel)
                    .Reference(vm => vm.Model)
                    .LoadAsync();
            }

            reservation.Status = status;
            reservation.UpdatedAt = DateTime.UtcNow;

            if (status == "PickedUp")
            {
                // Don't automatically change vehicle status - manual control required
                // var vehicle = await _context.Vehicles.FindAsync(reservation.VehicleId);
                // if (vehicle != null)
                //     vehicle.Status = VehicleStatus.Rented;

                // Charge security deposit when booking status changes to PickedUp
                if (reservation.SecurityDeposit == 0)
                {
                    var securityDepositAmount = reservation.Company?.SecurityDeposit ?? 1000m;
                    
                    if (securityDepositAmount > 0 && !string.IsNullOrEmpty(reservation.Customer.StripeCustomerId))
                    {
                        try
                        {
                            // Get the payment method from the original booking payment
                            var originalPayment = reservation.Payments
                                .Where(p => p.Status == "succeeded" && !string.IsNullOrEmpty(p.StripePaymentMethodId))
                                .OrderByDescending(p => p.CreatedAt)
                                .FirstOrDefault();

                            string? paymentMethodId = originalPayment?.StripePaymentMethodId;

                            if (!string.IsNullOrEmpty(paymentMethodId))
                            {
                                // Create payment intent for security deposit
                                var securityDepositIntent = await _stripeService.CreatePaymentIntentAsync(
                                    securityDepositAmount,
                                    "USD",
                                    reservation.Customer.StripeCustomerId,
                                    paymentMethodId,
                                    metadata: new Dictionary<string, string>
                                    {
                                        { "booking_id", reservation.Id.ToString() },
                                        { "payment_type", "security_deposit" },
                                        { "booking_number", reservation.BookingNumber }
                                    },
                                    captureImmediately: true);

                                // Confirm the payment intent
                                var confirmedIntent = await _stripeService.ConfirmPaymentIntentAsync(securityDepositIntent.Id);

                                if (confirmedIntent.Status == "succeeded")
                                {
                                    // Update booking with security deposit amount
                                    reservation.SecurityDeposit = securityDepositAmount;

                                    // Create or update payment record for security deposit
                                    var securityDepositPayment = reservation.Payments
                                        .FirstOrDefault(p => p.PaymentType == "security_deposit");

                                    if (securityDepositPayment == null)
                                    {
                                        securityDepositPayment = new Payment
                                        {
                                            CustomerId = reservation.CustomerId,
                                            CompanyId = reservation.CompanyId,
                                            ReservationId = reservation.Id,
                                            Amount = securityDepositAmount,
                                            Currency = "USD",
                                            PaymentType = "security_deposit",
                                            PaymentMethod = "card",
                                            StripePaymentIntentId = confirmedIntent.Id,
                                            StripePaymentMethodId = paymentMethodId,
                                            Status = "succeeded",
                                            ProcessedAt = DateTime.UtcNow,
                                            SecurityDepositAmount = securityDepositAmount,
                                            SecurityDepositStatus = "captured",
                                            SecurityDepositPaymentIntentId = confirmedIntent.Id,
                                            SecurityDepositChargeId = confirmedIntent.LatestChargeId,
                                            SecurityDepositAuthorizedAt = DateTime.UtcNow,
                                            SecurityDepositCapturedAt = DateTime.UtcNow
                                        };
                                        _context.Payments.Add(securityDepositPayment);
                                    }
                                    else
                                    {
                                        securityDepositPayment.SecurityDepositAmount = securityDepositAmount;
                                        securityDepositPayment.SecurityDepositStatus = "captured";
                                        securityDepositPayment.SecurityDepositPaymentIntentId = confirmedIntent.Id;
                                        securityDepositPayment.SecurityDepositChargeId = confirmedIntent.LatestChargeId;
                                        securityDepositPayment.SecurityDepositAuthorizedAt = DateTime.UtcNow;
                                        securityDepositPayment.SecurityDepositCapturedAt = DateTime.UtcNow;
                                        securityDepositPayment.Status = "succeeded";
                                        securityDepositPayment.ProcessedAt = DateTime.UtcNow;
                                    }

                                    _logger.LogInformation(
                                        "[Booking] Security deposit of {Amount} charged for booking {BookingId} when status changed to PickedUp",
                                        securityDepositAmount,
                                        reservation.Id);
                                }
                                else
                                {
                                    _logger.LogWarning(
                                        "[Booking] Failed to charge security deposit for booking {BookingId}. Payment intent status: {Status}",
                                        reservation.Id,
                                        confirmedIntent.Status);
                                }
                            }
                            else
                            {
                                _logger.LogWarning(
                                    "[Booking] No payment method found for booking {BookingId} to charge security deposit",
                                    reservation.Id);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex,
                                "[Booking] Error charging security deposit for booking {BookingId} when status changed to PickedUp",
                                reservation.Id);
                            // Continue with status update even if security deposit charge fails
                        }
                    }
                }
            }
            else if (status == "Returned" || status == "Cancelled")
            {
                // Don't automatically change vehicle status - manual control required
                // var vehicle = await _context.Vehicles.FindAsync(reservation.VehicleId);
                // if (vehicle != null)
                //     vehicle.Status = VehicleStatus.Available;
            }

            await _context.SaveChangesAsync();

            var reservationDto = new BookingDto
            {
                Id = reservation.Id,
                CustomerId = reservation.CustomerId,
                CustomerName = reservation.Customer.FirstName + " " + reservation.Customer.LastName,
                CustomerEmail = reservation.Customer.Email,
                VehicleId = reservation.VehicleId,
                VehicleName = (reservation.Vehicle?.VehicleModel?.Model != null)
                    ? reservation.Vehicle.VehicleModel.Model.Make + " " + reservation.Vehicle.VehicleModel.Model.ModelName + " (" + reservation.Vehicle.VehicleModel.Model.Year + ")"
                    : "Unknown Vehicle",
                LicensePlate = reservation.Vehicle?.LicensePlate ?? "",
                CompanyId = reservation.CompanyId,
                CompanyName = reservation.Company?.CompanyName ?? "",
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
                SecurityDeposit = reservation.SecurityDeposit,
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
    [HttpPost("bookings/{id}/cancel")]
    [Authorize]
    [ProducesResponseType(typeof(BookingDto), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> CancelReservation(Guid id)
    {
        try
        {
            var reservation = await _context.Bookings
                .Include(r => r.Customer)
                .Include(r => r.Vehicle)
                    .ThenInclude(v => v.VehicleModel)
                .Include(r => r.Company)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reservation == null)
                return NotFound();

            if (reservation.Vehicle?.VehicleModel != null)
            {
                await _context.Entry(reservation.Vehicle.VehicleModel)
                    .Reference(vm => vm.Model)
                    .LoadAsync();
            }

            reservation.Status = "Cancelled";
            reservation.UpdatedAt = DateTime.UtcNow;

            // Don't automatically change vehicle status - manual control required
            // var vehicle = await _context.Vehicles.FindAsync(reservation.VehicleId);
            // if (vehicle != null)
            //     vehicle.Status = VehicleStatus.Available;

            await _context.SaveChangesAsync();

            var reservationDto = new BookingDto
            {
                Id = reservation.Id,
                CustomerId = reservation.CustomerId,
                CustomerName = reservation.Customer.FirstName + " " + reservation.Customer.LastName,
                CustomerEmail = reservation.Customer.Email,
                VehicleId = reservation.VehicleId,
                VehicleName = (reservation.Vehicle?.VehicleModel?.Model != null)
                    ? reservation.Vehicle.VehicleModel.Model.Make + " " + reservation.Vehicle.VehicleModel.Model.ModelName + " (" + reservation.Vehicle.VehicleModel.Model.Year + ")"
                    : "Unknown Vehicle",
                LicensePlate = reservation.Vehicle?.LicensePlate ?? "",
                CompanyId = reservation.CompanyId,
                CompanyName = reservation.Company?.CompanyName ?? "",
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
                SecurityDeposit = reservation.SecurityDeposit,
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
    [HttpGet("bookings/booking-number/{bookingNumber}")]
    [Authorize]
    [ProducesResponseType(typeof(BookingDto), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetReservationByBookingNumber(string bookingNumber)
    {
        try
        {
            var reservation = await _context.Bookings
                .Include(r => r.Customer)
                .Include(r => r.Vehicle)
                .Include(r => r.Company)
                .FirstOrDefaultAsync(r => r.BookingNumber == bookingNumber);

            if (reservation == null)
                return NotFound();

            var reservationDto = new BookingDto
            {
                Id = reservation.Id,
                CustomerId = reservation.CustomerId,
                CustomerName = reservation.Customer.FirstName + " " + reservation.Customer.LastName,
                CustomerEmail = reservation.Customer.Email,
                VehicleId = reservation.VehicleId,
                VehicleName = (reservation.Vehicle?.VehicleModel?.Model != null)
                    ? reservation.Vehicle.VehicleModel.Model.Make + " " + reservation.Vehicle.VehicleModel.Model.ModelName + " (" + reservation.Vehicle.VehicleModel.Model.Year + ")"
                    : "Unknown Vehicle",
                LicensePlate = reservation.Vehicle?.LicensePlate ?? "",
                CompanyId = reservation.CompanyId,
                CompanyName = reservation.Company?.CompanyName ?? "",
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
                SecurityDeposit = reservation.SecurityDeposit,
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
    [HttpDelete("bookings/{id}")]
    [Authorize]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeleteReservation(Guid id)
    {
        try
        {
            var reservation = await _context.Bookings.FindAsync(id);

            if (reservation == null)
                return NotFound();

            // Don't automatically change vehicle status - manual control required
            // var vehicle = await _context.Vehicles.FindAsync(reservation.VehicleId);
            // if (vehicle != null && vehicle.Status == VehicleStatus.Rented)
            //     vehicle.Status = VehicleStatus.Available;

            _context.Bookings.Remove(reservation);
            await _context.SaveChangesAsync();

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting reservation {ReservationId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    #endregion

    private string GenerateSecureToken()
    {
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[32];
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").Replace("=", "");
    }
}
