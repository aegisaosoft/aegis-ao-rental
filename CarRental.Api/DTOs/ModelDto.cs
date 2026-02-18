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

public class ModelDto
{
    public Guid Id { get; set; }
    public string Make { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public int Year { get; set; }
    public string? FuelType { get; set; }
    public string? Transmission { get; set; }
    public int? Seats { get; set; }
    public decimal? DailyRate { get; set; }
    public string[]? Features { get; set; }
    public string? Description { get; set; }
    public Guid? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public int VehicleCount { get; set; } // Total number of vehicles for this model
    public int AvailableCount { get; set; } // Number of available vehicles for this model
}

public class ModelsGroupedByCategoryDto
{
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string? CategoryDescription { get; set; }
    public List<ModelDto> Models { get; set; } = new();
}

public class UpdateModelCategoryDto
{
    public Guid? CategoryId { get; set; }
}

public class BulkUpdateModelDailyRateDto
{
    [Required]
    public decimal DailyRate { get; set; }
    
    public Guid? CategoryId { get; set; }
    
    public string? Make { get; set; }
    
    public string? ModelName { get; set; }
    
    public int? Year { get; set; }
    
    public Guid? CompanyId { get; set; } // Filter to only update models for vehicles of this company
}

