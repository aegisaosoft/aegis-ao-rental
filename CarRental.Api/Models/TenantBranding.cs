namespace CarRental.Api.Models;

/// <summary>
/// Tenant branding configuration for email templates
/// </summary>
public class TenantBranding
{
    /// <summary>
    /// Tenant ID (CompanyId as string)
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Company name to display in emails
    /// </summary>
    public string CompanyName { get; set; } = string.Empty;

    /// <summary>
    /// Primary brand color (hex format, e.g., #2563eb)
    /// </summary>
    public string BrandColor { get; set; } = "#2563eb";

    /// <summary>
    /// Logo URL (should be publicly accessible)
    /// </summary>
    public string? LogoUrl { get; set; }

    /// <summary>
    /// Support email address
    /// </summary>
    public string SupportEmail { get; set; } = string.Empty;

    /// <summary>
    /// Support phone number
    /// </summary>
    public string? SupportPhone { get; set; }

    /// <summary>
    /// Company website URL
    /// </summary>
    public string? WebsiteUrl { get; set; }

    /// <summary>
    /// Company address (optional, for footer)
    /// </summary>
    public string? Address { get; set; }

    /// <summary>
    /// From email address for this tenant
    /// </summary>
    public string? FromEmail { get; set; }

    /// <summary>
    /// Secondary color for accents (optional)
    /// </summary>
    public string? SecondaryColor { get; set; }

    /// <summary>
    /// Footer text (optional custom footer)
    /// </summary>
    public string? FooterText { get; set; }
}

