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

[Table("bookings")]
public class Booking
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [Column("customer_id")]
    public Guid CustomerId { get; set; }

    [Required]
    [Column("vehicle_id")]
    public Guid VehicleId { get; set; }

    [Required]
    [Column("company_id")]
    public Guid CompanyId { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("booking_number")]
    public string BookingNumber { get; set; } = string.Empty;

    [MaxLength(100)]
    [Column("alt_booking_number")]
    public string? AltBookingNumber { get; set; }

    [Required]
    [Column("pickup_date")]
    public DateTime PickupDate { get; set; }

    [Required]
    [Column("return_date")]
    public DateTime ReturnDate { get; set; }

    [MaxLength(255)]
    [Column("pickup_location")]
    public string? PickupLocation { get; set; }

    [MaxLength(255)]
    [Column("return_location")]
    public string? ReturnLocation { get; set; }

    [Required]
    [Column("daily_rate", TypeName = "decimal(10,2)")]
    public decimal DailyRate { get; set; }

    [Required]
    [Column("total_days")]
    public int TotalDays { get; set; }

    [Column("subtotal", TypeName = "decimal(10,2)")]
    public decimal Subtotal { get; set; }

    [Column("tax_amount", TypeName = "decimal(10,2)")]
    public decimal TaxAmount { get; set; } = 0;

    [Column("insurance_amount", TypeName = "decimal(10,2)")]
    public decimal InsuranceAmount { get; set; } = 0;

    [Column("additional_fees", TypeName = "decimal(10,2)")]
    public decimal AdditionalFees { get; set; } = 0;

    [Required]
    [Column("total_amount", TypeName = "decimal(10,2)")]
    public decimal TotalAmount { get; set; }

    [MaxLength(3)]
    [Column("currency")]
    public string? Currency { get; set; } = "USD";

    [Column("security_deposit", TypeName = "decimal(10,2)")]
    public decimal SecurityDeposit { get; set; } = 1000m;

    [Required]
    [MaxLength(50)]
    [Column("status")]
    public string Status { get; set; } = "Pending";

    // Payment Intent IDs
    [MaxLength(255)]
    [Column("payment_intent_id")]
    public string? PaymentIntentId { get; set; }

    [MaxLength(255)]
    [Column("stripe_payment_intent_id")]
    public string? StripePaymentIntentId { get; set; }

    [MaxLength(255)]
    [Column("setup_intent_id")]
    public string? SetupIntentId { get; set; }

    [MaxLength(255)]
    [Column("payment_method_id")]
    public string? PaymentMethodId { get; set; }

    [MaxLength(255)]
    [Column("stripe_customer_id")]
    public string? StripeCustomerId { get; set; }

    // Security Deposit Tracking
    [Column("security_deposit_amount", TypeName = "decimal(10,2)")]
    public decimal? SecurityDepositAmount { get; set; }

    [MaxLength(50)]
    [Column("security_deposit_status")]
    public string SecurityDepositStatus { get; set; } = "pending";

    [Column("security_deposit_charged_amount", TypeName = "decimal(10,2)")]
    public decimal? SecurityDepositChargedAmount { get; set; }

    [MaxLength(255)]
    [Column("security_deposit_payment_intent_id")]
    public string? SecurityDepositPaymentIntentId { get; set; }

    // Security Deposit Timestamps
    [Column("security_deposit_authorized_at")]
    public DateTime? SecurityDepositAuthorizedAt { get; set; }

    [Column("security_deposit_captured_at")]
    public DateTime? SecurityDepositCapturedAt { get; set; }

    [Column("security_deposit_released_at")]
    public DateTime? SecurityDepositReleasedAt { get; set; }

    [Column("security_deposit_refunded_at")]
    public DateTime? SecurityDepositRefundedAt { get; set; }

    [Column("security_deposit_capture_reason")]
    public string? SecurityDepositCaptureReason { get; set; }

    // Platform Fee Tracking
    [Column("platform_fee_amount", TypeName = "decimal(10,2)")]
    public decimal PlatformFeeAmount { get; set; } = 0;

    [Column("net_amount", TypeName = "decimal(10,2)")]
    public decimal? NetAmount { get; set; }

    [MaxLength(255)]
    [Column("stripe_transfer_id")]
    public string? StripeTransferId { get; set; }

    // Payment Status
    [MaxLength(50)]
    [Column("payment_status")]
    public string PaymentStatus { get; set; } = "pending";

    [Column("notes")]
    public string? Notes { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("CustomerId")]
    public virtual Customer Customer { get; set; } = null!;

    [ForeignKey("VehicleId")]
    public virtual Vehicle Vehicle { get; set; } = null!;

    [ForeignKey("CompanyId")]
    public virtual Company Company { get; set; } = null!;

    public virtual ICollection<Rental> Rentals { get; set; } = new List<Rental>();
    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public virtual ICollection<RefundRecord> RefundRecords { get; set; } = new List<RefundRecord>();
}

