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

[Table("vehicles")]
public class Vehicle
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [Column("company_id")]
    public Guid CompanyId { get; set; }

    [MaxLength(50)]
    [Column("color")]
    public string? Color { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("license_plate")]
    public string LicensePlate { get; set; } = string.Empty;

    [MaxLength(17)]
    [Column("vin")]
    public string? Vin { get; set; }

    [Column("mileage")]
    public int Mileage { get; set; } = 0;

    [MaxLength(50)]
    [Column("transmission")]
    public string? Transmission { get; set; }

    [Column("seats")]
    public int? Seats { get; set; }

    [Column("status")]
    public VehicleStatus Status { get; set; } = VehicleStatus.Available;

    [MaxLength(2)]
    [Column("state")]
    public string? State { get; set; }

    [MaxLength(255)]
    [Column("location")]
    public string? Location { get; set; } // Deprecated: use LocationId instead

    [Column("location_id")]
    public Guid? LocationId { get; set; }

    [Column("current_location_id")]
    public Guid? CurrentLocationId { get; set; }

    [Column("image_url")]
    public string? ImageUrl { get; set; }

    [Column("features")]
    public string[]? Features { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Column("vehicle_model_id")]
    public Guid? VehicleModelId { get; set; }

    // Navigation properties
    [ForeignKey("CompanyId")]
    public virtual RentalCompany Company { get; set; } = null!;

    [ForeignKey("LocationId")]
    public virtual CompanyLocation? LocationDetails { get; set; }

    [ForeignKey("CurrentLocationId")]
    public virtual Location? CurrentLocation { get; set; }

    public virtual ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
    public virtual ICollection<Rental> Rentals { get; set; } = new List<Rental>();
    public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();
    
    // Navigation to vehicle_model catalog for model information
    [ForeignKey("VehicleModelId")]
    public virtual VehicleModel? VehicleModel { get; set; }
}
