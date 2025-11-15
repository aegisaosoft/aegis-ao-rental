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

[Table("stripe_payout_records")]
public class StripePayoutRecord
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [Column("company_id")]
    public Guid CompanyId { get; set; }

    [Required]
    [MaxLength(255)]
    [Column("stripe_payout_id")]
    public string StripePayoutId { get; set; } = string.Empty;

    [Required]
    [Column("amount", TypeName = "decimal(10,2)")]
    public decimal Amount { get; set; }

    [MaxLength(3)]
    [Column("currency")]
    public string Currency { get; set; } = "USD";

    [Required]
    [MaxLength(50)]
    [Column("status")]
    public string Status { get; set; } = string.Empty;

    [MaxLength(50)]
    [Column("payout_type")]
    public string PayoutType { get; set; } = "bank_account";

    [Column("arrival_date")]
    public DateTime? ArrivalDate { get; set; }

    [Column("description")]
    public string? Description { get; set; }

    [MaxLength(50)]
    [Column("method")]
    public string? Method { get; set; }

    [MaxLength(100)]
    [Column("failure_code")]
    public string? FailureCode { get; set; }

    [Column("failure_message")]
    public string? FailureMessage { get; set; }

    [MaxLength(255)]
    [Column("statement_descriptor")]
    public string? StatementDescriptor { get; set; }

    [Column("metadata", TypeName = "jsonb")]
    public string? Metadata { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey("CompanyId")]
    public virtual Company Company { get; set; } = null!;
}

