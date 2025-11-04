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
using System.Text.Json;

namespace CarRental.Api.Controllers;

[ApiController]
[Route("api/companies")]
[Authorize] // Require authentication for all endpoints
public class CompaniesController : ControllerBase
{
    private readonly CarRentalDbContext _context;
    private readonly ILogger<CompaniesController> _logger;
    private readonly ICompanyService _companyService;

    public CompaniesController(
        CarRentalDbContext context, 
        ILogger<CompaniesController> logger,
        ICompanyService companyService)
    {
        _context = context;
        _logger = logger;
        _companyService = companyService;
    }

    /// <summary>
    /// Get all companies with optional filtering
    /// </summary>
    [HttpGet]
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
                bookingIntegrated = !string.IsNullOrEmpty(c.BookingIntegrated) && (c.BookingIntegrated.ToLower() == "true" || c.BookingIntegrated == "1"),
                taxId = c.TaxId,
                stripeAccountId = c.StripeAccountId,
                isActive = c.IsActive,
                createdAt = c.CreatedAt,
                updatedAt = c.UpdatedAt
            });

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting companies");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get company by ID
    /// </summary>
    [HttpGet("{id}")]
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
                bookingIntegrated = !string.IsNullOrEmpty(company.BookingIntegrated) && (company.BookingIntegrated.ToLower() == "true" || company.BookingIntegrated == "1"),
                taxId = company.TaxId,
                stripeAccountId = company.StripeAccountId,
                isActive = company.IsActive,
                createdAt = company.CreatedAt,
                updatedAt = company.UpdatedAt
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
                
                // Check if subdomain already exists
                var existingSubdomain = await _context.Companies
                    .FirstOrDefaultAsync(c => c.Subdomain != null && c.Subdomain.ToLower() == subdomain);

                if (existingSubdomain != null)
                {
                    return Conflict(new { error = $"Subdomain '{subdomain}' already exists" });
                }
            }

            // Check if email already exists
            var existingEmail = await _context.Companies
                .FirstOrDefaultAsync(c => c.Email.ToLower() == request.Email.ToLower());

            if (existingEmail != null)
            {
                return Conflict(new { error = "Email already exists" });
            }

            var company = new RentalCompany
            {
                CompanyName = request.CompanyName,
                Email = request.Email,
                Subdomain = subdomain, // Can be NULL - set manually later
                PrimaryColor = request.PrimaryColor ?? "#007bff",
                SecondaryColor = request.SecondaryColor ?? "#6c757d",
                LogoUrl = request.LogoUrl,
                FaviconUrl = request.FaviconUrl,
                Country = request.Country,
                Language = request.Language ?? "en",
                Motto = request.Motto,
                MottoDescription = request.MottoDescription,
                About = request.About,
                Website = request.Website,
                CustomCss = request.CustomCss,
                VideoLink = request.VideoLink,
                BannerLink = request.BannerLink,
                BackgroundLink = request.BackgroundLink,
                Invitation = request.Invitation,
                BookingIntegrated = request.BookingIntegrated.HasValue && request.BookingIntegrated.Value ? "true" : null,
                TaxId = request.TaxId,
                StripeAccountId = request.StripeAccountId,
                IsActive = request.IsActive ?? true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Companies.Add(company);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Company created: {CompanyName} (ID: {CompanyId})", company.CompanyName, company.Id);

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
                bookingIntegrated = !string.IsNullOrEmpty(company.BookingIntegrated) && (company.BookingIntegrated.ToLower() == "true" || company.BookingIntegrated == "1"),
                taxId = company.TaxId,
                stripeAccountId = company.StripeAccountId,
                isActive = company.IsActive,
                createdAt = company.CreatedAt,
                updatedAt = company.UpdatedAt
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
                company.LogoUrl = request.LogoUrl;

            if (request.FaviconUrl != null)
                company.FaviconUrl = request.FaviconUrl;

            if (request.Country != null)
                company.Country = request.Country;

            if (request.Language != null)
                company.Language = request.Language;

            if (request.Motto != null)
                company.Motto = request.Motto;

            if (request.MottoDescription != null)
                company.MottoDescription = request.MottoDescription;

            if (request.About != null)
                company.About = request.About;

            if (request.Website != null)
                company.Website = request.Website;

            if (request.CustomCss != null)
                company.CustomCss = request.CustomCss;

            if (request.VideoLink != null)
                company.VideoLink = request.VideoLink;

            if (request.BannerLink != null)
                company.BannerLink = request.BannerLink;

            if (request.BackgroundLink != null)
                company.BackgroundLink = request.BackgroundLink;

            if (request.Invitation != null)
                company.Invitation = request.Invitation;

            if (request.BookingIntegrated.HasValue)
                company.BookingIntegrated = request.BookingIntegrated.Value ? "true" : null;

            if (request.TaxId != null)
                company.TaxId = request.TaxId;

            if (request.StripeAccountId != null)
                company.StripeAccountId = request.StripeAccountId;

            if (request.IsActive.HasValue)
                company.IsActive = request.IsActive.Value;

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
                bookingIntegrated = !string.IsNullOrEmpty(company.BookingIntegrated) && (company.BookingIntegrated.ToLower() == "true" || company.BookingIntegrated == "1"),
                taxId = company.TaxId,
                stripeAccountId = company.StripeAccountId,
                isActive = company.IsActive,
                createdAt = company.CreatedAt,
                updatedAt = company.UpdatedAt
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
            
            if (!companyId.HasValue)
            {
                return BadRequest(new { error = "Company ID is required. Domain-based company resolution failed." });
            }

            var company = await _companyService.GetCompanyByIdAsync(companyId.Value);
            
            if (company == null)
            {
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

    // Helper method to map RentalCompany to CompanyConfigDto
    private CompanyConfigDto MapToConfigDto(RentalCompany company)
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
            Language = company.Language ?? "en"
        };
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
    public bool? BookingIntegrated { get; set; }
    public string? TaxId { get; set; }
    public string? StripeAccountId { get; set; }
    public bool? IsActive { get; set; }
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
    public bool? BookingIntegrated { get; set; }
    public string? TaxId { get; set; }
    public string? StripeAccountId { get; set; }
    public bool? IsActive { get; set; }
}

