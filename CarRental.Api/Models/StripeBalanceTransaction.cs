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

[Table("stripe_balance_transactions")]
public class StripeBalanceTransaction
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [Column("company_id")]
    public Guid CompanyId { get; set; }

    [Required]
    [MaxLength(255)]
    [Column("stripe_balance_transaction_id")]
    public string StripeBalanceTransactionId { get; set; } = string.Empty;

    [Required]
    [Column("amount", TypeName = "decimal(10,2)")]
    public decimal Amount { get; set; }

    [MaxLength(3)]
    [Column("currency")]
    public string Currency { get; set; } = "USD";

    [Required]
    [Column("net", TypeName = "decimal(10,2)")]
    public decimal Net { get; set; }

    [Column("fee", TypeName = "decimal(10,2)")]
    public decimal Fee { get; set; } = 0;

    [Required]
    [MaxLength(50)]
    [Column("transaction_type")]
    public string TransactionType { get; set; } = string.Empty;

    [Column("description")]
    public string? Description { get; set; }

    [MaxLength(255)]
    [Column("source_id")]
    public string? SourceId { get; set; }

    [MaxLength(50)]
    [Column("source_type")]
    public string? SourceType { get; set; }

    [Column("available_on")]
    public DateTime? AvailableOn { get; set; }

    [Required]
    [Column("created")]
    public DateTime Created { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey("CompanyId")]
    public virtual Company Company { get; set; } = null!;
}

