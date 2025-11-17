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

namespace CarRental.Api.DTOs;

public class VehicleDto
{
    public Guid VehicleId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid? CategoryId { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string Make { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(100)]
    public string Model { get; set; } = string.Empty;
    
    [Required]
    public int Year { get; set; }
    
    [MaxLength(50)]
    public string? Color { get; set; }
    
    [Required]
    [MaxLength(50)]
    public string LicensePlate { get; set; } = string.Empty;
    
    [MaxLength(17)]
    public string? Vin { get; set; }
    
    public int Mileage { get; set; }
    
    [MaxLength(50)]
    public string? FuelType { get; set; }
    
    [MaxLength(50)]
    public string? Transmission { get; set; }
    
    public int? Seats { get; set; }
    
    [Required]
    public decimal DailyRate { get; set; }
    
    [MaxLength(50)]
    public string Status { get; set; } = "Available";
    
    [MaxLength(2)]
    public string? State { get; set; }
    
    [MaxLength(255)]
    public string? Location { get; set; }
    
    public Guid? LocationId { get; set; }
    
    public string? ImageUrl { get; set; }
    
    public string[]? Features { get; set; }

    public DateTime CreatedAt { get; set; }
    
    public DateTime UpdatedAt { get; set; }
    
    // Navigation properties
    public string? CompanyName { get; set; }
    public string? CategoryName { get; set; }
}

public class CreateVehicleDto
{
    [Required]
    public Guid CompanyId { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string Make { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(100)]
    public string Model { get; set; } = string.Empty;
    
    [Required]
    public int Year { get; set; }
    
    [MaxLength(50)]
    public string? Color { get; set; }
    
    [Required]
    [MaxLength(50)]
    public string LicensePlate { get; set; } = string.Empty;
    
    [MaxLength(17)]
    public string? Vin { get; set; }
    
    public int Mileage { get; set; } = 0;
    
    [MaxLength(50)]
    public string? Transmission { get; set; }
    
    public int? Seats { get; set; }
    
    [Required]
    public decimal DailyRate { get; set; }
    
    [MaxLength(50)]
    public string Status { get; set; } = "Available";
    
    [MaxLength(2)]
    public string? State { get; set; }
    
    [MaxLength(255)]
    public string? Location { get; set; }
    
    public string? ImageUrl { get; set; }
    
    public string[]? Features { get; set; }
}

public class UpdateVehicleDto
{
    [MaxLength(100)]
    public string? Make { get; set; }
    
    [MaxLength(100)]
    public string? Model { get; set; }
    
    public int? Year { get; set; }
    
    [MaxLength(50)]
    public string? Color { get; set; }
    
    [MaxLength(50)]
    public string? LicensePlate { get; set; }
    
    [MaxLength(17)]
    public string? Vin { get; set; }
    
    public int? Mileage { get; set; }
    
    [MaxLength(50)]
    public string? Transmission { get; set; }
    
    public int? Seats { get; set; }
    
    public decimal? DailyRate { get; set; }
    
    [MaxLength(50)]
    public string? Status { get; set; }
    
    [MaxLength(2)]
    public string? State { get; set; }
    
    [MaxLength(255)]
    public string? Location { get; set; }
    
    public Guid? LocationId { get; set; }
    
    public string? ImageUrl { get; set; }
    
    public string[]? Features { get; set; }
}

public class VehicleSearchDto
{
    public Guid? CompanyId { get; set; }
    public string? Make { get; set; }
    public string? Model { get; set; }
    public int? MinYear { get; set; }
    public int? MaxYear { get; set; }
    public decimal? MinDailyRate { get; set; }
    public decimal? MaxDailyRate { get; set; }
    public string? Transmission { get; set; }
    public int? MinSeats { get; set; }
    public string? Location { get; set; }
    public DateTime? AvailableFrom { get; set; }
    public DateTime? AvailableTo { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class BulkUpdateDailyRateDto
{
    [Required]
    public decimal DailyRate { get; set; }
    
    public Guid? CompanyId { get; set; }
    
    public Guid? CategoryId { get; set; }
    
    public string? Make { get; set; }
    
    public string? Model { get; set; }
    
    public int? Year { get; set; }
}