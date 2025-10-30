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

public class ReservationDto
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public Guid VehicleId { get; set; }
    public Guid CompanyId { get; set; }
    
    [Required]
    [MaxLength(50)]
    public string BookingNumber { get; set; } = string.Empty;
    
    [MaxLength(100)]
    public string? AltBookingNumber { get; set; }
    
    [Required]
    public DateTime PickupDate { get; set; }
    
    [Required]
    public DateTime ReturnDate { get; set; }
    
    [MaxLength(255)]
    public string? PickupLocation { get; set; }
    
    [MaxLength(255)]
    public string? ReturnLocation { get; set; }
    
    [Required]
    public decimal DailyRate { get; set; }
    
    [Required]
    public int TotalDays { get; set; }
    
    [Required]
    public decimal Subtotal { get; set; }
    
    public decimal TaxAmount { get; set; }
    public decimal InsuranceAmount { get; set; }
    public decimal AdditionalFees { get; set; }
    
    [Required]
    public decimal TotalAmount { get; set; }
    
    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = "Pending";
    
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    // Navigation properties
    public string? CustomerName { get; set; }
    public string? CustomerEmail { get; set; }
    public string? VehicleName { get; set; }
    public string? LicensePlate { get; set; }
    public string? CompanyName { get; set; }
}

public class CreateReservationDto
{
    [Required]
    public Guid CustomerId { get; set; }
    
    [Required]
    public Guid VehicleId { get; set; }
    
    [Required]
    public Guid CompanyId { get; set; }
    
    [MaxLength(100)]
    public string? AltBookingNumber { get; set; }
    
    [Required]
    public DateTime PickupDate { get; set; }
    
    [Required]
    public DateTime ReturnDate { get; set; }
    
    [MaxLength(255)]
    public string? PickupLocation { get; set; }
    
    [MaxLength(255)]
    public string? ReturnLocation { get; set; }
    
    [Required]
    public decimal DailyRate { get; set; }
    
    public decimal TaxAmount { get; set; } = 0;
    public decimal InsuranceAmount { get; set; } = 0;
    public decimal AdditionalFees { get; set; } = 0;
    
    public string? Notes { get; set; }
}

public class UpdateReservationDto
{
    [MaxLength(100)]
    public string? AltBookingNumber { get; set; }
    
    public DateTime? PickupDate { get; set; }
    public DateTime? ReturnDate { get; set; }
    
    [MaxLength(255)]
    public string? PickupLocation { get; set; }
    
    [MaxLength(255)]
    public string? ReturnLocation { get; set; }
    
    public decimal? TaxAmount { get; set; }
    public decimal? InsuranceAmount { get; set; }
    public decimal? AdditionalFees { get; set; }
    
    [MaxLength(50)]
    public string? Status { get; set; }
    
    public string? Notes { get; set; }
}

public class ReservationSearchDto
{
    public Guid? CustomerId { get; set; }
    public Guid? VehicleId { get; set; }
    public Guid? CompanyId { get; set; }
    public string? BookingNumber { get; set; }
    public DateTime? PickupDateFrom { get; set; }
    public DateTime? PickupDateTo { get; set; }
    public DateTime? ReturnDateFrom { get; set; }
    public DateTime? ReturnDateTo { get; set; }
    public string? Status { get; set; }
    public DateTime? CreatedFrom { get; set; }
    public DateTime? CreatedTo { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}