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

public class BookingDto
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

    public decimal SecurityDeposit { get; set; } = 1000m;

    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = "Pending";

    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public string? CustomerName { get; set; }
    public string? CustomerEmail { get; set; }
    public string? VehicleName { get; set; }
    public string? LicensePlate { get; set; }
    public string? CompanyName { get; set; }
    
    // Payment information
    public string? PaymentMethod { get; set; }
    public string? PaymentStatus { get; set; }
    public string? StripePaymentIntentId { get; set; }
    public DateTime? PaymentDate { get; set; }
    public decimal? RefundAmount { get; set; }
    
    // Security deposit information
    public string? SecurityDepositPaymentIntentId { get; set; }
    public string? SecurityDepositStatus { get; set; }
    public DateTime? SecurityDepositAuthorizedAt { get; set; }
    public DateTime? SecurityDepositCapturedAt { get; set; }
    public DateTime? SecurityDepositReleasedAt { get; set; }
    public decimal? SecurityDepositChargedAmount { get; set; }
    
    // Refund records
    public List<RefundRecordDto> RefundRecords { get; set; } = new List<RefundRecordDto>();
}

public class RefundRecordDto
{
    public Guid Id { get; set; }
    public Guid? BookingId { get; set; }
    public string StripeRefundId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? RefundType { get; set; }
    public string? Reason { get; set; }
    public string? Status { get; set; }
    public Guid? ProcessedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateBookingDto
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

    public decimal? SecurityDeposit { get; set; }

    public string? Notes { get; set; }
}

public class UpdateBookingDto
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

    public decimal? SecurityDeposit { get; set; }

    [MaxLength(50)]
    public string? Status { get; set; }

    public string? Notes { get; set; }
}

public class BookingSearchDto
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
    
    public decimal SecurityDeposit { get; set; } = 1000m;
    
    public VehicleInfoDto? VehicleInfo { get; set; }
    public CompanyInfoDto? CompanyInfo { get; set; }
    public LocationInfoDto? PickupLocationInfo { get; set; }
    public LocationInfoDto? ReturnLocationInfo { get; set; }
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
}

public class LocationInfoDto
{
    public string LocationName { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Country { get; set; }
    public string? PostalCode { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? OpeningHours { get; set; }
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
