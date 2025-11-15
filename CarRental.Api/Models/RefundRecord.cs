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

[Table("refund_records")]
public class RefundRecord
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("booking_id")]
    public Guid? BookingId { get; set; }

    [Required]
    [MaxLength(255)]
    [Column("stripe_refund_id")]
    public string StripeRefundId { get; set; } = string.Empty;

    [Required]
    [Column("amount", TypeName = "decimal(10,2)")]
    public decimal Amount { get; set; }

    [MaxLength(50)]
    [Column("refund_type")]
    public string? RefundType { get; set; }

    [Column("reason")]
    public string? Reason { get; set; }

    [MaxLength(50)]
    [Column("status")]
    public string? Status { get; set; }

    [Column("processed_by")]
    public Guid? ProcessedBy { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey("BookingId")]
    public virtual Booking? Booking { get; set; }

    [ForeignKey("ProcessedBy")]
    public virtual Customer? ProcessedByCustomer { get; set; }
}

