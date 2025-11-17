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

namespace CarRental.Api.DTOs;

/// <summary>
/// DTO for available vehicle data returned from stored procedure
/// </summary>
public class AvailableVehicleDto
{
    public Guid ModelId { get; set; }
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int Year { get; set; }
    public string? FuelType { get; set; }
    public string? Transmission { get; set; }
    public int? Seats { get; set; }
    public string? CategoryName { get; set; }
    public decimal? MinDailyRate { get; set; }
    public decimal? MaxDailyRate { get; set; }
    public decimal? AvgDailyRate { get; set; }
    public long AvailableCount { get; set; }
    public string[]? AvailableColors { get; set; }
    public string[]? AvailableLocations { get; set; }
    public string? SampleImageUrl { get; set; }
    public string[]? ModelFeatures { get; set; }
}

