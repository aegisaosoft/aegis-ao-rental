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

[Table("license_scans")]
public class LicenseScan
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();


    [Required]
    [Column("customer_id")]
    public Guid CustomerId { get; set; }

    [Column("customer_license_id")]
    public Guid? CustomerLicenseId { get; set; }

    [Column("scan_date")]
    public DateTime ScanDate { get; set; } = DateTime.UtcNow;

    [Column("scanned_by")]
    public Guid? ScannedBy { get; set; }

    [MaxLength(50)]
    [Column("scan_source")]
    public string ScanSource { get; set; } = "mobile_app";

    [MaxLength(100)]
    [Column("device_id")]
    public string? DeviceId { get; set; }

    [MaxLength(50)]
    [Column("device_type")]
    public string? DeviceType { get; set; }

    [MaxLength(20)]
    [Column("app_version")]
    public string? AppVersion { get; set; }

    [MaxLength(20)]
    [Column("scan_quality")]
    public string? ScanQuality { get; set; }

    [Column("all_fields_captured")]
    public bool AllFieldsCaptured { get; set; } = true;

    [Column("captured_data", TypeName = "jsonb")]
    public string? CapturedData { get; set; }

    [Column("barcode_data")]
    public string? BarcodeData { get; set; }

    [Column("age_at_scan")]
    public int? AgeAtScan { get; set; }

    [Column("was_expired")]
    public bool WasExpired { get; set; } = false;

    [Column("days_until_expiration")]
    public int? DaysUntilExpiration { get; set; }

    [Column("validation_passed")]
    public bool ValidationPassed { get; set; } = true;

    [Column("validation_errors", TypeName = "text[]")]
    public string[]? ValidationErrors { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties

    [ForeignKey("CustomerId")]
    public virtual Customer Customer { get; set; } = null!;

    [ForeignKey("CustomerLicenseId")]
    public virtual CustomerLicense? CustomerLicense { get; set; }
}

