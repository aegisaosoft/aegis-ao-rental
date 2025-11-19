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
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("booking_id")]
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
    [Column("amount", TypeName = "decimal(10,2)")]
    public decimal Amount { get; set; }

    [MaxLength(10)]
    [Column("currency")]
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
    [Column("status")]
    public string Status { get; set; } = "pending";

    [Column("security_deposit_amount", TypeName = "decimal(10,2)")]
    public decimal? SecurityDepositAmount { get; set; }

    [MaxLength(50)]
    [Column("security_deposit_status")]
    public string? SecurityDepositStatus { get; set; }

    [Column("security_deposit_payment_intent_id")]
    public string? SecurityDepositPaymentIntentId { get; set; }

    [Column("security_deposit_charge_id")]
    public string? SecurityDepositChargeId { get; set; }

    [Column("security_deposit_authorized_at")]
    public DateTime? SecurityDepositAuthorizedAt { get; set; }

    [Column("security_deposit_captured_at")]
    public DateTime? SecurityDepositCapturedAt { get; set; }

    [Column("security_deposit_released_at")]
    public DateTime? SecurityDepositReleasedAt { get; set; }

    [Column("security_deposit_captured_amount", TypeName = "decimal(10,2)")]
    public decimal? SecurityDepositCapturedAmount { get; set; }

    [Column("security_deposit_capture_reason")]
    public string? SecurityDepositCaptureReason { get; set; }

    // Platform Fee and Transfer Properties
    [MaxLength(255)]
    [Column("destination_account_id")]
    public string? DestinationAccountId { get; set; }

    [Column("platform_fee_amount", TypeName = "decimal(10,2)")]
    public decimal PlatformFeeAmount { get; set; } = 0;

    [MaxLength(255)]
    [Column("transfer_group")]
    public string? TransferGroup { get; set; }

    [MaxLength(255)]
    [Column("on_behalf_of")]
    public string? OnBehalfOf { get; set; }

    [MaxLength(255)]
    [Column("stripe_transfer_id")]
    public string? StripeTransferId { get; set; }

    [Column("failure_reason")]
    public string? FailureReason { get; set; }

    [Column("processed_at")]
    public DateTime? ProcessedAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("refund_amount", TypeName = "decimal(10,2)")]
    public decimal? RefundAmount { get; set; }

    [Column("refund_date")]
    public DateTime? RefundDate { get; set; }

    // Navigation properties
    [ForeignKey("ReservationId")]
    public virtual Reservation? Reservation { get; set; }

    [ForeignKey("RentalId")]
    public virtual Rental? Rental { get; set; }

    [ForeignKey("CustomerId")]
    public virtual Customer Customer { get; set; } = null!;

    [ForeignKey("CompanyId")]
    public virtual Company Company { get; set; } = null!;
}
