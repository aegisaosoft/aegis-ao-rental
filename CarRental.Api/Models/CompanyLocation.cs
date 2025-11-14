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

[Table("company_location")]
public class CompanyLocation
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [Column("company_id")]
    public Guid CompanyId { get; set; }

    [Required]
    [MaxLength(255)]
    [Column("location_name")]
    public string LocationName { get; set; } = string.Empty;

    [Column("address")]
    public string? Address { get; set; }

    [MaxLength(100)]
    [Column("city")]
    public string? City { get; set; }

    [MaxLength(100)]
    [Column("state")]
    public string? State { get; set; }

    [MaxLength(100)]
    [Column("country")]
    public string Country { get; set; } = "USA";

    [MaxLength(20)]
    [Column("postal_code")]
    public string? PostalCode { get; set; }

    [MaxLength(50)]
    [Column("phone")]
    public string? Phone { get; set; }

    [MaxLength(255)]
    [Column("email")]
    public string? Email { get; set; }

    [Column("latitude", TypeName = "decimal(10,8)")]
    public decimal? Latitude { get; set; }

    [Column("longitude", TypeName = "decimal(11,8)")]
    public decimal? Longitude { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("is_pickup_location")]
    public bool IsPickupLocation { get; set; } = true;

    [Column("is_return_location")]
    public bool IsReturnLocation { get; set; } = true;

    [Column("is_office")]
    public bool IsOffice { get; set; } = false;

    [Column("opening_hours")]
    public string? OpeningHours { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey("CompanyId")]
    public virtual Company Company { get; set; } = null!;

    public virtual ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
}

