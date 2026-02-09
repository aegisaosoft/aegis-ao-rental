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
                    .ThenInclude(vm => vm != null ? vm.Model : null!)
                        .ThenInclude(m => m != null ? m.Category : null!)
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
                VehicleMake = vehicle.VehicleModel?.Model?.Make,
                VehicleModel = vehicle.VehicleModel?.Model?.ModelName,
                VehicleYear = vehicle.VehicleModel?.Model?.Year,
                VehicleColor = vehicle.Color,
                VehicleCategory = vehicle.VehicleModel?.Model?.Category?.CategoryName,
                VehicleLicensePlate = vehicle.LicensePlate,
                VehicleName = $"{vehicle.VehicleModel?.Model?.Make ?? ""} {vehicle.VehicleModel?.Model?.ModelName ?? ""} ({vehicle.VehicleModel?.Model?.Year ?? 0})" // Deprecated
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
                    .ThenInclude(vm => vm != null ? vm.Model : null!)
                        .ThenInclude(m => m != null ? m.Category : null!)
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
            VehicleMake = bookingToken.Vehicle?.VehicleModel?.Model?.Make,
            VehicleModel = bookingToken.Vehicle?.VehicleModel?.Model?.ModelName,
            VehicleYear = bookingToken.Vehicle?.VehicleModel?.Model?.Year,
            VehicleColor = bookingToken.Vehicle?.Color,
            VehicleCategory = bookingToken.Vehicle?.VehicleModel?.Model?.Category?.CategoryName,
            VehicleLicensePlate = bookingToken.Vehicle?.LicensePlate,
            VehicleName = (bookingToken.Vehicle?.VehicleModel?.Model != null) ?
                $"{bookingToken.Vehicle.VehicleModel.Model.Make} {bookingToken.Vehicle.VehicleModel.Model.ModelName} ({bookingToken.Vehicle.VehicleModel.Model.Year})" :
                "Unknown Vehicle" // Deprecated
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

            // Update payment status to completed after successful payment
            var rowsUpdated = await _context.Bookings
                .Where(r => r.Id == reservation.Id)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(r => r.PaymentStatus, "completed")
                    .SetProperty(r => r.UpdatedAt, DateTime.UtcNow));

            if (rowsUpdated == 0)
            {
                _logger.LogWarning("[Booking] Failed to update reservation {ReservationId} payment status", reservation.Id);
            }
            else
            {
                reservation.PaymentStatus = "completed";
                reservation.UpdatedAt = DateTime.UtcNow;
            }

            // Check if booking is ready to be confirmed (payment + agreement signed)
            await CheckAndUpdateBookingStatusAsync(reservation.Id);

            _logger.LogInformation(
                "[Booking] Payment confirmed. ReservationId: {ReservationId}, Amount: {Amount}, CompanyId: {CompanyId}, CustomerId: {CustomerId}, PaymentStatus: {PaymentStatus}, BookingStatus: {BookingStatus}",
                reservation.Id,
                reservation.TotalAmount,
                reservation.CompanyId,
                reservation.CustomerId,
                reservation.PaymentStatus,
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
                        .ThenInclude(vm => vm != null ? vm.Model : null!)
                            .ThenInclude(m => m != null ? m.Category : null!)
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
                VehicleMake = r.Vehicle?.VehicleModel?.Model?.Make,
                VehicleModel = r.Vehicle?.VehicleModel?.Model?.ModelName,
                VehicleYear = r.Vehicle?.VehicleModel?.Model?.Year,
                VehicleColor = r.Vehicle?.Color,
                VehicleCategory = r.Vehicle?.VehicleModel?.Model?.Category?.CategoryName,
                VehicleLicensePlate = r.Vehicle?.LicensePlate,
                VehicleName = (r.Vehicle?.VehicleModel?.Model != null)
                    ? r.Vehicle.VehicleModel.Model.Make + " " + r.Vehicle.VehicleModel.Model.ModelName + " (" + r.Vehicle.VehicleModel.Model.Year + ")"
                    : "Unknown Vehicle", // Deprecated
                LicensePlate = r.Vehicle?.LicensePlate ?? "", // Deprecated: use VehicleLicensePlate instead
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
                        .ThenInclude(vm => vm != null ? vm.Model : null!)
                            .ThenInclude(m => m != null ? m.Category : null!)
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
                    VehicleMake = r.Vehicle?.VehicleModel?.Model?.Make,
                    VehicleModel = r.Vehicle?.VehicleModel?.Model?.ModelName,
                    VehicleYear = r.Vehicle?.VehicleModel?.Model?.Year,
                    VehicleColor = r.Vehicle?.Color,
                    VehicleCategory = r.Vehicle?.VehicleModel?.Model?.Category?.CategoryName,
                    VehicleLicensePlate = r.Vehicle?.LicensePlate,
                    VehicleName = r.Vehicle?.VehicleModel?.Model != null
                        ? $"{r.Vehicle.VehicleModel.Model.Make} {r.Vehicle.VehicleModel.Model.ModelName} ({r.Vehicle.VehicleModel.Model.Year})"
                        : "Unknown Vehicle", // Deprecated
                    LicensePlate = r.Vehicle?.LicensePlate ?? "", // Deprecated: use VehicleLicensePlate instead
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
                        .ThenInclude(vm => vm != null ? vm.Model : null!)
                            .ThenInclude(m => m != null ? m.Category : null!)
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
                VehicleMake = reservation.Vehicle?.VehicleModel?.Model?.Make,
                VehicleModel = reservation.Vehicle?.VehicleModel?.Model?.ModelName,
                VehicleYear = reservation.Vehicle?.VehicleModel?.Model?.Year,
                VehicleColor = reservation.Vehicle?.Color,
                VehicleCategory = reservation.Vehicle?.VehicleModel?.Model?.Category?.CategoryName,
                VehicleLicensePlate = reservation.Vehicle?.LicensePlate,
                VehicleName = (reservation.Vehicle?.VehicleModel?.Model != null)
                    ? reservation.Vehicle.VehicleModel.Model.Make + " " + reservation.Vehicle.VehicleModel.Model.ModelName + " (" + reservation.Vehicle.VehicleModel.Model.Year + ")"
                    : "Unknown Vehicle", // Deprecated // Deprecated
                LicensePlate = reservation.Vehicle?.LicensePlate ?? "", // Deprecated: use VehicleLicensePlate instead
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
                // Additional services from JSON
                Services = !string.IsNullOrEmpty(reservation.AdditionalServicesJson)
                    ? System.Text.Json.JsonSerializer.Deserialize<List<AgreementServiceDto>>(reservation.AdditionalServicesJson)
                    : null,
                ServicesTotal = !string.IsNullOrEmpty(reservation.AdditionalServicesJson)
                    ? (System.Text.Json.JsonSerializer.Deserialize<List<AgreementServiceDto>>(reservation.AdditionalServicesJson)?.Sum(s => s.Total) ?? 0)
                    : 0,
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
                Notes = createReservationDto.Notes,
                // Save additional services as JSON
                AdditionalServicesJson = createReservationDto.AdditionalServices != null && createReservationDto.AdditionalServices.Any()
                    ? System.Text.Json.JsonSerializer.Serialize(createReservationDto.AdditionalServices)
                    : (createReservationDto.AgreementData?.AdditionalServices != null && createReservationDto.AgreementData.AdditionalServices.Any()
                        ? System.Text.Json.JsonSerializer.Serialize(createReservationDto.AgreementData.AdditionalServices)
                        : null)
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
                        VehicleMake = reservation.Vehicle?.VehicleModel?.Model?.Make,
                        VehicleModel = reservation.Vehicle?.VehicleModel?.Model?.ModelName,
                        VehicleYear = reservation.Vehicle?.VehicleModel?.Model?.Year,
                        VehicleColor = reservation.Vehicle?.Color,
                        VehicleCategory = reservation.Vehicle?.VehicleModel?.Model?.Category?.CategoryName,
                        VehicleLicensePlate = vehicle.LicensePlate,
                        VehicleName = vehicleName, // Deprecated
                        VehiclePlate = vehicle.LicensePlate, // Deprecated: use VehicleLicensePlate instead
                        
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
                    catch (InvalidOperationException ex) when (ex.Message.Contains("Azure Blob Storage is not configured"))
                    {
                        _logger.LogWarning("Azure Blob Storage not configured for booking {BookingId}. PDF generation skipped: {Message}", reservation.Id, ex.Message);
                        // Continue without PDF - this is acceptable for local development
                    }
                    catch (Exception ex)
                    {
                        // Log error but don't fail the booking creation itself
                        _logger.LogError(ex, "Failed to generate agreement PDF for booking {BookingId}: {Message}", reservation.Id, ex.Message);
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
                VehicleMake = reservation.Vehicle?.VehicleModel?.Model?.Make,
                VehicleModel = reservation.Vehicle?.VehicleModel?.Model?.ModelName,
                VehicleYear = reservation.Vehicle?.VehicleModel?.Model?.Year,
                VehicleColor = reservation.Vehicle?.Color,
                VehicleCategory = reservation.Vehicle?.VehicleModel?.Model?.Category?.CategoryName,
                VehicleLicensePlate = reservation.Vehicle?.LicensePlate,
                VehicleName = (reservation.Vehicle?.VehicleModel?.Model != null)
                    ? reservation.Vehicle.VehicleModel.Model.Make + " " + reservation.Vehicle.VehicleModel.Model.ModelName + " (" + reservation.Vehicle.VehicleModel.Model.Year + ")"
                    : "Unknown Vehicle", // Deprecated
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
                        .ThenInclude(vm => vm != null ? vm.Model : null!)
                            .ThenInclude(m => m != null ? m.Category : null!)
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
                VehicleMake = reservation.Vehicle?.VehicleModel?.Model?.Make,
                VehicleModel = reservation.Vehicle?.VehicleModel?.Model?.ModelName,
                VehicleYear = reservation.Vehicle?.VehicleModel?.Model?.Year,
                VehicleColor = reservation.Vehicle?.Color,
                VehicleCategory = reservation.Vehicle?.VehicleModel?.Model?.Category?.CategoryName,
                VehicleLicensePlate = reservation.Vehicle?.LicensePlate,
                VehicleName = (reservation.Vehicle?.VehicleModel?.Model != null)
                    ? reservation.Vehicle.VehicleModel.Model.Make + " " + reservation.Vehicle.VehicleModel.Model.ModelName + " (" + reservation.Vehicle.VehicleModel.Model.Year + ")"
                    : "Unknown Vehicle", // Deprecated
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
                        .ThenInclude(vm => vm != null ? vm.Model : null!)
                            .ThenInclude(m => m != null ? m.Category : null!)
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
                VehicleMake = reservation.Vehicle?.VehicleModel?.Model?.Make,
                VehicleModel = reservation.Vehicle?.VehicleModel?.Model?.ModelName,
                VehicleYear = reservation.Vehicle?.VehicleModel?.Model?.Year,
                VehicleColor = reservation.Vehicle?.Color,
                VehicleCategory = reservation.Vehicle?.VehicleModel?.Model?.Category?.CategoryName,
                VehicleLicensePlate = reservation.Vehicle?.LicensePlate,
                VehicleName = (reservation.Vehicle?.VehicleModel?.Model != null)
                    ? reservation.Vehicle.VehicleModel.Model.Make + " " + reservation.Vehicle.VehicleModel.Model.ModelName + " (" + reservation.Vehicle.VehicleModel.Model.Year + ")"
                    : "Unknown Vehicle", // Deprecated
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
                        .ThenInclude(vm => vm != null ? vm.Model : null!)
                            .ThenInclude(m => m != null ? m.Category : null!)
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
                VehicleMake = reservation.Vehicle?.VehicleModel?.Model?.Make,
                VehicleModel = reservation.Vehicle?.VehicleModel?.Model?.ModelName,
                VehicleYear = reservation.Vehicle?.VehicleModel?.Model?.Year,
                VehicleColor = reservation.Vehicle?.Color,
                VehicleCategory = reservation.Vehicle?.VehicleModel?.Model?.Category?.CategoryName,
                VehicleLicensePlate = reservation.Vehicle?.LicensePlate,
                VehicleName = (reservation.Vehicle?.VehicleModel?.Model != null)
                    ? reservation.Vehicle.VehicleModel.Model.Make + " " + reservation.Vehicle.VehicleModel.Model.ModelName + " (" + reservation.Vehicle.VehicleModel.Model.Year + ")"
                    : "Unknown Vehicle", // Deprecated
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
                VehicleMake = reservation.Vehicle?.VehicleModel?.Model?.Make,
                VehicleModel = reservation.Vehicle?.VehicleModel?.Model?.ModelName,
                VehicleYear = reservation.Vehicle?.VehicleModel?.Model?.Year,
                VehicleColor = reservation.Vehicle?.Color,
                VehicleCategory = reservation.Vehicle?.VehicleModel?.Model?.Category?.CategoryName,
                VehicleLicensePlate = reservation.Vehicle?.LicensePlate,
                VehicleName = (reservation.Vehicle?.VehicleModel?.Model != null)
                    ? reservation.Vehicle.VehicleModel.Model.Make + " " + reservation.Vehicle.VehicleModel.Model.ModelName + " (" + reservation.Vehicle.VehicleModel.Model.Year + ")"
                    : "Unknown Vehicle", // Deprecated
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

            // Check if security deposit is already authorized or captured
            if (booking.SecurityDepositAuthorizedAt.HasValue)
            {
                _logger.LogInformation(
                    "Security deposit already authorized for booking {BookingId} at {AuthorizedAt}. Status: {Status}",
                    booking.Id, booking.SecurityDepositAuthorizedAt.Value, booking.SecurityDepositStatus);

                return Ok(new
                {
                    alreadyAuthorized = true,
                    bookingId = booking.Id,
                    bookingNumber = booking.BookingNumber,
                    authorizedAt = booking.SecurityDepositAuthorizedAt.Value,
                    status = booking.SecurityDepositStatus,
                    message = "Security deposit already authorized",
                    redirectUrl = GetDynamicFrontendUrl($"/admin-dashboard?tab=reservations&deposit_success=true&booking_id={booking.Id}")
                });
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

            // Get Rules of Action and Full Terms texts based on language using unified service
            var (rulesText, fullTermsText) = RentalTermsService.GetRulesAndTermsTexts(request.Language ?? "en");

            // Get SMS Consent text based on language using unified service
            var smsConsentText = request.IncludeSmsConsent
                ? (request.SmsConsentText ?? RentalTermsService.GetSmsConsentText(request.Language ?? "en"))
                : "";

            // Debug logging
            _logger.LogInformation($"Preview PDF: IncludeSmsConsent={request.IncludeSmsConsent}, SmsConsentText length={smsConsentText?.Length ?? 0}, Language={request.Language}");

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
                BookingNumber = "PREVIEW",
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
                SmsConsentText = smsConsentText,
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
    /// REMOVED: Duplicate methods moved to RentalTermsService for consistency between preview and final agreements
    /// - GetSmsConsentText() -> RentalTermsService.GetSmsConsentText()
    /// - GetRulesAndTermsTexts() -> RentalTermsService.GetRulesAndTermsTexts()
    /// - GetFullTermsTextEnglish() -> RentalTermsService.GetFullTermsTextEnglish()
    /// - GetFullTermsTextSpanish() -> RentalTermsService.GetFullTermsTextSpanish()
    /// </summary>

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
                SuccessUrl = GetDynamicFrontendUrl($"/admin-dashboard?tab=reservations&deposit_success=true&booking_id={booking.Id}"),
                CancelUrl = GetDynamicFrontendUrl($"/admin-dashboard?tab=reservations&deposit_cancelled=true&booking_id={booking.Id}"),
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

    /// <summary>
    /// Get the dynamic frontend URL based on current request context and tenant
    /// </summary>
    private string GetDynamicFrontendUrl(string path)
    {
        var host = HttpContext.Request.Host.Host;
        var scheme = HttpContext.Request.Scheme;

        _logger.LogInformation("[GetDynamicFrontendUrl] Processing: Host={Host}, Scheme={Scheme}, Path={Path}", host, scheme, path);

        // For localhost development
        if (host.Contains("localhost") || host == "127.0.0.1")
        {
            // Check if request is from admin app (port 4000) or web app (port 3000)
            var origin = HttpContext.Request.Headers["Origin"].ToString();
            var referer = HttpContext.Request.Headers["Referer"].ToString();
            var sourceUrl = !string.IsNullOrEmpty(origin) ? origin : referer;

            _logger.LogInformation("[GetDynamicFrontendUrl] Localhost mode - Origin={Origin}, Referer={Referer}, SourceUrl={SourceUrl}", origin, referer, sourceUrl);

            bool isAdminApp = !string.IsNullOrEmpty(sourceUrl) && (
                sourceUrl.Contains("localhost:4000") ||
                sourceUrl.Contains("admin.aegis-rental.com")
            );

            if (isAdminApp)
            {
                _logger.LogInformation("[BookingController] Using admin app localhost URL for path: {Path}", path);
                return $"http://localhost:4000{path}";
            }
            else
            {
                // Extract subdomain from Origin/Referer for proper redirect
                var targetHost = "localhost:3000";
                if (!string.IsNullOrEmpty(sourceUrl))
                {
                    try
                    {
                        var uri = new Uri(sourceUrl);
                        targetHost = uri.Authority; // This preserves subdomain (abc.localhost:3000)
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[BookingController] Failed to parse sourceUrl: {SourceUrl}, using default localhost:3000", sourceUrl);
                    }
                }

                _logger.LogInformation("[BookingController] Using web app localhost URL for path: {Path}, targetHost: {TargetHost}", path, targetHost);
                return $"http://{targetHost}{path}";
            }
        }

        // For production - determine if admin or tenant site
        var currentOrigin = HttpContext.Request.Headers["Origin"].ToString();
        var currentReferer = HttpContext.Request.Headers["Referer"].ToString();
        var currentSource = !string.IsNullOrEmpty(currentOrigin) ? currentOrigin : currentReferer;

        bool isAdminSite = !string.IsNullOrEmpty(currentSource) && currentSource.Contains("admin.aegis-rental.com");

        if (isAdminSite)
        {
            _logger.LogInformation("[BookingController] Using admin production URL for path: {Path}", path);
            return $"https://admin.aegis-rental.com{path}";
        }

        // Azure-specific domain mapping
        var currentHost = HttpContext.Request.Host.Host.ToLower();

        // Check for Azure App Service domains
        if (currentHost.Contains("azurewebsites.net"))
        {
            // Map Azure subdomain to tenant domain
            var azureAppName = currentHost.Split('.')[0]; // Extract app name from xxx.azurewebsites.net
            var tenantDomain = $"https://{azureAppName}.aegis-rental.com{path}";
            _logger.LogInformation("[BookingController] Using Azure tenant mapping: {AzureHost} -> {TenantDomain}", currentHost, tenantDomain);
            return tenantDomain;
        }

        // Check for direct aegis-rental.com subdomains
        if (currentHost.Contains("aegis-rental.com") && !currentHost.Contains("admin."))
        {
            var tenantUrl = $"https://{currentHost}{path}";
            _logger.LogInformation("[BookingController] Using direct aegis-rental tenant URL: {TenantUrl}", tenantUrl);
            return tenantUrl;
        }

        // Dynamic tenant support - no static mapping needed for aegis-rental.com subdomains
        // All *.aegis-rental.com subdomains are handled automatically above

        // Fallback: use current host with appropriate scheme
        var finalScheme = scheme;

        // Force HTTPS for production environments (Azure, non-localhost)
        if (!host.Contains("localhost") && !host.Equals("127.0.0.1"))
        {
            finalScheme = "https";
        }

        var fallbackUrl = $"{finalScheme}://{HttpContext.Request.Host}{path}";
        _logger.LogInformation("[BookingController] Using fallback tenant URL: {FallbackUrl} (Host: {Host}, OriginalScheme: {OriginalScheme}, FinalScheme: {FinalScheme})",
            fallbackUrl, HttpContext.Request.Host, scheme, finalScheme);
        return fallbackUrl;
    }

    /// <summary>
    /// Checks if booking has both payment and agreement completed, and updates status to Confirmed if ready.
    /// This prevents double payments by ensuring status is confirmed only when both conditions are met.
    /// </summary>
    private async Task CheckAndUpdateBookingStatusAsync(Guid bookingId)
    {
        try
        {
            _logger.LogInformation("[Booking] Checking if booking {BookingId} is ready for confirmation", bookingId);

            // Get booking with current status
            var booking = await _context.Bookings.FindAsync(bookingId);
            if (booking == null)
            {
                _logger.LogWarning("[Booking] Booking {BookingId} not found for status check", bookingId);
                return;
            }

            // Skip if already confirmed
            if (booking.Status == BookingStatus.Confirmed)
            {
                _logger.LogInformation("[Booking] Booking {BookingId} already confirmed", bookingId);
                return;
            }

            // Check if payment is completed
            bool paymentCompleted = booking.PaymentStatus == "completed";

            // Check if agreement is signed
            bool agreementSigned = await _context.RentalAgreements
                .AnyAsync(a => a.BookingId == bookingId && a.Status != "Voided" && !string.IsNullOrEmpty(a.SignatureImage));

            _logger.LogInformation(
                "[Booking] Status check for booking {BookingId}: PaymentCompleted={PaymentCompleted}, AgreementSigned={AgreementSigned}",
                bookingId, paymentCompleted, agreementSigned);

            // Update status to Confirmed only if both conditions are met
            if (paymentCompleted && agreementSigned)
            {
                var rowsUpdated = await _context.Bookings
                    .Where(b => b.Id == bookingId && b.Status != BookingStatus.Confirmed)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(b => b.Status, BookingStatus.Confirmed)
                        .SetProperty(b => b.UpdatedAt, DateTime.UtcNow));

                if (rowsUpdated > 0)
                {
                    _logger.LogInformation(
                        "[Booking] Successfully updated booking {BookingId} status to Confirmed (payment + agreement both completed)",
                        bookingId);
                }
                else
                {
                    _logger.LogInformation(
                        "[Booking] Booking {BookingId} was already confirmed by another process",
                        bookingId);
                }
            }
            else
            {
                _logger.LogInformation(
                    "[Booking] Booking {BookingId} not ready for confirmation yet. Waiting for: {MissingRequirements}",
                    bookingId,
                    !paymentCompleted && !agreementSigned ? "payment and agreement" :
                    !paymentCompleted ? "payment" : "agreement");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Booking] Error checking booking {BookingId} status for confirmation", bookingId);
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

    // SMS Consent
    public string? SmsConsentText { get; set; }
    public bool IncludeSmsConsent { get; set; } = true; // Include by default for preview
}

public class PreviewAdditionalServiceItem
{
    public string? Name { get; set; }
    public decimal DailyRate { get; set; }
    public int Days { get; set; }
    public decimal Total { get; set; }
}
