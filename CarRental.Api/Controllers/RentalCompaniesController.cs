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

using System;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using System.Security.Claims;
using CarRental.Api.Data;
using CarRental.Api.DTOs;
using CarRental.Api.DTOs.Stripe;
using CarRental.Api.Models;
using CarRental.Api.Services;
using CarRental.Api.Helpers;
using Stripe;

namespace CarRental.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RentalCompaniesController : ControllerBase
{
    private readonly CarRentalDbContext _context;
    private readonly IStripeService _stripeService;
    private readonly ILogger<RentalCompaniesController> _logger;
    private readonly IEncryptionService _encryptionService;
    private readonly IStripeConnectService _stripeConnectService;
    private readonly IAzureDnsService? _azureDnsService;
    private readonly IWebHostEnvironment _environment;
    private readonly IEmailService _emailService;
    private readonly IAzureBlobStorageService _blobStorageService;

    public RentalCompaniesController(
        CarRentalDbContext context, 
        IStripeService stripeService, 
        ILogger<RentalCompaniesController> logger,
        IEncryptionService encryptionService,
        IStripeConnectService stripeConnectService,
        IWebHostEnvironment environment,
        IEmailService emailService,
        IAzureBlobStorageService blobStorageService,
        IAzureDnsService? azureDnsService = null)
    {
        _context = context;
        _stripeService = stripeService;
        _logger = logger;
        _encryptionService = encryptionService;
        _stripeConnectService = stripeConnectService;
        _environment = environment;
        _emailService = emailService;
        _blobStorageService = blobStorageService;
        _azureDnsService = azureDnsService;
    }

    private async Task<string?> ResolveStripeAccountIdAsync(Company company)
    {
        if (company.StripeSettingsId == null)
            return null;

        var stripeCompany = await _context.StripeCompanies
            .FirstOrDefaultAsync(sc => sc.CompanyId == company.Id && sc.SettingsId == company.StripeSettingsId.Value);

        if (stripeCompany == null || string.IsNullOrWhiteSpace(stripeCompany.StripeAccountId))
            return null;

        try
        {
            return _encryptionService.Decrypt(stripeCompany.StripeAccountId);
        }
        catch (FormatException)
        {
            return await ReEncryptPlainStripeAccountIdAsync(stripeCompany, stripeCompany.StripeAccountId!);
        }
        catch (CryptographicException)
        {
            return await ReEncryptPlainStripeAccountIdAsync(stripeCompany, stripeCompany.StripeAccountId!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt Stripe account ID for company {CompanyId}", company.Id);
            return null;
        }
    }

    private async Task<string?> ReEncryptPlainStripeAccountIdAsync(CarRental.Api.Models.StripeCompany stripeCompany, string plaintext)
    {
        if (string.IsNullOrWhiteSpace(plaintext))
            return null;

        try
        {
            stripeCompany.StripeAccountId = _encryptionService.Encrypt(plaintext);
            stripeCompany.UpdatedAt = DateTime.UtcNow;
            _context.StripeCompanies.Update(stripeCompany);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to re-encrypt Stripe account ID for StripeCompany {StripeCompanyId}", stripeCompany.Id);
        }

        return plaintext;
    }

    /// <summary>
    /// Get all rental companies with optional filtering
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<RentalCompanyDto>>> GetRentalCompanies(
        string? search = null,
        bool? isActive = null,
        int page = 1,
        int pageSize = 20)
    {
        var query = _context.Companies.AsQueryable();

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(c => 
                c.CompanyName.Contains(search) || 
                c.Email.Contains(search));
        }

        if (isActive.HasValue)
            query = query.Where(c => c.IsActive == isActive.Value);

        var companies = await query
            .OrderBy(c => c.CompanyName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new RentalCompanyDto
            {
                CompanyId = c.Id,
                CompanyName = c.CompanyName,
                Email = c.Email,
                Website = c.Website,
                StripeAccountId = null,
                StripeSettingsId = c.StripeSettingsId,
                TaxId = c.TaxId,
                VideoLink = c.VideoLink,
                BannerLink = c.BannerLink,
                LogoLink = c.LogoLink,
                Motto = c.Motto,
                MottoDescription = c.MottoDescription,
                Invitation = c.Invitation,
                Texts = c.Texts,
                BackgroundLink = c.BackgroundLink,
                About = c.About,
                TermsOfUse = c.TermsOfUse,
                BookingIntegrated = c.BookingIntegrated,
                CompanyPath = c.CompanyPath,
                Subdomain = c.Subdomain,
                PrimaryColor = c.PrimaryColor,
                SecondaryColor = c.SecondaryColor,
                Currency = c.Currency,
                LogoUrl = c.LogoUrl,
                FaviconUrl = c.FaviconUrl,
                CustomCss = c.CustomCss,
                Country = c.Country,
                BlinkKey = c.BlinkKey,
                SecurityDeposit = c.SecurityDeposit,
                IsSecurityDepositMandatory = c.IsSecurityDepositMandatory,
                IsActive = c.IsActive,
                IsTestCompany = c.IsTestCompany,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt
            })
            .ToListAsync();

        return Ok(companies);
    }

    /// <summary>
    /// Get a specific rental company by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<RentalCompanyDto>> GetRentalCompany(Guid id)
    {
        var company = await _context.Companies.FindAsync(id);

        if (company == null)
            return NotFound();

        // Load CompanyMode
        var companyMode = await _context.CompanyModes
            .FirstOrDefaultAsync(cm => cm.CompanyId == company.Id);

        var companyDto = new RentalCompanyDto
        {
            CompanyId = company.Id,
            CompanyName = company.CompanyName,
            Email = company.Email,
            Website = company.Website,
            StripeAccountId = null,
            StripeSettingsId = company.StripeSettingsId,
            TaxId = company.TaxId,
            VideoLink = company.VideoLink,
            BannerLink = company.BannerLink,
            LogoLink = company.LogoLink,
            Motto = company.Motto,
            MottoDescription = company.MottoDescription,
            Invitation = company.Invitation,
            Texts = company.Texts,
            BackgroundLink = company.BackgroundLink,
            About = company.About,
            TermsOfUse = company.TermsOfUse,
            BookingIntegrated = company.BookingIntegrated,
            CompanyPath = company.CompanyPath,
            Subdomain = company.Subdomain,
            PrimaryColor = company.PrimaryColor,
            SecondaryColor = company.SecondaryColor,
            Currency = company.Currency,
            LogoUrl = company.LogoUrl,
            FaviconUrl = company.FaviconUrl,
            CustomCss = company.CustomCss,
            Country = company.Country,
            BlinkKey = company.BlinkKey,
            SecurityDeposit = company.SecurityDeposit,
            IsSecurityDepositMandatory = company.IsSecurityDepositMandatory,
            IsActive = company.IsActive,
            IsTestCompany = company.IsTestCompany,
            IsRental = companyMode?.IsRental ?? true,
            IsViolations = companyMode?.IsViolations ?? true,
            CreatedAt = company.CreatedAt,
            UpdatedAt = company.UpdatedAt
        };

        return Ok(companyDto);
    }

    /// <summary>
    /// Get rental company by email (public endpoint, no auth required)
    /// </summary>
    [HttpGet("email/{email}")]
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public async Task<ActionResult<RentalCompanyDto>> GetRentalCompanyByEmail(string email)
    {
        var company = await _context.Companies
            .FirstOrDefaultAsync(c => c.Email == email);

        if (company == null)
            return NotFound();

        // Load CompanyMode
        var companyMode = await _context.CompanyModes
            .FirstOrDefaultAsync(cm => cm.CompanyId == company.Id);

        var companyDto = new RentalCompanyDto
        {
            CompanyId = company.Id,
            CompanyName = company.CompanyName,
            Email = company.Email,
            Website = company.Website,
            StripeAccountId = null,
            StripeSettingsId = company.StripeSettingsId,
            TaxId = company.TaxId,
            VideoLink = company.VideoLink,
            BannerLink = company.BannerLink,
            LogoLink = company.LogoLink,
            Motto = company.Motto,
            MottoDescription = company.MottoDescription,
            Invitation = company.Invitation,
            Texts = company.Texts,
            BackgroundLink = company.BackgroundLink,
            About = company.About,
            TermsOfUse = company.TermsOfUse,
            BookingIntegrated = company.BookingIntegrated,
            CompanyPath = company.CompanyPath,
            Subdomain = company.Subdomain,
            PrimaryColor = company.PrimaryColor,
            SecondaryColor = company.SecondaryColor,
            Currency = company.Currency,
            LogoUrl = company.LogoUrl,
            FaviconUrl = company.FaviconUrl,
            CustomCss = company.CustomCss,
            Country = company.Country,
            BlinkKey = company.BlinkKey,
            SecurityDeposit = company.SecurityDeposit,
            IsSecurityDepositMandatory = company.IsSecurityDepositMandatory,
            IsActive = company.IsActive,
            IsTestCompany = company.IsTestCompany,
            IsRental = companyMode?.IsRental ?? true,
            IsViolations = companyMode?.IsViolations ?? true,
            CreatedAt = company.CreatedAt,
            UpdatedAt = company.UpdatedAt
        };

        return Ok(companyDto);
    }

    /// <summary>
    /// Send email notification that company with this email already exists (public endpoint, no auth required)
    /// </summary>
    [HttpPost("email/{email}/notify-exists")]
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public async Task<ActionResult> NotifyCompanyExists(string email)
    {
        try
        {
            var decodedEmail = Uri.UnescapeDataString(email);
            var company = await _context.Companies
                .FirstOrDefaultAsync(c => c.Email == decodedEmail);

            if (company == null)
            {
                return NotFound(new { message = "Company not found" });
            }

            // Send email notification
            var subject = "Company Already Exists - Aegis Rental";
            var body = $@"
                <html>
                <body>
                    <h2>Company Already Exists</h2>
                    <p>Hello,</p>
                    <p>A company with the email address <strong>{decodedEmail}</strong> already exists in our system.</p>
                    <p>Company Name: <strong>{company.CompanyName}</strong></p>
                    <p>If you believe this is an error or if you need assistance, please contact our support team.</p>
                    <p>Best regards,<br/>Aegis Rental Team</p>
                </body>
                </html>";

            var emailSent = await _emailService.SendEmailAsync(decodedEmail, subject, body);

            if (emailSent)
            {
                return Ok(new { message = "Email notification sent successfully" });
            }
            else
            {
                return StatusCode(500, new { message = "Failed to send email notification" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending company exists notification to {Email}", email);
            return StatusCode(500, new { message = "An error occurred while sending notification" });
        }
    }

    /// <summary>
    /// Check if the current user is an Aegis admin user
    /// </summary>
    /// <returns>True if user is an Aegis admin (agent, admin, mainadmin, or designer)</returns>
    private bool IsAegisAdmin()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userGuid))
        {
            return false;
        }

        // Check if user exists in AegisUsers table
        var aegisUser = _context.AegisUsers.FirstOrDefault(u => u.Id == userGuid);
        
        if (aegisUser != null)
        {
            // All Aegis user roles (agent, admin, mainadmin, designer) can create companies
            var role = aegisUser.Role?.ToLowerInvariant();
            _logger.LogInformation("IsAegisAdmin: Aegis user found. Role: {Role}, UserId: {UserId}", role, userGuid);
            return true; // Any Aegis user role can create companies
        }

        return false;
    }

    /// <summary>
    /// Create a new rental company (Aegis admin users only)
    /// </summary>
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<RentalCompanyDto>> CreateRentalCompany(CreateRentalCompanyDto createCompanyDto)
    {
        // Check if user is an Aegis admin
        if (!IsAegisAdmin())
        {
            _logger.LogWarning("CreateRentalCompany: Unauthorized attempt by non-Aegis admin user");
            return Forbid("Only Aegis admin users can create companies");
        }

        // Check if company with email already exists
        var existingCompany = await _context.Companies
            .FirstOrDefaultAsync(c => c.Email == createCompanyDto.Email);

        if (existingCompany != null)
            return Conflict("Company with this email already exists");

        // Validate subdomain if provided
        string? subdomain = null;
        if (!string.IsNullOrWhiteSpace(createCompanyDto.Subdomain))
        {
            // Normalize subdomain: lowercase and trim
            subdomain = createCompanyDto.Subdomain.ToLower().Trim();
            
            // Validate subdomain format (alphanumeric and hyphens only, no spaces)
            if (!System.Text.RegularExpressions.Regex.IsMatch(subdomain, @"^[a-z0-9-]+$"))
            {
                return BadRequest("Subdomain can only contain lowercase letters, numbers, and hyphens");
            }
            
            // Check if subdomain already exists in database
            var existingSubdomain = await _context.Companies
                .FirstOrDefaultAsync(c => c.Subdomain != null && c.Subdomain.ToLower() == subdomain);

            if (existingSubdomain != null)
            {
                return Conflict($"Subdomain '{subdomain}' already exists in database");
            }
            
            // Check if subdomain already exists in Azure DNS (required check - must pass)
            if (_azureDnsService != null)
            {
                try
                {
                    var existsInAzure = await _azureDnsService.SubdomainExistsAsync(subdomain);
                    if (existsInAzure)
                    {
                        return Conflict($"Subdomain '{subdomain}' already exists in Azure DNS");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error checking Azure DNS for subdomain availability: {Subdomain}", subdomain);
                    return StatusCode(500, $"Error checking subdomain availability in Azure DNS: {ex.Message}. " +
                        "Please ensure Azure DNS is properly configured and accessible.");
                }
            }
            else
            {
                _logger.LogWarning("Azure DNS service is not available. Cannot verify subdomain '{Subdomain}' in Azure DNS.", subdomain);
                return StatusCode(503, "Azure DNS service is not configured. Cannot verify subdomain availability.");
            }
        }

        // Resolve StripeSettingsId based on IsTestCompany flag and country code
        var stripeSettingsId = await ResolveStripeSettingsIdAsync(
            createCompanyDto.IsTestCompany ?? true,
            string.IsNullOrWhiteSpace(createCompanyDto.Country) 
                ? null 
                : CountryHelper.NormalizeToIsoCode(createCompanyDto.Country));

        var company = new Company
        {
            CompanyName = createCompanyDto.CompanyName,
            Email = createCompanyDto.Email,
            Website = createCompanyDto.Website,
            TaxId = createCompanyDto.TaxId,
            VideoLink = createCompanyDto.VideoLink,
            BannerLink = createCompanyDto.BannerLink,
            LogoLink = createCompanyDto.LogoLink,
            Motto = createCompanyDto.Motto,
            MottoDescription = createCompanyDto.MottoDescription,
            Invitation = createCompanyDto.Invitation,
            Texts = createCompanyDto.Texts,
            BackgroundLink = createCompanyDto.BackgroundLink,
            About = createCompanyDto.About,
            BookingIntegrated = createCompanyDto.BookingIntegrated ? "true" : "false",
            CompanyPath = createCompanyDto.CompanyPath,
            Subdomain = subdomain, // Use validated and normalized subdomain
            PrimaryColor = createCompanyDto.PrimaryColor,
            SecondaryColor = createCompanyDto.SecondaryColor,
            LogoUrl = createCompanyDto.LogoUrl,
            FaviconUrl = createCompanyDto.FaviconUrl,
            CustomCss = createCompanyDto.CustomCss,
            Country = string.IsNullOrWhiteSpace(createCompanyDto.Country) 
                ? null 
                : CountryHelper.NormalizeToIsoCode(createCompanyDto.Country),
            Currency = CurrencyHelper.ResolveCurrency(createCompanyDto.Currency, createCompanyDto.Country),
            SecurityDeposit = createCompanyDto.SecurityDeposit ?? 1000m,
            IsSecurityDepositMandatory = createCompanyDto.IsSecurityDepositMandatory ?? true,
            IsTestCompany = createCompanyDto.IsTestCompany ?? true,
            StripeSettingsId = stripeSettingsId,
            IsActive = true
        };

        try
        {
            _context.Companies.Add(company);
            await _context.SaveChangesAsync();

            // Create or update CompanyMode - Always ensure it exists
            try
            {
                _logger.LogInformation("Creating CompanyMode for new company {CompanyId}: Request.IsRental={IsRental}, Request.IsViolations={IsViolations}", 
                    company.Id, createCompanyDto.IsRental, createCompanyDto.IsViolations);
                
                var companyMode = await _context.CompanyModes
                    .FirstOrDefaultAsync(cm => cm.CompanyId == company.Id);
                
                if (companyMode == null)
                {
                    // Create new CompanyMode
                    companyMode = new CompanyMode
                    {
                        CompanyId = company.Id,
                        IsRental = createCompanyDto.IsRental ?? true,
                        IsViolations = createCompanyDto.IsViolations ?? true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.CompanyModes.Add(companyMode);
                    _logger.LogInformation("Adding CompanyMode to context for new company {CompanyId}: IsRental={IsRental}, IsViolations={IsViolations}, CompanyId={CompanyId}", 
                        company.Id, companyMode.IsRental, companyMode.IsViolations, companyMode.CompanyId);
                    
                    var saveResult = await _context.SaveChangesAsync();
                    _logger.LogInformation("Successfully created CompanyMode for new company {CompanyId}. SaveChangesAsync returned {SaveResult}", 
                        company.Id, saveResult);
                }
                else
                {
                    // Update existing CompanyMode (shouldn't happen for new companies, but handle it)
                    bool changed = false;
                    if (createCompanyDto.IsRental.HasValue && createCompanyDto.IsRental.Value != companyMode.IsRental)
                    {
                        companyMode.IsRental = createCompanyDto.IsRental.Value;
                        changed = true;
                    }
                    if (createCompanyDto.IsViolations.HasValue && createCompanyDto.IsViolations.Value != companyMode.IsViolations)
                    {
                        companyMode.IsViolations = createCompanyDto.IsViolations.Value;
                        changed = true;
                    }
                    
                    if (changed)
                    {
                        companyMode.UpdatedAt = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("Updated CompanyMode for new company {CompanyId}: IsRental={IsRental}, IsViolations={IsViolations}", 
                            company.Id, companyMode.IsRental, companyMode.IsViolations);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving CompanyMode for new company {CompanyId}: {Message}. Stack trace: {StackTrace}", 
                    company.Id, ex.Message, ex.StackTrace);
                // Don't fail the entire creation if CompanyMode save fails, but log it
            }

            // Create Stripe Connect account
            try
            {
                // Validate subdomain is present (required for Stripe account identification)
                if (!string.IsNullOrWhiteSpace(company.Subdomain))
                {
                    // Construct full domain name from subdomain
                    string? fullDomainName = $"{company.Subdomain.ToLower()}.aegis-rental.com";

                    var stripeAccount = await _stripeService.CreateConnectedAccountAsync(
                        company.Subdomain, 
                        "express", // Use express account type (email collected during onboarding)
                        createCompanyDto.Country ?? "US", // Use country from DTO or default to US
                        company.Id, // companyId
                        fullDomainName // domain name
                    );
                    
                    // Get or create StripeCompany record
                    if (company.StripeSettingsId == null)
                    {
                        _logger.LogWarning("Cannot create Stripe account for company {CompanyId}: StripeSettingsId is missing", company.Id);
                    }
                    else
                    {
                        var stripeCompany = await _context.StripeCompanies
                            .FirstOrDefaultAsync(sc => sc.CompanyId == company.Id && sc.SettingsId == company.StripeSettingsId.Value);

                        if (stripeCompany == null)
                        {
                            stripeCompany = new CarRental.Api.Models.StripeCompany
                            {
                                CompanyId = company.Id,
                                SettingsId = company.StripeSettingsId.Value,
                                CreatedAt = DateTime.UtcNow,
                                UpdatedAt = DateTime.UtcNow
                            };
                            _context.StripeCompanies.Add(stripeCompany);
                        }

                        stripeCompany.StripeAccountId = _encryptionService.Encrypt(stripeAccount.Id);
                        stripeCompany.UpdatedAt = DateTime.UtcNow;
                    }
                    
                    company.StripeAccountType = stripeAccount.Type;
                    _context.Companies.Update(company);
                    await _context.SaveChangesAsync();
                }
                else
                {
                    _logger.LogWarning("Cannot create Stripe account for company {CompanyId}: subdomain is missing", company.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create Stripe Connect account for company {CompanyId}", company.Id);
                // Continue without Stripe account for now
            }

            // Create Azure DNS subdomain if subdomain is provided
            if (!string.IsNullOrWhiteSpace(company.Subdomain) && _azureDnsService != null)
            {
                try
                {
                    var dnsCreated = await _azureDnsService.CreateSubdomainAsync(company.Subdomain);
                    if (dnsCreated)
                    {
                        _logger.LogInformation("Azure DNS subdomain created: {Subdomain} for company {CompanyId}", 
                            company.Subdomain, company.Id);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to create Azure DNS subdomain: {Subdomain} for company {CompanyId}. " +
                            "Subdomain may already exist in Azure DNS.", company.Subdomain, company.Id);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating Azure DNS subdomain: {Subdomain} for company {CompanyId}. " +
                        "Company was created successfully, but DNS record creation failed.", 
                        company.Subdomain, company.Id);
                    // Don't fail company creation if DNS creation fails
                }
            }

            // Reload CompanyMode to get the actual saved values
            var savedCompanyMode = await _context.CompanyModes
                .FirstOrDefaultAsync(cm => cm.CompanyId == company.Id);

            var companyDto = new RentalCompanyDto
            {
                CompanyId = company.Id,
                CompanyName = company.CompanyName,
                Email = company.Email,
                Website = company.Website,
                StripeAccountId = null,
                TaxId = company.TaxId,
                VideoLink = company.VideoLink,
                BannerLink = company.BannerLink,
                LogoLink = company.LogoLink,
                Motto = company.Motto,
                MottoDescription = company.MottoDescription,
                Invitation = company.Invitation,
                Texts = company.Texts,
                BackgroundLink = company.BackgroundLink,
                About = company.About,
                TermsOfUse = company.TermsOfUse,
                BookingIntegrated = company.BookingIntegrated,
                CompanyPath = company.CompanyPath,
                Subdomain = company.Subdomain,
                PrimaryColor = company.PrimaryColor,
                SecondaryColor = company.SecondaryColor,
                Currency = company.Currency,
                LogoUrl = company.LogoUrl,
                FaviconUrl = company.FaviconUrl,
                CustomCss = company.CustomCss,
                Country = company.Country,
                BlinkKey = company.BlinkKey,
                SecurityDeposit = company.SecurityDeposit,
                IsSecurityDepositMandatory = company.IsSecurityDepositMandatory,
                IsActive = company.IsActive,
                IsTestCompany = company.IsTestCompany,
                IsRental = savedCompanyMode?.IsRental ?? true,
                IsViolations = savedCompanyMode?.IsViolations ?? true,
                CreatedAt = company.CreatedAt,
                UpdatedAt = company.UpdatedAt
            };

            return CreatedAtAction(nameof(GetRentalCompany), new { id = company.Id }, companyDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating rental company");
            return BadRequest("Error creating rental company");
        }
    }

    /// <summary>
    /// Update a rental company
    /// </summary>
    [HttpPut("{id}")]
    [RequestSizeLimit(10_000_000)] // 10MB limit to handle large base64 images (2MB image = ~2.67MB base64)
    public async Task<IActionResult> UpdateRentalCompany(Guid id, UpdateRentalCompanyDto updateCompanyDto)
    {
        _logger.LogInformation("UpdateRentalCompany called for company ID: {CompanyId}", id);
        _logger.LogInformation("UpdateRentalCompany - SecurityDeposit={SecurityDeposit}, IsSecurityDepositMandatory={IsSecurityDepositMandatory}", 
            updateCompanyDto.SecurityDeposit, updateCompanyDto.IsSecurityDepositMandatory);
        _logger.LogInformation("UpdateRentalCompany - BannerLink length: {BannerLength}, BackgroundLink length: {BackgroundLength}, LogoUrl length: {LogoLength}, FaviconUrl length: {FaviconLength}",
            updateCompanyDto.BannerLink?.Length ?? 0,
            updateCompanyDto.BackgroundLink?.Length ?? 0,
            updateCompanyDto.LogoUrl?.Length ?? 0,
            updateCompanyDto.FaviconUrl?.Length ?? 0);
        _logger.LogInformation("UpdateRentalCompany - BannerLink starts with data: {IsDataUrl}, BackgroundLink starts with data: {IsBackgroundDataUrl}",
            updateCompanyDto.BannerLink?.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ?? false,
            updateCompanyDto.BackgroundLink?.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ?? false);
        
        var company = await _context.Companies.FindAsync(id);

        if (company == null)
            return NotFound();

        // Check if email is being changed and if it already exists
        if (!string.IsNullOrEmpty(updateCompanyDto.Email) && updateCompanyDto.Email != company.Email)
        {
            var existingCompany = await _context.Companies
                .FirstOrDefaultAsync(c => c.Email == updateCompanyDto.Email && c.Id != id);

            if (existingCompany != null)
                return Conflict("Company with this email already exists");
        }

        var originalCountry = company.Country;
        var countryUpdated = false;
        var originalSubdomain = company.Subdomain;

        // Update fields
        if (!string.IsNullOrEmpty(updateCompanyDto.CompanyName))
            company.CompanyName = updateCompanyDto.CompanyName;

        if (!string.IsNullOrEmpty(updateCompanyDto.Email))
            company.Email = updateCompanyDto.Email;

        if (updateCompanyDto.Website != null)
            company.Website = updateCompanyDto.Website;

        if (updateCompanyDto.TaxId != null)
            company.TaxId = updateCompanyDto.TaxId;

        // Resolve StripeSettingsId if IsTestCompany or Country changed
        var isTestCompanyChanged = updateCompanyDto.IsTestCompany.HasValue && updateCompanyDto.IsTestCompany.Value != company.IsTestCompany;
        var countryChanged = !string.IsNullOrWhiteSpace(updateCompanyDto.Country) && 
            CountryHelper.NormalizeToIsoCode(updateCompanyDto.Country) != company.Country;
        
        if (isTestCompanyChanged || countryChanged)
        {
            var newStripeSettingsId = await ResolveStripeSettingsIdAsync(
                updateCompanyDto.IsTestCompany ?? company.IsTestCompany,
                !string.IsNullOrWhiteSpace(updateCompanyDto.Country) 
                    ? CountryHelper.NormalizeToIsoCode(updateCompanyDto.Country) 
                    : company.Country);
            company.StripeSettingsId = newStripeSettingsId;
            _logger.LogInformation("Resolved StripeSettingsId: {StripeSettingsId} for company {CompanyId} (IsTestCompany: {IsTestCompany}, Country: {Country})",
                newStripeSettingsId, id, updateCompanyDto.IsTestCompany ?? company.IsTestCompany, 
                !string.IsNullOrWhiteSpace(updateCompanyDto.Country) ? CountryHelper.NormalizeToIsoCode(updateCompanyDto.Country) : company.Country);
        }
        else if (updateCompanyDto.StripeSettingsId.HasValue)
        {
            company.StripeSettingsId = updateCompanyDto.StripeSettingsId.Value;
        }
        else if (updateCompanyDto.StripeSettingsId == null && updateCompanyDto.GetType().GetProperty("StripeSettingsId")?.GetValue(updateCompanyDto) == null)
        {
            // Explicitly set to null if the property was included in the request with null value
            // This allows clearing the StripeSettingsId
            company.StripeSettingsId = null;
        }

        var publicBaseUrl = GetPublicBaseUrl();

        if (updateCompanyDto.VideoLink != null)
            company.VideoLink = await NormalizeAndSaveAssetAsync(company.Id, "video", updateCompanyDto.VideoLink, publicBaseUrl);

        if (updateCompanyDto.BannerLink != null)
            company.BannerLink = await NormalizeAndSaveAssetAsync(company.Id, "banner", updateCompanyDto.BannerLink, publicBaseUrl);

        if (updateCompanyDto.LogoLink != null)
            company.LogoLink = updateCompanyDto.LogoLink;

        if (updateCompanyDto.Motto != null)
            company.Motto = updateCompanyDto.Motto;

        if (updateCompanyDto.MottoDescription != null)
            company.MottoDescription = updateCompanyDto.MottoDescription;

        if (updateCompanyDto.Invitation != null)
            company.Invitation = updateCompanyDto.Invitation;

        if (updateCompanyDto.Texts != null)
            company.Texts = updateCompanyDto.Texts;

        if (updateCompanyDto.BackgroundLink != null)
            company.BackgroundLink = await NormalizeAndSaveAssetAsync(company.Id, "background", updateCompanyDto.BackgroundLink, publicBaseUrl);

        if (updateCompanyDto.About != null)
            company.About = updateCompanyDto.About;

        if (updateCompanyDto.TermsOfUse != null)
            company.TermsOfUse = updateCompanyDto.TermsOfUse;

        if (updateCompanyDto.BookingIntegrated.HasValue)
            company.BookingIntegrated = updateCompanyDto.BookingIntegrated.Value ? "true" : "false";

        if (updateCompanyDto.CompanyPath != null)
            company.CompanyPath = updateCompanyDto.CompanyPath;

        // Handle subdomain changes and Azure DNS updates
        if (updateCompanyDto.Subdomain != null)
        {
            string? newSubdomain = string.IsNullOrWhiteSpace(updateCompanyDto.Subdomain) 
                ? null 
                : updateCompanyDto.Subdomain.ToLower().Trim();
            
            // Validate subdomain format if provided
            if (!string.IsNullOrWhiteSpace(newSubdomain))
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(newSubdomain, @"^[a-z0-9-]+$"))
                {
                    return BadRequest("Subdomain can only contain lowercase letters, numbers, and hyphens");
                }
                
                // Check if subdomain already exists in database (excluding current company)
                var existingSubdomain = await _context.Companies
                    .FirstOrDefaultAsync(c => c.Subdomain != null && c.Subdomain.ToLower() == newSubdomain && c.Id != id);
                
                if (existingSubdomain != null)
                {
                    return Conflict($"Subdomain '{newSubdomain}' already exists in database");
                }
                
                // Check if subdomain already exists in Azure DNS (required check - must pass)
                if (_azureDnsService != null)
                {
                    try
                    {
                        var existsInAzure = await _azureDnsService.SubdomainExistsAsync(newSubdomain);
                        if (existsInAzure)
                        {
                            return Conflict($"Subdomain '{newSubdomain}' already exists in Azure DNS");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error checking Azure DNS for subdomain availability: {Subdomain}", newSubdomain);
                        return StatusCode(500, $"Error checking subdomain availability in Azure DNS: {ex.Message}. " +
                            "Please ensure Azure DNS is properly configured and accessible.");
                    }
                }
                else
                {
                    _logger.LogWarning("Azure DNS service is not available. Cannot verify subdomain '{Subdomain}' in Azure DNS.", newSubdomain);
                    return StatusCode(503, "Azure DNS service is not configured. Cannot verify subdomain availability.");
                }
            }
            
            if (newSubdomain != company.Subdomain)
            {
                // If subdomain is being changed and Azure DNS service is available
                if (_azureDnsService != null)
                {
                    try
                    {
                        // Delete old DNS record if it exists
                        if (!string.IsNullOrWhiteSpace(originalSubdomain))
                        {
                            try
                            {
                                await _azureDnsService.DeleteSubdomainAsync(originalSubdomain);
                                _logger.LogInformation("Deleted old Azure DNS subdomain: {OldSubdomain} for company {CompanyId}", 
                                    originalSubdomain, id);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to delete old Azure DNS subdomain: {OldSubdomain} for company {CompanyId}", 
                                    originalSubdomain, id);
                            }
                        }
                        
                        // Create new DNS record if new subdomain is provided
                        if (!string.IsNullOrWhiteSpace(newSubdomain))
                        {
                            // If setting subdomain for the first time (originalSubdomain is empty), use CreateSubdomainWithSslAsync
                            // to fully set up DNS, App Service binding, and SSL (same as company creation)
                            // Run in background to avoid timeout - fire and forget
                            if (string.IsNullOrWhiteSpace(originalSubdomain))
                            {
                                var subdomainToSetup = newSubdomain;
                                var companyIdToSetup = id;
                                
                                // Fire and forget - run in background to avoid timeout (same as CreateCompany)
                                _ = Task.Run(async () =>
                                {
                                    try
                                    {
                                        _logger.LogInformation("Starting background domain setup for subdomain: {Subdomain}, company: {CompanyId}", 
                                            subdomainToSetup, companyIdToSetup);
                                        
                                        // Use CreateSubdomainWithSslAsync to fully set up DNS, App Service binding, and SSL
                                        var dnsCreated = await _azureDnsService.CreateSubdomainWithSslAsync(subdomainToSetup);
                                        if (dnsCreated)
                                        {
                                            _logger.LogInformation("Azure DNS subdomain with SSL created: {Subdomain} for company {CompanyId}", 
                                                subdomainToSetup, companyIdToSetup);
                                        }
                                        else
                                        {
                                            _logger.LogWarning("Failed to create Azure DNS subdomain with SSL: {Subdomain} for company {CompanyId}. " +
                                                "Subdomain may already exist in Azure DNS.", subdomainToSetup, companyIdToSetup);
                                        }
                                    }
                                    catch (ArgumentException ex) when (ex.Message.Contains("not configured"))
                                    {
                                        // Azure DNS is not configured - log warning but don't fail company update
                                        _logger.LogWarning("Azure DNS service is not configured. Cannot create subdomain '{Subdomain}' for company {CompanyId}. " +
                                            "Company was updated successfully, but DNS/SSL setup was skipped. Error: {Error}", 
                                            subdomainToSetup, companyIdToSetup, ex.Message);
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogError(ex, "Error creating Azure DNS subdomain with SSL: {Subdomain} for company {CompanyId}. " +
                                            "Company was updated successfully, but DNS/SSL setup failed.", 
                                            subdomainToSetup, companyIdToSetup);
                                        // Don't fail company update if DNS creation fails
                                    }
                                });
                                
                                _logger.LogInformation("Domain setup initiated in background for subdomain: {Subdomain}, company: {CompanyId}", 
                                    subdomainToSetup, companyIdToSetup);
                            }
                            else
                            {
                                // If changing existing subdomain, just create the DNS record synchronously (quick operation)
                                var dnsCreated = await _azureDnsService.CreateSubdomainAsync(newSubdomain);
                                
                                if (dnsCreated)
                                {
                                    _logger.LogInformation("Created new Azure DNS subdomain: {NewSubdomain} for company {CompanyId}", 
                                        newSubdomain, id);
                                }
                                else
                                {
                                    _logger.LogWarning("Failed to create Azure DNS subdomain: {NewSubdomain} for company {CompanyId}. " +
                                        "Subdomain may already exist in Azure DNS.", newSubdomain, id);
                                    return StatusCode(500, $"Failed to create Azure DNS subdomain: '{newSubdomain}'. " +
                                        "Subdomain may already exist in Azure DNS.");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error updating Azure DNS subdomain for company {CompanyId}. " +
                            "Old: {OldSubdomain}, New: {NewSubdomain}", 
                            id, originalSubdomain, newSubdomain);
                        return StatusCode(500, $"Error updating subdomain in Azure DNS: {ex.Message}. " +
                            "Please ensure Azure DNS is properly configured and accessible.");
                    }
                }
                else
                {
                    _logger.LogWarning("Azure DNS service is not available. Cannot update subdomain '{Subdomain}' in Azure DNS.", newSubdomain);
                    return StatusCode(503, "Azure DNS service is not configured. Cannot update subdomain in Azure DNS.");
                }
                
                company.Subdomain = newSubdomain;
            }
        }

        if (updateCompanyDto.PrimaryColor != null)
            company.PrimaryColor = updateCompanyDto.PrimaryColor;

        if (updateCompanyDto.SecondaryColor != null)
            company.SecondaryColor = updateCompanyDto.SecondaryColor;

        if (updateCompanyDto.LogoUrl != null)
            company.LogoUrl = await NormalizeAndSaveAssetAsync(company.Id, "logo", updateCompanyDto.LogoUrl, publicBaseUrl);

        if (updateCompanyDto.FaviconUrl != null)
            company.FaviconUrl = await NormalizeAndSaveAssetAsync(company.Id, "favicon", updateCompanyDto.FaviconUrl, publicBaseUrl);

        if (updateCompanyDto.CustomCss != null)
            company.CustomCss = updateCompanyDto.CustomCss;

        if (updateCompanyDto.Country != null)
        {
            var normalizedCountry = string.IsNullOrWhiteSpace(updateCompanyDto.Country) 
                ? null 
                : CountryHelper.NormalizeToIsoCode(updateCompanyDto.Country);
            company.Country = normalizedCountry;
            countryUpdated = !string.Equals(originalCountry, normalizedCountry, StringComparison.OrdinalIgnoreCase);
        }

        if (!string.IsNullOrWhiteSpace(updateCompanyDto.Currency))
        {
            company.Currency = CurrencyHelper.ResolveCurrency(updateCompanyDto.Currency, company.Country);
        }
        else if (countryUpdated)
        {
            company.Currency = CurrencyHelper.GetCurrencyForCountry(company.Country);
        }

        if (updateCompanyDto.Language != null)
            company.Language = updateCompanyDto.Language;

        if (updateCompanyDto.BlinkKey != null)
            company.BlinkKey = updateCompanyDto.BlinkKey;

        if (updateCompanyDto.IsActive.HasValue)
            company.IsActive = updateCompanyDto.IsActive.Value;

        if (updateCompanyDto.SecurityDeposit.HasValue)
            company.SecurityDeposit = updateCompanyDto.SecurityDeposit.Value;

        // Always update IsSecurityDepositMandatory if provided
        if (updateCompanyDto.IsSecurityDepositMandatory.HasValue)
        {
            _logger.LogInformation("Updating IsSecurityDepositMandatory: Current={Current}, New={New}", 
                company.IsSecurityDepositMandatory, updateCompanyDto.IsSecurityDepositMandatory.Value);
            company.IsSecurityDepositMandatory = updateCompanyDto.IsSecurityDepositMandatory.Value;
        }

        // Always update IsTestCompany if provided
        if (updateCompanyDto.IsTestCompany.HasValue)
        {
            _logger.LogInformation("Updating IsTestCompany: Current={Current}, New={New}", 
                company.IsTestCompany, updateCompanyDto.IsTestCompany.Value);
            company.IsTestCompany = updateCompanyDto.IsTestCompany.Value;
        }

        company.UpdatedAt = DateTime.UtcNow;

        try
        {
            _context.Companies.Update(company);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Successfully saved company {CompanyId}. IsSecurityDepositMandatory is now: {IsSecurityDepositMandatory}", 
                company.Id, company.IsSecurityDepositMandatory);

            // Update CompanyMode - Always ensure it exists
            CompanyMode? companyMode = null;
            try
            {
                _logger.LogInformation("Updating CompanyMode for company {CompanyId}: Request.IsRental={IsRental}, Request.IsViolations={IsViolations}", 
                    company.Id, updateCompanyDto.IsRental, updateCompanyDto.IsViolations);
                
                companyMode = await _context.CompanyModes
                    .FirstOrDefaultAsync(cm => cm.CompanyId == company.Id);
                
                if (companyMode == null)
                {
                    // Create new CompanyMode if it doesn't exist (with default or provided values)
                    companyMode = new CompanyMode
                    {
                        CompanyId = company.Id,
                        IsRental = updateCompanyDto.IsRental ?? true,
                        IsViolations = updateCompanyDto.IsViolations ?? true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.CompanyModes.Add(companyMode);
                    _logger.LogInformation("Creating CompanyMode for company {CompanyId}: IsRental={IsRental}, IsViolations={IsViolations}", 
                        company.Id, companyMode.IsRental, companyMode.IsViolations);
                }
                else
                {
                    // Update existing CompanyMode if values are provided
                    bool changed = false;
                    if (updateCompanyDto.IsRental.HasValue && updateCompanyDto.IsRental.Value != companyMode.IsRental)
                    {
                        companyMode.IsRental = updateCompanyDto.IsRental.Value;
                        changed = true;
                    }
                    if (updateCompanyDto.IsViolations.HasValue && updateCompanyDto.IsViolations.Value != companyMode.IsViolations)
                    {
                        companyMode.IsViolations = updateCompanyDto.IsViolations.Value;
                        changed = true;
                    }
                    
                    if (changed)
                    {
                        companyMode.UpdatedAt = DateTime.UtcNow;
                        _logger.LogInformation("Updating CompanyMode for company {CompanyId}: IsRental={IsRental}, IsViolations={IsViolations}", 
                            company.Id, companyMode.IsRental, companyMode.IsViolations);
                    }
                }
                
                await _context.SaveChangesAsync();
                _logger.LogInformation("Successfully saved CompanyMode for company {CompanyId}", company.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving CompanyMode for company {CompanyId}: {Message}. Stack trace: {StackTrace}", 
                    company.Id, ex.Message, ex.StackTrace);
                // Try to reload companyMode if save failed to get current values
                try
                {
                    companyMode = await _context.CompanyModes
                        .FirstOrDefaultAsync(cm => cm.CompanyId == company.Id);
                }
                catch (Exception reloadEx)
                {
                    _logger.LogError(reloadEx, "Error reloading CompanyMode for company {CompanyId}", company.Id);
                }
            }

            // Update Stripe Connect account if exists
            if (!string.IsNullOrEmpty(company.StripeAccountId))
            {
                var stripeAccountId = await ResolveStripeAccountIdAsync(company);
                if (!string.IsNullOrEmpty(stripeAccountId))
                {
                    try
                    {
                        await _stripeService.GetAccountAsync(stripeAccountId, company.Id);
                        // Additional Stripe account updates can be added here
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to update Stripe Connect account for company {CompanyId}", company.Id);
                    }
                }
            }

            // Reload company and CompanyMode to return updated values
            var updatedCompany = await _context.Companies.FindAsync(id);
            if (updatedCompany == null)
                return NotFound();

            var updatedCompanyMode = await _context.CompanyModes
                .FirstOrDefaultAsync(cm => cm.CompanyId == id);

            var updatedDto = new RentalCompanyDto
            {
                CompanyId = updatedCompany.Id,
                CompanyName = updatedCompany.CompanyName,
                Email = updatedCompany.Email,
                Website = updatedCompany.Website,
                StripeAccountId = null,
                StripeSettingsId = updatedCompany.StripeSettingsId,
                TaxId = updatedCompany.TaxId,
                VideoLink = updatedCompany.VideoLink,
                BannerLink = updatedCompany.BannerLink,
                LogoLink = updatedCompany.LogoLink,
                Motto = updatedCompany.Motto,
                MottoDescription = updatedCompany.MottoDescription,
                Invitation = updatedCompany.Invitation,
                Texts = updatedCompany.Texts,
                BackgroundLink = updatedCompany.BackgroundLink,
                About = updatedCompany.About,
                TermsOfUse = updatedCompany.TermsOfUse,
                BookingIntegrated = updatedCompany.BookingIntegrated,
                CompanyPath = updatedCompany.CompanyPath,
                Subdomain = updatedCompany.Subdomain,
                PrimaryColor = updatedCompany.PrimaryColor,
                SecondaryColor = updatedCompany.SecondaryColor,
                Currency = updatedCompany.Currency,
                LogoUrl = updatedCompany.LogoUrl,
                FaviconUrl = updatedCompany.FaviconUrl,
                CustomCss = updatedCompany.CustomCss,
                Country = updatedCompany.Country,
                BlinkKey = updatedCompany.BlinkKey,
                SecurityDeposit = updatedCompany.SecurityDeposit,
                IsSecurityDepositMandatory = updatedCompany.IsSecurityDepositMandatory,
                IsActive = updatedCompany.IsActive,
                IsTestCompany = updatedCompany.IsTestCompany,
                IsRental = updatedCompanyMode?.IsRental ?? true,
                IsViolations = updatedCompanyMode?.IsViolations ?? true,
                CreatedAt = updatedCompany.CreatedAt,
                UpdatedAt = updatedCompany.UpdatedAt
            };

            return Ok(updatedDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating rental company {CompanyId}", id);
            return BadRequest("Error updating rental company");
        }
    }

    /// <summary>
    /// Clear the About field for a rental company
    /// </summary>
    [HttpDelete("{id}/about")]
    [ActionName("ClearAbout")]
    public async Task<IActionResult> ClearRentalCompanyAbout(Guid id)
    {
        try
        {
            var company = await _context.Companies.FindAsync(id);

            if (company == null)
            {
                return NotFound();
            }

            company.About = null;
            company.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("About field cleared for rental company {CompanyId}", id);

            return Ok(new
            {
                id = company.Id,
                companyName = company.CompanyName,
                about = (string?)null,
                updatedAt = company.UpdatedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing about field for rental company {CompanyId}", id);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Update only the Terms of Use field for a rental company
    /// </summary>
    [HttpPut("{id}/terms-of-use")]
    public async Task<IActionResult> UpdateRentalCompanyTermsOfUse(Guid id, [FromBody] UpdateTermsOfUseDto dto)
    {
        try
        {
            var company = await _context.Companies.FindAsync(id);

            if (company == null)
            {
                return NotFound();
            }

            // Allow setting to null by checking if the property was provided
            // If TermsOfUse is explicitly null in the request, clear it
            // If it's a string (empty or with content), set it
            company.TermsOfUse = dto.TermsOfUse;
            company.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Terms of Use updated for rental company {CompanyId}", id);

            return Ok(new
            {
                id = company.Id,
                companyName = company.CompanyName,
                termsOfUse = company.TermsOfUse,
                updatedAt = company.UpdatedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating Terms of Use for rental company {CompanyId}", id);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Clear the Terms of Use field for a rental company
    /// </summary>
    [HttpDelete("{id}/terms-of-use")]
    [ActionName("ClearTermsOfUse")]
    public async Task<IActionResult> ClearRentalCompanyTermsOfUse(Guid id)
    {
        try
        {
            var company = await _context.Companies.FindAsync(id);

            if (company == null)
            {
                return NotFound();
            }

            company.TermsOfUse = null;
            company.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Terms of Use cleared for rental company {CompanyId}", id);

            return Ok(new
            {
                id = company.Id,
                companyName = company.CompanyName,
                termsOfUse = (string?)null,
                updatedAt = company.UpdatedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing Terms of Use for rental company {CompanyId}", id);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Delete a rental company
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteRentalCompany(Guid id)
    {
        var company = await _context.Companies.FindAsync(id);

        if (company == null)
            return NotFound();

        // Check if company has active vehicles, reservations, or rentals
        var hasActiveVehicles = await _context.Vehicles
            .AnyAsync(v => v.CompanyId == id && v.Status != VehicleStatus.OutOfService);

        var hasActiveReservations = await _context.Bookings
            .AnyAsync(r => r.CompanyId == id && r.Status == "Confirmed");

        var hasActiveRentals = await _context.Rentals
            .AnyAsync(r => r.CompanyId == id && r.Status == "active");

        if (hasActiveVehicles || hasActiveReservations || hasActiveRentals)
            return BadRequest("Cannot delete company with active vehicles, reservations, or rentals");

        try
        {
            _context.Companies.Remove(company);
            await _context.SaveChangesAsync();
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting rental company {CompanyId}", id);
            return BadRequest("Error deleting rental company");
        }
    }

    /// <summary>
    /// Activate/deactivate a rental company
    /// </summary>
    [HttpPost("{id}/toggle-status")]
    public async Task<IActionResult> ToggleCompanyStatus(Guid id)
    {
        var company = await _context.Companies.FindAsync(id);

        if (company == null)
            return NotFound();

        company.IsActive = !company.IsActive;
        company.UpdatedAt = DateTime.UtcNow;

        try
        {
            _context.Companies.Update(company);
            await _context.SaveChangesAsync();
            return Ok(new { IsActive = company.IsActive });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling company status {CompanyId}", id);
            return BadRequest("Error updating company status");
        }
    }

    /// <summary>
    /// Get company statistics
    /// </summary>
    [HttpGet("{id}/stats")]
    public async Task<ActionResult<object>> GetCompanyStats(Guid id)
    {
        var company = await _context.Companies.FindAsync(id);

        if (company == null)
            return NotFound();

        var stats = new
        {
            TotalVehicles = await _context.Vehicles.CountAsync(v => v.CompanyId == id),
            ActiveVehicles = await _context.Vehicles.CountAsync(v => v.CompanyId == id && v.Status == VehicleStatus.Available),
            TotalReservations = await _context.Bookings.CountAsync(r => r.CompanyId == id),
            ActiveReservations = await _context.Bookings.CountAsync(r => r.CompanyId == id && r.Status == "Confirmed"),
            TotalRentals = await _context.Rentals.CountAsync(r => r.CompanyId == id),
            ActiveRentals = await _context.Rentals.CountAsync(r => r.CompanyId == id && r.Status == "active"),
            TotalRevenue = await _context.Payments.Where(p => p.CompanyId == id && p.Status == "succeeded")
                .SumAsync(p => p.Amount),
            AverageRating = await _context.Reviews.Where(r => r.CompanyId == id)
                .AverageAsync(r => (double?)r.Rating),
            LastActivity = await _context.Bookings
                .Where(r => r.CompanyId == id)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => r.CreatedAt)
                .FirstOrDefaultAsync()
        };

        return Ok(stats);
    }

    /// <summary>
    /// Get company vehicles
    /// </summary>
    [HttpGet("{id}/vehicles")]
    public async Task<ActionResult<IEnumerable<VehicleDto>>> GetCompanyVehicles(
        Guid id,
        string? status = null,
        int page = 1,
        int pageSize = 20)
    {
        var query = _context.Vehicles
            .Include(v => v.VehicleModel)
            .Where(v => v.CompanyId == id);

        if (!string.IsNullOrEmpty(status))
            query = query.Where(v => v.Status.ToString() == status);

        var allVehicles = await query.ToListAsync();
        
        // Load Model for each VehicleModel that has one
        foreach (var vehicle in allVehicles.Where(v => v.VehicleModel != null))
        {
            var vm = vehicle.VehicleModel!;
            await _context.Entry(vm)
                .Reference(v => v.Model)
                .LoadAsync();
            
            if (vm.Model != null)
            {
                await _context.Entry(vm.Model)
                    .Reference(m => m.Category)
                    .LoadAsync();
            }
        }
        
        // Now order by loaded data and paginate
        var vehiclesList = allVehicles
            .OrderBy(v => v.VehicleModel?.Model?.Make ?? "")
            .ThenBy(v => v.VehicleModel?.Model?.ModelName ?? "")
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var vehicles = vehiclesList.Select(v =>
        {
            var vm = v.VehicleModel;
            var matchingModel = vm?.Model;

            return new VehicleDto
            {
                VehicleId = v.Id,
                CompanyId = v.CompanyId,
                CategoryId = matchingModel?.CategoryId,
                CategoryName = matchingModel?.Category?.CategoryName,
                Make = matchingModel?.Make ?? "",
                Model = matchingModel?.ModelName ?? "",
                Year = matchingModel?.Year ?? 0,
                Color = v.Color,
                LicensePlate = v.LicensePlate,
                Vin = v.Vin,
                Mileage = v.Mileage,
                FuelType = matchingModel?.FuelType,
                Transmission = v.Transmission,
                Seats = v.Seats,
                DailyRate = vm?.DailyRate ?? 0, // Rate from catalog
                Status = v.Status.ToString(),
                Location = v.Location,
                ImageUrl = v.ImageUrl,
                Features = v.Features,
                CreatedAt = v.CreatedAt,
                UpdatedAt = v.UpdatedAt
            };
        }).ToList();

        return Ok(vehicles);
    }

    /// <summary>
    /// Get company reservations
    /// </summary>
    [HttpGet("{id}/reservations")]
    public async Task<ActionResult<IEnumerable<ReservationDto>>> GetCompanyReservations(
        Guid id,
        string? status = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int page = 1,
        int pageSize = 20)
    {
        var company = await _context.Companies.FindAsync(id);
        if (company == null)
            return NotFound();

        var query = _context.Bookings
            .Include(r => r.Customer)
            .Include(r => r.Vehicle)
                .ThenInclude(v => v.VehicleModel)
                    .ThenInclude(vm => vm != null ? vm.Model : null!)
                        .ThenInclude(m => m != null ? m.Category : null!)
            .Where(r => r.CompanyId == id);

        if (!string.IsNullOrEmpty(status))
            query = query.Where(r => r.Status == status);

        if (fromDate.HasValue)
            query = query.Where(r => r.PickupDate >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(r => r.ReturnDate <= toDate.Value);

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
            VehicleId = r.VehicleId,
            CompanyId = r.CompanyId,
            BookingNumber = r.BookingNumber,
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
            UpdatedAt = r.UpdatedAt,
            CustomerName = r.Customer.FirstName + " " + r.Customer.LastName,
            CustomerEmail = r.Customer.Email,
            VehicleMake = r.Vehicle?.VehicleModel?.Model?.Make,
            VehicleModel = r.Vehicle?.VehicleModel?.Model?.ModelName,
            VehicleYear = r.Vehicle?.VehicleModel?.Model?.Year,
            VehicleColor = r.Vehicle?.Color,
            VehicleCategory = r.Vehicle?.VehicleModel?.Model?.Category?.CategoryName,
            VehicleLicensePlate = r.Vehicle?.LicensePlate,
            VehicleName = (r.Vehicle?.VehicleModel?.Model != null) ?
                r.Vehicle.VehicleModel.Model.Make + " " + r.Vehicle.VehicleModel.Model.ModelName + " (" + r.Vehicle.VehicleModel.Model.Year + ")" :
                "Unknown Vehicle", // Deprecated
            LicensePlate = r.Vehicle?.LicensePlate ?? "", // Deprecated: use VehicleLicensePlate instead
            CompanyName = company.CompanyName
        }).ToList();

        return Ok(reservations);
    }

    /// <summary>
    /// Get company revenue report
    /// </summary>
    [HttpGet("{id}/revenue")]
    public async Task<ActionResult<object>> GetCompanyRevenue(
        Guid id,
        DateTime? fromDate = null,
        DateTime? toDate = null)
    {
        var company = await _context.Companies.FindAsync(id);
        if (company == null)
            return NotFound();

        var query = _context.Payments
            .Where(p => p.CompanyId == id && p.Status == "succeeded");

        if (fromDate.HasValue)
            query = query.Where(p => p.CreatedAt >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(p => p.CreatedAt <= toDate.Value);

        var revenue = await query
            .GroupBy(p => p.CreatedAt.Date)
            .Select(g => new
            {
                Date = g.Key,
                TotalAmount = g.Sum(p => p.Amount),
                TransactionCount = g.Count()
            })
            .OrderBy(r => r.Date)
            .ToListAsync();

        var totalRevenue = revenue.Sum(r => r.TotalAmount);
        var totalTransactions = revenue.Sum(r => r.TransactionCount);
        var averageTransaction = totalTransactions > 0 ? totalRevenue / totalTransactions : 0;

        return Ok(new
        {
            CompanyName = company.CompanyName,
            Period = new
            {
                From = fromDate ?? revenue.FirstOrDefault()?.Date,
                To = toDate ?? revenue.LastOrDefault()?.Date
            },
            Summary = new
            {
                TotalRevenue = totalRevenue,
                TotalTransactions = totalTransactions,
                AverageTransaction = averageTransaction
            },
            DailyRevenue = revenue
        });
    }

    /// <summary>
    /// Create Stripe Connect account for company
    /// </summary>
    [HttpPost("{id}/stripe/connect/create")]
    public async Task<ActionResult<object>> CreateConnectAccount(Guid id)
    {
        var (success, accountId, error) = await _stripeConnectService.CreateConnectedAccountAsync(id);
        
        if (!success)
            return BadRequest(new { error });

        return Ok(new
        {
            success = true,
            accountId = accountId,
            message = "Stripe Connect account created successfully"
        });
    }

    /// <summary>
    /// Start Stripe Connect onboarding flow
    /// </summary>
    [HttpPost("{id}/stripe/connect/onboard")]
    public async Task<ActionResult<object>> StartOnboarding(
        Guid id, 
        [FromBody] CreateAccountLinkDto dto)
    {
        if (string.IsNullOrEmpty(dto.ReturnUrl) || string.IsNullOrEmpty(dto.RefreshUrl))
            return BadRequest("ReturnUrl and RefreshUrl are required");

        var (success, onboardingUrl, error) = await _stripeConnectService.StartOnboardingAsync(
            id, 
            dto.ReturnUrl, 
            dto.RefreshUrl
        );

        if (!success)
            return BadRequest(new { error });

        return Ok(new
        {
            success = true,
            onboardingUrl = onboardingUrl,
            expiresAt = DateTime.UtcNow.AddHours(1)
        });
    }

    /// <summary>
    /// Complete onboarding (called after user returns from Stripe)
    /// </summary>
    [HttpPost("{id}/stripe/connect/complete-onboarding")]
    public async Task<ActionResult<object>> CompleteOnboarding(Guid id)
    {
        var company = await _context.Companies.FindAsync(id);
        if (company == null)
            return NotFound();

        if (string.IsNullOrEmpty(company.StripeAccountId))
            return BadRequest("Company does not have a Stripe account");

        try
        {
            var stripeAccountId = await ResolveStripeAccountIdAsync(company);
            if (string.IsNullOrEmpty(stripeAccountId))
                return BadRequest("Unable to retrieve Stripe account ID");

            // Sync account status from Stripe
            await _stripeConnectService.SyncAccountStatusAsync(stripeAccountId);

            // Mark onboarding session as complete
            var session = await _context.StripeOnboardingSessions
                .Where(s => s.CompanyId == id && !s.Completed)
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync();

            if (session != null)
            {
                session.Completed = true;
                session.CompletedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            // Get updated status
            var status = await _stripeConnectService.GetAccountStatusAsync(id);

            return Ok(new
            {
                success = true,
                status = status,
                message = "Onboarding completed successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing onboarding for company {CompanyId}", id);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get detailed Stripe Connect account status
    /// </summary>
    [HttpGet("{id}/stripe/connect/status")]
    public async Task<ActionResult<DTOs.Stripe.StripeAccountStatusDto>> GetConnectAccountStatus(Guid id)
    {
        var status = await _stripeConnectService.GetAccountStatusAsync(id);
        return Ok(status);
    }

    /// <summary>
    /// Sync account status from Stripe
    /// </summary>
    [HttpPost("{id}/stripe/connect/sync")]
    public async Task<ActionResult<object>> SyncAccountStatus(Guid id)
    {
        var company = await _context.Companies.FindAsync(id);
        if (company == null)
            return NotFound();

        if (string.IsNullOrEmpty(company.StripeAccountId))
            return BadRequest("Company does not have a Stripe account");

        try
        {
            var stripeAccountId = await ResolveStripeAccountIdAsync(company);
            if (string.IsNullOrEmpty(stripeAccountId))
                return BadRequest("Unable to retrieve Stripe account ID");

            await _stripeConnectService.SyncAccountStatusAsync(stripeAccountId);

            var status = await _stripeConnectService.GetAccountStatusAsync(id);

            return Ok(new
            {
                success = true,
                status = status,
                syncedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing account status for company {CompanyId}", id);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get Stripe Connect dashboard link (for Express accounts)
    /// </summary>
    [HttpPost("{id}/stripe/connect/dashboard-link")]
    public async Task<ActionResult<object>> GetDashboardLink(Guid id)
    {
        var company = await _context.Companies.FindAsync(id);
        if (company == null)
            return NotFound();

        if (string.IsNullOrEmpty(company.StripeAccountId))
            return BadRequest("Company does not have a Stripe account");

        try
        {
            var stripeAccountId = await ResolveStripeAccountIdAsync(company);
            if (string.IsNullOrEmpty(stripeAccountId))
                return BadRequest("Unable to retrieve Stripe account ID");

            // Create login link for Express dashboard
            var loginLinkService = new AccountLinkService();
            var options = new AccountLinkCreateOptions
            {
                Account = stripeAccountId,
                RefreshUrl = $"{Request.Scheme}://{Request.Host}/company/{id}/stripe/refresh",
                ReturnUrl = $"{Request.Scheme}://{Request.Host}/company/{id}/stripe/return",
                Type = "account_onboarding"
            };

            var loginLink = await loginLinkService.CreateAsync(options);

            return Ok(new
            {
                success = true,
                url = loginLink.Url,
                expiresAt = DateTime.UtcNow.AddHours(1)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating dashboard link for company {CompanyId}", id);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get company financial summary with platform fees
    /// </summary>
    [HttpGet("{id}/financials")]
    public async Task<ActionResult<object>> GetFinancials(
        Guid id,
        DateTime? fromDate = null,
        DateTime? toDate = null)
    {
        var company = await _context.Companies
            .Include(c => c.Bookings)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (company == null)
            return NotFound();

        var query = _context.Bookings
            .Where(b => b.CompanyId == id && 
                       (b.Status == BookingStatus.Confirmed || 
                        b.Status == BookingStatus.PickedUp || 
                        b.Status == BookingStatus.Returned));

        if (fromDate.HasValue)
            query = query.Where(b => b.CreatedAt >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(b => b.CreatedAt <= toDate.Value);

        var bookings = await query.ToListAsync();

        var totalRevenue = bookings.Sum(b => b.TotalAmount);
        var platformFees = bookings.Sum(b => b.PlatformFeeAmount);
        var netRevenue = bookings.Sum(b => b.NetAmount ?? 0m);

        var transfers = await _context.StripeTransfers
            .Where(t => t.CompanyId == id)
            .ToListAsync();

        var transferredAmount = transfers
            .Where(t => t.Status == "paid")
            .Sum(t => t.NetAmount);

        var pendingTransfers = transfers
            .Where(t => t.Status == "pending")
            .Sum(t => t.NetAmount);

        return Ok(new
        {
            companyId = id,
            companyName = company.CompanyName,
            platformFeePercentage = company.PlatformFeePercentage,
            period = new
            {
                from = fromDate ?? bookings.Min(b => (DateTime?)b.CreatedAt),
                to = toDate ?? bookings.Max(b => (DateTime?)b.CreatedAt)
            },
            revenue = new
            {
                totalRevenue = totalRevenue,
                platformFees = platformFees,
                netRevenue = netRevenue
            },
            transfers = new
            {
                transferred = transferredAmount,
                pending = pendingTransfers,
                totalTransfers = transfers.Count
            },
            bookings = new
            {
                total = bookings.Count,
                confirmed = bookings.Count(b => b.Status == BookingStatus.Confirmed),
                completed = bookings.Count(b => b.Status == BookingStatus.Returned)
            }
        });
    }

    /// <summary>
    /// Resolves StripeSettingsId based on IsTestCompany flag and country code
    /// Priority: 1. Test settings if IsTestCompany is true, 2. Country-specific settings, 3. US settings as fallback
    /// </summary>
    private async Task<Guid?> ResolveStripeSettingsIdAsync(bool isTestCompany, string? countryCode)
    {
        try
        {
            // If test company, look for "test" settings
            if (isTestCompany)
            {
                var testSettings = await _context.StripeSettings
                    .FirstOrDefaultAsync(s => s.Name.ToLower() == "test".ToLower());
                
                if (testSettings != null)
                {
                    _logger.LogInformation("Resolved StripeSettingsId: {Id} (Name: {Name}) for test company", 
                        testSettings.Id, testSettings.Name);
                    return testSettings.Id;
                }
                
                _logger.LogWarning("Test Stripe settings not found. Falling back to country-based settings.");
            }

            // Look for country-specific settings
            if (!string.IsNullOrWhiteSpace(countryCode))
            {
                var countryCodeLower = countryCode.ToLower();
                var countrySettings = await _context.StripeSettings
                    .FirstOrDefaultAsync(s => s.Name.ToLower() == countryCodeLower);
                
                if (countrySettings != null)
                {
                    _logger.LogInformation("Resolved StripeSettingsId: {Id} (Name: {Name}) for country: {Country}", 
                        countrySettings.Id, countrySettings.Name, countryCode);
                    return countrySettings.Id;
                }
                
                _logger.LogWarning("Stripe settings for country '{Country}' not found. Falling back to US settings.", countryCode);
            }

            // Fallback to US settings
            var usSettings = await _context.StripeSettings
                .FirstOrDefaultAsync(s => s.Name.ToLower() == "us".ToLower());
            
            if (usSettings != null)
            {
                _logger.LogInformation("Resolved StripeSettingsId: {Id} (Name: {Name}) as fallback US settings", 
                    usSettings.Id, usSettings.Name);
                return usSettings.Id;
            }

            _logger.LogWarning("US Stripe settings not found. No StripeSettingsId will be assigned.");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving StripeSettingsId for IsTestCompany: {IsTestCompany}, Country: {Country}", 
                isTestCompany, countryCode);
            return null;
        }
    }

    private async Task<string?> NormalizeAndSaveAssetAsync(Guid companyId, string assetName, string? rawValue, string publicBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        try
        {
            if (rawValue.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                var match = Regex.Match(rawValue, @"^data:(?<mime>[\w/\-\.]+);base64,(?<data>.+)$");
                if (!match.Success)
                {
                    _logger.LogWarning("Invalid data URL format for {AssetName} on company {CompanyId}", assetName, companyId);
                    return null;
                }

                var mimeType = match.Groups["mime"].Value;
                var base64Data = match.Groups["data"].Value;

                byte[] fileBytes;
                try
                {
                    fileBytes = Convert.FromBase64String(base64Data);
                }
                catch (FormatException ex)
                {
                    _logger.LogWarning(ex, "Failed to decode base64 payload for {AssetName} on company {CompanyId}", assetName, companyId);
                    return null;
                }

                var extension = GetExtensionFromMime(mimeType, assetName);
                if (extension == null)
                {
                    _logger.LogWarning("Unsupported mime type {Mime} for {AssetName} on company {CompanyId}", mimeType, assetName, companyId);
                    return null;
                }

                // Check if Azure Blob Storage is configured
                if (!await _blobStorageService.IsConfiguredAsync())
                {
                    _logger.LogError("Azure Blob Storage is not configured. Cannot save {AssetName} for company {CompanyId}", assetName, companyId);
                    throw new InvalidOperationException("Azure Blob Storage is not configured. Please configure it in Settings > Azure Blob Storage.");
                }

                var fileName = $"{assetName}.{extension}";
                var blobPath = $"{companyId}/{fileName}";
                const string containerName = "companies";

                // Delete existing files with same asset name but different extension
                var existingFiles = await _blobStorageService.ListFilesAsync(containerName, $"{companyId}/{assetName}.");
                foreach (var existingFile in existingFiles)
                {
                    await _blobStorageService.DeleteFileAsync(containerName, existingFile);
                }

                // Upload new file to Azure Blob Storage
                using var stream = new MemoryStream(fileBytes);
                var blobUrl = await _blobStorageService.UploadFileAsync(stream, containerName, blobPath, mimeType);
                _logger.LogInformation("Uploaded {AssetName} to blob storage: {BlobUrl}", assetName, blobUrl);
                return blobUrl;
            }

            // Check if it's already a blob storage URL - return as-is
            if (rawValue.Contains("blob.core.windows.net"))
            {
                return rawValue;
            }

            // For legacy local URLs, return as-is (they will still work until migrated)
            if (rawValue.StartsWith("/public/", StringComparison.OrdinalIgnoreCase))
            {
                return CombineWithBase(publicBaseUrl, rawValue.Replace("\\", "/"));
            }

            if (rawValue.StartsWith("/"))
            {
                return CombineWithBase(publicBaseUrl, rawValue.Replace("\\", "/"));
            }

            return rawValue;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process {AssetName} for company {CompanyId}", assetName, companyId);
            throw;
        }
    }

    private static string? GetExtensionFromMime(string mimeType, string assetName)
    {
        if (string.IsNullOrWhiteSpace(mimeType))
        {
            return null;
        }

        return mimeType.ToLowerInvariant() switch
        {
            "image/jpeg" or "image/jpg" => "jpg",
            "image/png" => "png",
            "image/gif" => "gif",
            "image/webp" => "webp",
            "image/svg+xml" => "svg",
            "image/x-icon" => "ico",
            "image/vnd.microsoft.icon" => "ico",
            "video/mp4" => "mp4",
            "video/webm" => "webm",
            "video/ogg" => "ogv",
            "video/quicktime" => "mov",
            _ when mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) => "png",
            _ when mimeType.StartsWith("video/", StringComparison.OrdinalIgnoreCase) => "mp4",
            _ => null
        };
    }

    private string GetPublicBaseUrl()
    {
        var request = HttpContext?.Request;
        if (request == null)
        {
            return string.Empty;
        }

        var baseUrl = $"{request.Scheme}://{request.Host}".TrimEnd('/');
        return string.IsNullOrWhiteSpace(baseUrl) ? string.Empty : baseUrl;
    }

    private static string CombineWithBase(string baseUrl, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return relativePath;
        }

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return relativePath;
        }

        if (!relativePath.StartsWith('/'))
        {
            relativePath = "/" + relativePath;
        }

        return $"{baseUrl.TrimEnd('/')}{relativePath}";
    }
}
