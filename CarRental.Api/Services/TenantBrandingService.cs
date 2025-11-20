using CarRental.Api.Data;
using CarRental.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CarRental.Api.Services;

/// <summary>
/// Service for managing tenant branding with caching
/// Uses Company model as the source of branding data
/// </summary>
public class TenantBrandingService : ITenantBrandingService
{
    private readonly CarRentalDbContext _context;
    private readonly ILogger<TenantBrandingService> _logger;
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromHours(24);

    public TenantBrandingService(
        CarRentalDbContext context,
        ILogger<TenantBrandingService> logger,
        IMemoryCache cache)
    {
        _context = context;
        _logger = logger;
        _cache = cache;
    }

    public async Task<TenantBranding> GetTenantBrandingAsync(Guid companyId)
    {
        // Try to get from cache first
        var cacheKey = $"tenant_branding_{companyId}";
        if (_cache.TryGetValue(cacheKey, out TenantBranding? cachedBranding))
        {
            _logger.LogDebug("Retrieved tenant branding from cache for company {CompanyId}", companyId);
            return cachedBranding!;
        }

        // Load from database (Company table)
        var company = await _context.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == companyId);

        TenantBranding branding;

        if (company == null)
        {
            _logger.LogWarning("Company {CompanyId} not found, using default branding", companyId);
            branding = GetDefaultBranding(companyId);
        }
        else
        {
            branding = new TenantBranding
            {
                TenantId = companyId.ToString(),
                CompanyName = company.CompanyName ?? "Car Rental",
                BrandColor = company.PrimaryColor ?? "#2563eb",
                SecondaryColor = company.SecondaryColor ?? "#059669",
                LogoUrl = company.LogoUrl,
                SupportEmail = company.Email ?? "support@example.com",
                SupportPhone = null, // Company model doesn't have phone field
                WebsiteUrl = company.Website,
                Address = null, // Company model doesn't have address field
                FromEmail = null, // Will use default from settings
                FooterText = company.Motto
            };
        }

        // Cache the result
        _cache.Set(cacheKey, branding, _cacheExpiration);
        _logger.LogInformation("Loaded and cached branding for company {CompanyId}", companyId);

        return branding;
    }

    public async Task<bool> SaveTenantBrandingAsync(TenantBranding branding)
    {
        try
        {
            if (branding == null || string.IsNullOrEmpty(branding.TenantId))
            {
                throw new ArgumentException("Invalid branding configuration");
            }

            if (!Guid.TryParse(branding.TenantId, out var companyId))
            {
                throw new ArgumentException("Invalid tenant ID format");
            }

            var company = await _context.Companies.FirstOrDefaultAsync(c => c.Id == companyId);
            if (company == null)
            {
                _logger.LogWarning("Company {CompanyId} not found, cannot save branding", companyId);
                return false;
            }

            // Update company with branding data
            company.PrimaryColor = branding.BrandColor;
            company.SecondaryColor = branding.SecondaryColor;
            company.LogoUrl = branding.LogoUrl;
            company.Motto = branding.FooterText;
            company.Website = branding.WebsiteUrl;
            company.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Invalidate cache
            var cacheKey = $"tenant_branding_{companyId}";
            _cache.Remove(cacheKey);

            _logger.LogInformation("Saved branding for company {CompanyId}", companyId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving tenant branding for {TenantId}", branding?.TenantId);
            return false;
        }
    }

    public async Task<bool> HasCustomBrandingAsync(Guid companyId)
    {
        var branding = await GetTenantBrandingAsync(companyId);
        
        // Check if it's using default values
        return branding != null && 
               !string.IsNullOrEmpty(branding.LogoUrl) &&
               branding.CompanyName != "Car Rental";
    }

    private TenantBranding GetDefaultBranding(Guid companyId)
    {
        return new TenantBranding
        {
            TenantId = companyId.ToString(),
            CompanyName = "Car Rental",
            BrandColor = "#2563eb",
            SecondaryColor = "#059669",
            SupportEmail = "support@example.com",
            FromEmail = null
        };
    }
}

