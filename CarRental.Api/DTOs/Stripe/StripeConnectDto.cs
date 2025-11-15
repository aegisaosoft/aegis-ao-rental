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

namespace CarRental.Api.DTOs.Stripe;

public class CreateConnectedAccountDto
{
    [Required]
    public Guid CompanyId { get; set; }

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    public string BusinessType { get; set; } = "individual"; // or "company"

    [Required]
    [StringLength(2)]
    public string Country { get; set; } = "US";
}

public class CreateAccountLinkDto
{
    [Required]
    public Guid CompanyId { get; set; }

    [Required]
    [Url]
    public string ReturnUrl { get; set; } = string.Empty;

    [Required]
    [Url]
    public string RefreshUrl { get; set; } = string.Empty;
}

public class StripeAccountStatusDto
{
    public bool ChargesEnabled { get; set; }

    public bool PayoutsEnabled { get; set; }

    public bool DetailsSubmitted { get; set; }

    public bool OnboardingCompleted { get; set; }

    public string AccountStatus { get; set; } = string.Empty;

    public List<string> RequirementsCurrentlyDue { get; set; } = new();

    public List<string> RequirementsPastDue { get; set; } = new();

    public string? DisabledReason { get; set; }

    public DateTime? LastSyncAt { get; set; }
}

public class TransferFundsDto
{
    [Required]
    public Guid BookingId { get; set; }
}

public class SecurityDepositAuthorizationDto
{
    [Required]
    public Guid BookingId { get; set; }

    [Required]
    public string PaymentMethodId { get; set; } = string.Empty;

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
    public decimal Amount { get; set; }
}

