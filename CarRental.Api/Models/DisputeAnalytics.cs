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

[Table("dispute_analytics")]
public class DisputeAnalytics
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

    [Column("total_disputes")]
    public int TotalDisputes { get; set; } = 0;

    [Column("disputes_won")]
    public int DisputesWon { get; set; } = 0;

    [Column("disputes_lost")]
    public int DisputesLost { get; set; } = 0;

    [Column("disputes_pending")]
    public int DisputesPending { get; set; } = 0;

    [Column("total_disputed_amount", TypeName = "decimal(10,2)")]
    public decimal TotalDisputedAmount { get; set; } = 0;

    [Column("total_lost_amount", TypeName = "decimal(10,2)")]
    public decimal TotalLostAmount { get; set; } = 0;

    [Column("avg_resolution_days", TypeName = "decimal(5,2)")]
    public decimal? AvgResolutionDays { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey("CompanyId")]
    public virtual Company? Company { get; set; }
}

