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

[Table("booking_services")]
public class BookingService
{
    [Required]
    [Column("booking_id")]
    public Guid BookingId { get; set; }

    [Required]
    [Column("additional_service_id")]
    public Guid AdditionalServiceId { get; set; }

    [Required]
    [Column("quantity")]
    [Range(1, int.MaxValue)]
    public int Quantity { get; set; } = 1;

    [Required]
    [Column("price_at_booking")]
    [Range(0, double.MaxValue)]
    public decimal PriceAtBooking { get; set; }

    [Required]
    [Column("subtotal")]
    [Range(0, double.MaxValue)]
    public decimal Subtotal { get; set; }

    [Required]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey("BookingId")]
    public Reservation Booking { get; set; } = null!;

    [ForeignKey("AdditionalServiceId")]
    public AdditionalService AdditionalService { get; set; } = null!;
}

