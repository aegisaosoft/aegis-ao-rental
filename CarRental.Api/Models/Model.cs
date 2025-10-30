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

[Table("models")]
public class Model
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(100)]
    [Column("make")]
    public string Make { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [Column("model")]
    public string ModelName { get; set; } = string.Empty;

    [Required]
    [Column("year")]
    public int Year { get; set; }

    [MaxLength(50)]
    [Column("fuel_type")]
    public string? FuelType { get; set; }

    [MaxLength(50)]
    [Column("transmission")]
    public string? Transmission { get; set; }

    [Column("seats")]
    public int? Seats { get; set; }

    [Column("daily_rate", TypeName = "decimal(10,2)")]
    public decimal? DailyRate { get; set; }

    [Column("features")]
    public string[]? Features { get; set; }

    [Column("description")]
    public string? Description { get; set; }

    [Column("category_id")]
    public Guid? CategoryId { get; set; }

    // Navigation properties
    [ForeignKey("CategoryId")]
    public virtual VehicleCategory? Category { get; set; }
}
