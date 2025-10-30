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

public class AdditionalServiceDto
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    
    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;
    
    public string? Description { get; set; }
    
    [Required]
    [Range(0, double.MaxValue)]
    public decimal Price { get; set; }
    
    [Required]
    [MaxLength(50)]
    public string ServiceType { get; set; } = string.Empty;
    
    public bool IsMandatory { get; set; }
    
    [Range(1, int.MaxValue)]
    public int MaxQuantity { get; set; } = 1;
    
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    // Navigation properties
    public string? CompanyName { get; set; }
}

public class CreateAdditionalServiceDto
{
    [Required]
    public Guid CompanyId { get; set; }
    
    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;
    
    public string? Description { get; set; }
    
    [Required]
    [Range(0, double.MaxValue)]
    public decimal Price { get; set; }
    
    [Required]
    [MaxLength(50)]
    public string ServiceType { get; set; } = string.Empty;
    
    public bool IsMandatory { get; set; } = false;
    
    [Range(1, int.MaxValue)]
    public int MaxQuantity { get; set; } = 1;
    
    public bool IsActive { get; set; } = true;
}

public class UpdateAdditionalServiceDto
{
    [MaxLength(255)]
    public string? Name { get; set; }
    
    public string? Description { get; set; }
    
    [Range(0, double.MaxValue)]
    public decimal? Price { get; set; }
    
    [MaxLength(50)]
    public string? ServiceType { get; set; }
    
    public bool? IsMandatory { get; set; }
    
    [Range(1, int.MaxValue)]
    public int? MaxQuantity { get; set; }
    
    public bool? IsActive { get; set; }
}

