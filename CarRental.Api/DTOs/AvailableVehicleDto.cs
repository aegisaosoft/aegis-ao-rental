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

using System.ComponentModel.DataAnnotations.Schema;

namespace CarRental.Api.DTOs;

/// <summary>
/// DTO for available vehicle data returned from stored procedure
/// </summary>
public class AvailableVehicleDto
{
    [Column("model_id")]
    public Guid ModelId { get; set; }
    
    [Column("make")]
    public string Make { get; set; } = string.Empty;
    
    [Column("model")]
    public string Model { get; set; } = string.Empty;
    
    [Column("fuel_type")]
    public string? FuelType { get; set; }
    
    [Column("transmission")]
    public string? Transmission { get; set; }
    
    [Column("seats")]
    public int? Seats { get; set; }
    
    [Column("category_id")]
    public Guid? CategoryId { get; set; }
    
    [Column("category_name")]
    public string? CategoryName { get; set; }
    
    [Column("min_daily_rate")]
    public decimal? MinDailyRate { get; set; }
    
    [Column("max_daily_rate")]
    public decimal? MaxDailyRate { get; set; }
    
    [Column("avg_daily_rate")]
    public decimal? AvgDailyRate { get; set; }
    
    [Column("available_count")]
    public long AvailableCount { get; set; }
    
    [Column("total_available_vehicles")]
    public long TotalAvailableVehicles { get; set; }
    
    [Column("all_vehicles_count")]
    public long AllVehiclesCount { get; set; }
    
    [Column("years_available")]
    public string? YearsAvailable { get; set; }
    
    [Column("available_colors")]
    public string[]? AvailableColors { get; set; }
    
    [Column("available_locations")]
    public string[]? AvailableLocations { get; set; }
    
    [Column("sample_image_url")]
    public string? SampleImageUrl { get; set; }
    
    [Column("model_features")]
    public string[]? ModelFeatures { get; set; }
}

