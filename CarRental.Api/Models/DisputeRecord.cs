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

[Table("dispute_records")]
public class DisputeRecord
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(255)]
    [Column("stripe_dispute_id")]
    public string StripeDisputeId { get; set; } = string.Empty;

    [Column("booking_id")]
    public Guid? BookingId { get; set; }

    [Required]
    [MaxLength(255)]
    [Column("charge_id")]
    public string ChargeId { get; set; } = string.Empty;

    [Required]
    [Column("amount", TypeName = "decimal(10,2)")]
    public decimal Amount { get; set; }

    [MaxLength(100)]
    [Column("reason")]
    public string? Reason { get; set; }

    [MaxLength(50)]
    [Column("status")]
    public string? Status { get; set; }

    [Column("evidence_due_by")]
    public DateTime? EvidenceDueBy { get; set; }

    [Column("evidence_submitted")]
    public bool EvidenceSubmitted { get; set; } = false;

    [Column("evidence_submitted_at")]
    public DateTime? EvidenceSubmittedAt { get; set; }

    [Column("evidence_details", TypeName = "jsonb")]
    public string? EvidenceDetails { get; set; }

    [Column("is_security_deposit_dispute")]
    public bool IsSecurityDepositDispute { get; set; } = false;

    [MaxLength(50)]
    [Column("outcome")]
    public string? Outcome { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("closed_at")]
    public DateTime? ClosedAt { get; set; }

    // Navigation properties
    [ForeignKey("BookingId")]
    public virtual Booking? Booking { get; set; }

    public virtual ICollection<DisputeEvidenceFile> EvidenceFiles { get; set; } = new List<DisputeEvidenceFile>();
}

