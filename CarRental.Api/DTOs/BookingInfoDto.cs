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
/// Combined response for the booking page — all data in one request.
/// Tenant-level data (services, locations, stripe) is cached 30 min.
/// Vehicle data is always fresh (depends on make/model/dates).
/// </summary>
public class BookingInfoDto
{
    /// <summary>Stripe account availability for this company</summary>
    public bool HasStripeAccount { get; set; }

    /// <summary>Additional services/addons (GPS, child seat, etc.)</summary>
    public List<CompanyServiceDto> Services { get; set; } = new();

    /// <summary>Active pickup locations for this company</summary>
    public List<CompanyLocationDto> PickupLocations { get; set; } = new();

    /// <summary>Available vehicles matching make/model filters (always fresh, not cached)</summary>
    public List<BookingVehicleDto> Vehicles { get; set; } = new();

    /// <summary>Model info (daily rate, category, description) if make/model specified</summary>
    public BookingModelInfoDto? ModelInfo { get; set; }

    /// <summary>Timestamp when tenant data was cached (for debugging)</summary>
    public DateTime CachedAt { get; set; }
}

/// <summary>Minimal vehicle info for booking page</summary>
public class BookingVehicleDto
{
    public Guid VehicleId { get; set; }
    public string? Make { get; set; }
    public string? Model { get; set; }
    public int Year { get; set; }
    public string? Color { get; set; }
    public string? LicensePlate { get; set; }
    public string? Status { get; set; }
    public string? ImageUrl { get; set; }
    public decimal DailyRate { get; set; }
    public Guid? LocationId { get; set; }
    public string? LocationName { get; set; }
}

/// <summary>Model rate/category info for booking page</summary>
public class BookingModelInfoDto
{
    public string? Make { get; set; }
    public string? ModelName { get; set; }
    public int Year { get; set; }
    public decimal DailyRate { get; set; }
    public string? CategoryName { get; set; }
    public string? Description { get; set; }
}
