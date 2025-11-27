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

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace CarRental.Api.DTOs;

public class RentalCompanyDto
{
    public Guid CompanyId { get; set; }
    
    [Required]
    [MaxLength(255)]
    public string CompanyName { get; set; } = string.Empty;
    
    [Required]
    [EmailAddress]
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;
    
    [MaxLength(255)]
    public string? Website { get; set; }
    
    public string? StripeAccountId { get; set; }
    
    public Guid? StripeSettingsId { get; set; }
    
    [MaxLength(100)]
    public string? TaxId { get; set; }
    
    [MaxLength(500)]
    public string? VideoLink { get; set; }
    
    // Allow base64 data URLs (can be very long) - backend will convert to file URLs before saving
    // Base64 encoding increases size by ~33%, so a 2MB image becomes ~2.67MB = ~2,670,000 chars
    // Setting to 3000000 (3M chars) to handle images up to ~2.25MB original size
    [MaxLength(3000000)]
    public string? BannerLink { get; set; }
    
    [MaxLength(500)]
    public string? LogoLink { get; set; }
    
    [MaxLength(255)]
    public string? Motto { get; set; }
    
    [MaxLength(500)]
    public string? MottoDescription { get; set; }
    
    public string? Invitation { get; set; }
    
    public string? Texts { get; set; } // JSONB field
    
    // Allow base64 data URLs (can be very long) - backend will convert to file URLs before saving
    // Base64 encoding increases size by ~33%, so a 2MB image becomes ~2.67MB = ~2,670,000 chars
    // Setting to 3000000 (3M chars) to handle images up to ~2.25MB original size
    [MaxLength(3000000)]
    public string? BackgroundLink { get; set; }
    
    public string? About { get; set; }
    
    public string? TermsOfUse { get; set; }
    
    public string? BookingIntegrated { get; set; }

    [MaxLength(3)]
    public string Currency { get; set; } = "USD";

    public decimal SecurityDeposit { get; set; } = 1000m;

    public bool IsSecurityDepositMandatory { get; set; } = true;
    
    public string? CompanyPath { get; set; }
    
    [MaxLength(100)]
    public string? Subdomain { get; set; }
    
    [MaxLength(7)]
    public string? PrimaryColor { get; set; }
    
    [MaxLength(7)]
    public string? SecondaryColor { get; set; }
    
    // Allow base64 data URLs (can be very long) - backend will convert to file URLs before saving
    // Base64 encoding increases size by ~33%, so a 2MB image becomes ~2.67MB = ~2,670,000 chars
    // Setting to 3000000 (3M chars) to handle images up to ~2.25MB original size
    [MaxLength(3000000)]
    public string? LogoUrl { get; set; }
    
    // Allow base64 data URLs (can be very long) - backend will convert to file URLs before saving
    // Base64 encoding increases size by ~33%, so a 2MB image becomes ~2.67MB = ~2,670,000 chars
    // Setting to 3000000 (3M chars) to handle images up to ~2.25MB original size
    [MaxLength(3000000)]
    public string? FaviconUrl { get; set; }
    
    public string? CustomCss { get; set; }
    
    [MaxLength(100)]
    public string? Country { get; set; }
    
    public string? BlinkKey { get; set; } // BlinkID license key for the company
    
    public bool IsActive { get; set; }
    public bool IsTestCompany { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateRentalCompanyDto
{
    [Required]
    [MaxLength(255)]
    public string CompanyName { get; set; } = string.Empty;
    
    [Required]
    [EmailAddress]
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;
    
    [MaxLength(255)]
    public string? Website { get; set; }
    
    [MaxLength(100)]
    public string? TaxId { get; set; }
    
    [MaxLength(500)]
    public string? VideoLink { get; set; }
    
    // Allow base64 data URLs (can be very long) - backend will convert to file URLs before saving
    // Base64 encoding increases size by ~33%, so a 2MB image becomes ~2.67MB = ~2,670,000 chars
    // Setting to 3000000 (3M chars) to handle images up to ~2.25MB original size
    [MaxLength(3000000)]
    public string? BannerLink { get; set; }
    
    [MaxLength(500)]
    public string? LogoLink { get; set; }
    
    [MaxLength(255)]
    public string? Motto { get; set; }
    
    [MaxLength(500)]
    public string? MottoDescription { get; set; }
    
    public string? Invitation { get; set; }
    
    public string? Texts { get; set; } // JSONB field
    
    // Allow base64 data URLs (can be very long) - backend will convert to file URLs before saving
    // Base64 encoding increases size by ~33%, so a 2MB image becomes ~2.67MB = ~2,670,000 chars
    // Setting to 3000000 (3M chars) to handle images up to ~2.25MB original size
    [MaxLength(3000000)]
    public string? BackgroundLink { get; set; }
    
    public string? About { get; set; }
    
    public string? TermsOfUse { get; set; }
    
    public bool BookingIntegrated { get; set; }
    
    public string? CompanyPath { get; set; }
    
    [MaxLength(100)]
    public string? Subdomain { get; set; }
    
    [MaxLength(7)]
    public string? PrimaryColor { get; set; }
    
    [MaxLength(7)]
    public string? SecondaryColor { get; set; }
    
    // Allow base64 data URLs (can be very long) - backend will convert to file URLs before saving
    // Base64 encoding increases size by ~33%, so a 2MB image becomes ~2.67MB = ~2,670,000 chars
    // Setting to 3000000 (3M chars) to handle images up to ~2.25MB original size
    [MaxLength(3000000)]
    public string? LogoUrl { get; set; }
    
    // Allow base64 data URLs (can be very long) - backend will convert to file URLs before saving
    // Base64 encoding increases size by ~33%, so a 2MB image becomes ~2.67MB = ~2,670,000 chars
    // Setting to 3000000 (3M chars) to handle images up to ~2.25MB original size
    [MaxLength(3000000)]
    public string? FaviconUrl { get; set; }
    
    public string? CustomCss { get; set; }
    
    [MaxLength(100)]
    public string? Country { get; set; }
    
    [MaxLength(3)]
    public string? Currency { get; set; }

    public decimal? SecurityDeposit { get; set; }

    public bool? IsSecurityDepositMandatory { get; set; }

    public string? BlinkKey { get; set; } // BlinkID license key for the company
    
    public bool? IsTestCompany { get; set; }
}

public class UpdateRentalCompanyDto
{
    [MaxLength(255)]
    public string? CompanyName { get; set; }
    
    [EmailAddress]
    [MaxLength(255)]
    public string? Email { get; set; }
    
    [MaxLength(255)]
    public string? Website { get; set; }
    
    [MaxLength(100)]
    public string? TaxId { get; set; }
    
    public Guid? StripeSettingsId { get; set; }
    
    [MaxLength(500)]
    public string? VideoLink { get; set; }
    
    // Allow base64 data URLs (can be very long) - backend will convert to file URLs before saving
    // Base64 encoding increases size by ~33%, so a 2MB image becomes ~2.67MB = ~2,670,000 chars
    // Setting to 3000000 (3M chars) to handle images up to ~2.25MB original size
    [MaxLength(3000000)]
    public string? BannerLink { get; set; }
    
    [MaxLength(500)]
    public string? LogoLink { get; set; }
    
    [MaxLength(255)]
    public string? Motto { get; set; }
    
    [MaxLength(500)]
    public string? MottoDescription { get; set; }
    
    public string? Invitation { get; set; }
    
    public string? Texts { get; set; } // JSONB field
    
    // Allow base64 data URLs (can be very long) - backend will convert to file URLs before saving
    // Base64 encoding increases size by ~33%, so a 2MB image becomes ~2.67MB = ~2,670,000 chars
    // Setting to 3000000 (3M chars) to handle images up to ~2.25MB original size
    [MaxLength(3000000)]
    public string? BackgroundLink { get; set; }
    
    public string? About { get; set; }
    
    [JsonPropertyName("termsOfUse")]
    public string? TermsOfUse { get; set; }
    
    public bool? BookingIntegrated { get; set; }
    
    public string? CompanyPath { get; set; }
    
    [MaxLength(100)]
    public string? Subdomain { get; set; }
    
    [MaxLength(7)]
    public string? PrimaryColor { get; set; }
    
    [MaxLength(7)]
    public string? SecondaryColor { get; set; }
    
    // Allow base64 data URLs (can be very long) - backend will convert to file URLs before saving
    // Base64 encoding increases size by ~33%, so a 2MB image becomes ~2.67MB = ~2,670,000 chars
    // Setting to 3000000 (3M chars) to handle images up to ~2.25MB original size
    [MaxLength(3000000)]
    public string? LogoUrl { get; set; }
    
    // Allow base64 data URLs (can be very long) - backend will convert to file URLs before saving
    // Base64 encoding increases size by ~33%, so a 2MB image becomes ~2.67MB = ~2,670,000 chars
    // Setting to 3000000 (3M chars) to handle images up to ~2.25MB original size
    [MaxLength(3000000)]
    public string? FaviconUrl { get; set; }
    
    public string? CustomCss { get; set; }
    
    [MaxLength(100)]
    public string? Country { get; set; }
    
    [MaxLength(3)]
    public string? Currency { get; set; }
    
    [MaxLength(10)]
    public string? Language { get; set; }

    public string? BlinkKey { get; set; } // BlinkID license key for the company

    public decimal? SecurityDeposit { get; set; }

    public bool? IsSecurityDepositMandatory { get; set; }
    
    [MaxLength(20)]
    public string? AiIntegration { get; set; }
    
    public bool? IsActive { get; set; }
    
    public bool? IsTestCompany { get; set; }
}

public class UpdateTermsOfUseDto
{
    [JsonPropertyName("termsOfUse")]
    public string? TermsOfUse { get; set; }
}

// DTO for company configuration (public API - used by frontend)
public class CompanyConfigDto
{
    public Guid Id { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string Subdomain { get; set; } = string.Empty;
    public string FullDomain { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string? FaviconUrl { get; set; }
    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }
    public string? Motto { get; set; }
    public string? MottoDescription { get; set; }
    public string? About { get; set; }
    public string? VideoLink { get; set; }
    public string? BannerLink { get; set; }
    public string? BackgroundLink { get; set; }
    public string? Website { get; set; }
    public string? CustomCss { get; set; }
    public string? Country { get; set; }
    public bool BookingIntegrated { get; set; }
    public string? Invitation { get; set; }
    public string? Texts { get; set; }
    public string? Language { get; set; }
    public string? BlinkKey { get; set; } // BlinkID license key for the company
    public string Currency { get; set; } = "USD";
    public string AiIntegration { get; set; } = "claude";
    public decimal SecurityDeposit { get; set; } = 1000m;
    public bool IsSecurityDepositMandatory { get; set; } = false;
    public string? TermsOfUse { get; set; }
}

// DTO for company list (admin)
public class CompanyListDto
{
    public Guid Id { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string Subdomain { get; set; } = string.Empty;
    public string FullDomain { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool BookingIntegrated { get; set; }
    public string? Country { get; set; }
    public string Currency { get; set; } = "USD";
    public string AiIntegration { get; set; } = "claude";
    public decimal SecurityDeposit { get; set; } = 1000m;
    public bool IsSecurityDepositMandatory { get; set; } = false;
    public DateTime CreatedAt { get; set; }
}

// DTO for company details (admin)
public class CompanyDetailDto
{
    public Guid Id { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Subdomain { get; set; } = string.Empty;
    public string FullDomain { get; set; } = string.Empty;
    public string? StripeAccountId { get; set; }
    public string? TaxId { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? VideoLink { get; set; }
    public string? BannerLink { get; set; }
    public string? LogoLink { get; set; }
    public string? Motto { get; set; }
    public string? MottoDescription { get; set; }
    public string? Invitation { get; set; }
    public string? Texts { get; set; }
    public string? Website { get; set; }
    public string? BackgroundLink { get; set; }
    public string? About { get; set; }
    public string? CompanyPath { get; set; }
    public bool BookingIntegrated { get; set; }
    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }
    public string? LogoUrl { get; set; }
    public string? FaviconUrl { get; set; }
    public string? CustomCss { get; set; }
    public string? Country { get; set; }
    public string? Language { get; set; }
    public string? BlinkKey { get; set; } // BlinkID license key for the company
    public string Currency { get; set; } = "USD";
    public string AiIntegration { get; set; } = "claude";
    public decimal SecurityDeposit { get; set; } = 1000m;
    public bool IsSecurityDepositMandatory { get; set; } = true;
    public bool IsTestCompany { get; set; } = true;
}
