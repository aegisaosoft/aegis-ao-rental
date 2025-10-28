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
using CarRental.Api.Services;
using System.Security.Cryptography;
using System.Text;

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
            var company = await _context.RentalCompanies.FindAsync(createDto.CompanyId);
            if (company == null)
                return NotFound("Company not found");

            var vehicle = await _context.Vehicles
                .Include(v => v.Company)
                .Include(v => v.Category)
                .FirstOrDefaultAsync(v => v.VehicleId == createDto.VehicleId && v.CompanyId == createDto.CompanyId);

            if (vehicle == null)
                return NotFound("Vehicle not found or not available");

            if (!vehicle.IsActive || vehicle.Status != "available")
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
                    VehicleInfo = new VehicleInfo
                    {
                        Make = vehicle.Make,
                        Model = vehicle.Model,
                        Year = vehicle.Year,
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
            await _context.SaveChangesAsync();

            // Send booking link email
            var bookingUrl = $"{Request.Scheme}://{Request.Host}/booking/{token}";
            await _emailService.SendBookingLinkAsync(bookingToken, bookingUrl);

            var bookingTokenDto = new BookingTokenDto
            {
                TokenId = bookingToken.TokenId,
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
                VehicleName = $"{vehicle.Make} {vehicle.Model} ({vehicle.Year})"
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
            .FirstOrDefaultAsync(bt => bt.Token == token);

        if (bookingToken == null)
            return NotFound("Booking token not found");

        if (bookingToken.IsUsed)
            return BadRequest("Booking token has already been used");

        if (bookingToken.ExpiresAt < DateTime.UtcNow)
            return BadRequest("Booking token has expired");

        var bookingTokenDto = new BookingTokenDto
        {
            TokenId = bookingToken.TokenId,
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
                    Email = bookingToken.BookingData.CompanyInfo?.Email ?? "",
                    Phone = bookingToken.BookingData.CompanyInfo?.Phone,
                    Address = bookingToken.BookingData.CompanyInfo?.Address,
                    City = bookingToken.BookingData.CompanyInfo?.City,
                    State = bookingToken.BookingData.CompanyInfo?.State,
                    Country = bookingToken.BookingData.CompanyInfo?.Country
                },
                Notes = bookingToken.BookingData.Notes
            },
            ExpiresAt = bookingToken.ExpiresAt,
            IsUsed = bookingToken.IsUsed,
            UsedAt = bookingToken.UsedAt,
            CreatedAt = bookingToken.CreatedAt,
            UpdatedAt = bookingToken.UpdatedAt,
            CompanyName = bookingToken.Company.CompanyName,
            VehicleName = $"{bookingToken.Vehicle.Make} {bookingToken.Vehicle.Model} ({bookingToken.Vehicle.Year})"
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

            // Process payment with Stripe
            var paymentIntent = await _stripeService.CreatePaymentIntentAsync(
                bookingToken.BookingData.TotalAmount,
                "USD",
                customer.StripeCustomerId ?? "",
                processDto.PaymentMethodId);

            // Confirm payment
            var confirmedPayment = await _stripeService.ConfirmPaymentIntentAsync(paymentIntent.Id);

            if (confirmedPayment.Status != "succeeded")
                return BadRequest("Payment failed");

            // Create reservation
            var reservationNumber = $"RES-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";
            var reservation = new Reservation
            {
                CustomerId = customer.CustomerId,
                VehicleId = bookingToken.VehicleId,
                CompanyId = bookingToken.CompanyId,
                ReservationNumber = reservationNumber,
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
                Status = "confirmed",
                Notes = processDto.CustomerNotes
            };

            _context.Reservations.Add(reservation);
            await _context.SaveChangesAsync();

            // Create payment record
            var payment = new Payment
            {
                CustomerId = customer.CustomerId,
                CompanyId = bookingToken.CompanyId,
                ReservationId = reservation.ReservationId,
                Amount = bookingToken.BookingData.TotalAmount,
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
                BookingTokenId = bookingToken.TokenId,
                ReservationId = reservation.ReservationId,
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

            // Update vehicle status if needed
            bookingToken.Vehicle.Status = "reserved";

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
                ConfirmationId = bookingConfirmation.ConfirmationId,
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
            ConfirmationId = confirmation.ConfirmationId,
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
                    Email = confirmation.BookingDetails.CompanyInfo?.Email ?? "",
                    Phone = confirmation.BookingDetails.CompanyInfo?.Phone,
                    Address = confirmation.BookingDetails.CompanyInfo?.Address,
                    City = confirmation.BookingDetails.CompanyInfo?.City,
                    State = confirmation.BookingDetails.CompanyInfo?.State,
                    Country = confirmation.BookingDetails.CompanyInfo?.Country
                },
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

    private string GenerateSecureToken()
    {
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[32];
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").Replace("=", "");
    }
}
