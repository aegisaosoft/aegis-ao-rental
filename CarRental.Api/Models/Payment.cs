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

[Table("payments")]
public class Payment
{
    [Key]
    [Column("payment_id")]
    public Guid PaymentId { get; set; } = Guid.NewGuid();

    [Column("reservation_id")]
    public Guid? ReservationId { get; set; }

    [Column("rental_id")]
    public Guid? RentalId { get; set; }

    [Required]
    [Column("customer_id")]
    public Guid CustomerId { get; set; }

    [Required]
    [Column("company_id")]
    public Guid CompanyId { get; set; }

    [Required]
    [Column(TypeName = "decimal(10,2)")]
    public decimal Amount { get; set; }

    [MaxLength(10)]
    public string Currency { get; set; } = "USD";

    [Required]
    [MaxLength(50)]
    [Column("payment_type")]
    public string PaymentType { get; set; } = string.Empty;

    [MaxLength(50)]
    [Column("payment_method")]
    public string? PaymentMethod { get; set; }

    [MaxLength(255)]
    [Column("stripe_payment_intent_id")]
    public string? StripePaymentIntentId { get; set; }

    [MaxLength(255)]
    [Column("stripe_charge_id")]
    public string? StripeChargeId { get; set; }

    [MaxLength(255)]
    [Column("stripe_payment_method_id")]
    public string? StripePaymentMethodId { get; set; }

    [MaxLength(50)]
    public string Status { get; set; } = "pending";

    [Column("failure_reason")]
    public string? FailureReason { get; set; }

    [Column("processed_at")]
    public DateTime? ProcessedAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey("ReservationId")]
    public virtual Reservation? Reservation { get; set; }

    [ForeignKey("RentalId")]
    public virtual Rental? Rental { get; set; }

    [ForeignKey("CustomerId")]
    public virtual Customer Customer { get; set; } = null!;

    [ForeignKey("CompanyId")]
    public virtual RentalCompany Company { get; set; } = null!;
}
