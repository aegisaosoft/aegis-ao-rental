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

public class BookingTokenDto
{
    public Guid TokenId { get; set; }
    public Guid CompanyId { get; set; }
    public string CustomerEmail { get; set; } = string.Empty;
    public Guid VehicleId { get; set; }
    public string Token { get; set; } = string.Empty;
    public BookingDataDto BookingData { get; set; } = new();
    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; }
    public DateTime? UsedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    // Navigation properties
    public string? CompanyName { get; set; }
    public string? VehicleName { get; set; }
}

public class CreateBookingTokenDto
{
    [Required]
    public Guid CompanyId { get; set; }
    
    [Required]
    [EmailAddress]
    public string CustomerEmail { get; set; } = string.Empty;
    
    [Required]
    public Guid VehicleId { get; set; }
    
    [Required]
    public BookingDataDto BookingData { get; set; } = new();
    
    public int ExpirationHours { get; set; } = 24; // Default 24 hours
}

public class BookingDataDto
{
    [Required]
    public DateTime PickupDate { get; set; }
    
    [Required]
    public DateTime ReturnDate { get; set; }
    
    public string? PickupLocation { get; set; }
    public string? ReturnLocation { get; set; }
    
    [Required]
    public decimal DailyRate { get; set; }
    
    [Required]
    public int TotalDays { get; set; }
    
    [Required]
    public decimal Subtotal { get; set; }
    
    public decimal TaxAmount { get; set; } = 0;
    public decimal InsuranceAmount { get; set; } = 0;
    public decimal AdditionalFees { get; set; } = 0;
    
    [Required]
    public decimal TotalAmount { get; set; }
    
    public VehicleInfoDto? VehicleInfo { get; set; }
    public CompanyInfoDto? CompanyInfo { get; set; }
    public string? Notes { get; set; }
}

public class VehicleInfoDto
{
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int Year { get; set; }
    public string? Color { get; set; }
    public string LicensePlate { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string[]? Features { get; set; }
}

public class CompanyInfoDto
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Country { get; set; }
}

public class ProcessBookingDto
{
    [Required]
    public string Token { get; set; } = string.Empty;
    
    [Required]
    public string PaymentMethodId { get; set; } = string.Empty;
    
    public string? CustomerNotes { get; set; }
}

public class BookingConfirmationDto
{
    public Guid ConfirmationId { get; set; }
    public Guid BookingTokenId { get; set; }
    public Guid? ReservationId { get; set; }
    public string CustomerEmail { get; set; } = string.Empty;
    public string ConfirmationNumber { get; set; } = string.Empty;
    public BookingDataDto BookingDetails { get; set; } = new();
    public string PaymentStatus { get; set; } = string.Empty;
    public string? StripePaymentIntentId { get; set; }
    public bool ConfirmationSent { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class EmailNotificationDto
{
    public Guid NotificationId { get; set; }
    public Guid? BookingTokenId { get; set; }
    public string CustomerEmail { get; set; } = string.Empty;
    public string NotificationType { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? SentAt { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SendBookingLinkDto
{
    [Required]
    public Guid CompanyId { get; set; }
    
    [Required]
    [EmailAddress]
    public string CustomerEmail { get; set; } = string.Empty;
    
    [Required]
    public Guid VehicleId { get; set; }
    
    [Required]
    public BookingDataDto BookingData { get; set; } = new();
    
    public int ExpirationHours { get; set; } = 24;
    public string? CustomMessage { get; set; }
}
