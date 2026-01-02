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

/// <summary>
/// Junction table linking companies to their available additional services.
/// Renamed from CompanyService to avoid conflict with CarRental.Api.Services.CompanyService
/// </summary>
[Table("company_services")]
public class CompanyServiceLink
{
    [Required]
    [Column("company_id")]
    public Guid CompanyId { get; set; }

    [Required]
    [Column("additional_service_id")]
    public Guid AdditionalServiceId { get; set; }

    [Column("price", TypeName = "decimal(10,2)")]
    public decimal? Price { get; set; }

    [Column("is_mandatory")]
    public bool? IsMandatory { get; set; }

    [Required]
    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Required]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey("CompanyId")]
    public Company Company { get; set; } = null!;

    [ForeignKey("AdditionalServiceId")]
    public AdditionalService AdditionalService { get; set; } = null!;
}

