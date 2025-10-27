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

public class ReviewDto
{
    public Guid ReviewId { get; set; }
    public Guid RentalId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid VehicleId { get; set; }
    
    [Required]
    [Range(1, 5)]
    public int Rating { get; set; }
    
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
    
    // Navigation properties
    public string? CustomerName { get; set; }
    public string? VehicleName { get; set; }
    public string? CompanyName { get; set; }
    public string? RentalNumber { get; set; }
}

public class CreateReviewDto
{
    [Required]
    public Guid RentalId { get; set; }
    
    [Required]
    public Guid CustomerId { get; set; }
    
    [Required]
    public Guid CompanyId { get; set; }
    
    [Required]
    public Guid VehicleId { get; set; }
    
    [Required]
    [Range(1, 5)]
    public int Rating { get; set; }
    
    public string? Comment { get; set; }
}

public class UpdateReviewDto
{
    [Range(1, 5)]
    public int? Rating { get; set; }
    
    public string? Comment { get; set; }
}

public class ReviewSearchDto
{
    public Guid? CustomerId { get; set; }
    public Guid? CompanyId { get; set; }
    public Guid? VehicleId { get; set; }
    public Guid? RentalId { get; set; }
    public int? MinRating { get; set; }
    public int? MaxRating { get; set; }
    public DateTime? CreatedFrom { get; set; }
    public DateTime? CreatedTo { get; set; }
}
