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
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRental.Api.Models;

[Table("customers")]
public class Customer
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(255)]
    [EmailAddress]
    [Column("email")]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [Column("first_name")]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [Column("last_name")]
    public string LastName { get; set; } = string.Empty;

    [MaxLength(50)]
    [Column("phone")]
    public string? Phone { get; set; }

    [MaxLength(500)]
    [Column("password_hash")]
    public string? PasswordHash { get; set; }

    [MaxLength(50)]
    [Column("role")]
    public string Role { get; set; } = "customer"; // customer, worker, admin, mainadmin

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("last_login")]
    public DateTime? LastLogin { get; set; }

    [Column("company_id")]
    public Guid? CompanyId { get; set; }

    [Column("date_of_birth")]
    public DateTime? DateOfBirth { get; set; }

    [Column("address")]
    public string? Address { get; set; }

    [MaxLength(100)]
    [Column("city")]
    public string? City { get; set; }

    [MaxLength(100)]
    [Column("state")]
    public string? State { get; set; }

    [MaxLength(100)]
    [Column("country")]
    public string? Country { get; set; }

    [MaxLength(20)]
    [Column("postal_code")]
    public string? PostalCode { get; set; }

    [MaxLength(255)]
    [Column("stripe_customer_id")]
    public string? StripeCustomerId { get; set; }

    [Column("is_verified")]
    public bool IsVerified { get; set; } = false;

    [Column("customer_type")]
    public CustomerType CustomerType { get; set; } = CustomerType.Individual;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey("CompanyId")]
    public virtual Company? Company { get; set; }
    
    public virtual CustomerLicense? License { get; set; }
    
    public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public virtual ICollection<Rental> Rentals { get; set; } = new List<Rental>();
    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public virtual ICollection<CustomerPaymentMethod> PaymentMethods { get; set; } = new List<CustomerPaymentMethod>();
    public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();
}
