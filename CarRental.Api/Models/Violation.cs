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

[Table("violations")]
public class Violation
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [Column("company_id")]
    public Guid CompanyId { get; set; }

    [MaxLength(255)]
    [Column("citation_number")]
    public string? CitationNumber { get; set; }

    [MaxLength(255)]
    [Column("notice_number")]
    public string? NoticeNumber { get; set; }

    [Required]
    [Column("provider")]
    public int Provider { get; set; } = 0;

    [MaxLength(255)]
    [Column("agency")]
    public string? Agency { get; set; }

    [Column("address", TypeName = "text")]
    public string? Address { get; set; }

    [MaxLength(50)]
    [Column("tag")]
    public string? Tag { get; set; }

    [MaxLength(10)]
    [Column("state")]
    public string? State { get; set; }

    [Column("issue_date")]
    public DateTime? IssueDate { get; set; }

    [Column("start_date")]
    public DateTime? StartDate { get; set; }

    [Column("end_date")]
    public DateTime? EndDate { get; set; }

    [Required]
    [Column("amount", TypeName = "numeric(10,2)")]
    public decimal Amount { get; set; } = 0;

    [MaxLength(3)]
    [Column("currency")]
    public string? Currency { get; set; }

    [Required]
    [Column("payment_status")]
    public int PaymentStatus { get; set; } = 0;

    [Required]
    [Column("fine_type")]
    public int FineType { get; set; } = 0;

    [Column("note", TypeName = "text")]
    public string? Note { get; set; }

    [Column("link", TypeName = "text")]
    public string? Link { get; set; }

    [Required]
    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey("CompanyId")]
    public virtual Company? Company { get; set; }
}
