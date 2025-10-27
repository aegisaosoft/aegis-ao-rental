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

[Table("company_email_styles")]
public class CompanyEmailStyle
{
    [Key]
    [Column("style_id")]
    public Guid StyleId { get; set; } = Guid.NewGuid();

    [Required]
    [Column("company_id")]
    public Guid CompanyId { get; set; }

    [MaxLength(7)]
    [Column("primary_color")]
    public string PrimaryColor { get; set; } = "#007bff";

    [MaxLength(7)]
    [Column("secondary_color")]
    public string SecondaryColor { get; set; } = "#6c757d";

    [MaxLength(7)]
    [Column("success_color")]
    public string SuccessColor { get; set; } = "#28a745";

    [MaxLength(7)]
    [Column("warning_color")]
    public string WarningColor { get; set; } = "#ffc107";

    [MaxLength(7)]
    [Column("info_color")]
    public string InfoColor { get; set; } = "#17a2b8";

    [MaxLength(7)]
    [Column("background_color")]
    public string BackgroundColor { get; set; } = "#f8f9fa";

    [MaxLength(7)]
    [Column("border_color")]
    public string BorderColor { get; set; } = "#dee2e6";

    [MaxLength(7)]
    [Column("text_color")]
    public string TextColor { get; set; } = "#333333";

    [MaxLength(7)]
    [Column("header_text_color")]
    public string HeaderTextColor { get; set; } = "#ffffff";

    [MaxLength(7)]
    [Column("button_text_color")]
    public string ButtonTextColor { get; set; } = "#ffffff";

    [MaxLength(7)]
    [Column("footer_color")]
    public string FooterColor { get; set; } = "#343a40";

    [MaxLength(7)]
    [Column("footer_text_color")]
    public string FooterTextColor { get; set; } = "#ffffff";

    [MaxLength(500)]
    [Column("logo_url")]
    public string? LogoUrl { get; set; }

    [MaxLength(500)]
    [Column("footer_text")]
    public string? FooterText { get; set; }

    [MaxLength(100)]
    [Column("font_family")]
    public string FontFamily { get; set; } = "Arial, sans-serif";

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey("CompanyId")]
    public virtual RentalCompany Company { get; set; } = null!;
}
