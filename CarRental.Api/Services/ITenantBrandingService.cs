using CarRental.Api.Models;

namespace CarRental.Api.Services;

/// <summary>
/// Service for retrieving tenant branding configuration
/// </summary>
public interface ITenantBrandingService
{
    /// <summary>
    /// Get branding configuration for a specific tenant (company)
    /// </summary>
    Task<TenantBranding> GetTenantBrandingAsync(Guid companyId);

    /// <summary>
    /// Update or create tenant branding
    /// </summary>
    Task<bool> SaveTenantBrandingAsync(TenantBranding branding);

    /// <summary>
    /// Check if tenant has custom branding configured
    /// </summary>
    Task<bool> HasCustomBrandingAsync(Guid companyId);
}

