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

[Table("stripe_transfers")]
public class StripeTransfer
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [Column("booking_id")]
    public Guid BookingId { get; set; }

    [Required]
    [Column("company_id")]
    public Guid CompanyId { get; set; }

    [Required]
    [MaxLength(255)]
    [Column("stripe_transfer_id")]
    public string StripeTransferId { get; set; } = string.Empty;

    [MaxLength(255)]
    [Column("stripe_payment_intent_id")]
    public string? StripePaymentIntentId { get; set; }

    [Required]
    [Column("amount", TypeName = "decimal(10,2)")]
    public decimal Amount { get; set; }

    [MaxLength(3)]
    [Column("currency")]
    public string Currency { get; set; } = "USD";

    [Required]
    [Column("platform_fee", TypeName = "decimal(10,2)")]
    public decimal PlatformFee { get; set; }

    [Required]
    [Column("net_amount", TypeName = "decimal(10,2)")]
    public decimal NetAmount { get; set; }

    [Required]
    [MaxLength(255)]
    [Column("destination_account_id")]
    public string DestinationAccountId { get; set; } = string.Empty;

    [MaxLength(50)]
    [Column("status")]
    public string Status { get; set; } = "pending";

    [MaxLength(100)]
    [Column("failure_code")]
    public string? FailureCode { get; set; }

    [Column("failure_message")]
    public string? FailureMessage { get; set; }

    [Column("transferred_at")]
    public DateTime? TransferredAt { get; set; }

    [Column("reversed_at")]
    public DateTime? ReversedAt { get; set; }

    [MaxLength(255)]
    [Column("reversal_id")]
    public string? ReversalId { get; set; }

    [Column("metadata", TypeName = "jsonb")]
    public string? Metadata { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey("BookingId")]
    public virtual Booking Booking { get; set; } = null!;

    [ForeignKey("CompanyId")]
    public virtual Company Company { get; set; } = null!;
}

