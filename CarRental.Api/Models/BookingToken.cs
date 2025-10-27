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
using System.Text.Json;

namespace CarRental.Api.Models;

[Table("booking_tokens")]
public class BookingToken
{
    [Key]
    [Column("token_id")]
    public Guid TokenId { get; set; } = Guid.NewGuid();

    [Required]
    [Column("company_id")]
    public Guid CompanyId { get; set; }

    [Required]
    [EmailAddress]
    [MaxLength(255)]
    [Column("customer_email")]
    public string CustomerEmail { get; set; } = string.Empty;

    [Required]
    [Column("vehicle_id")]
    public Guid VehicleId { get; set; }

    [Required]
    [MaxLength(255)]
    public string Token { get; set; } = string.Empty;

    [Required]
    [Column("booking_data", TypeName = "jsonb")]
    public string BookingDataJson { get; set; } = string.Empty;

    [NotMapped]
    public BookingData BookingData
    {
        get => JsonSerializer.Deserialize<BookingData>(BookingDataJson) ?? new BookingData();
        set => BookingDataJson = JsonSerializer.Serialize(value);
    }

    [Required]
    [Column("expires_at")]
    public DateTime ExpiresAt { get; set; }

    [Column("is_used")]
    public bool IsUsed { get; set; } = false;

    [Column("used_at")]
    public DateTime? UsedAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey("CompanyId")]
    public virtual RentalCompany Company { get; set; } = null!;

    [ForeignKey("VehicleId")]
    public virtual Vehicle Vehicle { get; set; } = null!;

    public virtual ICollection<EmailNotification> EmailNotifications { get; set; } = new List<EmailNotification>();
    public virtual ICollection<BookingConfirmation> BookingConfirmations { get; set; } = new List<BookingConfirmation>();
}

public class BookingData
{
    public DateTime PickupDate { get; set; }
    public DateTime ReturnDate { get; set; }
    public string? PickupLocation { get; set; }
    public string? ReturnLocation { get; set; }
    public decimal DailyRate { get; set; }
    public int TotalDays { get; set; }
    public decimal Subtotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal InsuranceAmount { get; set; }
    public decimal AdditionalFees { get; set; }
    public decimal TotalAmount { get; set; }
    public VehicleInfo? VehicleInfo { get; set; }
    public CompanyInfo? CompanyInfo { get; set; }
    public string? Notes { get; set; }
}

public class VehicleInfo
{
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int Year { get; set; }
    public string? Color { get; set; }
    public string LicensePlate { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string[]? Features { get; set; }
}

public class CompanyInfo
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Country { get; set; }
}