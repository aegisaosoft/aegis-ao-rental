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
    
    [MaxLength(100)]
    public string? TaxId { get; set; }
    
    [MaxLength(500)]
    public string? VideoLink { get; set; }
    
    [MaxLength(500)]
    public string? BannerLink { get; set; }
    
    [MaxLength(500)]
    public string? LogoLink { get; set; }
    
    [MaxLength(255)]
    public string? Motto { get; set; }
    
    [MaxLength(500)]
    public string? MottoDescription { get; set; }
    
    public string? Invitation { get; set; }
    
    public string? Texts { get; set; } // JSONB field
    
    [MaxLength(255)]
    public string? BackgroundLink { get; set; }
    
    public string? About { get; set; }
    
    public string? BookingIntegrated { get; set; }
    
    public string? CompanyPath { get; set; }
    
    [MaxLength(100)]
    public string? Subdomain { get; set; }
    
    [MaxLength(7)]
    public string? PrimaryColor { get; set; }
    
    [MaxLength(7)]
    public string? SecondaryColor { get; set; }
    
    [MaxLength(500)]
    public string? LogoUrl { get; set; }
    
    [MaxLength(500)]
    public string? FaviconUrl { get; set; }
    
    public string? CustomCss { get; set; }
    
    public bool IsActive { get; set; }
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
    
    [MaxLength(500)]
    public string? BannerLink { get; set; }
    
    [MaxLength(500)]
    public string? LogoLink { get; set; }
    
    [MaxLength(255)]
    public string? Motto { get; set; }
    
    [MaxLength(500)]
    public string? MottoDescription { get; set; }
    
    public string? Invitation { get; set; }
    
    public string? Texts { get; set; } // JSONB field
    
    [MaxLength(255)]
    public string? BackgroundLink { get; set; }
    
    public string? About { get; set; }
    
    public string? BookingIntegrated { get; set; }
    
    public string? CompanyPath { get; set; }
    
    [MaxLength(100)]
    public string? Subdomain { get; set; }
    
    [MaxLength(7)]
    public string? PrimaryColor { get; set; }
    
    [MaxLength(7)]
    public string? SecondaryColor { get; set; }
    
    [MaxLength(500)]
    public string? LogoUrl { get; set; }
    
    [MaxLength(500)]
    public string? FaviconUrl { get; set; }
    
    public string? CustomCss { get; set; }
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
    
    [MaxLength(500)]
    public string? VideoLink { get; set; }
    
    [MaxLength(500)]
    public string? BannerLink { get; set; }
    
    [MaxLength(500)]
    public string? LogoLink { get; set; }
    
    [MaxLength(255)]
    public string? Motto { get; set; }
    
    [MaxLength(500)]
    public string? MottoDescription { get; set; }
    
    public string? Invitation { get; set; }
    
    public string? Texts { get; set; } // JSONB field
    
    [MaxLength(255)]
    public string? BackgroundLink { get; set; }
    
    public string? About { get; set; }
    
    public string? BookingIntegrated { get; set; }
    
    public string? CompanyPath { get; set; }
    
    [MaxLength(100)]
    public string? Subdomain { get; set; }
    
    [MaxLength(7)]
    public string? PrimaryColor { get; set; }
    
    [MaxLength(7)]
    public string? SecondaryColor { get; set; }
    
    [MaxLength(500)]
    public string? LogoUrl { get; set; }
    
    [MaxLength(500)]
    public string? FaviconUrl { get; set; }
    
    public string? CustomCss { get; set; }
    
    public bool? IsActive { get; set; }
}
