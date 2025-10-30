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

public class BookingServiceDto
{
    public Guid BookingId { get; set; }
    public Guid AdditionalServiceId { get; set; }
    
    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }
    
    [Range(0, double.MaxValue)]
    public decimal PriceAtBooking { get; set; }
    
    [Range(0, double.MaxValue)]
    public decimal Subtotal { get; set; }
    
    public DateTime CreatedAt { get; set; }
    
    // Expanded information
    public string? BookingNumber { get; set; }
    public string? ServiceName { get; set; }
    public string? ServiceDescription { get; set; }
    public string? ServiceType { get; set; }
}

public class CreateBookingServiceDto
{
    [Required]
    public Guid BookingId { get; set; }
    
    [Required]
    public Guid AdditionalServiceId { get; set; }
    
    [Range(1, int.MaxValue)]
    public int Quantity { get; set; } = 1;
    
    // Price will be fetched from the service at the time of booking
    // Subtotal will be calculated automatically
}

public class UpdateBookingServiceDto
{
    [Range(1, int.MaxValue)]
    public int? Quantity { get; set; }
    
    // When quantity changes, subtotal will be recalculated
}

