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

namespace CarRental.Api.DTOs.Stripe;

public class SuspendAccountDto
{
    public string Reason { get; set; } = string.Empty; // "fraud", "terms_of_service", "other"
}

public class ConnectedAccountDetailsDto
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // express, standard, custom
    public string? Email { get; set; }
    public string? Country { get; set; }
    public bool ChargesEnabled { get; set; }
    public bool PayoutsEnabled { get; set; }
    public bool DetailsSubmitted { get; set; }
    public DateTime Created { get; set; }
    
    // Business info
    public string? BusinessName { get; set; }
    public string? BusinessType { get; set; }
    
    // Requirements
    public List<string>? CurrentlyDue { get; set; }
    public List<string>? EventuallyDue { get; set; }
    public DateTime? CurrentDeadline { get; set; }
    public string? DisabledReason { get; set; }
    
    // Capabilities
    public Dictionary<string, string>? Capabilities { get; set; }
    
    // External account (bank)
    public ExternalAccountDto? ExternalAccount { get; set; }
}

public class ExternalAccountDto
{
    public string? BankName { get; set; }
    public string? Last4 { get; set; }
    public string? Country { get; set; }
    public string? Currency { get; set; }
}

