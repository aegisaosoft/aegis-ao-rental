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

[Table("refund_analytics")]
public class RefundAnalytics
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("company_id")]
    public Guid? CompanyId { get; set; }

    [Required]
    [Column("period_start")]
    public DateTime PeriodStart { get; set; }

    [Required]
    [Column("period_end")]
    public DateTime PeriodEnd { get; set; }

    [Column("total_refunds")]
    public int TotalRefunds { get; set; } = 0;

    [Column("total_refund_amount", TypeName = "decimal(10,2)")]
    public decimal TotalRefundAmount { get; set; } = 0;

    [Column("security_deposit_refunds")]
    public int SecurityDepositRefunds { get; set; } = 0;

    [Column("security_deposit_refund_amount", TypeName = "decimal(10,2)")]
    public decimal SecurityDepositRefundAmount { get; set; } = 0;

    [Column("rental_adjustment_refunds")]
    public int RentalAdjustmentRefunds { get; set; } = 0;

    [Column("rental_adjustment_amount", TypeName = "decimal(10,2)")]
    public decimal RentalAdjustmentAmount { get; set; } = 0;

    [Column("cancellation_refunds")]
    public int CancellationRefunds { get; set; } = 0;

    [Column("cancellation_refund_amount", TypeName = "decimal(10,2)")]
    public decimal CancellationRefundAmount { get; set; } = 0;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey("CompanyId")]
    public virtual Company? Company { get; set; }
}

