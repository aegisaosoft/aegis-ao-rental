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
using BCrypt.Net;

namespace CarRental.Api.Controllers;

// Helper DTO for raw SQL license query
public class LicenseInfoDto
{
    public string? LicenseNumber { get; set; }
    public string? StateIssued { get; set; }
}

[ApiController]
[Route("api/[controller]")]
public class BookingController : ControllerBase
{
    private readonly CarRentalDbContext _context;
    private readonly IEmailService _emailService;
    private readonly IStripeService _stripeService;
    private readonly ILogger<BookingController> _logger;
    private readonly IConfiguration _configuration;
    private readonly ISettingsService _settingsService;
    private readonly IEncryptionService _encryptionService;
    private readonly IRentalAgreementService _rentalAgreementService;

    public BookingController(
        CarRentalDbContext context,
        IEmailService emailService,
        IStripeService stripeService,
        ILogger<BookingController> logger,
        IConfiguration configuration,
        ISettingsService settingsService,
        IEncryptionService encryptionService,
        IRentalAgreementService rentalAgreementService)
    {
        _context = context;
        _emailService = emailService;
        _stripeService = stripeService;
        _logger = logger;
        _configuration = configuration;
        _settingsService = settingsService;
        _encryptionService = encryptionService;
        _rentalAgreementService = rentalAgreementService;
    }

    /// <summary>
    /// Decrypt Stripe key (handles both encrypted and plaintext keys for backward compatibility)
    /// </summary>
    private string? DecryptStripeKey(string? encryptedKey)
    {
        if (string.IsNullOrWhiteSpace(encryptedKey))
            return null;
        
        try
        {
            return _encryptionService.Decrypt(encryptedKey);
        }
        catch (Exception ex) when (ex is FormatException || ex is System.Security.Cryptography.CryptographicException)
        {
            // Key might be stored in plaintext (backward compatibility)
            _logger.LogDebug("Stripe key appears to be stored in plaintext, using as-is");
            return encryptedKey;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error decrypting Stripe key, using as-is");
            return encryptedKey;
        }
    }

    /// <summary>
    /// Get Stripe secret key for a company from stripe_settings table using company.StripeSettingsId
    /// This ensures we use the correct Stripe keys from the stripe_settings table
    /// </summary>
    private async Task<string?> GetStripeSecretKeyAsync(Guid? companyId)
    {
        if (!companyId.HasValue)
        {
            _logger.LogWarning("GetStripeSecretKeyAsync: No companyId provided, falling back to global settings");
            return await _settingsService.GetValueAsync("stripe.secretKey");
        }

        try
        {
            // Get company and its StripeSettingsId
            var company = await _context.Companies
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == companyId.Value);

            if (company == null)
            {
                _logger.LogWarning("GetStripeSecretKeyAsync: Company {CompanyId} not found, falling back to global settings", companyId);
                return await _settingsService.GetValueAsync("stripe.secretKey");
            }

            // Must use stripe_settings table via company.StripeSettingsId
            if (!company.StripeSettingsId.HasValue)
            {
                _logger.LogWarning(
                    "GetStripeSecretKeyAsync: Company {CompanyId} does not have StripeSettingsId configured. Cannot use stripe_settings table.", 
                    companyId
                );
                return await _settingsService.GetValueAsync("stripe.secretKey");
            }

            var settingsId = company.StripeSettingsId.Value;

            // Get secret key from stripe_settings table
            var stripeSettings = await _context.StripeSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(ss => ss.Id == settingsId);

            if (stripeSettings == null)
            {
                _logger.LogError(
                    "GetStripeSecretKeyAsync: stripe_settings record not found for StripeSettingsId {StripeSettingsId} (Company: {CompanyId})", 
                    settingsId, 
                    companyId
                );
                return await _settingsService.GetValueAsync("stripe.secretKey");
            }

            if (string.IsNullOrWhiteSpace(stripeSettings.SecretKey))
            {
                _logger.LogError(
                    "GetStripeSecretKeyAsync: stripe_settings.SecretKey is empty for StripeSettingsId {StripeSettingsId} (Company: {CompanyId})", 
                    settingsId, 
                    companyId
                );
                return await _settingsService.GetValueAsync("stripe.secretKey");
            }

            _logger.LogInformation(
                "[Stripe] Using secret key from stripe_settings table (Id: {SettingsId}, Name: {SettingsName}) for company {CompanyId}", 
                settingsId, 
                stripeSettings.Name ?? "unnamed",
                companyId
            );
            
            // Decrypt the key using the same pattern as StripeService (handles both encrypted and plaintext)
            return DecryptStripeKey(stripeSettings.SecretKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Stripe] Error loading company-specific settings from stripe_settings table for company {CompanyId}, falling back to global settings", companyId);
            return await _settingsService.GetValueAsync("stripe.secretKey");
        }
    }

    /// <summary>
    /// Get Stripe connected account ID for a company from stripe_company table
    /// REQUIRES: stripe_company record must exist with matching company_id and settings_id
    /// Returns null only if record doesn't exist - this will prohibit Stripe operations
    /// </summary>
    private async Task<string?> GetStripeAccountIdAsync(Guid companyId)
    {
        try
        {
            // Get company and its StripeSettingsId
            var company = await _context.Companies
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == companyId);

            if (company == null)
            {
                _logger.LogError("GetStripeAccountIdAsync: Company {CompanyId} not found", companyId);
                return null;
            }

            if (!company.StripeSettingsId.HasValue)
            {
                _logger.LogError("GetStripeAccountIdAsync: Company {CompanyId} does not have StripeSettingsId configured. Stripe operations are prohibited.", companyId);
                return null;
            }

            var companyStripeSettingsId = company.StripeSettingsId.Value;

            // Verify stripe_settings exists with matching ID
            var stripeSettings = await _context.StripeSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(ss => ss.Id == companyStripeSettingsId);

            if (stripeSettings == null)
            {
                _logger.LogError("GetStripeAccountIdAsync: stripe_settings record not found for StripeSettingsId {StripeSettingsId} (from company {CompanyId}). Stripe operations are prohibited.", 
                    companyStripeSettingsId, companyId);
                return null;
            }

            // STRICT REQUIREMENT: stripe_company record MUST exist with matching company_id and settings_id
            var stripeCompany = await _context.StripeCompanies
                .AsNoTracking()
                .FirstOrDefaultAsync(sc => sc.CompanyId == companyId && sc.SettingsId == companyStripeSettingsId);

            if (stripeCompany == null)
            {
                _logger.LogError("GetStripeAccountIdAsync: stripe_company record not found for CompanyId {CompanyId} and SettingsId {SettingsId}. Stripe operations are PROHIBITED until stripe_company record is created.", 
                    companyId, companyStripeSettingsId);
                return null;
            }

            if (string.IsNullOrEmpty(stripeCompany.StripeAccountId))
            {
                _logger.LogError("GetStripeAccountIdAsync: stripe_company.StripeAccountId is empty for CompanyId {CompanyId}. Stripe operations are PROHIBITED until Stripe account is created.", companyId);
                return null;
            }

            // Decrypt the account ID
            try
            {
                var accountId = _encryptionService.Decrypt(stripeCompany.StripeAccountId);
                _logger.LogInformation("GetStripeAccountIdAsync: Found Stripe account {AccountId} for company {CompanyId} from stripe_company table", accountId, companyId);
                return accountId;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "GetStripeAccountIdAsync: Failed to decrypt StripeAccountId for company {CompanyId}. The value might be stored in plain text.", companyId);
                // If decryption fails, it might be stored in plain text (for backward compatibility)
                return stripeCompany.StripeAccountId;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetStripeAccountIdAsync: Error getting Stripe account ID for company {CompanyId}. Stripe operations are prohibited.", companyId);
            return null;
        }
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

            // Process payment with Stripe using company's currency
            var currency = bookingToken.Company?.Currency ?? "USD";
            _logger.LogInformation(
                "[Booking] Processing payment with currency: {Currency} for company {CompanyId}",
                currency,
                bookingToken.CompanyId
            );
            
            // Get Stripe API key from stripe_settings table using company.StripeSettingsId
            var stripeSecretKey = await GetStripeSecretKeyAsync(bookingToken.CompanyId);
            if (string.IsNullOrEmpty(stripeSecretKey))
            {
                _logger.LogError("ProcessBooking: Stripe secret key not configured for company {CompanyId}", bookingToken.CompanyId);
                return StatusCode(500, new { error = "Stripe not configured for this company" });
            }

            // Get Stripe connected account ID from stripe_company table (REQUIRED)
            var stripeAccountId = await GetStripeAccountIdAsync(bookingToken.CompanyId);
            if (string.IsNullOrEmpty(stripeAccountId))
            {
                _logger.LogError(
                    "ProcessBooking: stripe_company record not found or StripeAccountId is missing for company {CompanyId}. " +
                    "Stripe operations are PROHIBITED. Please ensure stripe_company record exists with matching CompanyId and SettingsId.", 
                    bookingToken.CompanyId
                );
                return StatusCode(500, new { 
                    error = "Stripe account not configured for this company",
                    message = "Stripe account ID must exist in stripe_company table. Please configure Stripe account first."
                });
            }

            _logger.LogInformation(
                "[Booking] Creating payment intent for connected account {StripeAccountId} using Stripe keys from stripe_settings (CompanyId: {CompanyId})", 
                stripeAccountId, 
                bookingToken.CompanyId
            );
            
            // Create payment intent using connected account (same as security deposit)
            var paymentIntent = await _stripeService.CreatePaymentIntentAsync(
                bookingData.TotalAmount,
                currency,
                customer.StripeCustomerId ?? "",
                processDto.PaymentMethodId,
                companyId: bookingToken.CompanyId);

            // Confirm payment (pass companyId to use connected account)
            var confirmedPayment = await _stripeService.ConfirmPaymentIntentAsync(paymentIntent.Id, bookingToken.CompanyId);

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
                Currency = currency,
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
                Currency = currency,
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

            // Note: Invitation email will be sent by webhook handlers when payment is confirmed
            // This prevents duplicate emails and ensures emails are only sent after payment is fully processed

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
                PickupTime = r.PickupTime ?? "10:00",
                ReturnDate = r.ReturnDate,
                ReturnTime = r.ReturnTime ?? "22:00",
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
                .Include(r => r.Payments)
                .Include(r => r.RefundRecords)
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

            var result = bookings.Select(r =>
            {
                // Get the most recent successful payment
                var payment = r.Payments
                    .Where(p => p.Status == "succeeded")
                    .OrderByDescending(p => p.CreatedAt)
                    .FirstOrDefault();
                    
                return new BookingDto
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
                    UpdatedAt = r.UpdatedAt,
                    // Payment information
                    PaymentMethod = payment?.PaymentMethod,
                    PaymentStatus = payment?.Status ?? "Unpaid",
                    StripePaymentIntentId = payment?.StripePaymentIntentId,
                    PaymentDate = payment?.CreatedAt,
                    RefundAmount = payment?.RefundAmount,
                    // Security deposit information
                    SecurityDepositPaymentIntentId = r.SecurityDepositPaymentIntentId,
                    SecurityDepositStatus = r.SecurityDepositStatus,
                    SecurityDepositAuthorizedAt = r.SecurityDepositAuthorizedAt,
                    SecurityDepositCapturedAt = r.SecurityDepositCapturedAt,
                    SecurityDepositReleasedAt = r.SecurityDepositReleasedAt,
                    SecurityDepositChargedAmount = r.SecurityDepositChargedAmount,
                    // Refund records
                    RefundRecords = r.RefundRecords.Select(refund => new RefundRecordDto
                    {
                        Id = refund.Id,
                        BookingId = refund.BookingId,
                        StripeRefundId = refund.StripeRefundId,
                        Amount = refund.Amount,
                        RefundType = refund.RefundType,
                        Reason = refund.Reason,
                        Status = refund.Status,
                        ProcessedBy = refund.ProcessedBy,
                        CreatedAt = refund.CreatedAt
                    }).ToList()
                };
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
                .Include(r => r.Payments)
                .Include(r => r.RefundRecords)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reservation == null)
                return NotFound();

            if (reservation.Vehicle?.VehicleModel != null)
            {
                await _context.Entry(reservation.Vehicle.VehicleModel)
                    .Reference(vm => vm.Model)
                    .LoadAsync();
            }

            // Get the most recent successful payment
            var payment = reservation.Payments
                .Where(p => p.Status == "succeeded")
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefault();
                
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
                UpdatedAt = reservation.UpdatedAt,
                // Payment information
                PaymentMethod = payment?.PaymentMethod,
                PaymentStatus = payment?.Status ?? "Unpaid",
                StripePaymentIntentId = payment?.StripePaymentIntentId,
                PaymentDate = payment?.CreatedAt,
                RefundAmount = payment?.RefundAmount,
                // Security deposit information
                SecurityDepositPaymentIntentId = reservation.SecurityDepositPaymentIntentId,
                SecurityDepositStatus = reservation.SecurityDepositStatus,
                SecurityDepositAuthorizedAt = reservation.SecurityDepositAuthorizedAt,
                SecurityDepositCapturedAt = reservation.SecurityDepositCapturedAt,
                SecurityDepositReleasedAt = reservation.SecurityDepositReleasedAt,
                SecurityDepositChargedAmount = reservation.SecurityDepositChargedAmount,
                // Refund records
                RefundRecords = reservation.RefundRecords.Select(refund => new RefundRecordDto
                {
                    Id = refund.Id,
                    BookingId = refund.BookingId,
                    StripeRefundId = refund.StripeRefundId,
                    Amount = refund.Amount,
                    RefundType = refund.RefundType,
                    Reason = refund.Reason,
                    Status = refund.Status,
                    ProcessedBy = refund.ProcessedBy,
                    CreatedAt = refund.CreatedAt
                }).ToList()
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
            // Log received booking data for debugging
            _logger.LogInformation(
                "[Booking] CreateBooking called - CustomerId: {CustomerId}, VehicleId: {VehicleId}, Make: {Make}, Model: {Model}, CompanyId: {CompanyId}, HasAgreementData: {HasAgreementData}, HasSignature: {HasSignature}",
                createReservationDto.CustomerId,
                createReservationDto.VehicleId,
                createReservationDto.Make,
                createReservationDto.Model,
                createReservationDto.CompanyId,
                createReservationDto.AgreementData != null,
                createReservationDto.AgreementData?.SignatureImage != null && !string.IsNullOrEmpty(createReservationDto.AgreementData.SignatureImage)
            );
            
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("[Booking] ModelState is invalid: {Errors}", 
                    string.Join(", ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
                return BadRequest(ModelState);
            }

            // Validate that either VehicleId or Make/Model are provided
            if (!createReservationDto.VehicleId.HasValue && (string.IsNullOrWhiteSpace(createReservationDto.Make) || string.IsNullOrWhiteSpace(createReservationDto.Model)))
            {
                return BadRequest("Either VehicleId or both Make and Model must be provided");
            }

            var customer = await _context.Customers.FindAsync(createReservationDto.CustomerId);
            if (customer == null)
                return BadRequest("Customer not found");

            var company = await _context.Companies.FindAsync(createReservationDto.CompanyId);
            if (company == null)
                return BadRequest("Company not found");

            // Check for overlapping bookings - prevent double booking
            var unavailableStatuses = new[] { BookingStatus.Pending, BookingStatus.Confirmed, BookingStatus.PickedUp, BookingStatus.Active };
            
            // Normalize request dates: pickup at start of day, return at end of day
            var requestPickupDateStart = createReservationDto.PickupDate.Date; // Start of pickup day (00:00:00)
            var requestReturnDateEnd = createReservationDto.ReturnDate.Date.AddDays(1); // Start of day after return (00:00:00, exclusive)

            Vehicle? vehicle = null;
            Guid? modelId = null;
            string? make = null;
            string? modelName = null;

            // If VehicleId is provided, get the model info; otherwise find model by Make/Model
            if (createReservationDto.VehicleId.HasValue)
            {
                var requestedVehicle = await _context.Vehicles
                    .Include(v => v.VehicleModel)
                        .ThenInclude(vm => vm != null ? vm.Model : null!)
                    .FirstOrDefaultAsync(v => v.Id == createReservationDto.VehicleId!.Value);
                
                if (requestedVehicle == null)
                    return BadRequest("Vehicle not found");

                // Get model info from the requested vehicle
                modelId = requestedVehicle.VehicleModel?.ModelId;
                make = requestedVehicle.VehicleModel?.Model?.Make;
                modelName = requestedVehicle.VehicleModel?.Model?.ModelName;

                if (modelId == null || make == null || modelName == null)
                    return BadRequest("Vehicle model information not found");
            }
            else
            {
                // Find model by Make and Model name
                var model = await _context.Models
                    .FirstOrDefaultAsync(m => m.Make == createReservationDto.Make && 
                                             m.ModelName == createReservationDto.Model);
                
                if (model == null)
                    return BadRequest($"Vehicle model not found: {createReservationDto.Make} {createReservationDto.Model}");

                modelId = model.Id;
                make = model.Make;
                modelName = model.ModelName;
            }

            // Find first available vehicle of this model for the particular dates (no overlapping bookings)
            vehicle = await _context.Vehicles
                .Include(v => v.VehicleModel)
                    .ThenInclude(vm => vm != null ? vm.Model : null!)
                .Where(v => v.CompanyId == createReservationDto.CompanyId &&
                           v.Status == VehicleStatus.Available &&
                           v.VehicleModel != null &&
                           v.VehicleModel.ModelId == modelId!.Value)
                .Where(v => !_context.Bookings
                    .Any(b => b.VehicleId == v.Id &&
                             unavailableStatuses.Contains(b.Status) &&
                             b.PickupDate < requestReturnDateEnd &&
                             b.ReturnDate >= requestPickupDateStart))
                .FirstOrDefaultAsync();

            if (vehicle == null)
            {
                _logger.LogWarning(
                    "No available vehicle found for Make={Make}, Model={Model}, CompanyId={CompanyId} for dates {PickupDate} to {ReturnDate}",
                    make,
                    modelName,
                    createReservationDto.CompanyId,
                    createReservationDto.PickupDate,
                    createReservationDto.ReturnDate
                );
                return BadRequest($"No available {make} {modelName} vehicles found for the selected dates ({createReservationDto.PickupDate:yyyy-MM-dd} to {createReservationDto.ReturnDate:yyyy-MM-dd}).");
            }

            _logger.LogInformation(
                "Found first available vehicle {VehicleId} for Make={Make}, Model={Model}",
                vehicle.Id,
                vehicle.VehicleModel?.Model?.Make ?? createReservationDto.Make ?? "Unknown",
                vehicle.VehicleModel?.Model?.ModelName ?? createReservationDto.Model ?? "Unknown"
            );

            // Update VehicleId in DTO for consistency
            createReservationDto.VehicleId = vehicle.Id;
            
            // Update daily rate from vehicle model if available
            if (vehicle.VehicleModel?.DailyRate.HasValue == true)
            {
                createReservationDto.DailyRate = vehicle.VehicleModel.DailyRate.Value;
            }

            var totalDays = (int)(createReservationDto.ReturnDate - createReservationDto.PickupDate).TotalDays + 1;
            var subtotal = createReservationDto.DailyRate * totalDays;
            var totalAmount = subtotal + createReservationDto.TaxAmount + createReservationDto.InsuranceAmount + createReservationDto.AdditionalFees;

            var bookingNumber = $"BK-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";

            // VehicleId is guaranteed to be set at this point (set above at line 1480)
            var vehicleId = createReservationDto.VehicleId ?? throw new InvalidOperationException("VehicleId must be set before creating booking");

            var reservation = new Booking
            {
                CustomerId = createReservationDto.CustomerId,
                VehicleId = vehicleId,
                CompanyId = createReservationDto.CompanyId,
                BookingNumber = bookingNumber,
                AltBookingNumber = createReservationDto.AltBookingNumber,
                PickupDate = createReservationDto.PickupDate,
                PickupTime = createReservationDto.PickupTime ?? "10:00",
                ReturnDate = createReservationDto.ReturnDate,
                ReturnTime = createReservationDto.ReturnTime ?? "22:00",
                PickupLocation = createReservationDto.PickupLocation,
                ReturnLocation = createReservationDto.ReturnLocation,
                DailyRate = createReservationDto.DailyRate,
                TotalDays = totalDays,
                Subtotal = subtotal,
                TaxAmount = createReservationDto.TaxAmount,
                InsuranceAmount = createReservationDto.InsuranceAmount,
                AdditionalFees = createReservationDto.AdditionalFees,
                TotalAmount = totalAmount,
                SecurityDeposit = createReservationDto.SecurityDeposit ?? 0m,
                Currency = company.Currency ?? "USD",
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

            // Create rental agreement if agreement data is provided
            _logger.LogInformation(
                "[Booking] Agreement data check - AgreementData is null: {IsNull}, SignatureImage is null/empty: {IsSignatureEmpty}",
                createReservationDto.AgreementData == null,
                createReservationDto.AgreementData?.SignatureImage == null || string.IsNullOrEmpty(createReservationDto.AgreementData.SignatureImage)
            );
            
            if (createReservationDto.AgreementData != null && !string.IsNullOrEmpty(createReservationDto.AgreementData.SignatureImage))
            {
                try
                {
                    _logger.LogInformation("[Booking] Creating rental agreement for booking {BookingId}", reservation.Id);
                    
                    // Get customer license info (select only fields that exist in database)
                    // Use raw SQL to avoid EF trying to load CompanyId which doesn't exist in DB
                    var licenseQuery = _context.Database
                        .SqlQueryRaw<LicenseInfoDto>(
                            "SELECT license_number AS \"LicenseNumber\", state_issued AS \"StateIssued\" FROM customer_licenses WHERE customer_id = {0} LIMIT 1",
                            customer.Id);
                    
                    var license = await licenseQuery.FirstOrDefaultAsync();
                    
                    // Format customer address
                    var customerAddressParts = new List<string>();
                    if (!string.IsNullOrEmpty(customer.Address)) customerAddressParts.Add(customer.Address);
                    if (!string.IsNullOrEmpty(customer.City)) customerAddressParts.Add(customer.City);
                    if (!string.IsNullOrEmpty(customer.State)) customerAddressParts.Add(customer.State);
                    if (!string.IsNullOrEmpty(customer.PostalCode)) customerAddressParts.Add(customer.PostalCode);
                    var customerAddress = customerAddressParts.Count > 0 ? string.Join(", ", customerAddressParts) : null;
                    
                    // Get vehicle name
                    var vehicleName = (reservation.Vehicle?.VehicleModel?.Model != null)
                        ? $"{reservation.Vehicle.VehicleModel.Model.Make} {reservation.Vehicle.VehicleModel.Model.ModelName} ({reservation.Vehicle.VehicleModel.Model.Year})"
                        : "Unknown Vehicle";
                    
                    var agreementRequest = new CreateAgreementRequest
                    {
                        CompanyId = createReservationDto.CompanyId,
                        BookingId = reservation.Id,
                        CustomerId = createReservationDto.CustomerId,
                        VehicleId = vehicleId,
                        
                        // Customer info
                        CustomerName = $"{customer.FirstName} {customer.LastName}",
                        CustomerEmail = customer.Email,
                        CustomerPhone = customer.Phone,
                        CustomerAddress = customerAddress,
                        DriverLicenseNumber = license != null ? license.LicenseNumber : null,
                        DriverLicenseState = license != null ? license.StateIssued : null,
                        
                        // Vehicle info
                        VehicleName = vehicleName,
                        VehiclePlate = vehicle.LicensePlate,
                        
                        // Rental details
                        PickupDate = createReservationDto.PickupDate,
                        PickupLocation = createReservationDto.PickupLocation,
                        ReturnDate = createReservationDto.ReturnDate,
                        ReturnLocation = createReservationDto.ReturnLocation,
                        RentalAmount = totalAmount,
                        DepositAmount = createReservationDto.SecurityDeposit ?? 0m,
                        Currency = company.Currency ?? "USD",
                        
                        // Additional services from agreement data
                        AdditionalServices = createReservationDto.AgreementData?.AdditionalServices?.Select(s => new AgreementServiceItem
                        {
                            Name = s.Name,
                            DailyRate = s.DailyRate,
                            Days = s.Days,
                            Total = s.Total
                        }).ToList(),
                        
                        // Agreement data from frontend
                        AgreementData = createReservationDto.AgreementData ?? new AgreementDataDto(),
                        
                        // Request context
                        IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
                    };
                    
                    // Log additional services for debugging
                    _logger.LogInformation(
                        "[Booking] Additional services from AgreementData: Count={Count}, Services={Services}",
                        createReservationDto.AgreementData?.AdditionalServices?.Count ?? 0,
                        System.Text.Json.JsonSerializer.Serialize(createReservationDto.AgreementData?.AdditionalServices)
                    );
                    
                    var createdAgreement = await _rentalAgreementService.CreateAgreementAsync(agreementRequest);
                    
                    _logger.LogInformation(
                        "Successfully created rental agreement {AgreementId} (AgreementNumber: {AgreementNumber}) for booking {BookingId}",
                        createdAgreement.Id,
                        createdAgreement.AgreementNumber,
                        reservation.Id
                    );
                    
                    // Generate PDF immediately so errors surface and file paths are created deterministically
                    try
                    {
                        await _rentalAgreementService.GenerateAndStorePdfAsync(createdAgreement.Id);
                    }
                    catch (Exception ex)
                    {
                        // Log error but don't fail the booking creation itself
                        _logger.LogError(ex, "Failed to generate agreement PDF for booking {BookingId}", reservation.Id);
                    }
                }
                catch (Exception ex)
                {
                    // Log error but don't fail the booking
                    _logger.LogError(ex, 
                        "Failed to create rental agreement for booking {BookingId}. Error: {ErrorMessage}, StackTrace: {StackTrace}", 
                        reservation.Id, 
                        ex.Message,
                        ex.StackTrace);
                    
                    // Log inner exception if present
                    if (ex.InnerException != null)
                    {
                        _logger.LogError(ex.InnerException, 
                            "Inner exception when creating rental agreement for booking {BookingId}", 
                            reservation.Id);
                    }
                }
            }
            else
            {
                _logger.LogWarning(
                    "[Booking] Rental agreement NOT created for booking {BookingId} - AgreementData is null: {IsNull}, SignatureImage is null/empty: {IsSignatureEmpty}",
                    reservation.Id,
                    createReservationDto.AgreementData == null,
                    createReservationDto.AgreementData?.SignatureImage == null || string.IsNullOrEmpty(createReservationDto.AgreementData.SignatureImage)
                );
            }

            // ... existing code ...

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

            // Determine the dates to check for overlaps
            var newPickupDate = updateReservationDto.PickupDate ?? reservation.PickupDate;
            var newReturnDate = updateReservationDto.ReturnDate ?? reservation.ReturnDate;

            // If dates are being changed, check for overlapping bookings (excluding current booking)
            if (updateReservationDto.PickupDate.HasValue || updateReservationDto.ReturnDate.HasValue)
            {
                var unavailableStatuses = new[] { BookingStatus.Pending, BookingStatus.Confirmed, BookingStatus.PickedUp, BookingStatus.Active };
                
                // Normalize request dates: pickup at start of day, return at end of day
                var requestPickupDateStart = newPickupDate.Date; // Start of pickup day (00:00:00)
                var requestReturnDateEnd = newReturnDate.Date.AddDays(1); // Start of day after return (00:00:00, exclusive)
                
                // Check if there are any existing bookings for this vehicle with overlapping dates (excluding current booking)
                var hasOverlappingBooking = await _context.Bookings
                    .AnyAsync(b => b.VehicleId == reservation.VehicleId &&
                                   b.Id != id && // Exclude the current booking being updated
                                   unavailableStatuses.Contains(b.Status) &&
                                   // Booking's pickup date is before the end of requested return date
                                   b.PickupDate < requestReturnDateEnd &&
                                   // Booking's return date is on or after the start of requested pickup date
                                   b.ReturnDate >= requestPickupDateStart);

                if (hasOverlappingBooking)
                {
                    _logger.LogWarning(
                        "Attempted to update booking {BookingId} for vehicle {VehicleId} with overlapping dates. Pickup: {PickupDate}, Return: {ReturnDate}",
                        id,
                        reservation.VehicleId,
                        newPickupDate,
                        newReturnDate
                    );
                    return BadRequest("This vehicle is already booked for the selected dates. Please choose different dates.");
                }
            }

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

            // Check if status is being changed to Completed
            bool statusChangedToCompleted = !string.IsNullOrEmpty(updateReservationDto.Status) && 
                                            updateReservationDto.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase) &&
                                            !reservation.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase);

            if (!string.IsNullOrEmpty(updateReservationDto.Status))
                reservation.Status = updateReservationDto.Status;

            if (updateReservationDto.Notes != null)
                reservation.Notes = updateReservationDto.Notes;

            if (updateReservationDto.PickupDate.HasValue || updateReservationDto.ReturnDate.HasValue)
            {
                reservation.TotalDays = (int)(reservation.ReturnDate - reservation.PickupDate).TotalDays + 1;
                reservation.Subtotal = reservation.DailyRate * reservation.TotalDays;
            }

            reservation.TotalAmount = reservation.Subtotal + reservation.TaxAmount +
                                     reservation.InsuranceAmount + reservation.AdditionalFees;

            reservation.UpdatedAt = DateTime.UtcNow;

            // If status is being changed to Completed and there's a security deposit payment intent, handle it
            if (statusChangedToCompleted && !string.IsNullOrEmpty(reservation.SecurityDepositPaymentIntentId))
            {
                // Check if there's damage to charge for
                bool hasDamage = updateReservationDto.SecurityDepositDamageAmount.HasValue && 
                                 updateReservationDto.SecurityDepositDamageAmount.Value > 0;
                
                try
                {
                    // Get Stripe API key from new tables or settings fallback
                    var stripeSecretKey = await GetStripeSecretKeyAsync(reservation.CompanyId);
                    if (string.IsNullOrEmpty(stripeSecretKey))
                    {
                        _logger.LogError("Stripe secret key not configured in database settings");
                        return StatusCode(500, new { error = "Stripe configuration missing in database" });
                    }
                    Stripe.StripeConfiguration.ApiKey = stripeSecretKey;
                    
                    // First, check the payment intent status
                    var paymentIntentService = new Stripe.PaymentIntentService();
                    var existingIntent = await paymentIntentService.GetAsync(reservation.SecurityDepositPaymentIntentId);
                    
                    _logger.LogInformation(
                        "Booking {BookingId} status changed to Completed. Payment intent {PaymentIntentId} current status: {Status}, Amount: {Amount}, Currency: {Currency}, CaptureMethod: {CaptureMethod}, HasDamage: {HasDamage}",
                        reservation.Id,
                        reservation.SecurityDepositPaymentIntentId,
                        existingIntent.Status,
                        existingIntent.Amount,
                        existingIntent.Currency,
                        existingIntent.CaptureMethod,
                        hasDamage);

                    if (hasDamage)
                    {
                        // Get the damage amount (we know it has a value from the hasDamage check)
                        decimal damageAmount = updateReservationDto.SecurityDepositDamageAmount!.Value;
                        
                        // There's damage - capture the security deposit (partial or full)
                        // Check if payment intent is in a capturable state
                        if (existingIntent.Status != "requires_capture" && existingIntent.Status != "succeeded")
                        {
                            var errorMessage = $"Payment intent {reservation.SecurityDepositPaymentIntentId} is in status '{existingIntent.Status}' and cannot be captured. Expected status: 'requires_capture'";
                            _logger.LogWarning(
                                "Cannot capture security deposit for booking {BookingId}. {ErrorMessage}",
                                reservation.Id,
                                errorMessage);
                            return BadRequest(new { message = errorMessage });
                        }

                        // If already succeeded, it was already captured
                        if (existingIntent.Status == "succeeded")
                        {
                            _logger.LogInformation(
                                "Payment intent {PaymentIntentId} for booking {BookingId} is already captured (status: succeeded). Updating booking record.",
                                reservation.SecurityDepositPaymentIntentId,
                                reservation.Id);
                            
                            reservation.SecurityDepositStatus = "captured";
                            int decimalPlaces = GetCurrencyDecimalPlaces(existingIntent.Currency?.ToLower() ?? "usd");
                            decimal divisor = (decimal)Math.Pow(10, decimalPlaces);
                            reservation.SecurityDepositChargedAmount = existingIntent.AmountReceived > 0 
                                ? existingIntent.AmountReceived / divisor 
                                : existingIntent.Amount / divisor;
                            reservation.SecurityDepositCapturedAt = DateTime.UtcNow;
                            reservation.SecurityDepositCaptureReason = "Vehicle damage reported upon completion - Already captured";
                        }
                        else
                        {
                            // Determine the amount to capture (partial or full)
                            
                            // Get the full security deposit amount from the payment intent
                            int decimalPlaces = GetCurrencyDecimalPlaces(existingIntent.Currency?.ToLower() ?? "usd");
                            decimal divisor = (decimal)Math.Pow(10, decimalPlaces);
                            decimal fullDepositAmount = existingIntent.Amount / divisor;
                            
                            // Determine if this is a partial or full charge
                            bool isFullCharge = damageAmount >= fullDepositAmount;
                            decimal? amountToCapture = isFullCharge ? null : damageAmount; // null = full capture
                            
                            _logger.LogInformation(
                                "Booking {BookingId} status changed to Completed with damage. Full deposit: {FullAmount}, Damage amount: {DamageAmount}, IsFullCharge: {IsFullCharge}. Attempting to capture security deposit from payment intent {PaymentIntentId}",
                                reservation.Id,
                                fullDepositAmount,
                                damageAmount,
                                isFullCharge,
                                reservation.SecurityDepositPaymentIntentId);

                            // Get company currency for proper amount conversion
                            var companyCurrency = reservation.Company?.Currency ?? reservation.Currency ?? existingIntent.Currency?.ToLower() ?? "USD";
                            
                            // Capture the security deposit payment intent (partial or full)
                            var capturedIntent = await _stripeService.CapturePaymentIntentAsync(
                                reservation.SecurityDepositPaymentIntentId,
                                amountToCapture,
                                companyCurrency);
                            
                            _logger.LogInformation(
                                "Security deposit payment intent {PaymentIntentId} captured successfully. Status: {Status}, Amount: {Amount}, AmountReceived: {AmountReceived}",
                                capturedIntent.Id,
                                capturedIntent.Status,
                                capturedIntent.Amount,
                                capturedIntent.AmountReceived);

                            // Update booking with captured information
                            if (capturedIntent.Status == "succeeded")
                            {
                                reservation.SecurityDepositStatus = "captured";
                                int capturedDecimalPlaces = GetCurrencyDecimalPlaces(capturedIntent.Currency?.ToLower() ?? "usd");
                                decimal capturedDivisor = (decimal)Math.Pow(10, capturedDecimalPlaces);
                                reservation.SecurityDepositChargedAmount = capturedIntent.AmountReceived > 0 
                                    ? capturedIntent.AmountReceived / capturedDivisor 
                                    : capturedIntent.Amount / capturedDivisor;
                                reservation.SecurityDepositCapturedAt = DateTime.UtcNow;
                                reservation.SecurityDepositCaptureReason = isFullCharge 
                                    ? $"Vehicle damage reported upon completion - Full charge: {reservation.SecurityDepositChargedAmount:C}"
                                    : $"Vehicle damage reported upon completion - Partial charge: {damageAmount:C}";
                                
                                _logger.LogInformation(
                                    "Security deposit captured for booking {BookingId}. Amount: {Amount}, Currency: {Currency}, ChargeType: {ChargeType}",
                                    reservation.Id,
                                    reservation.SecurityDepositChargedAmount,
                                    capturedIntent.Currency,
                                    isFullCharge ? "Full" : "Partial");
                            }
                            else
                            {
                                var errorMessage = $"Payment intent capture returned status '{capturedIntent.Status}' instead of 'succeeded'";
                                _logger.LogError(
                                    "Security deposit capture did not succeed for booking {BookingId}. Payment intent status: {Status}",
                                    reservation.Id,
                                    capturedIntent.Status);
                                return BadRequest(new { message = errorMessage });
                            }
                        }
                    }
                    else
                    {
                        // No damage - release the security deposit
                        _logger.LogInformation(
                            "Booking {BookingId} status changed to Completed with no damage. Releasing security deposit payment intent {PaymentIntentId}",
                            reservation.Id,
                            reservation.SecurityDepositPaymentIntentId);

                        // Check if payment intent can be cancelled/released
                        if (existingIntent.Status == "requires_capture")
                        {
                            // Cancel the payment intent to release the hold
                            var cancelledIntent = await _stripeService.CancelPaymentIntentAsync(reservation.SecurityDepositPaymentIntentId);
                            
                            _logger.LogInformation(
                                "Security deposit payment intent {PaymentIntentId} cancelled/released successfully. Status: {Status}",
                                cancelledIntent.Id,
                                cancelledIntent.Status);

                            reservation.SecurityDepositStatus = "released";
                            reservation.SecurityDepositReleasedAt = DateTime.UtcNow;
                            reservation.SecurityDepositCaptureReason = "Vehicle returned in good condition - No damage";
                            
                            _logger.LogInformation(
                                "Security deposit released for booking {BookingId}. No charge applied.",
                                reservation.Id);
                        }
                        else if (existingIntent.Status == "succeeded")
                        {
                            // Already captured - can't release
                            _logger.LogWarning(
                                "Security deposit for booking {BookingId} is already captured (status: succeeded). Cannot release.",
                                reservation.Id);
                            // Don't fail - just log the warning
                        }
                        else if (existingIntent.Status == "canceled")
                        {
                            // Already cancelled/released
                            _logger.LogInformation(
                                "Security deposit for booking {BookingId} is already released (status: canceled). Updating booking record.",
                                reservation.Id);
                            reservation.SecurityDepositStatus = "released";
                            reservation.SecurityDepositReleasedAt = DateTime.UtcNow;
                            reservation.SecurityDepositCaptureReason = "Vehicle returned in good condition - No damage";
                        }
                        else
                        {
                            _logger.LogWarning(
                                "Security deposit payment intent {PaymentIntentId} for booking {BookingId} is in status '{Status}' and cannot be released. Expected status: 'requires_capture'",
                                reservation.SecurityDepositPaymentIntentId,
                                reservation.Id,
                                existingIntent.Status);
                            // Don't fail - just log the warning and mark as released in our system
                            reservation.SecurityDepositStatus = "released";
                            reservation.SecurityDepositReleasedAt = DateTime.UtcNow;
                            reservation.SecurityDepositCaptureReason = "Vehicle returned in good condition - No damage";
                        }
                    }
                }
                catch (Stripe.StripeException ex)
                {
                    var errorMessage = hasDamage 
                        ? $"Failed to capture security deposit: {ex.Message}"
                        : $"Failed to release security deposit: {ex.Message}";
                    _logger.LogError(ex,
                        "Stripe error processing security deposit payment intent {PaymentIntentId} for booking {BookingId}. StripeError: {StripeError}, StripeErrorCode: {StripeErrorCode}",
                        reservation.SecurityDepositPaymentIntentId,
                        reservation.Id,
                        ex.StripeError?.Message,
                        ex.StripeError?.Code);
                    return BadRequest(new { message = errorMessage, stripeError = ex.StripeError?.Message, stripeErrorCode = ex.StripeError?.Code });
                }
                catch (Exception ex)
                {
                    var errorMessage = hasDamage 
                        ? $"Failed to capture security deposit: {ex.Message}"
                        : $"Failed to release security deposit: {ex.Message}";
                    _logger.LogError(ex,
                        "Failed to process security deposit payment intent {PaymentIntentId} for booking {BookingId}",
                        reservation.SecurityDepositPaymentIntentId,
                        reservation.Id);
                    return BadRequest(new { message = errorMessage });
                }
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
                                    captureImmediately: true,
                                    companyId: reservation.CompanyId);

                                // Confirm the payment intent (pass companyId to use connected account)
                                var confirmedIntent = await _stripeService.ConfirmPaymentIntentAsync(securityDepositIntent.Id, reservation.CompanyId);

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
    /// Get rental agreement for a booking
    /// </summary>
    /// <param name="id">Booking ID</param>
    /// <returns>Rental agreement details</returns>
    [HttpGet("bookings/{id}/rental-agreement")]
    [Authorize]
    [ProducesResponseType(typeof(RentalAgreementResponseDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetRentalAgreement(Guid id)
    {
        try
        {
            var agreement = await _rentalAgreementService.GetByBookingIdAsync(id);
            
            if (agreement == null)
            {
                return NotFound(new { message = "Rental agreement not found for this booking" });
            }

            var response = new RentalAgreementResponseDto
            {
                Id = agreement.Id,
                AgreementNumber = agreement.AgreementNumber,
                BookingId = agreement.BookingId,
                CustomerId = agreement.CustomerId,
                VehicleId = agreement.VehicleId,
                CompanyId = agreement.CompanyId,
                Language = agreement.Language,
                CustomerName = agreement.CustomerName,
                CustomerEmail = agreement.CustomerEmail,
                CustomerPhone = agreement.CustomerPhone,
                CustomerAddress = agreement.CustomerAddress,
                DriverLicenseNumber = agreement.DriverLicenseNumber,
                DriverLicenseState = agreement.DriverLicenseState,
                VehicleName = agreement.VehicleName,
                VehiclePlate = agreement.VehiclePlate,
                PickupDate = agreement.PickupDate,
                PickupLocation = agreement.PickupLocation,
                ReturnDate = agreement.ReturnDate,
                ReturnLocation = agreement.ReturnLocation,
                RentalAmount = agreement.RentalAmount,
                DepositAmount = agreement.DepositAmount,
                Currency = agreement.Currency,
                SignatureImage = agreement.SignatureImage,
                SignedAt = agreement.SignedAt,
                PdfUrl = agreement.PdfUrl,
                PdfGeneratedAt = agreement.PdfGeneratedAt,
                Status = agreement.Status,
                CreatedAt = agreement.CreatedAt,
                // Consent timestamps
                Consents = new AgreementConsentsDto
                {
                    TermsAcceptedAt = agreement.TermsAcceptedAt,
                    NonRefundableAcceptedAt = agreement.NonRefundableAcceptedAt,
                    DamagePolicyAcceptedAt = agreement.DamagePolicyAcceptedAt,
                    CardAuthorizationAcceptedAt = agreement.CardAuthorizationAcceptedAt,
                },
                // Consent texts
                ConsentTexts = new ConsentTextsDto
                {
                    TermsTitle = agreement.TermsText?.Split('\n')[0] ?? "",
                    TermsText = (agreement.TermsText?.Contains('\n') ?? false) ? agreement.TermsText.Substring(agreement.TermsText.IndexOf('\n') + 1) : "",
                    NonRefundableTitle = agreement.NonRefundableText?.Split('\n')[0] ?? "",
                    NonRefundableText = (agreement.NonRefundableText?.Contains('\n') ?? false) ? agreement.NonRefundableText.Substring(agreement.NonRefundableText.IndexOf('\n') + 1) : "",
                    DamagePolicyTitle = agreement.DamagePolicyText?.Split('\n')[0] ?? "",
                    DamagePolicyText = (agreement.DamagePolicyText?.Contains('\n') ?? false) ? agreement.DamagePolicyText.Substring(agreement.DamagePolicyText.IndexOf('\n') + 1) : "",
                    CardAuthorizationTitle = agreement.CardAuthorizationText?.Split('\n')[0] ?? "",
                    CardAuthorizationText = (agreement.CardAuthorizationText?.Contains('\n') ?? false) ? agreement.CardAuthorizationText.Substring(agreement.CardAuthorizationText.IndexOf('\n') + 1) : "",
                }
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving rental agreement for booking {BookingId}", id);
            return StatusCode(500, new { message = "Error retrieving rental agreement" });
        }
    }

    /// <summary>
    /// Sign an existing booking that doesn't have an agreement yet.
    /// Creates the agreement and generates PDF.
    /// </summary>
    /// <param name="id">Booking ID</param>
    /// <param name="agreementData">Agreement signature and consent data</param>
    /// <returns>Created rental agreement</returns>
    [HttpPost("bookings/{id}/sign-agreement")]
    [Authorize]
    [ProducesResponseType(typeof(RentalAgreementResponseDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> SignExistingBooking(Guid id, [FromBody] AgreementDataDto agreementData)
    {
        try
        {
            _logger.LogInformation(
                "[Booking] SignExistingBooking called - BookingId: {BookingId}, HasSignature: {HasSignature}",
                id,
                !string.IsNullOrEmpty(agreementData?.SignatureImage)
            );

            if (agreementData == null || string.IsNullOrEmpty(agreementData.SignatureImage))
            {
                return BadRequest(new { message = "Signature is required" });
            }

            // Check if booking exists
            var booking = await _context.Bookings.FindAsync(id);
            if (booking == null)
            {
                return NotFound(new { message = "Booking not found" });
            }

            // Get IP address
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

            // Sign the booking
            var agreement = await _rentalAgreementService.SignExistingBookingAsync(id, agreementData, ipAddress);

            // Return agreement details
            var response = new RentalAgreementResponseDto
            {
                Id = agreement.Id,
                AgreementNumber = agreement.AgreementNumber,
                BookingId = agreement.BookingId,
                CustomerId = agreement.CustomerId,
                VehicleId = agreement.VehicleId,
                CompanyId = agreement.CompanyId,
                Language = agreement.Language,
                CustomerName = agreement.CustomerName,
                CustomerEmail = agreement.CustomerEmail,
                CustomerPhone = agreement.CustomerPhone,
                CustomerAddress = agreement.CustomerAddress,
                DriverLicenseNumber = agreement.DriverLicenseNumber,
                DriverLicenseState = agreement.DriverLicenseState,
                VehicleName = agreement.VehicleName,
                VehiclePlate = agreement.VehiclePlate,
                PickupDate = agreement.PickupDate,
                PickupLocation = agreement.PickupLocation,
                ReturnDate = agreement.ReturnDate,
                ReturnLocation = agreement.ReturnLocation,
                RentalAmount = agreement.RentalAmount,
                DepositAmount = agreement.DepositAmount,
                Currency = agreement.Currency,
                SignatureImage = agreement.SignatureImage,
                SignedAt = agreement.SignedAt,
                PdfUrl = agreement.PdfUrl,
                PdfGeneratedAt = agreement.PdfGeneratedAt,
                Status = agreement.Status,
                CreatedAt = agreement.CreatedAt,
                Consents = new AgreementConsentsDto
                {
                    TermsAcceptedAt = agreement.TermsAcceptedAt,
                    NonRefundableAcceptedAt = agreement.NonRefundableAcceptedAt,
                    DamagePolicyAcceptedAt = agreement.DamagePolicyAcceptedAt,
                    CardAuthorizationAcceptedAt = agreement.CardAuthorizationAcceptedAt,
                },
                ConsentTexts = new ConsentTextsDto
                {
                    TermsTitle = agreement.TermsText?.Split('\n')[0] ?? "",
                    TermsText = (agreement.TermsText?.Contains('\n') ?? false) ? agreement.TermsText.Substring(agreement.TermsText.IndexOf('\n') + 1) : "",
                    NonRefundableTitle = agreement.NonRefundableText?.Split('\n')[0] ?? "",
                    NonRefundableText = (agreement.NonRefundableText?.Contains('\n') ?? false) ? agreement.NonRefundableText.Substring(agreement.NonRefundableText.IndexOf('\n') + 1) : "",
                    DamagePolicyTitle = agreement.DamagePolicyText?.Split('\n')[0] ?? "",
                    DamagePolicyText = (agreement.DamagePolicyText?.Contains('\n') ?? false) ? agreement.DamagePolicyText.Substring(agreement.DamagePolicyText.IndexOf('\n') + 1) : "",
                    CardAuthorizationTitle = agreement.CardAuthorizationText?.Split('\n')[0] ?? "",
                    CardAuthorizationText = (agreement.CardAuthorizationText?.Contains('\n') ?? false) ? agreement.CardAuthorizationText.Substring(agreement.CardAuthorizationText.IndexOf('\n') + 1) : "",
                }
            };

            _logger.LogInformation(
                "[Booking] Successfully signed booking {BookingId} - AgreementId: {AgreementId}",
                id,
                agreement.Id
            );

            return Ok(response);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already exists"))
        {
            _logger.LogWarning(ex, "Agreement already exists for booking {BookingId}", id);
            return Conflict(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid argument when signing booking {BookingId}", id);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error signing booking {BookingId}", id);
            return StatusCode(500, new { message = "Error signing booking" });
        }
    }

    /// <summary>
    /// Generate a preview PDF for rental agreement without saving.
    /// Used for previewing the agreement before booking is created.
    /// </summary>
    /// <param name="request">Preview data</param>
    /// <returns>PDF file</returns>
    [HttpPost("preview-agreement-pdf")]
    [ProducesResponseType(typeof(FileContentResult), 200)]
    [ProducesResponseType(400)]
    public IActionResult PreviewAgreementPdf([FromBody] PreviewAgreementPdfRequest request)
    {
        try
        {
            if (request == null)
            {
                return BadRequest(new { message = "Request data is required" });
            }

            // Get Rules of Action and Full Terms texts based on language
            var (rulesText, fullTermsText) = GetRulesAndTermsTexts(request.Language ?? "en");

            // Parse customer name if individual fields not provided
            var customerFirstName = request.CustomerFirstName ?? "";
            var customerLastName = request.CustomerLastName ?? "";
            if (string.IsNullOrEmpty(customerFirstName) && !string.IsNullOrEmpty(request.CustomerName))
            {
                var nameParts = request.CustomerName.Split(' ', 2);
                customerFirstName = nameParts.Length > 0 ? nameParts[0] : "";
                customerLastName = nameParts.Length > 1 ? nameParts[1] : "";
            }

            var pdfGenerator = new RentalAgreementPdfGenerator();
            var pdfData = new RentalAgreementPdfData
            {
                AgreementNumber = "PREVIEW",
                Language = request.Language ?? "en",
                
                // Company Info
                CompanyName = request.CompanyName ?? "Rental Company",
                CompanyAddress = request.CompanyAddress,
                CompanyPhone = request.CompanyPhone,
                CompanyEmail = request.CompanyEmail,
                
                // Customer / Primary Renter
                CustomerFirstName = customerFirstName,
                CustomerMiddleName = request.CustomerMiddleName,
                CustomerLastName = customerLastName,
                CustomerName = request.CustomerName ?? $"{customerFirstName} {customerLastName}".Trim(),
                CustomerEmail = request.CustomerEmail ?? "",
                CustomerPhone = request.CustomerPhone,
                CustomerAddress = request.CustomerAddress,
                DriverLicenseNumber = request.DriverLicenseNumber,
                DriverLicenseState = request.DriverLicenseState,
                DriverLicenseExpiration = request.DriverLicenseExpiration,
                CustomerDateOfBirth = request.CustomerDateOfBirth,
                
                // Additional Driver
                AdditionalDriverFirstName = request.AdditionalDriverFirstName,
                AdditionalDriverMiddleName = request.AdditionalDriverMiddleName,
                AdditionalDriverLastName = request.AdditionalDriverLastName,
                AdditionalDriverEmail = request.AdditionalDriverEmail,
                AdditionalDriverPhone = request.AdditionalDriverPhone,
                AdditionalDriverLicenseNumber = request.AdditionalDriverLicenseNumber,
                AdditionalDriverLicenseState = request.AdditionalDriverLicenseState,
                AdditionalDriverLicenseExpiration = request.AdditionalDriverLicenseExpiration,
                AdditionalDriverDateOfBirth = request.AdditionalDriverDateOfBirth,
                AdditionalDriverAddress = request.AdditionalDriverAddress,
                
                // Rental Vehicle
                VehicleType = request.VehicleType,
                VehicleName = request.VehicleName ?? "Vehicle",
                VehicleYear = request.VehicleYear,
                VehicleColor = request.VehicleColor,
                VehiclePlate = request.VehiclePlate,
                VehicleVin = request.VehicleVin,
                OdometerStart = request.OdometerStart,
                
                // Rental Period
                PickupDate = request.PickupDate ?? DateTime.UtcNow,
                PickupTime = request.PickupTime,
                PickupLocation = request.PickupLocation,
                ReturnDate = request.ReturnDate ?? DateTime.UtcNow.AddDays(1),
                ReturnTime = request.ReturnTime,
                ReturnLocation = request.ReturnLocation,
                DueDate = request.DueDate,
                
                // Fuel Level
                FuelAtPickup = request.FuelAtPickup,
                FuelAtReturn = request.FuelAtReturn,
                
                // Financial
                RentalAmount = request.RentalAmount ?? 0,
                DepositAmount = request.DepositAmount ?? 0,
                DailyRate = request.DailyRate ?? 0,
                RentalDays = request.RentalDays ?? 1,
                Currency = request.Currency ?? "USD",
                
                // Additional Services
                AdditionalServices = request.AdditionalServices?.Select(s => new AdditionalServiceItem
                {
                    Name = s.Name ?? "",
                    DailyRate = s.DailyRate,
                    Days = s.Days,
                    Total = s.Total
                }).ToList() ?? new List<AdditionalServiceItem>(),
                Subtotal = request.Subtotal ?? request.RentalAmount ?? 0,
                TotalCharges = request.TotalCharges ?? request.RentalAmount ?? 0,
                
                // Additional Charges
                LateReturnFee = request.LateReturnFee ?? 0,
                DamageFee = request.DamageFee ?? 0,
                FuelServiceFee = request.FuelServiceFee ?? 0,
                CleaningFee = request.CleaningFee ?? 0,
                Refund = request.Refund ?? 0,
                BalanceDue = request.BalanceDue ?? 0,
                
                // Signature
                SignatureImage = "", // No signature for preview
                SignedAt = DateTime.UtcNow,
                
                // Terms
                RulesText = rulesText,
                FullTermsText = fullTermsText,
            };

            var pdfBytes = pdfGenerator.Generate(pdfData);

            return File(pdfBytes, "application/pdf", "rental-agreement-preview.pdf");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating preview PDF");
            return StatusCode(500, new { message = "Error generating preview PDF" });
        }
    }

    /// <summary>
    /// Get Rules of Action and Full Terms texts by language
    /// </summary>
    private static (string RulesText, string FullTermsText) GetRulesAndTermsTexts(string language)
    {
        var lang = language?.ToLower().Substring(0, Math.Min(2, language?.Length ?? 0)) ?? "en";
        
        return lang switch
        {
            "es" => (
                RulesText: "REGLAS DE ACCIÓN\n\n" +
                    "1. CONDUCTOR AUTORIZADO: Está absolutamente PROHIBIDO usar u operar este vehículo alquilado por cualquier persona que no esté listada en el contrato de alquiler. Cada conductor debe ser precalificado en persona con licencia de conducir válida y tener un mínimo de 21 años de edad. Si el conductor es menor de 25 años pueden aplicar tarifas adicionales.\n\n" +
                    "2. ALCOHOL Y DROGAS: Está absolutamente PROHIBIDO usar u operar este vehículo alquilado por cualquier persona que esté bajo la influencia de ALCOHOL o cualquier NARCÓTICO.\n\n" +
                    "3. NO FUMAR: NO está permitido FUMAR en el vehículo alquilado bajo ninguna circunstancia. Cualquier evidencia encontrada de cualquier tipo de fumar en el vehículo aplicará una multa independientemente de si hay daño o no.\n\n" +
                    "4. LLAVES PERDIDAS: En caso de LLAVES PERDIDAS, DAÑADAS o BLOQUEADAS DENTRO que requieran recuperación de llaves, puede aplicar un cargo máximo.\n\n" +
                    "5. CAPACIDAD DE PASAJEROS: La capacidad de pasajeros de este vehículo está determinada por el número de cinturones de seguridad y, por ley, NO debe EXCEDERSE. Mientras esté en el vehículo, siempre use el cinturón de seguridad. ¡Es la ley!\n\n" +
                    "6. TARIFA DE LIMPIEZA: En caso de que el vehículo sea devuelto excepcionalmente sucio, puede aplicar una TARIFA DE LIMPIEZA.\n\n" +
                    "7. NEUMÁTICOS: Los NEUMÁTICOS pinchados o dañados son responsabilidad del Arrendatario y son la responsabilidad financiera del Arrendatario o Conductor(es) Autorizado(s).\n\n" +
                    "8. MULTAS: TODAS LAS MULTAS, PENALIZACIONES, TARIFAS y otros recibidos durante el período de alquiler, causados como resultado del arrendatario y/o período de alquiler deben ser pagados por el Arrendatario.\n\n" +
                    "9. PERÍODO DE 24 HORAS: Los Días de Alquiler se basan en Períodos de Alquiler de 24 horas. Por favor devuelva a tiempo. Cada hora pasada del Período de Alquiler se calculará a ¼ del cargo diario incluyendo todos los impuestos y tarifas y hasta un día completo.\n\n" +
                    "10. USO DE CELULAR: No usar el celular mientras opera el Vehículo a menos que sea un dispositivo manos libres como Bluetooth. Es la ley, el conductor tiene prohibido manejar cualquier dispositivo electrónico mientras conduce.\n\n" +
                    "11. AUTORIZACIÓN DE TARJETA: Autorizo a la empresa de alquiler a cargar en mi tarjeta de crédito/débito: el monto del alquiler, depósito de seguridad, cargos de combustible si aplica, infracciones de tráfico, multas de estacionamiento, cargos de peaje, reparaciones por daños, tarifas de limpieza y cualquier otro cargo incurrido durante o como resultado de este alquiler.",
                FullTermsText: GetFullTermsTextSpanish()
            ),
            _ => ( // English (default)
                RulesText: "RULES OF ACTION\n\n" +
                    "1. AUTHORIZED DRIVERS ONLY: It is absolutely PROHIBITED to use or operate this rented vehicle by anyone not listed on the rental agreement. Each driver must be pre-qualified in person with valid driver's license and be a minimum of 21 years of age. If Driver is under the age of 25 additional fees may apply.\n\n" +
                    "2. NO ALCOHOL OR NARCOTICS: It is absolutely PROHIBITED to use or operate this rented vehicle by anyone who is under the influence of ALCOHOL or any NARCOTICS.\n\n" +
                    "3. NO SMOKING POLICY: There is absolutely NO SMOKING allowed in the rented vehicle under any circumstances. Any found evidence of any kind of smoking in the vehicle will result in a fine regardless of damage or not.\n\n" +
                    "4. LOST KEYS POLICY: In case of LOST, DAMAGED or LOCKED INSIDE KEYS requiring key recovery, a maximum charge may apply.\n\n" +
                    "5. PASSENGER CAPACITY: The passenger capacity of this vehicle is determined by the number of seatbelts and, by law, must NOT be EXCEEDED. While in the Vehicle, please always fasten your seatbelts. It is the law!\n\n" +
                    "6. CLEANING FEE: In case the Vehicle will be returned exceptionally dirty, a CLEANING FEE may apply.\n\n" +
                    "7. TIRES: Flat or damaged TIRES are the responsibility of the Renter and are the financial responsibility of the Renter or Authorized Driver(s).\n\n" +
                    "8. TICKETS AND FINES: ALL TICKETS, FINES, FEES and others received during the rental period, caused as a result of the renter and/or rental period must be paid by the Renter.\n\n" +
                    "9. 24-HOUR RENTAL PERIOD: Rental Days are based on 24-hour Rental Periods. Please return at the proper time. Each hour past the Rental Period will be calculated at ¼ of a day charge including all taxes and fees and up to a full day.\n\n" +
                    "10. NO CELL PHONE USE: No cell phone use while operating the Vehicle unless it is a hands-free device such as Bluetooth. It's the law - the driver is prohibited from handling any electronic device while driving regardless of the behavior of use.\n\n" +
                    "11. CARD AUTHORIZATION: I authorize the rental company to charge my credit/debit card on file for: the rental amount, security deposit, fuel charges if applicable, traffic violations, parking tickets, toll charges, damage repairs, cleaning fees, and any other charges incurred during or as a result of this rental.",
                FullTermsText: GetFullTermsTextEnglish()
            )
        };
    }

    private static string GetFullTermsTextEnglish()
    {
        return @"3. ELECTRONIC COMMUNICATIONS AND TELEMATICS

(a) You agree that the Company may use electronic or verbal means to contact You. You agree that the Company may use any email address or telephone number You provide to contact You, including manual calling, voice messages, text messages, emails or automatic telephone dialing systems. The Car may be equipped with global positioning technology or other telematics systems and a transmitter that allows the Company to track or otherwise locate the Car and privacy is not guaranteed. Information collected by any such technology or telematics is governed by the Company's privacy policy. You acknowledge that the data derived from the in-Car telematics and other devices may contain personal information and You authorize the Company to share that data with the device manufacturer, the original equipment manufacturer and its affiliates (collectively, ""OEM""), service providers, and other third parties to whom the Company or OEM grants access. To the extent permitted by law, You authorize the Company, the OEM and any third-party service provider's use of the technology included in the Car, including to track the location of the Car, to disable the Car and to assist in the repossession of the Car. It is Your responsibility to delete any Bluetooth synced data from the Car upon Your return. You acknowledge and agree that, to the extent permitted by applicable law, the Company, the OEM and any third-party service provider may collect, process, charge on the basis of, add to Your customer profile and take disciplinary action on the basis of the data derived from in-Car telematics and other devices and gauges. Actions may include suspension or termination of Your ability to continue to rent Cars from the Company or its affiliates.

(b) The Car may have telematics, tracking, and related services in which case, You understand that Your access and use of the Car or the services are subject to the Car, service provider's or device manufacturer's terms and privacy statement, which may include other terms, service limitations, warranty exclusions, limitations of liability, wireless service provider terms and privacy practices.

(c) Upon return, if the Car requires more than the Company's standard cleaning on its return, the Company may charge You for the actual costs incurred by the Company to have the Car cleaned.

(d) In California: electronic service technology included in the Car may be activated if the Car is not returned within 72 hours after the contracted return date or extension of the return date.

(e) For rentals commencing in Arizona, it is required by law that You acknowledge Your understanding that it will be a violation of Arizona Statutes 131806 if the Car is not returned within 72 hours of the due date and time specified on the rental record and that You shall be subject to a maximum penalty not to exceed US$150,000 and/or imprisonment of 2.25 years. By renting a Car under this agreement, You acknowledge that You have received and understand this notice.

(f) For rentals in the District of Columbia, it is required by law that You be notified that if You fail to return the Car in accordance with this Agreement, it may result in a criminal penalty of up to 3 years in jail.

4. YOUR RESPONSIBILITY FOR LOSS OF OR DAMAGE TO THE CAR AND OPTIONAL LOSS DAMAGE WAIVER

(a) Except as stated below, You are responsible for any and all loss of or damage to the Car resulting from any cause including but not limited to collision, rollover, theft, vandalism, seizure, fire, flood, hail or other acts of nature or god regardless of fault.

(b) Except as stated below, Your responsibility will not exceed the greater of the retail fair market value of the Car and its manufacturer buyback program value at the time the Car is lost or damaged, less its salvage value, plus actual towing, storage and impound fees, diminution of value of the Car as determined by the Company, an administrative charge and a charge for loss of use, regardless of fleet utilization. As more generally provided in paragraph 6, the Company may, where permitted under applicable law, process one or more vouchers or payment slips against Your credit, charge or debit Card for these losses, costs and charges, together with any other applicable charges, at or following the completion of the rental.

(c) Your responsibility for damage due to theft or otherwise is limited by law in certain jurisdictions. As of June 1, 2020, the following limitations exist. Should the laws imposing these regulations be repealed, the provisions of subparagraphs 4(a) and 4(b) shall apply without such limitations.

CALIFORNIA: For rentals commencing in California: (A) You are only responsible for loss of or damage to the Car resulting from collision, rollover, theft or vandalism, (B) Your responsibility for loss or damage to the Car will in no event exceed the fair market value of the Car at the time it is lost or damaged, plus actual charges for towing, storage and impound fees, and an administrative charge, (C) Your responsibility for loss of or damage to the Car resulting from vandalism unrelated to the theft of the Car will not exceed US$500 and (D) You are not responsible for loss of or damage to the Car resulting from theft unless it results from a failure to exercise ordinary Care by You or any Authorized Operator.

ILLINOIS: For rentals commencing in Illinois, for a Car with an MSRP of $50,000 or less, Your responsibility for loss or damage due to causes other than theft will not exceed $19,500 through May 31, 2021, which limit will increase by $500 per year starting June 1, 2021; and Your responsibility for theft will not exceed $2,000 unless it is established that You or an Authorized Operator failed to exercise ordinary Care while in possession of the Car or committed or aided in the commission of the theft. For a Car with an MSRP of more than $50,000, Your responsibility for loss or damage due to causes other than theft, and for theft, will not exceed $50,000 through September 30, 2020, which limit will increase by $1,000 per year starting October 1, 2020.

INDIANA: For rentals in Indiana, You will be responsible for no more than: (A) loss or damage to the Car up to its fair market value resulting from the collision, theft or vandalism, (B) loss of use of the Car, if You are liable for damage, (C) actual charges for towing, storage, and impound fees paid by the Company, if You are liable for the damage, and (D) an administrative charge.

NEVADA: For rentals in Nevada: (A) Your responsibility for loss or damage to the Car will not exceed the fair market value of the Car at the time the Car is lost or damaged plus actual towing, storage and impound fees, an administrative charge and a reasonable charge for loss of use, regardless of fleet utilization; (B) Your responsibility for damage to the Car and loss of use of the Car resulting from vandalism not related to the theft of the Car and not caused by You will not exceed $2500; and (C) You are not responsible for loss of or damage to the Car resulting from theft or vandalism related to the theft if You have possession of the ignition key or You establish that the ignition key was not in the Car at the time of the theft, You file an official report of the theft with the police within 24 hours of learning of the theft and You cooperate with the Company and the police in providing information regarding the theft, and neither You nor an Authorized Operator committed or aided and abetted the commission of the theft.

NEW YORK: For rentals commencing in New York: (A) This Agreement offers, for an additional charge, optional vehicle protection to cover Your financial responsibility for damage or loss to the Car. The purchase of optional vehicle protection is optional and may be declined. You are advised to carefully consider whether to purchase this protection if You have rental vehicle collision coverage provided by Your credit Card or automobile insurance policy. Before deciding whether to purchase optional vehicle protection, You may wish to determine whether Your credit Card or Your vehicle insurance affords You coverage for damage to the Car and the amount of deductible under such coverage. (B) Your responsibility for loss or damage to the Car will not exceed the lesser of (a) the actual and reasonable costs incurred by the Company to repair the Car or which the Company would have incurred if the Car was repaired, which shall reflect any discounts, price reductions or adjustments available to the Company; or (b) the fair market value of the Car at the time the Car is lost or damaged, less any net disposal proceeds. You will not be responsible for damages incurred by the Company for the loss of use of the Car, related administrative charges, or amounts that the Company recovers from any other party. You are not responsible for mechanical damage unrelated to an accident or that could reasonably be expected from normal use of the Car except in instances where abuse or neglect by You or an Authorized Operator is shown.

WISCONSIN: For rentals commencing in Wisconsin, (A) You are not responsible for any damage to the Car other than damage (x) resulting from an accident occurring while the Car is under this agreement or (y) caused intentionally by, or by the reckless or wanton misconduct of, You or an Authorized Operator; and (B) Your responsibility will in no event exceed the fair market value of the Car immediately before the damage occurs, less its salvage value, plus actual towing fees and storage fees for no more than 2 days.

(d) You grant the Company a limited power of attorney to present claims for damage to or loss of the Car to Your insurance Carrier. For rentals which commence in New Mexico or New York, if such coverage exists under Your automobile insurance policy, You may require that the Company submit any claims to Your insurance Carrier as Your agent.

5. PROHIBITED USE OF THE CAR

Neither You nor any Authorized Operator may:
• Permit the use of the Car by anyone other than You or an Authorized Operator;
• Intentionally destroy, damage or aid in the theft of the Car;
• Take or attempt to take the Car into Mexico or to anywhere else outside of the United States or Canada, except as expressly permitted under this agreement;
• Engage in any willful or wanton misconduct, which, among other things, may include reckless conduct such as: the failure to use seat belts, the failure to use child seats or other child restraints where legally required, use of the Car when overloaded or carrying passengers in excess of the number of seat belts in the Car, use off paved roads or on roads which are not regularly maintained, refueling the Car with the wrong type of fuel, leaving the Car and failing to remove the keys, or failing to close and lock all doors, Car windows or the trunk;
• Use or permit the use of the Car while legally intoxicated or under the influence of alcohol, drugs or other absorbed elements which may adversely affect a person's ability to drive safely;
• Use the Car for any purpose that could properly be charged as a crime, such as the illegal transportation of persons, drugs or contraband or any act of terrorism;
• Use the Car to tow or push anything, in a speed test, speed contest, race, rally, or in driver training activity;
• Use the Car to carry persons or property for hire (for a charge or fee), unless specifically authorized in writing by the Company.

Any use of the Car in a manner prohibited in this paragraph 5: (i) to the extent permitted by applicable law, will cause You to lose the benefit of any limitation on Your liability for loss of or damage to the Car, even if You have accepted LDW; (ii) to the extent permitted by applicable law, void personal accident insurance (""PAI"") and personal effects coverage (""PEC""), liability insurance supplement (""LIS"") coverage, emergency sickness protection (""ESP"") and any liability protection provided by the Company under this agreement; and (iii) will constitute a breach of this agreement, making You responsible, to the fullest extent permitted by law, for the actual and consequential damages to the Company caused by the breach, together with the Company's related costs and attorneys' fees.

6. PAYMENT OF CHARGES

(a) You and any person, corporation or other entity to whom, with the Company's consent, You expressly direct the charges in any way incurred under this Agreement (""Charges"") to be billed, are jointly and severally responsible for payment of all charges. If You direct Charges to be billed to any person, corporation or other entity, You represent that You are authorized to do so. Charges not paid on time as required by this Agreement may be subject to a late payment fee. You may also be charged a fee for any check used for payment of Charges that is returned to the Company unpaid or for any credit, charge, debit or stored value/prepaid/gift Card charges which are not honored by the Card issuer.

(b) Payment for all Charges is due at the completion of the rental in cash or by a credit Card, charge Card, debit Card or other device acceptable to the Company. IF YOU PRESENT A CREDIT, CHARGE CARD OR DEBIT/CHECK CARD AT THE COMMENCEMENT OF THE RENTAL, YOU AUTHORIZE THE COMPANY TO RESERVE CREDIT WITH, OR OBTAIN AN AUTHORIZATION FROM, THE CARD ISSUER AT THE TIME OF RENTAL, IN AN AMOUNT THAT MAY BE GREATER THAN THE ESTIMATED CHARGES. IF YOU USE A DEBIT/CHECK CARD TO QUALIFY FOR A RENTAL, THE COMPANY WILL NOT BE LIABLE FOR OVERDRAFT CHARGES, OR FOR ANY OTHER LOSSES OR LIABILITIES WHICH YOU MAY INCUR.

(c) The Company may from time to time issue prepaid vouchers, coupons represented either by documents or by entries in the Company's records (""Vouchers"") which may be used to pay rental charges subject to the terms and conditions of the Vouchers. Vouchers must be submitted at the time that the rental commences. Restrictions on the use of Vouchers may apply.

7. COMPUTATION OF CHARGES

(a) TIME CHARGES are computed at the rates specified on the Rental Record for days, weeks, months, extra hours and extra days. THE MINIMUM RENTAL CHARGE IS FOR ONE RENTAL DAY. RENTAL DAYS CONSIST OF CONSECUTIVE 24 HOUR PERIODS STARTING AT THE TIME THE RENTAL BEGINS. RENTAL RATE IS SUBJECT TO INCREASE IF YOU RETURN THE CAR MORE THAN 24 HOURS BEFORE OR 24 HOURS AFTER THE SCHEDULED RETURN TIME. LATE RETURNS BEYOND 29 MINUTE GRACE PERIOD SUBJECT TO EXTRA HOUR AND/OR EXTRA DAY CHARGES.

(b) MILEAGE CHARGES, including those for extra miles, if any, are based on the per mile rate specified on the Rental Record. The number of miles driven is determined by subtracting the Car's odometer reading at the beginning of the rental from the reading when the Car is returned, excluding tenths of miles.

(c) A SERVICE CHARGE may be applied if You return the Car to any location other than the location from which it is rented.

(d) LDW, PERS, PAI/PEC, ESP and LIS CHARGES, if applicable, are due and payable in full for each full or partial rental day, at the rates specified on the Rental Record.

(e) TAXES, TAX REIMBURSEMENTS, VEHICLE LICENSING FEES, AIRPORT AND/OR HOTEL RELATED FEES AND FEE RECOVERIES, GOVERNMENTAL OR OTHER SURCHARGES AND SIMILAR FEES are charged/recovered at the rates specified on the Rental Record or as otherwise required by applicable law.

(f) TOLL, PARKING & TRAFFIC OCCURRENCES/VIOLATIONS: YOU WILL BE RESPONSIBLE FOR AND PAY ALL TOLL OCCURRENCES, ALL PARKING, TRAFFIC AND TOLL VIOLATIONS, OTHER EXPENSES AND PENALTIES, ALL TOWING, STORAGE AND IMPOUND FEES AND ALL TICKETS CHARGED TO THE CAR ARISING OUT OF THE USE, POSSESSION OR OPERATION OF THE CAR BY YOU OR BY AN AUTHORIZED OPERATOR. You agree to pay, upon billing, applicable service fees (typically $30) and other fees related to such toll occurrences or toll, parking or traffic violations. For rentals throughout the U.S., including Hawaii: The amount of the service fee which You will be charged if the Company or the Toll Payment Processor is required to pay for such an infraction or toll occurrence is up to $42.00 per toll occurrence or citation.

(g) RECOVERY EXPENSE consists of all costs of any kind incurred by the Company in recovering the Car either under this Agreement, or if it is seized by governmental authorities as a result of its use by You, any Authorized Operator or any other operator with Your permission, including, but not limited to, all attorneys' fees and court costs.

(h) COLLECTION EXPENSE consists of all costs of any kind incurred by the Company in collecting Charges from You or the person to whom they are billed, including, but not limited to, all attorneys' fees and court costs.

(i) LATE PAYMENT FEES may be applied to any balance due for Charges that are not paid within 30 days of the Company's mailing an invoice for such Charges to You or the person to whom they are to be billed.

(j) FINES AND OTHER EXPENSES include, but are not limited to, fines, penalties, attorneys' fees and court costs assessed against or paid by the Company resulting from the use of the Car by You, any Authorized Operator or any other operator with Your permission.

(k) CHARGES FOR ADDITIONAL SERVICES, such as In Car Navigation System, alternative GPS or other navigation systems, and infant and toddler Car seats, if applicable, will be charged at the rates specified on the Rental Record.

(l) RETURN CHANGE FEE: A one-time Return Change Fee will be applied if You desire to extend Your rental or return the Car to a different location and You do not notify the Company at least 12 hours prior to Your scheduled return date/time. Failure to notify the Company of any change in Your scheduled return date/time or location will result in a one-time fee plus the cost of the rental based on the actual day and location of return.

(m) LOST KEYS/KEY FOBS/LOCKOUTS: If You lose the keys/key fob to the Car, the Company may charge You for the cost of replacing the keys or key fob and for the cost of delivering replacement keys/key fob (if possible) or towing the Car to the nearest Company location.

(n) LOST/BROKEN GPS UNITS, CAR SEATS, ETC.: If GPS units, Car Seats, or any other separately provided product is lost, stolen, or broken while on rent, You must notify the Company, and You will be responsible for replacement, delivery, and service costs.

(o) SMOKING FEE: In the event it is determined by Company personnel that You smoked in the Car (based on odor, test strips, or other mechanisms) or the Car smells of cigarette, marijuana, or other smoke, You will be charged a fee.

8. REFUELING OPTIONS

Most rentals come with a full tank of gas, but that is not always the case. There are three refueling options:

(1) IF YOU DO NOT PURCHASE FUEL FROM THE COMPANY AT THE BEGINNING OF YOUR RENTAL AND YOU RETURN THE CAR WITH AT LEAST AS MUCH FUEL AS WAS IN IT WHEN YOU RECEIVED IT, You will not pay the Company a charge for fuel.

(2) IF YOU DO NOT PURCHASE FUEL FROM THE COMPANY AT THE BEGINNING OF YOUR RENTAL AND YOU RETURN THE CAR WITH LESS FUEL THAN WAS IN IT WHEN YOU RECEIVED IT, the Company will charge You a Fuel and Service Charge at the applicable per-mile/kilometer or per-gallon rate specified on the Rental Record.

(3) IF YOU CHOOSE TO PURCHASE FUEL FROM THE COMPANY AT THE BEGINNING OF YOUR RENTAL BY SELECTING THE FUEL PURCHASE OPTION, You will be charged as shown on the Rental Record for that purchase. IF YOU CHOOSE THIS OPTION, YOU WILL NOT INCUR AN ADDITIONAL FUEL AND SERVICE CHARGE, BUT YOU WILL NOT RECEIVE ANY CREDIT FOR FUEL LEFT IN THE TANK AT THE TIME OF RETURN.

THE PER GALLON COST OF THE FUEL PURCHASE OPTION WILL ALWAYS BE LOWER THAN THE FUEL AND SERVICE CHARGE. BUT IF YOU ELECT THE FUEL PURCHASE OPTION YOU WILL NOT RECEIVE CREDIT FOR FUEL LEFT IN THE TANK AT THE TIME OF RETURN. THE COST OF REFUELING THE CAR YOURSELF AT A LOCAL SERVICE STATION WILL GENERALLY BE LOWER THAN THE FUEL AND SERVICE CHARGE OR THE FUEL PURCHASE OPTION.

9. ARBITRATION AND CLASS ACTION WAIVER

THIS AGREEMENT REQUIRES ARBITRATION OR A SMALL CLAIMS COURT CASE ON AN INDIVIDUAL BASIS, RATHER THAN JURY TRIALS OR CLASS ACTIONS. BY ENTERING INTO THIS AGREEMENT, YOU AGREE TO THIS ARBITRATION PROVISION. Except for claims for property damage, personal injury or death, ANY DISPUTES BETWEEN YOU AND US MUST BE RESOLVED ONLY BY ARBITRATION OR IN A SMALL CLAIMS COURT ON AN INDIVIDUAL BASIS; CLASS ARBITRATIONS AND CLASS ACTIONS ARE NOT ALLOWED. YOU AND WE EACH WAIVE THE RIGHT TO A TRIAL BY JURY OR TO PARTICIPATE IN A CLASS ACTION. The arbitration will take place in the county of Your billing address unless agreed otherwise. The American Arbitration Association (""AAA"") will administer any arbitration pursuant to its Consumer Arbitration Rules.

10. RESPONSIBILITY FOR PROPERTY

You agree that the Company is not responsible to You, any Authorized Operators or anyone else for any loss of or damage to Your or their personal property caused by Your or their acts or omissions, those of any third party or, to the extent permitted by law, by the Company's negligence. You and any Authorized Operators hereby waive any claim against the Company, its agents or employees, for loss of or damage to Your or anyone else's personal property, which includes, without limitation, property left in any Company vehicle or brought on the Company's premises. You and any Authorized Operators agree to indemnify and hold the Company harmless from any claim against the Company for loss of or damage to personal property that is connected with any rental under this agreement.

11. LIABILITY PROTECTION

(a) Within the limits stated in this subparagraph, the Company will indemnify, hold harmless, and defend You and any other Authorized Operators from and against liability to third parties for bodily injury (including death) and property damage, if the accident results from the use of the Car as permitted by this Agreement. The limits of this protection, including owner's liability, are the same as the minimum limits required by the automobile financial responsibility law of the jurisdiction in which the accident occurs. This protection will conform to the basic requirements of any applicable mandatory ""no fault"" law but does not include ""uninsured motorist,"" ""underinsured motorist,"" ""supplementary no fault"" or any other optional coverage.

(b) IF YOU DO NOT PURCHASE LIABILITY INSURANCE SUPPLEMENT (LIS) AT THE COMMENCEMENT OF THE RENTAL AND AN ACCIDENT RESULTS FROM THE USE OF THE CAR, YOUR INSURANCE AND THE INSURANCE OF THE OPERATOR OF THE CAR WILL BE PRIMARY. WHERE PERMITTED BY LAW, THE COMPANY DOES NOT PROVIDE ANY THIRD-PARTY LIABILITY PROTECTION COVERING THIS RENTAL. YOU AGREE THAT YOU AND YOUR INSURANCE COMPANY WILL BE RESPONSIBLE FOR HANDLING, DEFENDING AND PAYING ALL THIRD-PARTY CLAIMS.

FOR RENTALS COMMENCING IN FLORIDA: Florida law requires the Company's liability protection and personal injury protection to be primary unless otherwise stated. Therefore, the Company hereby informs You that the valid and collectible liability insurance and personal injury protection insurance of any authorized rental or leasing driver is primary for the limits of liability and personal injury protection coverage required by Florida Statutes.

(c) YOU AND ALL OPERATORS WILL INDEMNIFY AND HOLD THE COMPANY, ITS AGENTS, EMPLOYEES AND AFFILIATES HARMLESS FROM AND AGAINST ANY AND ALL LOSS, LIABILITY, CLAIM, DEMAND, CAUSE OF ACTION, ATTORNEYS' FEES AND EXPENSE OF ANY KIND ARISING FROM THE USE OR POSSESSION OF THE CAR BY YOU OR ANY OTHER OPERATOR(S), UNLESS SUCH LOSS ARISES OUT OF THE COMPANY'S SOLE NEGLIGENCE.

12. ACCIDENTS, THEFT AND VANDALISM

You must promptly and properly report any accident, theft or vandalism involving the Car to the Company and to the police in the jurisdiction in which such incident takes place. You should obtain details of witnesses and other vehicles involved and their drivers, owners and relevant insurances wherever possible. If You or any Authorized Operator receive any papers relating to such an incident, those papers must be promptly given to the Company. You and any Authorized Operators must cooperate fully with the Company's investigation of such incident and defense of any resulting claim. FAILURE TO COOPERATE FULLY MAY VOID ALL LIABILITY PROTECTION, PAI/PEC, LIS, AND LDW. You and any Authorized Operators authorize the Company to obtain any records or information relating to any incident, consent to the jurisdiction of the courts of the jurisdiction in which the incident occurs and waive any right to object to such jurisdiction.

13. LIMITS ON LIABILITY

The Company will not be liable to You or any Authorized Operators for any indirect, special or consequential damages (including lost profits) arising in any way out of any matter covered by this Agreement.

14. PRIVACY

The Company may collect and use personal data about You in accordance with the Company's Privacy Policy. Pursuant to the Privacy Policy, You have the option to limit use or sharing by the Company of personal data about You for marketing purposes and You may access and correct data about You. The Privacy Policy explains these options and provides information about how to choose an option.

15. WAIVER OF CHANGE OF TERMS/GOVERNING LAW

(a) No term of this Agreement may be waived or changed except by a writing signed by an expressly authorized representative of the Company. Rental representatives are not authorized to waive or change any term of this Agreement.

(b) This Agreement is governed by the substantive law of the jurisdiction in which the rental commences, without giving effect to the choice of law rules thereof, and You irrevocably and unconditionally consent and submit to the nonexclusive jurisdiction of the courts located in that jurisdiction.

(c) If any provision of this Agreement conflicts with any applicable law or regulation in any jurisdiction, then that provision shall be deemed to be modified as to the jurisdiction to be consistent with such law or regulation, or to be deleted if modification is impossible, and shall not affect the remainder of this Agreement, which shall continue in full force and effect.

16. PAYMENTS TO INTERMEDIARIES

If You arranged for this rental through a travel agent, internet travel site, broker or other intermediary acting on Your behalf, the Company or an affiliate of the Company's licensor may have paid commissions or other payments to that party to compensate it for arranging such rentals. That compensation may be based in part on the overall volume of business that party books with the Company or its affiliates and licensees. For details on such compensation, You should contact that party.

17. MIAMI-DADE COUNTY WAIVER

Unless waived, a renter in Miami-Dade County must be furnished a county approved visitor information map. These maps are generally furnished at all Company locations in Miami-Dade County. Each renter must either acknowledge receipt of the map at the commencement of each rental or waive his or her right to receive the map. By renting a Car under this Agreement, You waive Your right to receive such a map.

18. RECOVERY OF COSTS

Except if prohibited by applicable law or arbitration rule, in any arbitration or other legal proceeding between You and us, the prevailing party shall be entitled to receive from the other party the prevailing party's costs and expenses incurred in such arbitration or legal proceeding, including reasonable attorneys' fees, arbitration or court costs, and arbitrator's fees.";
    }

    private static string GetFullTermsTextSpanish()
    {
        return @"3. COMUNICACIONES ELECTRÓNICAS Y TELEMÁTICA

(a) Usted acepta que la Compañía puede usar medios electrónicos o verbales para contactarlo. La Compañía puede usar cualquier dirección de correo electrónico o número de teléfono que proporcione para contactarlo. El Carro puede estar equipado con tecnología de posicionamiento global u otros sistemas telemáticos.

(b) El Carro puede tener telemática, rastreo y servicios relacionados, en cuyo caso, Usted entiende que su acceso y uso están sujetos a los términos del proveedor del servicio.

4. SU RESPONSABILIDAD POR PÉRDIDA O DAÑO AL CARRO

(a) Excepto como se indica a continuación, Usted es responsable de toda pérdida o daño al Carro resultante de cualquier causa, incluyendo pero no limitado a colisión, volcadura, robo, vandalismo, incautación, incendio, inundación, granizo u otros actos de la naturaleza.

(b) Su responsabilidad no excederá el mayor valor entre el valor de mercado del Carro y su valor de programa de recompra del fabricante.

5. USO PROHIBIDO DEL CARRO

Ni Usted ni ningún Operador Autorizado puede:
• Permitir el uso del Carro por cualquier persona que no sea Usted o un Operador Autorizado
• Destruir intencionalmente, dañar o ayudar en el robo del Carro
• Llevar o intentar llevar el Carro a México o fuera de Estados Unidos o Canadá
• Usar el Carro bajo la influencia de alcohol o drogas
• Usar el Carro para cualquier propósito ilegal

6. PAGO DE CARGOS

(a) Usted es responsable del pago de todos los cargos.

(b) El pago es debido al completar el alquiler. SI PRESENTA UNA TARJETA DE CRÉDITO O DÉBITO, AUTORIZA A LA COMPAÑÍA A RESERVAR CRÉDITO EN UNA CANTIDAD QUE PUEDE SER MAYOR QUE LOS CARGOS ESTIMADOS.

7. CÁLCULO DE CARGOS

(a) LOS CARGOS DE TIEMPO se calculan a las tarifas especificadas. EL CARGO MÍNIMO ES POR UN DÍA DE ALQUILER.

(f) PEAJES, ESTACIONAMIENTO E INFRACCIONES DE TRÁFICO: USTED SERÁ RESPONSABLE DE TODOS LOS PEAJES, INFRACCIONES Y MULTAS.

8. OPCIONES DE REABASTECIMIENTO

La mayoría de los alquileres vienen con tanque lleno. Si devuelve el Carro con menos combustible, se le cobrará un Cargo de Combustible y Servicio.

9. ARBITRAJE Y RENUNCIA A ACCIÓN COLECTIVA

ESTE ACUERDO REQUIERE ARBITRAJE EN BASE INDIVIDUAL. AL FIRMAR ESTE ACUERDO, USTED ACEPTA ESTA DISPOSICIÓN DE ARBITRAJE.

10. RESPONSABILIDAD POR PROPIEDAD

Usted acepta que la Compañía no es responsable de ninguna pérdida o daño a su propiedad personal.

11. PROTECCIÓN DE RESPONSABILIDAD

La Compañía lo indemnizará de responsabilidad a terceros por lesiones corporales y daños a la propiedad si el accidente resulta del uso del Carro según lo permitido por este Acuerdo.

12. ACCIDENTES, ROBO Y VANDALISMO

Debe reportar inmediatamente cualquier accidente, robo o vandalismo a la Compañía y a la policía. LA FALTA DE COOPERACIÓN PUEDE ANULAR TODA PROTECCIÓN.

13. LÍMITES DE RESPONSABILIDAD

La Compañía no será responsable de daños indirectos, especiales o consecuentes.

14. PRIVACIDAD

La Compañía puede recopilar y usar datos personales de acuerdo con su Política de Privacidad.

15. LEY APLICABLE

Este Acuerdo se rige por la ley del lugar donde comienza el alquiler.";
    }

    /// <summary>
    /// Get default terms texts by language (legacy - kept for backward compatibility)
    /// </summary>
    private static (string Terms, string NonRefundable, string DamagePolicy, string CardAuthorization) GetDefaultTermsTexts(string language)
    {
        // This method is kept for backward compatibility but is no longer used for preview
        return ("", "", "", "");
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

    /// <summary>
    /// Refund a payment for a booking
    /// </summary>
    /// <param name="id">Booking ID</param>
    /// <param name="refundRequest">Refund request details</param>
    /// <returns>Refund result</returns>
    [HttpPost("{id}/refund")]
    [Authorize]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> RefundPayment(Guid id, [FromBody] RefundPaymentRequest refundRequest)
    {
        try
        {
            var booking = await _context.Bookings
                .Include(b => b.Payments)
                .Include(b => b.Customer)
                .Include(b => b.Company)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (booking == null)
                return NotFound("Booking not found");

            // Find the successful payment for this booking
            var payment = booking.Payments
                .FirstOrDefault(p => p.Status == "succeeded" && !string.IsNullOrEmpty(p.StripePaymentIntentId));

            if (payment == null)
                return BadRequest("No successful payment found for this booking");

            if (string.IsNullOrEmpty(payment.StripePaymentIntentId))
                return BadRequest("No Stripe Payment Intent ID found");

            // Check if already refunded
            if (payment.Status == "refunded")
                return BadRequest("Payment has already been refunded");

            // Validate refund amount
            if (refundRequest.Amount <= 0)
            {
                _logger.LogWarning(
                    "[Refund] Invalid refund amount {Amount} for booking {BookingId}",
                    refundRequest.Amount,
                    id
                );
                return BadRequest($"Refund amount must be greater than zero. Received: {refundRequest.Amount}");
            }

            if (refundRequest.Amount > payment.Amount)
            {
                _logger.LogWarning(
                    "[Refund] Refund amount {RefundAmount} exceeds payment amount {PaymentAmount} for booking {BookingId}",
                    refundRequest.Amount,
                    payment.Amount,
                    id
                );
                return BadRequest($"Refund amount ({refundRequest.Amount:F2}) cannot exceed the payment amount ({payment.Amount:F2})");
            }

            // Get Stripe API key from stripe_settings table using company.StripeSettingsId
            var stripeSecretKey = await GetStripeSecretKeyAsync(booking.CompanyId);
            if (string.IsNullOrEmpty(stripeSecretKey))
            {
                _logger.LogError("RefundPayment: Stripe secret key not configured for company {CompanyId}", booking.CompanyId);
                return StatusCode(500, new { error = "Stripe not configured for this company" });
            }

            // Get Stripe connected account ID from stripe_company table (REQUIRED)
            var stripeAccountId = await GetStripeAccountIdAsync(booking.CompanyId);
            if (string.IsNullOrEmpty(stripeAccountId))
            {
                _logger.LogError(
                    "RefundPayment: stripe_company record not found or StripeAccountId is missing for company {CompanyId}. " +
                    "Stripe operations are PROHIBITED.", 
                    booking.CompanyId
                );
                return StatusCode(500, new { 
                    error = "Stripe account not configured for this company",
                    message = "Stripe account ID must exist in stripe_company table. Please configure Stripe account first."
                });
            }

            // Process refund through Stripe
            try
            {
                var refundAmount = refundRequest.Amount;

                _logger.LogInformation(
                    "[Refund] Processing refund for booking {BookingId}: RequestedAmount={RequestedAmount}, PaymentAmount={PaymentAmount}, BookingTotal={BookingTotal}, ConnectedAccount={StripeAccountId}",
                    id,
                    refundAmount,
                    payment.Amount,
                    booking.TotalAmount,
                    stripeAccountId
                );

                var refund = await _stripeService.CreateRefundAsync(
                    payment.StripePaymentIntentId,
                    refundAmount,
                    booking.CompanyId
                );

                if (refund != null && refund.Status == "succeeded")
                {
                    // Use the actual refunded amount from Stripe (in cents, so divide by 100)
                    var actualRefundedAmount = refund.Amount / 100m;
                    
                    _logger.LogInformation(
                        "[Refund] Stripe refund successful. Requested: {RequestedAmount}, Actual refunded by Stripe: {ActualAmount} {Currency}",
                        refundAmount,
                        actualRefundedAmount,
                        refund.Currency?.ToUpper() ?? "UNKNOWN"
                    );

                    // Update payment status
                    payment.Status = "refunded";
                    payment.RefundAmount = actualRefundedAmount; // Use actual amount from Stripe
                    payment.RefundDate = DateTime.UtcNow;
                    payment.UpdatedAt = DateTime.UtcNow;

                    // Update booking status
                    booking.Status = "Cancelled";
                    booking.UpdatedAt = DateTime.UtcNow;

                    // Get current user ID from claims
                    var userIdClaim = User.FindFirst("nameid") ?? User.FindFirst("sub") ?? User.FindFirst("customer_id");
                    Guid? processedBy = userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId) ? userId : null;

                    // Create refund record with actual amount from Stripe
                    var refundRecord = new RefundRecord
                    {
                        BookingId = booking.Id,
                        StripeRefundId = refund.Id,
                        Amount = actualRefundedAmount, // Use actual amount from Stripe, not requested amount
                        RefundType = actualRefundedAmount >= booking.TotalAmount ? "full" : "partial",
                        Reason = refundRequest.Reason ?? "Booking cancellation",
                        Status = refund.Status,
                        ProcessedBy = processedBy,
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.RefundRecords.Add(refundRecord);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation(
                        "[Refund] Refunded {ActualAmount} {Currency} for booking {BookingId} (Payment Intent: {PaymentIntentId}, Refund ID: {RefundId}, Processed by: {ProcessedBy})",
                        actualRefundedAmount,
                        refund.Currency?.ToUpper() ?? "UNKNOWN",
                        id,
                        payment.StripePaymentIntentId,
                        refund.Id,
                        processedBy?.ToString() ?? "System"
                    );

                    return Ok(new
                    {
                        success = true,
                        refundId = refund.Id,
                        refundRecordId = refundRecord.Id,
                        amount = refundAmount,
                        currency = refund.Currency,
                        status = refund.Status,
                        message = "Refund processed successfully"
                    });
                }
                else
                {
                    _logger.LogWarning(
                        "Refund failed for booking {BookingId}: {Status}",
                        id,
                        refund?.Status ?? "unknown"
                    );
                    return BadRequest("Refund failed: " + (refund?.Status ?? "unknown error"));
                }
            }
            catch (Stripe.StripeException stripeEx)
            {
                _logger.LogError(stripeEx, "Stripe error processing refund for booking {BookingId}", id);
                return BadRequest($"Stripe error: {stripeEx.Message}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing refund for booking {BookingId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    #endregion

    #region Stripe Payment Sync

    /// <summary>
    /// Sync payment information from Stripe for a specific booking
    /// </summary>
    [HttpPost("{id}/sync-payment")]
    public async Task<IActionResult> SyncPaymentFromStripe(Guid id)
    {
        _logger.LogInformation("=== SyncPaymentFromStripe called for booking {BookingId} ===", id);
        
        try
        {
            _logger.LogInformation("Fetching booking from database...");
            var booking = await _context.Bookings
                .Include(b => b.Payments)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (booking == null)
            {
                _logger.LogWarning("Booking {BookingId} not found", id);
                return NotFound("Booking not found");
            }

            _logger.LogInformation("Booking found: {BookingNumber}, Current PaymentIntentId: {PaymentIntentId}", 
                booking.BookingNumber, booking.StripePaymentIntentId ?? "null");

            // Configure Stripe API key from database
            _logger.LogInformation("Retrieving Stripe API key from database...");
            var stripeSecretKey = await _settingsService.GetValueAsync("stripe.secretKey");
            if (string.IsNullOrEmpty(stripeSecretKey))
            {
                _logger.LogError("Stripe secret key not configured in database settings");
                return StatusCode(500, new { error = "Stripe configuration missing in database" });
            }
            _logger.LogInformation("Stripe API key retrieved from database (length: {Length})", stripeSecretKey.Length);
            Stripe.StripeConfiguration.ApiKey = stripeSecretKey;

            var paymentIntentService = new Stripe.PaymentIntentService();
            Stripe.PaymentIntent? paymentIntent = null;
            DateTime startTime;
            double duration;

            // If booking has no payment intent ID, search Stripe by booking ID in metadata
            if (string.IsNullOrEmpty(booking.StripePaymentIntentId))
            {
                _logger.LogInformation("No payment intent ID for booking {BookingId}. Searching Stripe by booking ID: {BookingIdStr}", 
                    booking.Id, booking.Id.ToString());

                // Search Stripe for payment intents with this booking ID in metadata
                var searchOptions = new Stripe.PaymentIntentSearchOptions
                {
                    Query = $"metadata['booking_id']:'{booking.Id}'",
                    Expand = new List<string> { "data.latest_charge" }
                };

                startTime = DateTime.UtcNow;
                var searchResults = await paymentIntentService.SearchAsync(searchOptions);
                duration = (DateTime.UtcNow - startTime).TotalMilliseconds;

                _logger.LogInformation("Stripe search completed in {Duration}ms. Found {Count} payment intents", 
                    duration, searchResults.Data.Count);

                if (searchResults.Data.Count == 0)
                {
                    _logger.LogInformation("No Stripe payment found for booking {BookingId}", booking.Id);
                    return BadRequest("No Stripe payment found for this booking");
                }

                // Get the first (most recent) payment intent
                paymentIntent = searchResults.Data.FirstOrDefault();
                
                if (paymentIntent != null)
                {
                    _logger.LogInformation("Found payment intent {PaymentIntentId} for booking {BookingId}", 
                        paymentIntent.Id, booking.Id);
                    
                    // Update booking with the found payment intent ID
                    booking.StripePaymentIntentId = paymentIntent.Id;
                }
            }
            else
            {
                // Fetch payment intent from Stripe with expanded charge data
                _logger.LogInformation("Calling Stripe API to fetch PaymentIntent: {PaymentIntentId} for booking {BookingId}", 
                    booking.StripePaymentIntentId, booking.Id);
                
                var options = new Stripe.PaymentIntentGetOptions
                {
                    Expand = new List<string> { "latest_charge" }
                };
                
                startTime = DateTime.UtcNow;
                paymentIntent = await paymentIntentService.GetAsync(booking.StripePaymentIntentId, options);
                duration = (DateTime.UtcNow - startTime).TotalMilliseconds;
            }
            
            _logger.LogInformation("Stripe API call completed in {Duration}ms. Status: {Status}, Amount: {Amount}", 
                duration, paymentIntent?.Status, paymentIntent?.Amount);

            if (paymentIntent == null)
            {
                _logger.LogWarning("PaymentIntent {PaymentIntentId} not found in Stripe", 
                    booking.StripePaymentIntentId);
                return NotFound("Payment intent not found in Stripe");
            }

            // Find or create payment record
            var payment = await _context.Payments
                .FirstOrDefaultAsync(p => p.StripePaymentIntentId == booking.StripePaymentIntentId);

            if (payment == null)
            {
                // Create new payment record
                payment = new Payment
                {
                    ReservationId = booking.Id,
                    CustomerId = booking.CustomerId,
                    CompanyId = booking.CompanyId,
                    Amount = paymentIntent.Amount / 100m, // Convert from cents
                    Currency = paymentIntent.Currency.ToUpper(),
                    PaymentType = "online",
                    PaymentMethod = paymentIntent.PaymentMethod?.ToString() ?? "card",
                    StripePaymentIntentId = paymentIntent.Id,
                    Status = paymentIntent.Status,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Payments.Add(payment);
            }
            else
            {
                // Update existing payment record
                payment.Status = paymentIntent.Status;
                payment.Amount = paymentIntent.Amount / 100m;
                payment.UpdatedAt = DateTime.UtcNow;
            }

            // Update charge ID if available
            if (!string.IsNullOrEmpty(paymentIntent.LatestChargeId))
            {
                payment.StripeChargeId = paymentIntent.LatestChargeId;
            }

            // Set processed time if succeeded
            if (paymentIntent.Status == "succeeded" && payment.ProcessedAt == null)
            {
                payment.ProcessedAt = DateTime.UtcNow;
            }

            // Check for refunds using the latest charge
            if (paymentIntent.LatestCharge != null)
            {
                var charge = paymentIntent.LatestCharge;
                _logger.LogInformation("Checking refund status for charge {ChargeId}: AmountRefunded={AmountRefunded}, Refunded={Refunded}", 
                    charge.Id, charge.AmountRefunded, charge.Refunded);
                
                if (charge.AmountRefunded > 0)
                {
                    payment.RefundAmount = charge.AmountRefunded / 100m;
                    
                    // Fetch refund details if needed
                    if (charge.Refunds?.Data != null && charge.Refunds.Data.Any())
                    {
                        payment.RefundDate = charge.Refunds.Data.First().Created;
                        _logger.LogInformation("Refund detected: Amount={RefundAmount}, Date={RefundDate}", 
                            payment.RefundAmount, payment.RefundDate);
                    }
                    
                    if (charge.Refunded)
                    {
                        payment.Status = "refunded";
                    }
                }
            }

            // Update booking status if payment succeeded and booking is still Pending
            if (paymentIntent.Status == "succeeded" && booking.Status == "Pending")
            {
                _logger.LogInformation("Payment succeeded - updating booking {BookingId} status from Pending to Confirmed", 
                    booking.Id);
                booking.Status = "Confirmed";
            }

            // Sync security deposit information
            string? securityDepositStatus = null;
            Stripe.PaymentIntent? secDepositIntent = null;
            
            // If booking already has a security deposit payment intent ID, fetch it
            if (!string.IsNullOrEmpty(booking.SecurityDepositPaymentIntentId))
            {
                _logger.LogInformation("Syncing security deposit info for booking {BookingId}, PaymentIntent: {PaymentIntentId}", 
                    booking.Id, booking.SecurityDepositPaymentIntentId);
                
                try
                {
                    secDepositIntent = await paymentIntentService.GetAsync(booking.SecurityDepositPaymentIntentId);
                }
                catch (Stripe.StripeException ex)
                {
                    _logger.LogWarning(ex, "Could not fetch security deposit PaymentIntent {PaymentIntentId} for booking {BookingId}", 
                        booking.SecurityDepositPaymentIntentId, booking.Id);
                }
            }
            // If no security deposit payment intent ID stored, search Stripe for it
            else
            {
                _logger.LogInformation("No security deposit payment intent ID stored for booking {BookingId}. Searching Stripe...", 
                    booking.Id);
                
                try
                {
                    // Search for security deposit payment intents with this booking ID and type in metadata
                    var secDepositSearchOptions = new Stripe.PaymentIntentSearchOptions
                    {
                        Query = $"metadata['booking_id']:'{booking.Id}' AND metadata['type']:'security_deposit'",
                    };
                    
                    var secDepositSearchResults = await paymentIntentService.SearchAsync(secDepositSearchOptions);
                    
                    if (secDepositSearchResults.Data.Count > 0)
                    {
                        secDepositIntent = secDepositSearchResults.Data.First();
                        _logger.LogInformation("Found security deposit payment intent {PaymentIntentId} for booking {BookingId}", 
                            secDepositIntent.Id, booking.Id);
                        
                        // Store the found payment intent ID
                        booking.SecurityDepositPaymentIntentId = secDepositIntent.Id;
                    }
                    else
                    {
                        _logger.LogInformation("No security deposit payment intent found in Stripe for booking {BookingId}", 
                            booking.Id);
                    }
                }
                catch (Stripe.StripeException ex)
                {
                    _logger.LogWarning(ex, "Could not search for security deposit payment intent for booking {BookingId}", 
                        booking.Id);
                }
            }
            
            // If we found/have a security deposit payment intent, update the booking
            if (secDepositIntent != null)
            {
                _logger.LogInformation("Security deposit intent status: {Status}, CaptureMethod: {CaptureMethod}", 
                    secDepositIntent.Status, secDepositIntent.CaptureMethod);
                
                // Update security deposit status based on payment intent status
                if (secDepositIntent.Status == "requires_capture")
                {
                    booking.SecurityDepositStatus = "authorized";
                    if (booking.SecurityDepositAuthorizedAt == null)
                    {
                        booking.SecurityDepositAuthorizedAt = DateTime.UtcNow;
                    }
                }
                else if (secDepositIntent.Status == "succeeded")
                {
                    booking.SecurityDepositStatus = "captured";
                    booking.SecurityDepositChargedAmount = secDepositIntent.Amount / 100m;
                    if (booking.SecurityDepositCapturedAt == null)
                    {
                        booking.SecurityDepositCapturedAt = DateTime.UtcNow;
                    }
                }
                else if (secDepositIntent.Status == "canceled")
                {
                    booking.SecurityDepositStatus = "released";
                    if (booking.SecurityDepositReleasedAt == null)
                    {
                        booking.SecurityDepositReleasedAt = DateTime.UtcNow;
                    }
                }
                
                securityDepositStatus = booking.SecurityDepositStatus;
                _logger.LogInformation("Updated security deposit status to {Status} for booking {BookingId}", 
                    securityDepositStatus, booking.Id);
            }

            _logger.LogInformation("Saving payment updates to database for booking {BookingId}: PaymentStatus={PaymentStatus}, BookingStatus={BookingStatus}, Amount={Amount}, RefundAmount={RefundAmount}, SecurityDepositStatus={SecurityDepositStatus}", 
                booking.Id, payment.Status, booking.Status, payment.Amount, payment.RefundAmount, securityDepositStatus);

            await _context.SaveChangesAsync();

            _logger.LogInformation("Successfully synced payment info from Stripe for booking {BookingId}, Payment Status: {PaymentStatus}, Booking Status: {BookingStatus}, Security Deposit Status: {SecurityDepositStatus}", 
                booking.Id, payment.Status, booking.Status, securityDepositStatus);

            return Ok(new 
            { 
                success = true, 
                status = payment.Status,
                bookingStatus = booking.Status,
                amount = payment.Amount,
                refundAmount = payment.RefundAmount,
                securityDepositStatus = securityDepositStatus
            });
        }
        catch (Stripe.StripeException ex)
        {
            _logger.LogError(ex, "Stripe API error syncing payment for booking {BookingId}: Code={Code}, Message={Message}, StackTrace={StackTrace}", 
                id, ex.StripeError?.Code, ex.Message, ex.StackTrace);
            return StatusCode(500, new { message = "Stripe error: " + ex.Message, error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error syncing payment from Stripe for booking {BookingId}: Type={Type}, Message={Message}, StackTrace={StackTrace}", 
                id, ex.GetType().Name, ex.Message, ex.StackTrace);
            return StatusCode(500, new { message = "Failed to sync payment: " + ex.Message, error = ex.Message, type = ex.GetType().Name });
        }
    }

    /// <summary>
    /// Sync payment information from Stripe for multiple bookings
    /// </summary>
    [HttpPost("sync-payments-bulk")]
    public async Task<IActionResult> SyncPaymentsFromStripeBulk([FromBody] List<Guid> bookingIds)
    {
        _logger.LogInformation("Starting bulk payment sync for {Count} bookings", bookingIds.Count);
        
        try
        {
            // Configure Stripe API key from new tables or settings fallback
            // Note: For bulk operations, we use the first booking's company ID or fallback to global settings
            var stripeSecretKey = await GetStripeSecretKeyAsync(null);
            if (string.IsNullOrEmpty(stripeSecretKey))
            {
                _logger.LogError("Stripe secret key not configured in database settings");
                return StatusCode(500, new { error = "Stripe configuration missing in database" });
            }
            Stripe.StripeConfiguration.ApiKey = stripeSecretKey;

            var results = new List<object>();
            var successCount = 0;
            var failureCount = 0;
            var startTime = DateTime.UtcNow;

            foreach (var bookingId in bookingIds)
            {
                _logger.LogInformation("Processing booking {BookingId} in bulk sync ({Current}/{Total})", 
                    bookingId, results.Count + 1, bookingIds.Count);
                
                try
                {
                    var booking = await _context.Bookings
                        .Include(b => b.Payments)
                        .FirstOrDefaultAsync(b => b.Id == bookingId);

                    if (booking == null || string.IsNullOrEmpty(booking.StripePaymentIntentId))
                    {
                        _logger.LogWarning("Booking {BookingId} has no payment intent, skipping", bookingId);
                        results.Add(new 
                        { 
                            bookingId, 
                            success = false, 
                            error = "No payment intent" 
                        });
                        failureCount++;
                        continue;
                    }

                    // Fetch payment intent from Stripe with expanded charge data
                    _logger.LogInformation("Calling Stripe API for booking {BookingId}, PaymentIntent: {PaymentIntentId}", 
                        bookingId, booking.StripePaymentIntentId);
                    
                    var paymentIntentService = new Stripe.PaymentIntentService();
                    var options = new Stripe.PaymentIntentGetOptions
                    {
                        Expand = new List<string> { "latest_charge" }
                    };
                    
                    var callStartTime = DateTime.UtcNow;
                    var paymentIntent = await paymentIntentService.GetAsync(booking.StripePaymentIntentId, options);
                    var callDuration = (DateTime.UtcNow - callStartTime).TotalMilliseconds;
                    
                    _logger.LogInformation("Stripe API call completed for booking {BookingId} in {Duration}ms", 
                        bookingId, callDuration);

                    if (paymentIntent == null)
                    {
                        results.Add(new 
                        { 
                            bookingId, 
                            success = false, 
                            error = "Payment not found in Stripe" 
                        });
                        failureCount++;
                        continue;
                    }

                    // Find or create payment record
                    var payment = await _context.Payments
                        .FirstOrDefaultAsync(p => p.StripePaymentIntentId == booking.StripePaymentIntentId);

                    if (payment == null)
                    {
                        payment = new Payment
                        {
                            ReservationId = booking.Id,
                            CustomerId = booking.CustomerId,
                            CompanyId = booking.CompanyId,
                            Amount = paymentIntent.Amount / 100m,
                            Currency = paymentIntent.Currency.ToUpper(),
                            PaymentType = "online",
                            PaymentMethod = paymentIntent.PaymentMethod?.ToString() ?? "card",
                            StripePaymentIntentId = paymentIntent.Id,
                            Status = paymentIntent.Status,
                            CreatedAt = DateTime.UtcNow
                        };
                        _context.Payments.Add(payment);
                    }
                    else
                    {
                        payment.Status = paymentIntent.Status;
                        payment.Amount = paymentIntent.Amount / 100m;
                        payment.UpdatedAt = DateTime.UtcNow;
                    }

                    if (!string.IsNullOrEmpty(paymentIntent.LatestChargeId))
                    {
                        payment.StripeChargeId = paymentIntent.LatestChargeId;
                    }

                    if (paymentIntent.Status == "succeeded" && payment.ProcessedAt == null)
                    {
                        payment.ProcessedAt = DateTime.UtcNow;
                    }

                    // Check for refunds using the latest charge
                    if (paymentIntent.LatestCharge != null)
                    {
                        var charge = paymentIntent.LatestCharge;
                        if (charge.AmountRefunded > 0)
                        {
                            payment.RefundAmount = charge.AmountRefunded / 100m;
                            
                            // Fetch refund details if needed
                            if (charge.Refunds?.Data != null && charge.Refunds.Data.Any())
                            {
                                payment.RefundDate = charge.Refunds.Data.First().Created;
                            }
                            
                            if (charge.Refunded)
                            {
                                payment.Status = "refunded";
                            }
                        }
                    }

                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Successfully synced booking {BookingId}: Status={Status}, Amount={Amount}", 
                        bookingId, payment.Status, payment.Amount);

                    results.Add(new 
                    { 
                        bookingId, 
                        success = true, 
                        status = payment.Status 
                    });
                    successCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error syncing payment for booking {BookingId}: {Message}", 
                        bookingId, ex.Message);
                    results.Add(new 
                    { 
                        bookingId, 
                        success = false, 
                        error = ex.Message 
                    });
                    failureCount++;
                }
            }

            var totalDuration = (DateTime.UtcNow - startTime).TotalSeconds;
            _logger.LogInformation("Bulk payment sync completed: Total={Total}, Success={Success}, Failed={Failed}, Duration={Duration}s", 
                bookingIds.Count, successCount, failureCount, totalDuration);

            return Ok(new 
            { 
                success = true,
                totalProcessed = bookingIds.Count,
                successCount,
                failureCount,
                results 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical error in bulk payment sync: {Message}", ex.Message);
            return StatusCode(500, new { error = "Failed to sync payments: " + ex.Message });
        }
    }

    /// <summary>
    /// Create a payment intent for security deposit (manual capture)
    /// </summary>
    /// <param name="id">Booking ID</param>
    /// <returns>Payment intent client secret</returns>
    [HttpPost("{id}/security-deposit-payment-intent")]
    [Authorize]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> CreateSecurityDepositPaymentIntent(Guid id)
    {
        try
        {
            var booking = await _context.Bookings
                .Include(b => b.Customer)
                .Include(b => b.Company)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (booking == null)
                return NotFound(new { error = "Booking not found" });

            // Get Stripe API key from stripe_settings table using company.StripeSettingsId
            var stripeSecretKey = await GetStripeSecretKeyAsync(booking.CompanyId);
            if (string.IsNullOrEmpty(stripeSecretKey))
            {
                _logger.LogError("Stripe secret key not configured for company {CompanyId}", booking.CompanyId);
                return StatusCode(500, new { error = "Stripe not configured for this company" });
            }

            // Get Stripe connected account ID from stripe_company table (REQUIRED)
            var stripeAccountId = await GetStripeAccountIdAsync(booking.CompanyId);
            if (string.IsNullOrEmpty(stripeAccountId))
            {
                _logger.LogError(
                    "CreateSecurityDepositPaymentIntent: stripe_company record not found or StripeAccountId is missing for company {CompanyId}. " +
                    "Stripe operations are PROHIBITED.", 
                    booking.CompanyId
                );
                return StatusCode(500, new { 
                    error = "Stripe account not configured for this company",
                    message = "Stripe account ID must exist in stripe_company table. Please configure Stripe account first."
                });
            }

            // Create RequestOptions with API key from stripe_settings and connected account ID
            var requestOptions = new Stripe.RequestOptions
            {
                ApiKey = stripeSecretKey,
                StripeAccount = stripeAccountId
            };

            // Determine security deposit amount
            decimal depositAmount = booking.SecurityDeposit > 0 
                ? booking.SecurityDeposit 
                : booking.Company.SecurityDeposit;

            if (depositAmount <= 0)
            {
                return BadRequest(new { error = "No security deposit amount configured" });
            }

            // Convert to cents for Stripe
            var amountInCents = (long)(depositAmount * 100);

            _logger.LogInformation(
                "Creating security deposit payment intent for booking {BookingId}, amount: ${Amount}, connected account: {StripeAccountId}", 
                booking.Id, 
                depositAmount,
                stripeAccountId
            );

            // Create payment intent with manual capture
            var paymentIntentService = new Stripe.PaymentIntentService();
            var options = new Stripe.PaymentIntentCreateOptions
            {
                Amount = amountInCents,
                Currency = (booking.Company.Currency ?? "USD").ToLower(),
                CaptureMethod = "manual", // Important: manual capture for security deposit
                Description = $"Security Deposit for Booking {booking.BookingNumber}",
                Metadata = new Dictionary<string, string>
                {
                    { "booking_id", booking.Id.ToString() },
                    { "booking_number", booking.BookingNumber },
                    { "customer_id", booking.CustomerId.ToString() },
                    { "customer_email", booking.Customer.Email },
                    { "type", "security_deposit" }
                }
            };

            // For connected accounts, don't use customer ID (customers are separate)
            // Stripe will handle customer creation/lookup automatically
            // For platform accounts, we could use customer ID, but to be safe, use email
            if (!string.IsNullOrEmpty(booking.Customer.Email))
            {
                // Note: PaymentIntent doesn't have CustomerEmail property, but we can use metadata
                // The customer will be created/linked during payment method attachment
            }

            var paymentIntent = await paymentIntentService.CreateAsync(options, requestOptions);

            // Store the payment intent ID with the booking
            booking.SecurityDepositPaymentIntentId = paymentIntent.Id;
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Created security deposit payment intent {PaymentIntentId} for booking {BookingId}", 
                paymentIntent.Id, 
                booking.Id
            );

            return Ok(new
            {
                clientSecret = paymentIntent.ClientSecret,
                paymentIntentId = paymentIntent.Id,
                amount = depositAmount,
                bookingId = booking.Id,
                bookingNumber = booking.BookingNumber
            });
        }
        catch (Stripe.StripeException ex)
        {
            _logger.LogError(ex, "Stripe error creating security deposit payment intent for booking {BookingId}", id);
            return StatusCode(500, new { error = "Stripe error: " + ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating security deposit payment intent for booking {BookingId}", id);
            return StatusCode(500, new { error = "Failed to create payment intent: " + ex.Message });
        }
    }

    /// <summary>
    /// Create a Stripe Checkout Session for security deposit (hosted payment page)
    /// POST: api/Booking/{id}/security-deposit-checkout
    /// </summary>
    [HttpPost("{id}/security-deposit-checkout")]
    public async Task<IActionResult> CreateSecurityDepositCheckout(Guid id, [FromQuery] string? language = null)
    {
        try
        {
            var booking = await _context.Bookings
                .Include(b => b.Customer)
                .Include(b => b.Company)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (booking == null)
                return NotFound(new { error = "Booking not found" });

            if (booking.Customer == null)
            {
                _logger.LogError("Customer not found for booking {BookingId}", id);
                return StatusCode(500, new { error = "Customer not found for this booking" });
            }

            if (string.IsNullOrEmpty(booking.Customer.Email))
            {
                _logger.LogError("Customer email is missing for booking {BookingId}, customer {CustomerId}", id, booking.Customer.Id);
                return StatusCode(500, new { error = "Customer email is missing" });
            }

            // Get Stripe API key from new tables or settings fallback
            var stripeSecretKey = await GetStripeSecretKeyAsync(booking.CompanyId);
            if (string.IsNullOrEmpty(stripeSecretKey))
            {
                _logger.LogError("Stripe secret key not configured");
                return StatusCode(500, new { error = "Stripe not configured" });
            }

            Stripe.StripeConfiguration.ApiKey = stripeSecretKey;

            // Determine security deposit amount
            decimal depositAmount = booking.SecurityDeposit > 0 
                ? booking.SecurityDeposit 
                : booking.Company.SecurityDeposit;

            if (depositAmount <= 0)
            {
                return BadRequest(new { error = "No security deposit amount configured" });
            }

            // Convert to cents for Stripe
            var amountInCents = (long)(depositAmount * 100);
            
            // Use booking's currency, fallback to company's currency, then USD
            var currency = (booking.Currency ?? booking.Company.Currency ?? "USD").ToLower();
            
            // Use user's current language (from request) or fall back to company's language
            var userLanguage = !string.IsNullOrEmpty(language) ? language : booking.Company.Language;
            var locale = GetStripeLocaleFromCountry(booking.Company.Country, userLanguage);
            var countryCode = GetCountryCode(booking.Company.Country);

            _logger.LogInformation(
                "Creating security deposit checkout session for booking {BookingId}, amount: {Amount} {Currency}, locale: {Locale} (User: {UserLanguage}, Company: {CompanyLanguage}), country: {Country}", 
                booking.Id, 
                depositAmount,
                currency,
                locale,
                language ?? "null",
                booking.Company.Language ?? "null",
                countryCode
            );

            // Get Stripe connected account ID for this company
            // REQUIRED: Must come from stripe_company table matching company.StripeSettingsId
            // If stripe_company record doesn't exist, Stripe operations are PROHIBITED
            var stripeAccountId = await GetStripeAccountIdAsync(booking.CompanyId);
            
            if (string.IsNullOrEmpty(stripeAccountId))
            {
                _logger.LogError(
                    "CreateSecurityDepositCheckout: stripe_company record not found or StripeAccountId is missing for company {CompanyId}. " +
                    "Stripe operations are PROHIBITED. Please ensure stripe_company record exists with matching CompanyId and SettingsId.", 
                    booking.CompanyId
                );
                return StatusCode(500, new { 
                    error = "Stripe account not configured for this company",
                    message = "Stripe account ID must exist in stripe_company table. Please configure Stripe account first."
                });
            }
            
            // Create RequestOptions with API key from stripe_settings table (via company.StripeSettingsId)
            // and connected account ID from stripe_company table (REQUIRED)
            var requestOptions = new Stripe.RequestOptions
            {
                ApiKey = stripeSecretKey, // Key from stripe_settings table using company.StripeSettingsId
                StripeAccount = stripeAccountId // Account ID from stripe_company table (REQUIRED)
            };
            
            _logger.LogInformation(
                "Creating security deposit checkout session for connected account {StripeAccountId} using Stripe keys from stripe_settings (CompanyId: {CompanyId})", 
                stripeAccountId, 
                booking.CompanyId
            );

            // Create Checkout Session
            var sessionService = new Stripe.Checkout.SessionService();
            var options = new Stripe.Checkout.SessionCreateOptions
            {
                PaymentMethodTypes = new List<string> { "card" },
                Mode = "payment",
                Locale = locale,
                BillingAddressCollection = "required",
                LineItems = new List<Stripe.Checkout.SessionLineItemOptions>
                {
                    new Stripe.Checkout.SessionLineItemOptions
                    {
                        PriceData = new Stripe.Checkout.SessionLineItemPriceDataOptions
                        {
                            Currency = currency,
                            UnitAmount = amountInCents,
                            ProductData = new Stripe.Checkout.SessionLineItemPriceDataProductDataOptions
                            {
                                Name = $"Security Deposit - Booking {booking.BookingNumber}",
                                Description = "Refundable security deposit (authorized, not charged)"
                            }
                        },
                        Quantity = 1
                    }
                },
                PaymentIntentData = new Stripe.Checkout.SessionPaymentIntentDataOptions
                {
                    CaptureMethod = "manual", // Manual capture for authorization only
                    Metadata = new Dictionary<string, string>
                    {
                        { "booking_id", booking.Id.ToString() },
                        { "booking_number", booking.BookingNumber },
                        { "customer_id", booking.CustomerId.ToString() },
                        { "customer_email", booking.Customer.Email },
                        { "type", "security_deposit" }
                    }
                },
                SuccessUrl = $"https://localhost:3000/admin-dashboard?tab=reservations&deposit_success=true&booking_id={booking.Id}",
                CancelUrl = $"https://localhost:3000/admin-dashboard?tab=reservations&deposit_cancelled=true&booking_id={booking.Id}",
                Metadata = new Dictionary<string, string>
                {
                    { "booking_id", booking.Id.ToString() },
                    { "booking_number", booking.BookingNumber },
                    { "type", "security_deposit" }
                },
                // Set customer - either Stripe customer ID or email
                Customer = !string.IsNullOrEmpty(booking.Customer.StripeCustomerId) ? booking.Customer.StripeCustomerId : null,
                CustomerEmail = string.IsNullOrEmpty(booking.Customer.StripeCustomerId) ? booking.Customer.Email : null,
                CustomerUpdate = !string.IsNullOrEmpty(booking.Customer.StripeCustomerId) 
                    ? new Stripe.Checkout.SessionCustomerUpdateOptions
                    {
                        Address = "auto",
                        Name = "auto"
                    }
                    : null
            };
            
            // When using a connected account, customers are separate from platform customers
            // The customer ID stored in our database might be from the platform account
            // For connected accounts, we should use email to let Stripe create/find the customer on the connected account
            // For platform accounts, we can try to use the customer ID if it exists
            
            string? validCustomerId = null;
            if (!string.IsNullOrEmpty(booking.Customer.StripeCustomerId) && string.IsNullOrEmpty(stripeAccountId))
            {
                // Only try to use customer ID on platform account (not connected account)
                // On connected accounts, customers are separate entities, so use email instead
                try
                {
                    var customerService = new Stripe.CustomerService();
                    var customerRequestOptions = new Stripe.RequestOptions { ApiKey = stripeSecretKey };
                    
                    try
                    {
                        // Verify customer exists on platform account
                        var stripeCustomer = await customerService.GetAsync(booking.Customer.StripeCustomerId);
                        validCustomerId = booking.Customer.StripeCustomerId;
                        
                        // Only update if customer doesn't have an address set
                        if (!string.IsNullOrEmpty(countryCode) && string.IsNullOrEmpty(stripeCustomer.Address?.Country))
                        {
                            await customerService.UpdateAsync(booking.Customer.StripeCustomerId, new Stripe.CustomerUpdateOptions
                            {
                                Address = new Stripe.AddressOptions
                                {
                                    Country = countryCode
                                }
                            }, customerRequestOptions);
                            
                            _logger.LogInformation(
                                "Updated customer {CustomerId} address with country {CountryCode}",
                                booking.Customer.StripeCustomerId,
                                countryCode
                            );
                        }
                    }
                    catch (Stripe.StripeException stripeEx) when (stripeEx.StripeError?.Code == "resource_missing")
                    {
                        // Customer doesn't exist on platform account - use email instead
                        _logger.LogWarning(
                            "Customer {CustomerId} not found on platform account. Will use email instead.",
                            booking.Customer.StripeCustomerId
                        );
                        validCustomerId = null;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to verify customer {CustomerId}. Will use email instead.", booking.Customer.StripeCustomerId);
                    validCustomerId = null;
                }
            }
            else if (!string.IsNullOrEmpty(stripeAccountId))
            {
                // Using connected account - always use email to let Stripe handle customer creation/lookup
                _logger.LogInformation(
                    "Using connected account {StripeAccountId}. Will use customer email {Email} instead of customer ID to let Stripe handle customer on connected account.",
                    stripeAccountId,
                    booking.Customer.Email
                );
                validCustomerId = null;
            }

            // Update options to use valid customer ID or email
            if (!string.IsNullOrEmpty(validCustomerId))
            {
                // Platform account with valid customer ID
                options.Customer = validCustomerId;
                options.CustomerEmail = null;
                options.CustomerUpdate = new Stripe.Checkout.SessionCustomerUpdateOptions
                {
                    Address = "auto",
                    Name = "auto"
                };
                _logger.LogInformation("Using customer ID {CustomerId} for checkout session on platform account", validCustomerId);
            }
            else
            {
                // Use email (for connected accounts or when customer ID doesn't exist)
                options.Customer = null;
                options.CustomerEmail = booking.Customer.Email;
                options.CustomerUpdate = null;
                _logger.LogInformation("Using customer email {Email} for checkout session", booking.Customer.Email);
            }

            var session = await sessionService.CreateAsync(options, requestOptions);

            _logger.LogInformation(
                "Created security deposit checkout session {SessionId} for booking {BookingId}", 
                session.Id, 
                booking.Id
            );

            return Ok(new
            {
                sessionId = session.Id,
                sessionUrl = session.Url,
                amount = depositAmount,
                bookingId = booking.Id,
                bookingNumber = booking.BookingNumber
            });
        }
        catch (Stripe.StripeException ex)
        {
            _logger.LogError(ex, "Stripe error creating security deposit checkout for booking {BookingId}. Stripe Error Code: {Code}, Message: {Message}, StackTrace: {StackTrace}", 
                id, ex.StripeError?.Code, ex.Message, ex.StackTrace);
            return StatusCode(500, new { error = "Stripe error: " + ex.Message, stripeErrorCode = ex.StripeError?.Code });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating security deposit checkout for booking {BookingId}. Exception Type: {Type}, Message: {Message}, StackTrace: {StackTrace}", 
                id, ex.GetType().Name, ex.Message, ex.StackTrace);
            return StatusCode(500, new { error = "Internal server error: " + ex.Message, exceptionType = ex.GetType().Name });
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
    
    /// <summary>
    /// Get Stripe locale based on user's current language and company country
    /// Priority: USER'S LANGUAGE > Country default
    /// </summary>
    private string GetStripeLocaleFromCountry(string? country, string? language)
    {
        // Normalize inputs
        var countryLower = (country ?? "").ToLower();
        var langLower = (language ?? "en").ToLower();
        
        // PRIORITY 1: Use the user's selected language with country code (if applicable)
        // For Portuguese speakers in Brazil
        if (langLower.StartsWith("pt") && countryLower.Contains("brazil"))
            return "pt-BR";
        
        // For Portuguese (general)
        if (langLower.StartsWith("pt"))
            return "pt";
            
        // For Spanish speakers in Latin America
        if (langLower.StartsWith("es") && (countryLower.Contains("mexico") || countryLower.Contains("argentina") || countryLower.Contains("colombia")))
            return "es-419";
            
        // For Spanish (general)
        if (langLower.StartsWith("es"))
            return "es";
        
        // Other languages
        if (langLower.StartsWith("fr")) return "fr";
        if (langLower.StartsWith("de")) return "de";
        if (langLower.StartsWith("it")) return "it";
        if (langLower.StartsWith("ja")) return "ja";
        if (langLower.StartsWith("zh")) return "zh";
        
        // Default to English
        return "en";
    }

    /// <summary>
    /// Get ISO country code from country name
    /// </summary>
    private string? GetCountryCode(string? country)
    {
        if (string.IsNullOrEmpty(country))
            return null;
            
        var countryLower = country.ToLower();
        
        // Map country names to ISO 3166-1 alpha-2 codes
        if (countryLower.Contains("brazil") || countryLower.Contains("brasil")) return "BR";
        if (countryLower.Contains("united states") || countryLower.Contains("usa") || countryLower == "us") return "US";
        if (countryLower.Contains("canada")) return "CA";
        if (countryLower.Contains("mexico")) return "MX";
        if (countryLower.Contains("argentina")) return "AR";
        if (countryLower.Contains("chile")) return "CL";
        if (countryLower.Contains("colombia")) return "CO";
        if (countryLower.Contains("peru")) return "PE";
        if (countryLower.Contains("portugal")) return "PT";
        if (countryLower.Contains("spain") || countryLower.Contains("españa")) return "ES";
        if (countryLower.Contains("france")) return "FR";
        if (countryLower.Contains("germany") || countryLower.Contains("deutschland")) return "DE";
        if (countryLower.Contains("italy") || countryLower.Contains("italia")) return "IT";
        if (countryLower.Contains("united kingdom") || countryLower.Contains("uk") || countryLower.Contains("england")) return "GB";
        if (countryLower.Contains("japan")) return "JP";
        if (countryLower.Contains("china")) return "CN";
        if (countryLower.Contains("india")) return "IN";
        if (countryLower.Contains("australia")) return "AU";
        
        // If it's already a 2-letter code, return as-is
        if (country.Length == 2)
            return country.ToUpper();
            
        return null;
    }
    
    private int GetCurrencyDecimalPlaces(string currency)
    {
        // Stripe currencies with 0 decimal places
        var zeroDecimalCurrencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "bif", "clp", "djf", "gnf", "jpy", "kmf", "krw", "mga", "pyg", "rwf", "ugx", "vnd", "vuv", "xaf", "xof", "xpf"
        };
        
        if (zeroDecimalCurrencies.Contains(currency))
            return 0;
        
        // All other currencies use 2 decimal places
        return 2;
    }

    private async Task SendInvitationEmailAfterPayment(Reservation reservation, Customer customer)
    {
        try
        {
            _logger.LogInformation(
                "SendInvitationEmailAfterPayment: Starting for booking {BookingId}, CustomerId: {CustomerId}",
                reservation.Id,
                reservation.CustomerId);

            // Only send invitation if customer has a password hash (meaning password was generated)
            // and hasn't received an invitation yet (we'll track this by checking if they've logged in)
            if (string.IsNullOrEmpty(customer.PasswordHash))
            {
                _logger.LogInformation(
                    "SendInvitationEmailAfterPayment: Skipping - Customer {CustomerId} has no password hash (may have been created with existing password)",
                    customer.Id);
                return; // Customer already has access or no password was set
            }

            if (customer.LastLogin.HasValue)
            {
                _logger.LogInformation(
                    "SendInvitationEmailAfterPayment: Skipping - Customer {CustomerId} has already logged in (LastLogin: {LastLogin})",
                    customer.Id,
                    customer.LastLogin);
                return; // Customer already has access
            }

            // Generate a temporary password for the invitation email
            // (We can't retrieve the original password from the hash)
            var random = new Random();
            const string chars = "ABCDEFGHJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var temporaryPassword = new string(Enumerable.Repeat(chars, 12)
                .Select(s => s[random.Next(s.Length)]).ToArray());

            // Update customer's password with the new temporary password
            customer.PasswordHash = BCrypt.Net.BCrypt.HashPassword(temporaryPassword);
            _context.Customers.Update(customer);
            await _context.SaveChangesAsync(); // Save password update before sending email

            // Load company and vehicle for email details
            var company = await _context.Companies.FindAsync(reservation.CompanyId);
            var vehicle = await _context.Vehicles
                .Include(v => v.VehicleModel)
                .ThenInclude(vm => vm!.Model)
                .FirstOrDefaultAsync(v => v.Id == reservation.VehicleId);

            if (company == null)
            {
                _logger.LogWarning(
                    "SendInvitationEmailAfterPayment: Company {CompanyId} not found for booking {BookingId}",
                    reservation.CompanyId,
                    reservation.Id);
                return;
            }

            // Determine language from company
            var languageCode = company.Language?.ToLower() ?? "en";
            var language = LanguageCodes.FromCode(languageCode);

            // Get email service
            var multiTenantEmailService = HttpContext.RequestServices.GetRequiredService<MultiTenantEmailService>();
            
            _logger.LogInformation(
                "SendInvitationEmailAfterPayment: Preparing to send email to {Email} for booking {BookingNumber}",
                customer.Email,
                reservation.BookingNumber);

            // Prepare booking details
            var vehicleName = vehicle?.VehicleModel?.Model != null
                ? $"{vehicle.VehicleModel.Model.Make} {vehicle.VehicleModel.Model.ModelName} ({vehicle.VehicleModel.Model.Year})"
                : "Vehicle";

            var invitationUrl = $"{Request.Scheme}://{Request.Host}/login?email={Uri.EscapeDataString(customer.Email)}";

            // Send invitation email with booking details and password
            var customerName = (!string.IsNullOrWhiteSpace(customer.FirstName) || !string.IsNullOrWhiteSpace(customer.LastName))
                ? $"{customer.FirstName} {customer.LastName}".Trim()
                : customer.Email;
            
            var emailSent = await multiTenantEmailService.SendInvitationEmailWithBookingDetailsAsync(
                reservation.CompanyId,
                customer.Email,
                customerName,
                invitationUrl,
                temporaryPassword,
                reservation.BookingNumber ?? reservation.Id.ToString(),
                reservation.PickupDate,
                reservation.ReturnDate,
                vehicleName,
                reservation.PickupLocation ?? "",
                reservation.TotalAmount,
                company.Currency ?? "USD",
                language);

            if (emailSent)
            {
                _logger.LogInformation(
                    "SendInvitationEmailAfterPayment: Invitation email with booking details sent successfully to {Email} for booking {BookingNumber}",
                    customer.Email,
                    reservation.BookingNumber);
            }
            else
            {
                _logger.LogWarning(
                    "SendInvitationEmailAfterPayment: Failed to send invitation email to {Email} for booking {BookingNumber}",
                    customer.Email,
                    reservation.BookingNumber);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error sending invitation email after payment for booking {BookingId}",
                reservation.Id);
            // Don't throw - payment was successful, email failure shouldn't break the flow
        }
    }
}

// Request DTO for refund
public class RefundPaymentRequest
{
    public decimal Amount { get; set; }
    public string? Reason { get; set; }
}

// Request DTO for preview PDF generation
public class PreviewAgreementPdfRequest
{
    public string? Language { get; set; }
    
    // Company Info
    public string? CompanyName { get; set; }
    public string? CompanyAddress { get; set; }
    public string? CompanyPhone { get; set; }
    public string? CompanyEmail { get; set; }
    
    // Customer / Primary Renter
    public string? CustomerFirstName { get; set; }
    public string? CustomerMiddleName { get; set; }
    public string? CustomerLastName { get; set; }
    public string? CustomerName { get; set; } // Full name (legacy)
    public string? CustomerEmail { get; set; }
    public string? CustomerPhone { get; set; }
    public string? CustomerAddress { get; set; }
    public string? DriverLicenseNumber { get; set; }
    public string? DriverLicenseState { get; set; }
    public DateTime? DriverLicenseExpiration { get; set; }
    public DateTime? CustomerDateOfBirth { get; set; }
    
    // Additional Driver
    public string? AdditionalDriverFirstName { get; set; }
    public string? AdditionalDriverMiddleName { get; set; }
    public string? AdditionalDriverLastName { get; set; }
    public string? AdditionalDriverEmail { get; set; }
    public string? AdditionalDriverPhone { get; set; }
    public string? AdditionalDriverLicenseNumber { get; set; }
    public string? AdditionalDriverLicenseState { get; set; }
    public DateTime? AdditionalDriverLicenseExpiration { get; set; }
    public DateTime? AdditionalDriverDateOfBirth { get; set; }
    public string? AdditionalDriverAddress { get; set; }
    
    // Rental Vehicle
    public string? VehicleType { get; set; }
    public string? VehicleName { get; set; }
    public int? VehicleYear { get; set; }
    public string? VehicleColor { get; set; }
    public string? VehiclePlate { get; set; }
    public string? VehicleVin { get; set; }
    public int? OdometerStart { get; set; }
    
    // Rental Period
    public DateTime? PickupDate { get; set; }
    public string? PickupTime { get; set; }
    public string? PickupLocation { get; set; }
    public DateTime? ReturnDate { get; set; }
    public string? ReturnTime { get; set; }
    public string? ReturnLocation { get; set; }
    public DateTime? DueDate { get; set; }
    
    // Fuel Level
    public string? FuelAtPickup { get; set; }
    public string? FuelAtReturn { get; set; }
    
    // Financial
    public decimal? RentalAmount { get; set; }
    public decimal? DepositAmount { get; set; }
    public decimal? DailyRate { get; set; }
    public int? RentalDays { get; set; }
    public string? Currency { get; set; }
    
    // Additional Services
    public List<PreviewAdditionalServiceItem>? AdditionalServices { get; set; }
    public decimal? Subtotal { get; set; }
    public decimal? TotalCharges { get; set; }
    
    // Additional Charges
    public decimal? LateReturnFee { get; set; }
    public decimal? DamageFee { get; set; }
    public decimal? FuelServiceFee { get; set; }
    public decimal? CleaningFee { get; set; }
    public decimal? Refund { get; set; }
    public decimal? BalanceDue { get; set; }
}

public class PreviewAdditionalServiceItem
{
    public string? Name { get; set; }
    public decimal DailyRate { get; set; }
    public int Days { get; set; }
    public decimal Total { get; set; }
}
