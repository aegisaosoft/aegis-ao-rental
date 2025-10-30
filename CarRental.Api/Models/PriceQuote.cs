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

namespace CarRental.Api.Models;

public class PriceQuote
{
    public decimal BasePrice { get; set; }
    public List<PriceComponent> PriceComponents { get; set; } = new List<PriceComponent>();
    public decimal TotalPrice { get; set; }
    
    [MaxLength(10)]
    public string Currency { get; set; } = "USD";
    
    public DateTime ValidUntil { get; set; }
    
    [MaxLength(100)]
    public string QuoteReference { get; set; } = string.Empty;
}

public class PriceComponent
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    
    public decimal Amount { get; set; }
    
    public PriceComponentType Type { get; set; }
}
