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

[Table("customer_licenses")]
public class CustomerLicense
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [Column("customer_id")]
    public Guid CustomerId { get; set; }

    [Required]
    [Column("company_id")]
    public Guid CompanyId { get; set; }

    // License Identity
    [Required]
    [MaxLength(50)]
    [Column("license_number")]
    public string LicenseNumber { get; set; } = string.Empty;

    [Required]
    [MaxLength(2)]
    [Column("state_issued")]
    public string StateIssued { get; set; } = string.Empty;

    [MaxLength(2)]
    [Column("country_issued")]
    public string CountryIssued { get; set; } = "US";

    // Physical Characteristics
    [MaxLength(1)]
    [Column("sex")]
    public string? Sex { get; set; }

    [MaxLength(20)]
    [Column("height")]
    public string? Height { get; set; }

    [MaxLength(20)]
    [Column("eye_color")]
    public string? EyeColor { get; set; }

    [MaxLength(100)]
    [Column("middle_name")]
    public string? MiddleName { get; set; }

    // License Dates
    [Column("issue_date")]
    public DateTime? IssueDate { get; set; }

    [Required]
    [Column("expiration_date")]
    public DateTime ExpirationDate { get; set; }

    // Address on License (separate from customer address)
    [MaxLength(255)]
    [Column("license_address")]
    public string? LicenseAddress { get; set; }

    [MaxLength(100)]
    [Column("license_city")]
    public string? LicenseCity { get; set; }

    [MaxLength(100)]
    [Column("license_state")]
    public string? LicenseState { get; set; }

    [MaxLength(20)]
    [Column("license_postal_code")]
    public string? LicensePostalCode { get; set; }

    [MaxLength(100)]
    [Column("license_country")]
    public string? LicenseCountry { get; set; }

    // Additional Info
    [MaxLength(50)]
    [Column("restriction_code")]
    public string? RestrictionCode { get; set; }

    [MaxLength(100)]
    [Column("endorsements")]
    public string? Endorsements { get; set; }

    [Column("raw_barcode_data")]
    public string? RawBarcodeData { get; set; }

    // License Images (URLs stored in Azure Blob Storage)
    [Column("front_image_url")]
    public string? FrontImageUrl { get; set; }

    [Column("back_image_url")]
    public string? BackImageUrl { get; set; }

    // Verification
    [Column("is_verified")]
    public bool IsVerified { get; set; } = true;

    [Column("verification_date")]
    public DateTime? VerificationDate { get; set; }

    [MaxLength(50)]
    [Column("verification_method")]
    public string VerificationMethod { get; set; } = "license_scan";

    [Column("notes")]
    public string? Notes { get; set; }

    // Audit
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("created_by")]
    public Guid? CreatedBy { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_by")]
    public Guid? UpdatedBy { get; set; }

    // Navigation properties
    [ForeignKey("CustomerId")]
    public virtual Customer Customer { get; set; } = null!;

    [ForeignKey("CompanyId")]
    public virtual Company? Company { get; set; }
}
