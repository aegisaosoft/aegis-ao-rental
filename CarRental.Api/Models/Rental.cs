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

[Table("rentals")]
public class Rental
{
    [Key]
    [Column("rental_id")]
    public Guid RentalId { get; set; } = Guid.NewGuid();

    [Required]
    [Column("reservation_id")]
    public Guid ReservationId { get; set; }

    [Required]
    [Column("customer_id")]
    public Guid CustomerId { get; set; }

    [Required]
    [Column("vehicle_id")]
    public Guid VehicleId { get; set; }

    [Required]
    [Column("company_id")]
    public Guid CompanyId { get; set; }

    [Required]
    [Column("actual_pickup_date")]
    public DateTime ActualPickupDate { get; set; }

    [Required]
    [Column("expected_return_date")]
    public DateTime ExpectedReturnDate { get; set; }

    [Column("actual_return_date")]
    public DateTime? ActualReturnDate { get; set; }

    [Column("pickup_mileage")]
    public int? PickupMileage { get; set; }

    [Column("return_mileage")]
    public int? ReturnMileage { get; set; }

    [MaxLength(50)]
    [Column("fuel_level_pickup")]
    public string? FuelLevelPickup { get; set; }

    [MaxLength(50)]
    [Column("fuel_level_return")]
    public string? FuelLevelReturn { get; set; }

    [Column("damage_notes_pickup")]
    public string? DamageNotesPickup { get; set; }

    [Column("damage_notes_return")]
    public string? DamageNotesReturn { get; set; }

    [Column("additional_charges", TypeName = "decimal(10,2)")]
    public decimal AdditionalCharges { get; set; } = 0;

    [MaxLength(50)]
    public string Status { get; set; } = "active";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey("ReservationId")]
    public virtual Reservation Reservation { get; set; } = null!;

    [ForeignKey("CustomerId")]
    public virtual Customer Customer { get; set; } = null!;

    [ForeignKey("VehicleId")]
    public virtual Vehicle Vehicle { get; set; } = null!;

    [ForeignKey("CompanyId")]
    public virtual RentalCompany Company { get; set; } = null!;

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();
}
