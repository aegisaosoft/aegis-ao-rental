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

namespace CarRental.Api.Models;

public enum PriceComponentType
{
    Base,
    Tax,
    Insurance,
    Equipment,
    Fee,
    Discount
}

public static class PriceComponentTypeConstants
{
    public const string Base = "Base";
    public const string Tax = "Tax";
    public const string Insurance = "Insurance";
    public const string Equipment = "Equipment";
    public const string Fee = "Fee";
    public const string Discount = "Discount";
}
