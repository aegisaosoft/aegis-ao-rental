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

public class CustomerDto
{
    public Guid CustomerId { get; set; }
    
    [Required]
    [EmailAddress]
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;
    
    [MaxLength(50)]
    public string? Phone { get; set; }
    
    public DateTime? DateOfBirth { get; set; }
    
    [MaxLength(100)]
    public string? DriversLicenseNumber { get; set; }
    
    [MaxLength(50)]
    public string? DriversLicenseState { get; set; }
    
    public DateTime? DriversLicenseExpiry { get; set; }
    
    public string? Address { get; set; }
    
    [MaxLength(100)]
    public string? City { get; set; }
    
    [MaxLength(100)]
    public string? State { get; set; }
    
    [MaxLength(100)]
    public string? Country { get; set; }
    
    [MaxLength(20)]
    public string? PostalCode { get; set; }
    
    public string? StripeCustomerId { get; set; }
    
    public bool IsVerified { get; set; }
    
    public string? Role { get; set; }
    
    public Guid? CompanyId { get; set; }
    
    public string? CompanyName { get; set; }
    
    public DateTime CreatedAt { get; set; }
    
    public DateTime UpdatedAt { get; set; }
}

public class CreateCustomerDto
{
    [Required]
    [EmailAddress]
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;
    
    [MaxLength(50)]
    public string? Phone { get; set; }
    
    public DateTime? DateOfBirth { get; set; }
    
    [MaxLength(100)]
    public string? DriversLicenseNumber { get; set; }
    
    [MaxLength(50)]
    public string? DriversLicenseState { get; set; }
    
    public DateTime? DriversLicenseExpiry { get; set; }
    
    public string? Address { get; set; }
    
    [MaxLength(100)]
    public string? City { get; set; }
    
    [MaxLength(100)]
    public string? State { get; set; }
    
    [MaxLength(100)]
    public string? Country { get; set; }
    
    [MaxLength(20)]
    public string? PostalCode { get; set; }
}

public class UpdateCustomerDto
{
    [EmailAddress]
    [MaxLength(255)]
    public string? Email { get; set; }
    
    [MaxLength(100)]
    public string? FirstName { get; set; }
    
    [MaxLength(100)]
    public string? LastName { get; set; }
    
    [MaxLength(50)]
    public string? Phone { get; set; }
    
    public DateTime? DateOfBirth { get; set; }
    
    [MaxLength(100)]
    public string? DriversLicenseNumber { get; set; }
    
    [MaxLength(50)]
    public string? DriversLicenseState { get; set; }
    
    public DateTime? DriversLicenseExpiry { get; set; }
    
    public string? Address { get; set; }
    
    [MaxLength(100)]
    public string? City { get; set; }
    
    [MaxLength(100)]
    public string? State { get; set; }
    
    [MaxLength(100)]
    public string? Country { get; set; }
    
    [MaxLength(20)]
    public string? PostalCode { get; set; }
}
