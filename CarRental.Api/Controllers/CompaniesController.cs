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
using CarRental.Api.Models;
using CarRental.Api.Services;
using CarRental.Api.Extensions;
using CarRental.Api.DTOs;
using CarRental.Api.Helpers;
using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using System.Text.Encodings.Web;
using System.Linq;

namespace CarRental.Api.Controllers;

[ApiController]
[Route("api/companies")]
[Authorize] // Require authentication for all endpoints
public class CompaniesController : ControllerBase
{
    private readonly CarRentalDbContext _context;
    private readonly ILogger<CompaniesController> _logger;
    private readonly ICompanyService _companyService;
    private readonly IWebHostEnvironment _environment;
    private readonly IEncryptionService _encryptionService;
    private readonly IAzureDnsService? _azureDnsService;
    private static readonly string[] SupportedLanguages = new[] { "en", "es", "pt", "fr", "de" };
    private static readonly HashSet<string> AllowedAiIntegrations = new(StringComparer.OrdinalIgnoreCase)
    {
        "free",
        "claude",
        "premium"
    };
    private const string DefaultAiIntegration = "claude";

    public CompaniesController(
        CarRentalDbContext context, 
        ILogger<CompaniesController> logger,
        ICompanyService companyService,
        IWebHostEnvironment environment,
        IEncryptionService encryptionService,
        IAzureDnsService? azureDnsService = null)
    {
        _context = context;
        _logger = logger;
        _companyService = companyService;
        _environment = environment;
        _encryptionService = encryptionService;
        _azureDnsService = azureDnsService;
    }

    /// <summary>
    /// Get all companies with optional filtering
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<object>>> GetAllCompanies([FromQuery] bool includeInactive = false)
    {
        try
        {
            var query = _context.Companies.AsQueryable();

            if (!includeInactive)
            {
                query = query.Where(c => c.IsActive);
            }

            var companies = await query
                .OrderBy(c => c.CompanyName)
                .ToListAsync();

            var result = companies.Select(c => new
            {
                id = c.Id,
                companyName = c.CompanyName,
                email = c.Email,
                subdomain = c.Subdomain,
                fullDomain = string.IsNullOrEmpty(c.Subdomain) ? null : $"{c.Subdomain}.aegis-rental.com",
                primaryColor = c.PrimaryColor ?? "#007bff",
                secondaryColor = c.SecondaryColor ?? "#6c757d",
                logoUrl = c.LogoUrl,
                faviconUrl = c.FaviconUrl,
                country = c.Country,
                currency = c.Currency,
                language = c.Language ?? "en",
                motto = c.Motto,
                mottoDescription = c.MottoDescription,
                about = c.About,
                website = c.Website,
                customCss = c.CustomCss,
                videoLink = c.VideoLink,
                bannerLink = c.BannerLink,
                backgroundLink = c.BackgroundLink,
                invitation = c.Invitation,
                aiIntegration = NormalizeAiIntegration(c.AiIntegration),
                bookingIntegrated = !string.IsNullOrEmpty(c.BookingIntegrated) && (c.BookingIntegrated.ToLower() == "true" || c.BookingIntegrated == "1"),
                taxId = c.TaxId,
                stripeAccountId = (string?)null,
                hasStripeAccount = !string.IsNullOrEmpty(c.StripeAccountId),
                blinkKey = c.BlinkKey,
                isActive = c.IsActive,
                createdAt = c.CreatedAt,
                updatedAt = c.UpdatedAt,
                securityDeposit = c.SecurityDeposit,
                isSecurityDepositMandatory = c.IsSecurityDepositMandatory
            });

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting companies: {Message}", ex.Message);
            _logger.LogError(ex, "Stack trace: {StackTrace}", ex.StackTrace);
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }

    /// <summary>
    /// Get company by ID
    /// </summary>
    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<object>> GetCompany(Guid id)
    {
        try
        {
            var company = await _context.Companies.FindAsync(id);

            if (company == null)
            {
                return NotFound(new { error = "Company not found" });
            }

            var result = new
            {
                id = company.Id,
                companyName = company.CompanyName,
                email = company.Email,
                subdomain = company.Subdomain,
                fullDomain = string.IsNullOrEmpty(company.Subdomain) ? null : $"{company.Subdomain}.aegis-rental.com",
                primaryColor = company.PrimaryColor ?? "#007bff",
                secondaryColor = company.SecondaryColor ?? "#6c757d",
                logoUrl = company.LogoUrl,
                faviconUrl = company.FaviconUrl,
                country = company.Country,
                currency = company.Currency,
                language = company.Language ?? "en",
                motto = company.Motto,
                mottoDescription = company.MottoDescription,
                about = company.About,
                website = company.Website,
                customCss = company.CustomCss,
                videoLink = company.VideoLink,
                bannerLink = company.BannerLink,
                backgroundLink = company.BackgroundLink,
                invitation = company.Invitation,
                aiIntegration = NormalizeAiIntegration(company.AiIntegration),
                texts = company.Texts,
                bookingIntegrated = !string.IsNullOrEmpty(company.BookingIntegrated) && (company.BookingIntegrated.ToLower() == "true" || company.BookingIntegrated == "1"),
                taxId = company.TaxId,
                hasStripeAccount = !string.IsNullOrEmpty(company.StripeAccountId),
                stripeSettingsId = company.StripeSettingsId,
                blinkKey = company.BlinkKey,
                isActive = company.IsActive,
                createdAt = company.CreatedAt,
                updatedAt = company.UpdatedAt,
                securityDeposit = company.SecurityDeposit,
                isSecurityDepositMandatory = company.IsSecurityDepositMandatory
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting company {CompanyId}", id);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Create a new company
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<object>> CreateCompany([FromBody] CreateCompanyRequest request)
    {
        try
        {
            // Validate required fields
            if (string.IsNullOrWhiteSpace(request.CompanyName))
            {
                return BadRequest(new { error = "Company name is required" });
            }

            if (string.IsNullOrWhiteSpace(request.Email))
            {
                return BadRequest(new { error = "Email is required" });
            }

            // Subdomain is optional - can be set manually later
            // But if provided, validate it's unique and in correct format
            string? subdomain = null;
            if (!string.IsNullOrWhiteSpace(request.Subdomain))
            {
                // Normalize subdomain: lowercase and trim
                subdomain = request.Subdomain.ToLower().Trim();
                
                // Validate subdomain format (alphanumeric and hyphens only, no spaces)
                if (!System.Text.RegularExpressions.Regex.IsMatch(subdomain, @"^[a-z0-9-]+$"))
                {
                    return BadRequest(new { error = "Subdomain can only contain lowercase letters, numbers, and hyphens" });
                }
                
                // Check if subdomain already exists in database
                var existingSubdomain = await _context.Companies
                    .FirstOrDefaultAsync(c => c.Subdomain != null && c.Subdomain.ToLower() == subdomain);

                if (existingSubdomain != null)
                {
                    return Conflict(new { error = $"Subdomain '{subdomain}' already exists in database" });
                }
                
                // Check if subdomain already exists in Azure DNS (required check - must pass)
                if (_azureDnsService != null)
                {
                    try
                    {
                        var existsInAzure = await _azureDnsService.SubdomainExistsAsync(subdomain);
                        if (existsInAzure)
                        {
                            return Conflict(new { error = $"Subdomain '{subdomain}' already exists in Azure DNS" });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error checking Azure DNS for subdomain availability: {Subdomain}", subdomain);
                        return StatusCode(500, new { error = $"Error checking subdomain availability in Azure DNS: {ex.Message}. " +
                            "Please ensure Azure DNS is properly configured and accessible." });
                    }
                }
                else
                {
                    _logger.LogWarning("Azure DNS service is not available. Cannot verify subdomain '{Subdomain}' in Azure DNS.", subdomain);
                    return StatusCode(503, new { error = "Azure DNS service is not configured. Cannot verify subdomain availability." });
                }
            }

            // Check if email already exists
            var existingEmail = await _context.Companies
                .FirstOrDefaultAsync(c => c.Email.ToLower() == request.Email.ToLower());

            if (existingEmail != null)
            {
                return Conflict(new { error = "Email already exists" });
            }

            // Resolve StripeSettingsId based on IsTestCompany flag and country code
            var stripeSettingsId = await ResolveStripeSettingsIdAsync(
                request.IsTestCompany ?? false, 
                string.IsNullOrWhiteSpace(request.Country) 
                    ? null 
                    : CountryHelper.NormalizeToIsoCode(request.Country));

            var company = new Company
            {
                CompanyName = request.CompanyName,
                Email = request.Email,
                Subdomain = subdomain, // Can be NULL - set manually later
                PrimaryColor = request.PrimaryColor ?? "#007bff",
                SecondaryColor = request.SecondaryColor ?? "#6c757d",
                LogoUrl = request.LogoUrl,
                FaviconUrl = request.FaviconUrl,
                Country = string.IsNullOrWhiteSpace(request.Country) 
                    ? null 
                    : CountryHelper.NormalizeToIsoCode(request.Country),
                Language = request.Language ?? "en",
                Motto = request.Motto,
                MottoDescription = request.MottoDescription,
                About = request.About,
                TermsOfUse = request.TermsOfUse,
                Website = request.Website,
                CustomCss = request.CustomCss,
                VideoLink = request.VideoLink,
                BannerLink = request.BannerLink,
                BackgroundLink = request.BackgroundLink,
                Invitation = request.Invitation,
                BookingIntegrated = request.BookingIntegrated.HasValue && request.BookingIntegrated.Value ? "true" : null,
                TaxId = request.TaxId,
                StripeSettingsId = stripeSettingsId,
                StripeAccountId = string.IsNullOrWhiteSpace(request.StripeAccountId)
                    ? null
                    : _encryptionService.Encrypt(request.StripeAccountId),
                BlinkKey = request.BlinkKey,
                AiIntegration = NormalizeAiIntegration(request.AiIntegration),
                Currency = CurrencyHelper.ResolveCurrency(request.Currency, request.Country),
                SecurityDeposit = request.SecurityDeposit ?? 1000m,
                IsActive = request.IsActive ?? true,
                IsTestCompany = request.IsTestCompany ?? false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var publicBaseUrl = GetPublicBaseUrl();

            company.LogoUrl = await NormalizeAndSaveAssetAsync(company.Id, "logo", request.LogoUrl, publicBaseUrl);
            company.FaviconUrl = await NormalizeAndSaveAssetAsync(company.Id, "favicon", request.FaviconUrl, publicBaseUrl);
            company.BannerLink = await NormalizeAndSaveAssetAsync(company.Id, "banner", request.BannerLink, publicBaseUrl);
            company.BackgroundLink = await NormalizeAndSaveAssetAsync(company.Id, "background", request.BackgroundLink, publicBaseUrl);
            company.VideoLink = await NormalizeAndSaveAssetAsync(company.Id, "video", request.VideoLink, publicBaseUrl);

            company.Texts = await ProcessDesignAssetsAsync(company.Id, request.Texts, publicBaseUrl);

            _context.Companies.Add(company);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Company created: {CompanyName} (ID: {CompanyId})", company.CompanyName, company.Id);

            // Create Azure DNS subdomain with SSL if subdomain is provided
            // Run in background to avoid timeout - fire and forget
            if (!string.IsNullOrWhiteSpace(company.Subdomain) && _azureDnsService != null)
            {
                var subdomainToSetup = company.Subdomain;
                var companyIdToSetup = company.Id;
                
                // Fire and forget - run in background to avoid timeout
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
                        // Azure DNS is not configured - log warning but don't fail company creation
                        _logger.LogWarning("Azure DNS service is not configured. Cannot create subdomain '{Subdomain}' for company {CompanyId}. " +
                            "Company was created successfully, but DNS/SSL setup was skipped. Error: {Error}", 
                            subdomainToSetup, companyIdToSetup, ex.Message);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error creating Azure DNS subdomain with SSL: {Subdomain} for company {CompanyId}. " +
                            "Company was created successfully, but DNS/SSL setup failed.", 
                            subdomainToSetup, companyIdToSetup);
                        // Don't fail company creation if DNS creation fails
                    }
                });
                
                _logger.LogInformation("Domain setup initiated in background for subdomain: {Subdomain}, company: {CompanyId}", 
                    subdomainToSetup, companyIdToSetup);
            }

            var result = new
            {
                id = company.Id,
                companyName = company.CompanyName,
                email = company.Email,
                subdomain = company.Subdomain,
                fullDomain = string.IsNullOrEmpty(company.Subdomain) ? null : $"{company.Subdomain}.aegis-rental.com",
                primaryColor = company.PrimaryColor ?? "#007bff",
                secondaryColor = company.SecondaryColor ?? "#6c757d",
                logoUrl = company.LogoUrl,
                faviconUrl = company.FaviconUrl,
                country = company.Country,
                currency = company.Currency,
                language = company.Language ?? "en",
                motto = company.Motto,
                mottoDescription = company.MottoDescription,
                about = company.About,
                website = company.Website,
                customCss = company.CustomCss,
                videoLink = company.VideoLink,
                bannerLink = company.BannerLink,
                backgroundLink = company.BackgroundLink,
                invitation = company.Invitation,
                aiIntegration = NormalizeAiIntegration(company.AiIntegration),
                texts = company.Texts,
                bookingIntegrated = !string.IsNullOrEmpty(company.BookingIntegrated) && (company.BookingIntegrated.ToLower() == "true" || company.BookingIntegrated == "1"),
                taxId = company.TaxId,
                stripeAccountId = (string?)null,
                hasStripeAccount = !string.IsNullOrEmpty(company.StripeAccountId),
                stripeSettingsId = company.StripeSettingsId,
                blinkKey = company.BlinkKey,
                isActive = company.IsActive,
                createdAt = company.CreatedAt,
                updatedAt = company.UpdatedAt,
                securityDeposit = company.SecurityDeposit,
                isSecurityDepositMandatory = company.IsSecurityDepositMandatory
            };

            return CreatedAtAction(nameof(GetCompany), new { id = company.Id }, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating company");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Update an existing company
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<object>> UpdateCompany(Guid id, [FromBody] UpdateCompanyRequest request)
    {
        try
        {
            var company = await _context.Companies.FindAsync(id);

            if (company == null)
            {
                return NotFound(new { error = "Company not found" });
            }

            // Handle subdomain update (if provided)
            if (request.Subdomain != null) // Allow setting subdomain or clearing it (empty string)
            {
                string? newSubdomain = string.IsNullOrWhiteSpace(request.Subdomain) ? null : request.Subdomain.ToLower().Trim();
                
                // Only validate if subdomain is being changed
                if (newSubdomain != company.Subdomain)
                {
                    // Validate subdomain format if provided
                    if (!string.IsNullOrWhiteSpace(newSubdomain))
                    {
                        if (!System.Text.RegularExpressions.Regex.IsMatch(newSubdomain, @"^[a-z0-9-]+$"))
                        {
                            return BadRequest(new { error = "Subdomain can only contain lowercase letters, numbers, and hyphens" });
                        }
                        
                        // Check if subdomain already exists (excluding current company)
                        var existingSubdomain = await _context.Companies
                            .FirstOrDefaultAsync(c => c.Subdomain != null && c.Subdomain.ToLower() == newSubdomain && c.Id != id);
                        
                        if (existingSubdomain != null)
                        {
                            return Conflict(new { error = $"Subdomain '{newSubdomain}' already exists" });
                        }
                    }
                    
                    company.Subdomain = newSubdomain;
                }
            }

            var publicBaseUrl = GetPublicBaseUrl();

            // Update fields if provided
            if (!string.IsNullOrWhiteSpace(request.CompanyName))
                company.CompanyName = request.CompanyName;

            if (!string.IsNullOrWhiteSpace(request.Email))
                company.Email = request.Email;

            if (!string.IsNullOrWhiteSpace(request.PrimaryColor))
                company.PrimaryColor = request.PrimaryColor;

            if (!string.IsNullOrWhiteSpace(request.SecondaryColor))
                company.SecondaryColor = request.SecondaryColor;

            if (request.LogoUrl != null)
                company.LogoUrl = await NormalizeAndSaveAssetAsync(company.Id, "logo", request.LogoUrl, publicBaseUrl);

            if (request.FaviconUrl != null)
                company.FaviconUrl = await NormalizeAndSaveAssetAsync(company.Id, "favicon", request.FaviconUrl, publicBaseUrl);

            var originalCountry = company.Country;
            var countryUpdated = false;
            var originalSubdomain = company.Subdomain;

            // Handle subdomain changes and Azure DNS updates
            if (request.Subdomain != null)
            {
                string? newSubdomain = string.IsNullOrWhiteSpace(request.Subdomain) ? null : request.Subdomain.ToLower().Trim();
                
                // Validate subdomain format if provided
                if (!string.IsNullOrWhiteSpace(newSubdomain))
                {
                    if (!System.Text.RegularExpressions.Regex.IsMatch(newSubdomain, @"^[a-z0-9-]+$"))
                    {
                        return BadRequest(new { error = "Subdomain can only contain lowercase letters, numbers, and hyphens" });
                    }
                }
                
                if (newSubdomain != company.Subdomain)
                {
                    // Check if new subdomain already exists in database (excluding current company)
                    if (!string.IsNullOrWhiteSpace(newSubdomain))
                    {
                        var existingSubdomain = await _context.Companies
                            .FirstOrDefaultAsync(c => c.Subdomain != null && c.Subdomain.ToLower() == newSubdomain && c.Id != id);

                        if (existingSubdomain != null)
                        {
                            return Conflict(new { error = $"Subdomain '{newSubdomain}' already exists in database" });
                        }
                    }
                    
                    // If subdomain is being changed and Azure DNS service is available
                    if (_azureDnsService != null)
                    {
                        try
                        {
                            // Check if new subdomain already exists in Azure DNS (required check - must pass)
                            if (!string.IsNullOrWhiteSpace(newSubdomain))
                            {
                                var existsInAzure = await _azureDnsService.SubdomainExistsAsync(newSubdomain);
                                if (existsInAzure)
                                {
                                    return Conflict(new { error = $"Subdomain '{newSubdomain}' already exists in Azure DNS" });
                                }
                            }
                            
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
                            
                            // Create new DNS record with SSL if new subdomain is provided
                            // Run in background to avoid timeout - fire and forget
                            if (!string.IsNullOrWhiteSpace(newSubdomain))
                            {
                                var subdomain = newSubdomain;
                                var companyId = id;
                                
                                // Fire and forget - run in background to avoid timeout
                                _ = Task.Run(async () =>
                                {
                                    try
                                    {
                                        _logger.LogInformation("Starting background domain setup for new subdomain: {Subdomain}, company: {CompanyId}", 
                                            subdomain, companyId);
                                        
                                        var dnsCreated = await _azureDnsService.CreateSubdomainWithSslAsync(subdomain);
                                        if (dnsCreated)
                                        {
                                            _logger.LogInformation("Created new Azure DNS subdomain with SSL: {NewSubdomain} for company {CompanyId}", 
                                                subdomain, companyId);
                                        }
                                        else
                                        {
                                            _logger.LogWarning("Failed to create Azure DNS subdomain with SSL: {NewSubdomain} for company {CompanyId}. " +
                                                "Subdomain may already exist in Azure DNS.", subdomain, companyId);
                                        }
                                    }
                                    catch (ArgumentException ex) when (ex.Message.Contains("not configured"))
                                    {
                                        _logger.LogWarning("Azure DNS service is not configured. Cannot create subdomain '{Subdomain}' for company {CompanyId}. Error: {Error}", 
                                            subdomain, companyId, ex.Message);
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogError(ex, "Error creating Azure DNS subdomain with SSL: {NewSubdomain} for company {CompanyId}", 
                                            subdomain, companyId);
                                    }
                                });
                                
                                _logger.LogInformation("Domain setup initiated in background for new subdomain: {Subdomain}, company: {CompanyId}", 
                                    subdomain, companyId);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error updating Azure DNS subdomain for company {CompanyId}. " +
                                "Old: {OldSubdomain}, New: {NewSubdomain}", 
                                id, originalSubdomain, newSubdomain);
                            return StatusCode(500, new { error = $"Error updating subdomain in Azure DNS: {ex.Message}. " +
                                "Please ensure Azure DNS is properly configured and accessible." });
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Azure DNS service is not available. Cannot verify subdomain '{Subdomain}' in Azure DNS.", newSubdomain);
                        return StatusCode(503, new { error = "Azure DNS service is not configured. Cannot verify subdomain availability." });
                    }
                    
                    company.Subdomain = newSubdomain;
                }
            }

            if (request.Country != null)
            {
                var normalizedCountry = string.IsNullOrWhiteSpace(request.Country) 
                    ? null 
                    : CountryHelper.NormalizeToIsoCode(request.Country);
                company.Country = normalizedCountry;
                countryUpdated = !string.Equals(originalCountry, normalizedCountry, StringComparison.OrdinalIgnoreCase);
            }

            if (request.Language != null)
                company.Language = request.Language;

            if (request.Motto != null)
                company.Motto = request.Motto;

            if (request.MottoDescription != null)
                company.MottoDescription = request.MottoDescription;

            if (request.About != null)
                company.About = request.About;

            if (request.TermsOfUse != null)
                company.TermsOfUse = request.TermsOfUse;

            if (request.Website != null)
                company.Website = request.Website;

            if (request.CustomCss != null)
                company.CustomCss = request.CustomCss;

            if (request.VideoLink != null)
                company.VideoLink = await NormalizeAndSaveAssetAsync(company.Id, "video", request.VideoLink, publicBaseUrl);

            if (request.BannerLink != null)
                company.BannerLink = await NormalizeAndSaveAssetAsync(company.Id, "banner", request.BannerLink, publicBaseUrl);

            if (request.BackgroundLink != null)
                company.BackgroundLink = await NormalizeAndSaveAssetAsync(company.Id, "background", request.BackgroundLink, publicBaseUrl);

            if (request.Invitation != null)
                company.Invitation = request.Invitation;

            if (request.Texts != null)
                company.Texts = await ProcessDesignAssetsAsync(company.Id, request.Texts, publicBaseUrl);

            if (request.BookingIntegrated.HasValue)
                company.BookingIntegrated = request.BookingIntegrated.Value ? "true" : null;

            if (request.TaxId != null)
                company.TaxId = request.TaxId;

            // Resolve StripeSettingsId if IsTestCompany or Country changed
            var isTestCompanyChanged = request.IsTestCompany.HasValue && request.IsTestCompany.Value != company.IsTestCompany;
            var countryChanged = !string.IsNullOrWhiteSpace(request.Country) && 
                CountryHelper.NormalizeToIsoCode(request.Country) != company.Country;
            
            if (isTestCompanyChanged || countryChanged)
            {
                var newStripeSettingsId = await ResolveStripeSettingsIdAsync(
                    request.IsTestCompany ?? company.IsTestCompany,
                    !string.IsNullOrWhiteSpace(request.Country) 
                        ? CountryHelper.NormalizeToIsoCode(request.Country) 
                        : company.Country);
                company.StripeSettingsId = newStripeSettingsId;
                _logger.LogInformation("Resolved StripeSettingsId: {StripeSettingsId} for company {CompanyId} (IsTestCompany: {IsTestCompany}, Country: {Country})",
                    newStripeSettingsId, id, request.IsTestCompany ?? company.IsTestCompany, 
                    !string.IsNullOrWhiteSpace(request.Country) ? CountryHelper.NormalizeToIsoCode(request.Country) : company.Country);
            }
            else if (request.StripeSettingsId.HasValue)
            {
                company.StripeSettingsId = request.StripeSettingsId.Value;
            }
            else if (request.StripeSettingsId == null && request.GetType().GetProperty("StripeSettingsId")?.GetValue(request) == null)
            {
                // Explicitly set to null if the property was included in the request with null value
                company.StripeSettingsId = null;
            }

            if (request.StripeAccountId != null)
            {
                company.StripeAccountId = string.IsNullOrWhiteSpace(request.StripeAccountId)
                    ? null
                    : _encryptionService.Encrypt(request.StripeAccountId);
            }

            if (request.BlinkKey != null)
                company.BlinkKey = request.BlinkKey;

            if (request.AiIntegration != null)
                company.AiIntegration = NormalizeAiIntegration(request.AiIntegration);

            if (!string.IsNullOrWhiteSpace(request.Currency))
            {
                company.Currency = CurrencyHelper.ResolveCurrency(request.Currency, company.Country);
            }
            else if (countryUpdated)
            {
                company.Currency = CurrencyHelper.GetCurrencyForCountry(company.Country);
            }

            if (request.SecurityDeposit.HasValue)
                company.SecurityDeposit = request.SecurityDeposit.Value;

            if (request.IsActive.HasValue)
                company.IsActive = request.IsActive.Value;

            if (request.IsTestCompany.HasValue)
                company.IsTestCompany = request.IsTestCompany.Value;

            company.UpdatedAt = DateTime.UtcNow;

            _context.Companies.Update(company);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Company updated: {CompanyName} (ID: {CompanyId})", company.CompanyName, company.Id);

            var result = new
            {
                id = company.Id,
                companyName = company.CompanyName,
                email = company.Email,
                subdomain = company.Subdomain,
                fullDomain = string.IsNullOrEmpty(company.Subdomain) ? null : $"{company.Subdomain}.aegis-rental.com",
                primaryColor = company.PrimaryColor ?? "#007bff",
                secondaryColor = company.SecondaryColor ?? "#6c757d",
                logoUrl = company.LogoUrl,
                faviconUrl = company.FaviconUrl,
                country = company.Country,
                language = company.Language ?? "en",
                motto = company.Motto,
                mottoDescription = company.MottoDescription,
                about = company.About,
                website = company.Website,
                customCss = company.CustomCss,
                videoLink = company.VideoLink,
                bannerLink = company.BannerLink,
                backgroundLink = company.BackgroundLink,
                invitation = company.Invitation,
                aiIntegration = NormalizeAiIntegration(company.AiIntegration),
                texts = company.Texts,
                bookingIntegrated = !string.IsNullOrEmpty(company.BookingIntegrated) && (company.BookingIntegrated.ToLower() == "true" || company.BookingIntegrated == "1"),
                taxId = company.TaxId,
                stripeAccountId = (string?)null,
                hasStripeAccount = !string.IsNullOrEmpty(company.StripeAccountId),
                stripeSettingsId = company.StripeSettingsId,
                blinkKey = company.BlinkKey,
                isActive = company.IsActive,
                createdAt = company.CreatedAt,
                updatedAt = company.UpdatedAt,
                securityDeposit = company.SecurityDeposit,
                isSecurityDepositMandatory = company.IsSecurityDepositMandatory
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating company {CompanyId}", id);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Manually set up domain (DNS + App Service binding + SSL) for an existing company
    /// </summary>
    [HttpPost("{id}/setup-domain")]
    public async Task<ActionResult> SetupCompanyDomain(Guid id)
    {
        try
        {
            var company = await _context.Companies.FindAsync(id);
            if (company == null)
            {
                return NotFound(new { error = "Company not found" });
            }

            if (string.IsNullOrWhiteSpace(company.Subdomain))
            {
                return BadRequest(new { error = "Company does not have a subdomain configured" });
            }

            if (_azureDnsService == null)
            {
                return StatusCode(503, new { error = "Azure DNS service is not configured" });
            }

            _logger.LogInformation("Manually setting up domain for company {CompanyId} with subdomain {Subdomain}", 
                id, company.Subdomain);

            try
            {
                var success = await _azureDnsService.CreateSubdomainWithSslAsync(company.Subdomain);
                
                if (success)
                {
                    var url = _azureDnsService.GetSubdomainUrl(company.Subdomain);
                    return Ok(new
                    {
                        success = true,
                        message = $"Domain setup completed successfully for {company.Subdomain}",
                        url = url,
                        subdomain = company.Subdomain
                    });
                }
                else
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Failed to set up domain. It may already exist or there was an error."
                    });
                }
            }
            catch (ArgumentException ex) when (ex.Message.Contains("not configured"))
            {
                _logger.LogWarning("Azure DNS service is not configured. Cannot set up domain for company {CompanyId}. Error: {Error}", 
                    id, ex.Message);
                return StatusCode(503, new
                {
                    success = false,
                    message = "Azure DNS service is not configured. Cannot set up domain."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting up domain for company {CompanyId}", id);
                return StatusCode(500, new
                {
                    success = false,
                    message = $"Error setting up domain: {ex.Message}"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SetupCompanyDomain for company {CompanyId}", id);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Delete a company
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteCompany(Guid id)
    {
        try
        {
            var company = await _context.Companies.FindAsync(id);

            if (company == null)
            {
                return NotFound(new { error = "Company not found" });
            }

            _context.Companies.Remove(company);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Company deleted: {CompanyName} (ID: {CompanyId})", company.CompanyName, company.Id);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting company {CompanyId}", id);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get current company configuration (public endpoint)
    /// Used by React frontend to load company branding based on domain
    /// </summary>
    [HttpGet("config")]
    [ProducesResponseType(typeof(CompanyConfigDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [AllowAnonymous] // Public endpoint for frontend
    public async Task<ActionResult<CompanyConfigDto>> GetCurrentCompanyConfig()
    {
        try
        {
            var companyId = HttpContext.GetCompanyIdAsGuid();
            var hostname = HttpContext.Request.Headers["X-Forwarded-Host"].ToString();
            if (string.IsNullOrEmpty(hostname))
            {
                hostname = HttpContext.Request.Host.Host;
            }
            
            _logger.LogInformation(
                "GetCurrentCompanyConfig: CompanyId={CompanyId}, Hostname={Hostname}, X-Forwarded-Host={ForwardedHost}, Host={Host}",
                companyId,
                hostname,
                HttpContext.Request.Headers["X-Forwarded-Host"].ToString(),
                HttpContext.Request.Host.Host
            );
            
            // Try to get company from HttpContext first (set by CompanyMiddleware to avoid duplicate DB query)
            Company? company = HttpContext.Items["Company"] as Company;
            
            if (company == null && companyId.HasValue)
            {
                // Fallback: query database if not in context
                company = await _companyService.GetCompanyByIdAsync(companyId.Value);
            }
            
            if (company == null)
            {
                if (!companyId.HasValue)
                {
                    _logger.LogWarning(
                        "GetCurrentCompanyConfig: No company ID found. Hostname={Hostname}, Headers={Headers}",
                        hostname,
                        string.Join(", ", HttpContext.Request.Headers.Select(h => $"{h.Key}={h.Value}"))
                    );
                    return BadRequest(new { error = "Company ID is required. Domain-based company resolution failed.", hostname = hostname });
                }
                return NotFound(new { error = "Company not found" });
            }

            var config = MapToConfigDto(company);
            return Ok(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting company config");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get domain mapping for all active companies
    /// Used by Node.js proxy to route requests
    /// </summary>
    [HttpGet("domain-mapping")]
    [ProducesResponseType(typeof(Dictionary<string, Guid>), 200)]
    [AllowAnonymous] // Public endpoint for proxy
    public async Task<ActionResult<Dictionary<string, Guid>>> GetDomainMapping()
    {
        try
        {
            var mapping = await _companyService.GetDomainMappingAsync();
            return Ok(mapping);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting domain mapping");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Invalidate company cache
    /// </summary>
    [HttpPost("invalidate-cache")]
    public ActionResult InvalidateCache()
    {
        try
        {
            _companyService.InvalidateCache();
            _logger.LogInformation("Cache invalidation requested");
            return Ok(new { message = "Cache invalidated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invalidating cache");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    private async Task<string?> ProcessDesignAssetsAsync(Guid companyId, string? texts, string publicBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(texts))
        {
            return texts;
        }

        JsonNode? rootNode;
        try
        {
            rootNode = JsonNode.Parse(texts);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse texts JSON for company {CompanyId}. Returning original payload.", companyId);
            return texts;
        }

        if (rootNode is not JsonArray array)
        {
            return texts;
        }

        bool convertedFromLegacy = false;
        JsonArray sectionsArray;
        if (array.Any(item => item is JsonObject obj && obj.ContainsKey("language")))
        {
            sectionsArray = ConvertLegacySections(array);
            convertedFromLegacy = true;
        }
        else
        {
            sectionsArray = array;
        }

        var designDirectory = Path.Combine(_environment.ContentRootPath, "wwwroot", "public", companyId.ToString(), "design");
        var sectionsDirectory = Path.Combine(_environment.ContentRootPath, "wwwroot", "public", companyId.ToString(), "sections");
        bool hasChanges = convertedFromLegacy;

        for (int sectionIndex = 0; sectionIndex < sectionsArray.Count; sectionIndex++)
        {
            if (sectionsArray[sectionIndex] is not JsonObject sectionObj)
            {
                continue;
            }

            if (EnsureSectionStructure(sectionObj))
            {
                hasChanges = true;
            }

            if (sectionObj["backgroundImage"] is JsonObject backgroundImageObj)
            {
                var backgroundUrl = backgroundImageObj["url"]?.GetValue<string>() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(backgroundUrl))
                {
                    var normalizedBackground = await NormalizeAndSaveSectionBackgroundAsync(
                        companyId,
                        sectionsDirectory,
                        backgroundUrl,
                        sectionIndex,
                        publicBaseUrl);
                    if (!string.Equals(normalizedBackground, backgroundUrl, StringComparison.Ordinal))
                    {
                        hasChanges = true;
                    }
                    backgroundImageObj["url"] = normalizedBackground;
                }
                else
                {
                    backgroundImageObj["url"] = string.Empty;
                }
            }

            if (sectionObj["notes"] is not JsonArray notesArray)
            {
                notesArray = new JsonArray { CreateEmptyNoteObject() };
                sectionObj["notes"] = notesArray;
                hasChanges = true;
            }

            for (int noteIndex = 0; noteIndex < notesArray.Count; noteIndex++)
            {
                if (notesArray[noteIndex] is not JsonObject noteObj)
                {
                    noteObj = CreateEmptyNoteObject();
                    notesArray[noteIndex] = noteObj;
                    hasChanges = true;
                }

                if (EnsureNoteStructure(noteObj))
                {
                    hasChanges = true;
                }

                JsonObject pictureObj;
                string pictureUrl = string.Empty;

                var pictureNode = noteObj["picture"];
                if (pictureNode is JsonObject existingPicture)
                {
                    pictureObj = existingPicture;
                    pictureUrl = existingPicture["url"]?.GetValue<string>() ?? string.Empty;
                }
                else if (pictureNode is JsonValue pictureValue)
                {
                    pictureUrl = pictureValue.GetValue<string>() ?? string.Empty;
                    pictureObj = new JsonObject { ["url"] = pictureUrl };
                    noteObj["picture"] = pictureObj;
                    hasChanges = true;
                }
                else
                {
                    pictureObj = new JsonObject { ["url"] = string.Empty };
                    noteObj["picture"] = pictureObj;
                    hasChanges = true;
                }

                var normalizedUrl = pictureUrl;
                if (!string.IsNullOrWhiteSpace(pictureUrl))
                {
                    normalizedUrl = await NormalizeAndSavePictureAsync(companyId, designDirectory, pictureUrl, sectionIndex, noteIndex, publicBaseUrl);
                    if (!string.Equals(normalizedUrl, pictureUrl, StringComparison.Ordinal))
                    {
                        hasChanges = true;
                    }
                }

                pictureObj["url"] = normalizedUrl;
            }
        }

        if (!hasChanges)
        {
            return texts;
        }

        var serializerOptions = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = false
        };

        return sectionsArray.ToJsonString(serializerOptions);
    }

    private JsonArray ConvertLegacySections(JsonArray legacyLanguages)
    {
        var sections = new List<JsonObject>();

        for (int languageIndex = 0; languageIndex < legacyLanguages.Count; languageIndex++)
        {
            if (legacyLanguages[languageIndex] is not JsonObject languageObj)
            {
                continue;
            }

            var languageCode = languageObj["language"]?.GetValue<string>() ?? "en";
            if (languageObj["sections"] is not JsonArray legacySections)
            {
                continue;
            }

            for (int sectionIndex = 0; sectionIndex < legacySections.Count; sectionIndex++)
            {
                if (legacySections[sectionIndex] is not JsonObject legacySection)
                {
                    continue;
                }

                while (sections.Count <= sectionIndex)
                {
                    sections.Add(CreateEmptySectionObject());
                }

                var targetSection = sections[sectionIndex];

                var backColor = legacySection["backColor"]?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(backColor))
                {
                    targetSection["backColor"] = backColor;
                }

                var foreColor = legacySection["foreColor"]?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(foreColor))
                {
                    targetSection["foreColor"] = foreColor;
                }

                var notesLayout = legacySection["notesLayout"]?.GetValue<string>();
                if (string.Equals(notesLayout, "horizontal", StringComparison.OrdinalIgnoreCase))
                {
                    targetSection["notesLayout"] = "horizontal";
                }

                var alignmentValue = legacySection["alignment"]?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(alignmentValue))
                {
                    var alignmentNormalized = alignmentValue.ToLowerInvariant();
                    if (alignmentNormalized is "left" or "center" or "right")
                    {
                        targetSection["alignment"] = alignmentNormalized;
                    }
                }

                var backgroundValue = GetPictureUrl(legacySection["backgroundImage"] ?? legacySection["backgroundImageUrl"]);
                if (!string.IsNullOrWhiteSpace(backgroundValue))
                {
                    targetSection["backgroundImage"] = new JsonObject { ["url"] = backgroundValue };
                }

                var titleObj = targetSection["title"] as JsonObject ?? CreateEmptyLocalizedObject();
                titleObj[languageCode] = JsonValue.Create(legacySection["title"]?.GetValue<string>() ?? string.Empty);
                targetSection["title"] = titleObj;

                var descriptionObj = targetSection["description"] as JsonObject ?? CreateEmptyLocalizedObject();
                descriptionObj[languageCode] = JsonValue.Create(legacySection["description"]?.GetValue<string>() ?? string.Empty);
                targetSection["description"] = descriptionObj;

                var notesArray = targetSection["notes"] as JsonArray ?? new JsonArray();
                targetSection["notes"] = notesArray;

                var legacyNotes = legacySection["notes"] as JsonArray;
                if (legacyNotes == null)
                {
                    continue;
                }

                for (int noteIndex = 0; noteIndex < legacyNotes.Count; noteIndex++)
                {
                    if (legacyNotes[noteIndex] is not JsonObject legacyNote)
                    {
                        continue;
                    }

                    while (notesArray.Count <= noteIndex)
                    {
                        notesArray.Add(CreateEmptyNoteObject());
                    }

                    if (notesArray[noteIndex] is not JsonObject targetNote)
                    {
                        targetNote = CreateEmptyNoteObject();
                        notesArray[noteIndex] = targetNote;
                    }

                    var pictureUrl = GetPictureUrl(legacyNote["picture"] ?? legacyNote["picturePng"]);
                    if (!string.IsNullOrWhiteSpace(pictureUrl))
                    {
                        targetNote["picture"] = new JsonObject { ["url"] = pictureUrl };
                    }

                    var symbolForeColor = legacyNote["symbolForeColor"]?.GetValue<string>();
                    if (!string.IsNullOrWhiteSpace(symbolForeColor))
                    {
                        targetNote["symbolForeColor"] = symbolForeColor;
                    }

                    var symbol = legacyNote["symbol"]?.GetValue<string>();
                    if (!string.IsNullOrWhiteSpace(symbol))
                    {
                        targetNote["symbol"] = symbol;
                    }

                    var foreColorValue = legacyNote["foreColor"]?.GetValue<string>();
                    if (!string.IsNullOrWhiteSpace(foreColorValue))
                    {
                        targetNote["foreColor"] = foreColorValue;
                    }

                    var backColorValue = legacyNote["backColor"]?.GetValue<string>();
                    if (!string.IsNullOrWhiteSpace(backColorValue))
                    {
                        targetNote["backColor"] = backColorValue;
                    }

                    var noteTitleObj = targetNote["title"] as JsonObject ?? CreateEmptyLocalizedObject();
                    noteTitleObj[languageCode] = JsonValue.Create(legacyNote["title"]?.GetValue<string>() ?? string.Empty);
                    targetNote["title"] = noteTitleObj;

                    var noteCaptionObj = targetNote["caption"] as JsonObject ?? CreateEmptyLocalizedObject();
                    noteCaptionObj[languageCode] = JsonValue.Create(legacyNote["caption"]?.GetValue<string>() ?? string.Empty);
                    targetNote["caption"] = noteCaptionObj;

                    var noteTextObj = targetNote["text"] as JsonObject ?? CreateEmptyLocalizedObject();
                    noteTextObj[languageCode] = JsonValue.Create(legacyNote["text"]?.GetValue<string>() ?? string.Empty);
                    targetNote["text"] = noteTextObj;
                }
            }
        }

        var result = new JsonArray();
        if (sections.Count == 0)
        {
            result.Add(CreateEmptySectionObject());
        }
        else
        {
            foreach (var section in sections)
            {
                EnsureSectionStructure(section);
                result.Add(section);
            }
        }

        return result;
    }

    private bool EnsureSectionStructure(JsonObject section)
    {
        bool changed = false;

        if (EnsureColorValue(section, "backColor", "#ffffff"))
        {
            changed = true;
        }

        if (EnsureColorValue(section, "foreColor", "#000000"))
        {
            changed = true;
        }

        var layoutValue = section["notesLayout"] is JsonValue layoutNode && layoutNode.TryGetValue<string>(out var layoutString)
            ? layoutString?.ToLowerInvariant()
            : null;

        if (layoutValue != "horizontal" && layoutValue != "vertical")
        {
            section["notesLayout"] = "vertical";
            changed = true;
        }
        else
        {
            section["notesLayout"] = layoutValue;
        }

        var alignmentValue = section["alignment"] is JsonValue alignmentNode && alignmentNode.TryGetValue<string>(out var alignmentString)
            ? alignmentString?.ToLowerInvariant()
            : null;

        if (alignmentValue is "left" or "center" or "right")
        {
            section["alignment"] = alignmentValue;
        }
        else
        {
            section["alignment"] = "left";
            changed = true;
        }

        if (EnsureLocalizedProperty(section, "title"))
        {
            changed = true;
        }

        if (EnsureLocalizedProperty(section, "description"))
        {
            changed = true;
        }

        if (EnsurePictureProperty(section, "backgroundImage"))
        {
            changed = true;
        }

        if (section["notes"] is JsonArray notesArray)
        {
            if (notesArray.Count == 0)
            {
                notesArray.Add(CreateEmptyNoteObject());
                changed = true;
            }

            for (int i = 0; i < notesArray.Count; i++)
            {
                if (notesArray[i] is JsonObject noteObj)
                {
                    if (EnsureNoteStructure(noteObj))
                    {
                        changed = true;
                    }
                }
                else
                {
                    notesArray[i] = CreateEmptyNoteObject();
                    changed = true;
                }
            }
        }
        else
        {
            section["notes"] = new JsonArray { CreateEmptyNoteObject() };
            changed = true;
        }

        return changed;
    }

    private bool EnsureNoteStructure(JsonObject note)
    {
        bool changed = false;

        if (EnsurePictureProperty(note, "picture"))
        {
            changed = true;
        }

        if (EnsureColorValue(note, "symbolForeColor", "#1f2937"))
        {
            changed = true;
        }

        if (EnsureStringValue(note, "symbol", string.Empty))
        {
            changed = true;
        }

        if (EnsureStringValue(note, "foreColor", string.Empty))
        {
            changed = true;
        }

        if (EnsureStringValue(note, "backColor", string.Empty))
        {
            changed = true;
        }

        if (EnsureLocalizedProperty(note, "title"))
        {
            changed = true;
        }

        if (EnsureLocalizedProperty(note, "caption"))
        {
            changed = true;
        }

        if (EnsureLocalizedProperty(note, "text"))
        {
            changed = true;
        }

        return changed;
    }

    private bool EnsurePictureProperty(JsonObject parent, string propertyName)
    {
        if (parent[propertyName] is JsonObject pictureObj)
        {
            if (pictureObj["url"] is JsonValue urlValue && urlValue.TryGetValue<string>(out var url))
            {
                pictureObj["url"] = url ?? string.Empty;
                return false;
            }

            pictureObj["url"] = string.Empty;
            return true;
        }

        parent[propertyName] = new JsonObject { ["url"] = string.Empty };
        return true;
    }

    private bool EnsureLocalizedProperty(JsonObject parent, string propertyName)
    {
        if (parent[propertyName] is JsonObject localizedObj)
        {
            return EnsureLocalizedObject(localizedObj);
        }

        parent[propertyName] = CreateEmptyLocalizedObject();
        return true;
    }

    private bool EnsureLocalizedObject(JsonObject obj)
    {
        bool changed = false;
        foreach (var language in SupportedLanguages)
        {
            if (obj[language] is JsonValue value && value.TryGetValue<string>(out var str) && str is not null)
            {
                obj[language] = str;
            }
            else
            {
                obj[language] = string.Empty;
                changed = true;
            }
        }

        return changed;
    }

    private bool EnsureStringValue(JsonObject parent, string propertyName, string defaultValue)
    {
        if (parent[propertyName] is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var str) && str is not null)
        {
            parent[propertyName] = str;
            return false;
        }

        parent[propertyName] = defaultValue;
        return true;
    }

    private bool EnsureColorValue(JsonObject parent, string propertyName, string defaultValue)
    {
        if (parent[propertyName] is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var str) && !string.IsNullOrWhiteSpace(str) && Regex.IsMatch(str, "^#[0-9A-Fa-f]{6}$"))
        {
            parent[propertyName] = str;
            return false;
        }

        parent[propertyName] = defaultValue;
        return true;
    }

    private JsonObject CreateEmptyLocalizedObject()
    {
        var obj = new JsonObject();
        foreach (var language in SupportedLanguages)
        {
            obj[language] = string.Empty;
        }
        return obj;
    }

    private JsonObject CreateEmptyNoteObject()
    {
        return new JsonObject
        {
            ["picture"] = new JsonObject { ["url"] = string.Empty },
            ["symbolForeColor"] = "#1f2937",
            ["symbol"] = string.Empty,
            ["foreColor"] = string.Empty,
            ["backColor"] = string.Empty,
            ["title"] = CreateEmptyLocalizedObject(),
            ["caption"] = CreateEmptyLocalizedObject(),
            ["text"] = CreateEmptyLocalizedObject()
        };
    }

    private JsonObject CreateEmptySectionObject()
    {
        return new JsonObject
        {
            ["backColor"] = "#ffffff",
            ["foreColor"] = "#000000",
            ["notesLayout"] = "vertical",
            ["alignment"] = "left",
            ["backgroundImage"] = new JsonObject { ["url"] = string.Empty },
            ["title"] = CreateEmptyLocalizedObject(),
            ["description"] = CreateEmptyLocalizedObject(),
            ["notes"] = new JsonArray { CreateEmptyNoteObject() }
        };
    }

    private static string GetPictureUrl(JsonNode? node)
    {
        if (node is JsonObject obj && obj["url"] is JsonValue urlValue && urlValue.TryGetValue<string>(out var url) && !string.IsNullOrWhiteSpace(url))
        {
            return url;
        }

        if (node is JsonValue value && value.TryGetValue<string>(out var str) && !string.IsNullOrWhiteSpace(str))
        {
            return str;
        }

        return string.Empty;
    }

    private async Task<string> NormalizeAndSavePictureAsync(Guid companyId, string designDirectory, string pictureUrl, int sectionIndex, int noteIndex, string publicBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(pictureUrl))
        {
            return string.Empty;
        }

        try
        {
            if (pictureUrl.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                var match = Regex.Match(pictureUrl, @"^data:(?<mime>[\w/\-\.]+);base64,(?<data>.+)$");
                if (!match.Success)
                {
                    _logger.LogWarning("Invalid data URL format for company {CompanyId}", companyId);
                    return string.Empty;
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
                    _logger.LogWarning(ex, "Failed to decode base64 image for company {CompanyId}", companyId);
                    return string.Empty;
                }

                var extension = mimeType switch
                {
                    "image/jpeg" or "image/jpg" => "jpg",
                    "image/gif" => "gif",
                    "image/webp" => "webp",
                    "image/svg+xml" => "svg",
                    _ => "png"
                };

                Directory.CreateDirectory(designDirectory);

                var fileName = $"note-{sectionIndex}-{noteIndex}-{DateTime.UtcNow:yyyyMMddHHmmssfff}.{extension}";
                var filePath = Path.Combine(designDirectory, fileName);

                await System.IO.File.WriteAllBytesAsync(filePath, fileBytes);

                var relativePath = $"/public/{companyId}/design/{fileName}".Replace("\\", "/");
                return CombineWithBase(publicBaseUrl, relativePath);
            }

            if (pictureUrl.StartsWith("/public/", StringComparison.OrdinalIgnoreCase))
            {
                return CombineWithBase(publicBaseUrl, pictureUrl.Replace("\\", "/"));
            }

            if (pictureUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || pictureUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return pictureUrl;
            }

            // Treat as relative path and ensure it points inside the design directory
            var fallbackPath = $"/public/{companyId}/design/{pictureUrl.TrimStart('/', '\\')}".Replace("\\", "/");
            return CombineWithBase(publicBaseUrl, fallbackPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process picture for company {CompanyId}", companyId);
            return string.Empty;
        }
    }

    private async Task<string> NormalizeAndSaveSectionBackgroundAsync(Guid companyId, string sectionsDirectory, string backgroundUrl, int sectionIndex, string publicBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(backgroundUrl))
        {
            return string.Empty;
        }

        try
        {
            if (backgroundUrl.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                var match = Regex.Match(backgroundUrl, @"^data:(?<mime>[\w/\-\.]+);base64,(?<data>.+)$");
                if (!match.Success)
                {
                    _logger.LogWarning("Invalid data URL format for section background on company {CompanyId}", companyId);
                    return string.Empty;
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
                    _logger.LogWarning(ex, "Failed to decode base64 section background for company {CompanyId}", companyId);
                    return string.Empty;
                }

                var extension = mimeType switch
                {
                    "image/jpeg" or "image/jpg" => "jpg",
                    "image/gif" => "gif",
                    "image/webp" => "webp",
                    "image/svg+xml" => "svg",
                    _ => "png"
                };

                Directory.CreateDirectory(sectionsDirectory);

                foreach (var existing in Directory.EnumerateFiles(sectionsDirectory, $"section{sectionIndex + 1}.*"))
                {
                    try
                    {
                        System.IO.File.Delete(existing);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete existing background image for section {SectionIndex} company {CompanyId}", sectionIndex, companyId);
                    }
                }

                var fileName = $"section{sectionIndex + 1}.{extension}";
                var filePath = Path.Combine(sectionsDirectory, fileName);
                await System.IO.File.WriteAllBytesAsync(filePath, fileBytes);

                var relativePath = $"/public/{companyId}/sections/{fileName}".Replace("\\", "/");
                return CombineWithBase(publicBaseUrl, relativePath);
            }

            if (backgroundUrl.StartsWith("/public/", StringComparison.OrdinalIgnoreCase))
            {
                return CombineWithBase(publicBaseUrl, backgroundUrl.Replace("\\", "/"));
            }

            if (backgroundUrl.StartsWith("/"))
            {
                return CombineWithBase(publicBaseUrl, backgroundUrl.Replace("\\", "/"));
            }

            if (backgroundUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || backgroundUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return backgroundUrl;
            }

            var sanitized = $"/public/{companyId}/sections/{backgroundUrl.TrimStart('/', '\\')}".Replace("\\", "/");
            return CombineWithBase(publicBaseUrl, sanitized);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process section background for company {CompanyId}", companyId);
            return string.Empty;
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

                var targetDirectory = Path.Combine(_environment.ContentRootPath, "wwwroot", "public", companyId.ToString());
                Directory.CreateDirectory(targetDirectory);

                foreach (var existing in Directory.EnumerateFiles(targetDirectory, $"{assetName}.*"))
                {
                    try
                    {
                        System.IO.File.Delete(existing);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete existing {AssetName} file {File} for company {CompanyId}", assetName, existing, companyId);
                    }
                }

                var fileName = $"{assetName}.{extension}";
                var filePath = Path.Combine(targetDirectory, fileName);

                await System.IO.File.WriteAllBytesAsync(filePath, fileBytes);

                var relativePath = $"/public/{companyId}/{fileName}".Replace("\\", "/");
                return CombineWithBase(publicBaseUrl, relativePath);
            }

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
            return null;
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

    // Helper method to map RentalCompany to CompanyConfigDto
    private CompanyConfigDto MapToConfigDto(Company company)
    {
        return new CompanyConfigDto
        {
            Id = company.Id,
            CompanyName = company.CompanyName,
            Subdomain = company.Subdomain ?? string.Empty,
            FullDomain = string.IsNullOrEmpty(company.Subdomain) ? string.Empty : $"{company.Subdomain}.aegis-rental.com",
            Email = company.Email,
            LogoUrl = company.LogoUrl,
            FaviconUrl = company.FaviconUrl,
            PrimaryColor = company.PrimaryColor,
            SecondaryColor = company.SecondaryColor,
            Motto = company.Motto,
            MottoDescription = company.MottoDescription,
            About = company.About,
            TermsOfUse = company.TermsOfUse,
            VideoLink = company.VideoLink,
            BannerLink = company.BannerLink,
            BackgroundLink = company.BackgroundLink,
            Website = company.Website,
            CustomCss = company.CustomCss,
            Country = company.Country,
            BookingIntegrated = !string.IsNullOrEmpty(company.BookingIntegrated) && 
                              (company.BookingIntegrated.ToLower() == "true" || company.BookingIntegrated == "1"),
            Invitation = company.Invitation,
            Texts = company.Texts,
            Language = company.Language ?? "en",
            BlinkKey = company.BlinkKey,
            Currency = company.Currency,
            AiIntegration = NormalizeAiIntegration(company.AiIntegration),
            SecurityDeposit = company.SecurityDeposit
        };
    }

    private static string NormalizeAiIntegration(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DefaultAiIntegration;
        }

        var normalized = value.Trim().ToLowerInvariant();
        return AllowedAiIntegrations.Contains(normalized) ? normalized : DefaultAiIntegration;
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
                var countrySettings = await _context.StripeSettings
                    .FirstOrDefaultAsync(s => s.Name.ToLower() == countryCode.ToLower());
                
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
}

public class CreateCompanyRequest
{
    public string CompanyName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Subdomain { get; set; } // Optional - can be set manually later
    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }
    public string? LogoUrl { get; set; }
    public string? FaviconUrl { get; set; }
    public string? Country { get; set; }
    public string? Currency { get; set; }
    public string? Language { get; set; }
    public string? Motto { get; set; }
    public string? MottoDescription { get; set; }
    public string? About { get; set; }
    public string? Website { get; set; }
    public string? CustomCss { get; set; }
    public string? VideoLink { get; set; }
    public string? BannerLink { get; set; }
    public string? BackgroundLink { get; set; }
    public string? Invitation { get; set; }
    public string? Texts { get; set; }
    public bool? BookingIntegrated { get; set; }
    public string? TaxId { get; set; }
    public string? StripeAccountId { get; set; }
    public string? BlinkKey { get; set; } // BlinkID license key for the company
    public bool? IsActive { get; set; }
    public bool? IsTestCompany { get; set; }
    public string? AiIntegration { get; set; }
    public decimal? SecurityDeposit { get; set; }
    public string? TermsOfUse { get; set; }
}

public class UpdateCompanyRequest
{
    public string? CompanyName { get; set; }
    public string? Email { get; set; }
    public string? Subdomain { get; set; }
    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }
    public string? LogoUrl { get; set; }
    public string? FaviconUrl { get; set; }
    public string? Country { get; set; }
    public string? Currency { get; set; }
    public string? Language { get; set; }
    public string? Motto { get; set; }
    public string? MottoDescription { get; set; }
    public string? About { get; set; }
    public string? Website { get; set; }
    public string? CustomCss { get; set; }
    public string? VideoLink { get; set; }
    public string? BannerLink { get; set; }
    public string? BackgroundLink { get; set; }
    public string? Invitation { get; set; }
    public string? Texts { get; set; }
    public bool? BookingIntegrated { get; set; }
    public string? TaxId { get; set; }
    public Guid? StripeSettingsId { get; set; }
    public bool? IsTestCompany { get; set; }
    public string? StripeAccountId { get; set; }
    public string? BlinkKey { get; set; } // BlinkID license key for the company
    public bool? IsActive { get; set; }
    public string? AiIntegration { get; set; }
    public decimal? SecurityDeposit { get; set; }
    public string? TermsOfUse { get; set; }
}

