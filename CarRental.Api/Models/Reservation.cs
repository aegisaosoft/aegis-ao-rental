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

[Table("reservations")]
public class Reservation
{
    [Key]
    [Column("reservation_id")]
    public Guid ReservationId { get; set; } = Guid.NewGuid();

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
    [MaxLength(50)]
    [Column("reservation_number")]
    public string ReservationNumber { get; set; } = string.Empty;

    [Required]
    [Column("pickup_date")]
    public DateTime PickupDate { get; set; }

    [Required]
    [Column("return_date")]
    public DateTime ReturnDate { get; set; }

    [MaxLength(255)]
    [Column("pickup_location")]
    public string? PickupLocation { get; set; }

    [MaxLength(255)]
    [Column("return_location")]
    public string? ReturnLocation { get; set; }

    [Required]
    [Column("daily_rate", TypeName = "decimal(10,2)")]
    public decimal DailyRate { get; set; }

    [Required]
    [Column("total_days")]
    public int TotalDays { get; set; }

    [Required]
    [Column(TypeName = "decimal(10,2)")]
    public decimal Subtotal { get; set; }

    [Column("tax_amount", TypeName = "decimal(10,2)")]
    public decimal TaxAmount { get; set; } = 0;

    [Column("insurance_amount", TypeName = "decimal(10,2)")]
    public decimal InsuranceAmount { get; set; } = 0;

    [Column("additional_fees", TypeName = "decimal(10,2)")]
    public decimal AdditionalFees { get; set; } = 0;

    [Required]
    [Column("total_amount", TypeName = "decimal(10,2)")]
    public decimal TotalAmount { get; set; }

    [MaxLength(50)]
    public string Status { get; set; } = "pending";

    public string? Notes { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey("CustomerId")]
    public virtual Customer Customer { get; set; } = null!;

    [ForeignKey("VehicleId")]
    public virtual Vehicle Vehicle { get; set; } = null!;

    [ForeignKey("CompanyId")]
    public virtual RentalCompany Company { get; set; } = null!;

    public virtual ICollection<Rental> Rentals { get; set; } = new List<Rental>();
    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
