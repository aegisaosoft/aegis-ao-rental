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

public class RentalDto
{
    public Guid RentalId { get; set; }
    public Guid ReservationId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid VehicleId { get; set; }
    public Guid CompanyId { get; set; }
    
    [Required]
    public DateTime ActualPickupDate { get; set; }
    
    [Required]
    public DateTime ExpectedReturnDate { get; set; }
    
    public DateTime? ActualReturnDate { get; set; }
    
    public int? PickupMileage { get; set; }
    public int? ReturnMileage { get; set; }
    
    [MaxLength(50)]
    public string? FuelLevelPickup { get; set; }
    
    [MaxLength(50)]
    public string? FuelLevelReturn { get; set; }
    
    public string? DamageNotesPickup { get; set; }
    public string? DamageNotesReturn { get; set; }
    
    public decimal AdditionalCharges { get; set; }
    
    [MaxLength(50)]
    public string Status { get; set; } = "active";
    
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    // Navigation properties
    public string? CustomerName { get; set; }
    public string? VehicleName { get; set; }
    public string? CompanyName { get; set; }
    public string? ReservationNumber { get; set; }
}

public class CreateRentalDto
{
    [Required]
    public Guid ReservationId { get; set; }
    
    [Required]
    public DateTime ActualPickupDate { get; set; }
    
    [Required]
    public DateTime ExpectedReturnDate { get; set; }
    
    public int? PickupMileage { get; set; }
    
    [MaxLength(50)]
    public string? FuelLevelPickup { get; set; }
    
    public string? DamageNotesPickup { get; set; }
}

public class UpdateRentalDto
{
    public DateTime? ActualReturnDate { get; set; }
    public int? ReturnMileage { get; set; }
    
    [MaxLength(50)]
    public string? FuelLevelReturn { get; set; }
    
    public string? DamageNotesReturn { get; set; }
    public decimal? AdditionalCharges { get; set; }
    
    [MaxLength(50)]
    public string? Status { get; set; }
}

public class ReturnRentalDto
{
    [Required]
    public Guid RentalId { get; set; }
    
    [Required]
    public DateTime ActualReturnDate { get; set; }
    
    [Required]
    public int ReturnMileage { get; set; }
    
    [Required]
    [MaxLength(50)]
    public string FuelLevelReturn { get; set; } = string.Empty;
    
    public string? DamageNotesReturn { get; set; }
    public decimal AdditionalCharges { get; set; } = 0;
}
