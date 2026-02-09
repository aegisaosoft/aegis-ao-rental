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

public class PaymentDto
{
    public Guid PaymentId { get; set; }
    public Guid? ReservationId { get; set; }
    public Guid? RentalId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid CompanyId { get; set; }
    
    [Required]
    public decimal Amount { get; set; }
    
    [MaxLength(10)]
    public string Currency { get; set; } = "USD";
    
    [Required]
    [MaxLength(50)]
    public string PaymentType { get; set; } = string.Empty;
    
    [MaxLength(50)]
    public string? PaymentMethod { get; set; }
    
    public string? StripePaymentIntentId { get; set; }
    public string? StripeChargeId { get; set; }
    public string? StripePaymentMethodId { get; set; }
    
    [MaxLength(50)]
    public string Status { get; set; } = "pending";

    public decimal? SecurityDepositAmount { get; set; }
    public string? SecurityDepositStatus { get; set; }
    public string? SecurityDepositPaymentIntentId { get; set; }
    public string? SecurityDepositChargeId { get; set; }
    public DateTime? SecurityDepositAuthorizedAt { get; set; }
    public DateTime? SecurityDepositCapturedAt { get; set; }
    public DateTime? SecurityDepositReleasedAt { get; set; }
    public decimal? SecurityDepositCapturedAmount { get; set; }
    public string? SecurityDepositCaptureReason { get; set; }
    
    public string? FailureReason { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    
    // Navigation properties
    public string? CustomerName { get; set; }
    public string? CompanyName { get; set; }
}

public class CreatePaymentDto
{
    [Required]
    public Guid CustomerId { get; set; }
    
    [Required]
    public Guid CompanyId { get; set; }
    
    public Guid? ReservationId { get; set; }
    public Guid? RentalId { get; set; }
    
    [Required]
    public decimal Amount { get; set; }
    
    [MaxLength(10)]
    public string Currency { get; set; } = "USD";
    
    [Required]
    [MaxLength(50)]
    public string PaymentType { get; set; } = string.Empty;
    
    [MaxLength(50)]
    public string? PaymentMethod { get; set; }
    
    public string? StripePaymentMethodId { get; set; }

    public decimal? SecurityDepositAmount { get; set; }
}

public class ProcessPaymentDto
{
    [Required]
    public Guid CustomerId { get; set; }

    public Guid? CompanyId { get; set; }

    public Guid? BookingId { get; set; }

    [Required]
    public decimal Amount { get; set; }

    [MaxLength(10)]
    public string Currency { get; set; } = "USD";

    [Required]
    public string PaymentMethodId { get; set; } = string.Empty;

    public string? Description { get; set; }
}

public class RefundPaymentDto
{
    [Required]
    public Guid PaymentId { get; set; }
    
    [Required]
    public decimal Amount { get; set; }
    
    public string? Reason { get; set; }
}

public class PaymentMethodDto
{
    public Guid PaymentMethodId { get; set; }
    public Guid CustomerId { get; set; }
    
    [Required]
    public string StripePaymentMethodId { get; set; } = string.Empty;
    
    [MaxLength(50)]
    public string? CardBrand { get; set; }
    
    [MaxLength(4)]
    public string? CardLast4 { get; set; }
    
    public int? CardExpMonth { get; set; }
    public int? CardExpYear { get; set; }
    
    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreatePaymentMethodDto
{
    [Required]
    public Guid CustomerId { get; set; }
    
    [Required]
    public string StripePaymentMethodId { get; set; } = string.Empty;
    
    public bool IsDefault { get; set; } = false;
}

public class CaptureSecurityDepositDto
{
    [Required]
    public Guid ReservationId { get; set; }

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }

    [MaxLength(500)]
    public string? Reason { get; set; }
}

public class ReleaseSecurityDepositDto
{
    [Required]
    public Guid ReservationId { get; set; }

    [MaxLength(500)]
    public string? Reason { get; set; }
}
