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

public class CompanyServiceDto
{
    public Guid CompanyId { get; set; }
    public Guid AdditionalServiceId { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    
    // Expanded information
    public string? CompanyName { get; set; }
    public string? ServiceName { get; set; }
    public string? ServiceDescription { get; set; }
    public decimal ServicePrice { get; set; }
    public string? ServiceType { get; set; }
    public bool ServiceIsMandatory { get; set; }
    public int ServiceMaxQuantity { get; set; }
}

public class CreateCompanyServiceDto
{
    [Required]
    public Guid CompanyId { get; set; }
    
    [Required]
    public Guid AdditionalServiceId { get; set; }
    
    public bool IsActive { get; set; } = true;
}

public class UpdateCompanyServiceDto
{
    public bool? IsActive { get; set; }
}

