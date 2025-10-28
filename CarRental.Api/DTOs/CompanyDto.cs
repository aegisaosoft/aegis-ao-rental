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
    
    [MaxLength(50)]
    public string? Phone { get; set; }
    
    [MaxLength(255)]
    public string? Website { get; set; }
    
    public string? Address { get; set; }
    
    [MaxLength(100)]
    public string? City { get; set; }
    
    [MaxLength(100)]
    public string? State { get; set; }
    
    [MaxLength(100)]
    public string? Country { get; set; }
    
    [MaxLength(20)]
    public string? PostalCode { get; set; }
    
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
    
    public string? Tests { get; set; } // JSONB field
    
    [MaxLength(255)]
    public string? BackgroundLink { get; set; }
    
    public string? About { get; set; }
    
    public string? BookingIntegrated { get; set; }
    
    public string? CompanyPath { get; set; }
    
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
    
    [MaxLength(50)]
    public string? Phone { get; set; }
    
    [MaxLength(255)]
    public string? Website { get; set; }
    
    public string? Address { get; set; }
    
    [MaxLength(100)]
    public string? City { get; set; }
    
    [MaxLength(100)]
    public string? State { get; set; }
    
    [MaxLength(100)]
    public string? Country { get; set; }
    
    [MaxLength(20)]
    public string? PostalCode { get; set; }
    
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
    
    public string? Tests { get; set; } // JSONB field
    
    [MaxLength(255)]
    public string? BackgroundLink { get; set; }
    
    public string? About { get; set; }
    
    public string? BookingIntegrated { get; set; }
    
    public string? CompanyPath { get; set; }
}

public class UpdateRentalCompanyDto
{
    [MaxLength(255)]
    public string? CompanyName { get; set; }
    
    [EmailAddress]
    [MaxLength(255)]
    public string? Email { get; set; }
    
    [MaxLength(50)]
    public string? Phone { get; set; }
    
    [MaxLength(255)]
    public string? Website { get; set; }
    
    public string? Address { get; set; }
    
    [MaxLength(100)]
    public string? City { get; set; }
    
    [MaxLength(100)]
    public string? State { get; set; }
    
    [MaxLength(100)]
    public string? Country { get; set; }
    
    [MaxLength(20)]
    public string? PostalCode { get; set; }
    
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
    
    public string? Tests { get; set; } // JSONB field
    
    [MaxLength(255)]
    public string? BackgroundLink { get; set; }
    
    public string? About { get; set; }
    
    public string? BookingIntegrated { get; set; }
    
    public string? CompanyPath { get; set; }
    
    public bool? IsActive { get; set; }
}
