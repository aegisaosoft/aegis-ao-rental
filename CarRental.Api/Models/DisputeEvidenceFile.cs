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

[Table("dispute_evidence_files")]
public class DisputeEvidenceFile
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("dispute_id")]
    public Guid? DisputeId { get; set; }

    [MaxLength(255)]
    [Column("stripe_file_id")]
    public string? StripeFileId { get; set; }

    [MaxLength(50)]
    [Column("file_type")]
    public string? FileType { get; set; }

    [MaxLength(500)]
    [Column("file_name")]
    public string? FileName { get; set; }

    [Column("file_url")]
    public string? FileUrl { get; set; }

    [Column("uploaded_at")]
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    [Column("uploaded_by")]
    public Guid? UploadedBy { get; set; }

    // Navigation properties
    [ForeignKey("DisputeId")]
    public virtual DisputeRecord? Dispute { get; set; }
}

