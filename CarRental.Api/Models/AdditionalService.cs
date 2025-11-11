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

[Table("additional_services")]
public class AdditionalService
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [Column("company_id")]
    public Guid CompanyId { get; set; }

    [Required]
    [MaxLength(255)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("description")]
    public string? Description { get; set; }

    [Required]
    [Column("price")]
    [Range(0, double.MaxValue)]
    public decimal Price { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("service_type")]
    public string ServiceType { get; set; } = ServiceTypeConstants.Other;

    [Required]
    [Column("is_mandatory")]
    public bool IsMandatory { get; set; } = false;

    [Required]
    [Column("max_quantity")]
    [Range(1, int.MaxValue)]
    public int MaxQuantity { get; set; } = 1;

    [Required]
    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Required]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Required]
    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey("CompanyId")]
    public Company Company { get; set; } = null!;
}

/// <summary>
/// Constants for service types
/// </summary>
public static class ServiceTypeConstants
{
    public const string Insurance = "Insurance";
    public const string GPS = "GPS";
    public const string ChildSeat = "ChildSeat";
    public const string AdditionalDriver = "AdditionalDriver";
    public const string FuelPrepay = "FuelPrepay";
    public const string Cleaning = "Cleaning";
    public const string Delivery = "Delivery";
    public const string Other = "Other";

    public static readonly string[] AllTypes = new[]
    {
        Insurance,
        GPS,
        ChildSeat,
        AdditionalDriver,
        FuelPrepay,
        Cleaning,
        Delivery,
        Other
    };
}

