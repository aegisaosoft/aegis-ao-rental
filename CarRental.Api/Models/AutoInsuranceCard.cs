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

[Table("auto_insurance_cards")]
public class AutoInsuranceCard
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [Column("customer_id")]
    public Guid CustomerId { get; set; }

    [MaxLength(255)]
    [Column("insurance_company")]
    public string? InsuranceCompany { get; set; }

    [MaxLength(100)]
    [Column("policy_number")]
    public string? PolicyNumber { get; set; }

    [MaxLength(255)]
    [Column("named_insured")]
    public string? NamedInsured { get; set; }

    [MaxLength(100)]
    [Column("vehicle_make")]
    public string? VehicleMake { get; set; }

    [MaxLength(100)]
    [Column("vehicle_model")]
    public string? VehicleModel { get; set; }

    [MaxLength(4)]
    [Column("vehicle_year")]
    public string? VehicleYear { get; set; }

    [MaxLength(17)]
    [Column("vin")]
    public string? Vin { get; set; }

    [Column("effective_date")]
    public DateTime? EffectiveDate { get; set; }

    [Required]
    [Column("expiration_date")]
    public DateTime ExpirationDate { get; set; }

    [MaxLength(255)]
    [Column("agent_name")]
    public string? AgentName { get; set; }

    [MaxLength(20)]
    [Column("agent_phone")]
    public string? AgentPhone { get; set; }

    [Required]
    [Column("front_image_url")]
    public string FrontImageUrl { get; set; } = string.Empty;

    [Column("back_image_url")]
    public string? BackImageUrl { get; set; }

    [Column("ocr_raw_text")]
    public string? OcrRawText { get; set; }

    [Column("ocr_confidence", TypeName = "decimal(5,2)")]
    public decimal? OcrConfidence { get; set; }

    [Column("ocr_processed_at")]
    public DateTime? OcrProcessedAt { get; set; }

    [Column("is_verified")]
    public bool IsVerified { get; set; } = false;

    [Column("is_expired")]
    public bool IsExpired { get; set; } = false;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("created_by")]
    public Guid? CreatedBy { get; set; }

    // Navigation properties
    [ForeignKey("CustomerId")]
    public virtual Customer Customer { get; set; } = null!;
}

