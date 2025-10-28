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

public class LocationDto
{
    public Guid? LocationId { get; set; }

    [Required]
    public Guid CompanyId { get; set; }

    [Required]
    [MaxLength(255)]
    public string LocationName { get; set; } = string.Empty;

    public string? Address { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    [MaxLength(100)]
    public string? State { get; set; }

    [MaxLength(100)]
    public string Country { get; set; } = "USA";

    [MaxLength(20)]
    public string? PostalCode { get; set; }

    [MaxLength(50)]
    [Phone]
    public string? Phone { get; set; }

    [MaxLength(255)]
    [EmailAddress]
    public string? Email { get; set; }

    [Range(-90, 90)]
    public decimal? Latitude { get; set; }

    [Range(-180, 180)]
    public decimal? Longitude { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsPickupLocation { get; set; } = true;

    public bool IsReturnLocation { get; set; } = true;

    public string? OpeningHours { get; set; }

    // Read-only properties
    public string? CompanyName { get; set; }
    public int? VehicleCount { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateLocationDto
{
    [Required]
    public Guid CompanyId { get; set; }

    [Required]
    [MaxLength(255)]
    public string LocationName { get; set; } = string.Empty;

    public string? Address { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    [MaxLength(100)]
    public string? State { get; set; }

    [MaxLength(100)]
    public string Country { get; set; } = "USA";

    [MaxLength(20)]
    public string? PostalCode { get; set; }

    [MaxLength(50)]
    [Phone]
    public string? Phone { get; set; }

    [MaxLength(255)]
    [EmailAddress]
    public string? Email { get; set; }

    [Range(-90, 90)]
    public decimal? Latitude { get; set; }

    [Range(-180, 180)]
    public decimal? Longitude { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsPickupLocation { get; set; } = true;

    public bool IsReturnLocation { get; set; } = true;

    public string? OpeningHours { get; set; }
}

public class UpdateLocationDto
{
    [Required]
    public Guid CompanyId { get; set; }

    [Required]
    [MaxLength(255)]
    public string LocationName { get; set; } = string.Empty;

    public string? Address { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    [MaxLength(100)]
    public string? State { get; set; }

    [MaxLength(100)]
    public string Country { get; set; } = "USA";

    [MaxLength(20)]
    public string? PostalCode { get; set; }

    [MaxLength(50)]
    [Phone]
    public string? Phone { get; set; }

    [MaxLength(255)]
    [EmailAddress]
    public string? Email { get; set; }

    [Range(-90, 90)]
    public decimal? Latitude { get; set; }

    [Range(-180, 180)]
    public decimal? Longitude { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsPickupLocation { get; set; } = true;

    public bool IsReturnLocation { get; set; } = true;

    public string? OpeningHours { get; set; }
}

