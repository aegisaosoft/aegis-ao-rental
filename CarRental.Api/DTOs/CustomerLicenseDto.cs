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

public class CustomerLicenseDto
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    
    [Required]
    [MaxLength(50)]
    public string LicenseNumber { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(2)]
    public string StateIssued { get; set; } = string.Empty;
    
    [MaxLength(2)]
    public string CountryIssued { get; set; } = "US";
    
    [MaxLength(1)]
    public string? Sex { get; set; }
    
    [MaxLength(20)]
    public string? Height { get; set; }
    
    [MaxLength(20)]
    public string? EyeColor { get; set; }
    
    [MaxLength(100)]
    public string? MiddleName { get; set; }
    
    public DateTime? IssueDate { get; set; }
    
    [Required]
    public DateTime ExpirationDate { get; set; }
    
    [MaxLength(255)]
    public string? LicenseAddress { get; set; }
    
    [MaxLength(100)]
    public string? LicenseCity { get; set; }
    
    [MaxLength(100)]
    public string? LicenseState { get; set; }
    
    [MaxLength(20)]
    public string? LicensePostalCode { get; set; }
    
    [MaxLength(100)]
    public string? LicenseCountry { get; set; }
    
    [MaxLength(50)]
    public string? RestrictionCode { get; set; }
    
    [MaxLength(100)]
    public string? Endorsements { get; set; }
    
    public bool IsVerified { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateCustomerLicenseDto
{
    [Required]
    [MaxLength(50)]
    public string LicenseNumber { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(2)]
    public string StateIssued { get; set; } = string.Empty;
    
    [MaxLength(2)]
    public string CountryIssued { get; set; } = "US";
    
    [MaxLength(1)]
    public string? Sex { get; set; }
    
    [MaxLength(20)]
    public string? Height { get; set; }
    
    [MaxLength(20)]
    public string? EyeColor { get; set; }
    
    [MaxLength(100)]
    public string? MiddleName { get; set; }
    
    public DateTime? IssueDate { get; set; }
    
    [Required]
    public DateTime ExpirationDate { get; set; }
    
    [MaxLength(255)]
    public string? LicenseAddress { get; set; }
    
    [MaxLength(100)]
    public string? LicenseCity { get; set; }
    
    [MaxLength(100)]
    public string? LicenseState { get; set; }
    
    [MaxLength(20)]
    public string? LicensePostalCode { get; set; }
    
    [MaxLength(100)]
    public string? LicenseCountry { get; set; }
    
    [MaxLength(50)]
    public string? RestrictionCode { get; set; }
    
    [MaxLength(100)]
    public string? Endorsements { get; set; }
}

public class UpdateCustomerLicenseDto
{
    [MaxLength(50)]
    public string? LicenseNumber { get; set; }
    
    [MaxLength(2)]
    public string? StateIssued { get; set; }
    
    [MaxLength(2)]
    public string? CountryIssued { get; set; }
    
    [MaxLength(1)]
    public string? Sex { get; set; }
    
    [MaxLength(20)]
    public string? Height { get; set; }
    
    [MaxLength(20)]
    public string? EyeColor { get; set; }
    
    [MaxLength(100)]
    public string? MiddleName { get; set; }
    
    public DateTime? IssueDate { get; set; }
    
    public DateTime? ExpirationDate { get; set; }
    
    [MaxLength(255)]
    public string? LicenseAddress { get; set; }
    
    [MaxLength(100)]
    public string? LicenseCity { get; set; }
    
    [MaxLength(100)]
    public string? LicenseState { get; set; }
    
    [MaxLength(20)]
    public string? LicensePostalCode { get; set; }
    
    [MaxLength(100)]
    public string? LicenseCountry { get; set; }
    
    [MaxLength(50)]
    public string? RestrictionCode { get; set; }
    
    [MaxLength(100)]
    public string? Endorsements { get; set; }
}

