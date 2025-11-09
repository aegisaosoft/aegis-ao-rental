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

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace CarRental.Api.Helpers;

public static class CurrencyHelper
{
    private static readonly Dictionary<string, string> CountryCurrencyMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // North America
        ["anguilla"] = "XCD",
        ["antigua and barbuda"] = "XCD",
        ["bahamas"] = "BSD",
        ["barbados"] = "BBD",
        ["belize"] = "BZD",
        ["bermuda"] = "BMD",
        ["british virgin islands"] = "USD",
        ["canada"] = "CAD",
        ["cayman islands"] = "KYD",
        ["costa rica"] = "CRC",
        ["cuba"] = "CUP",
        ["dominica"] = "XCD",
        ["dominican republic"] = "DOP",
        ["el salvador"] = "USD",
        ["greenland"] = "DKK",
        ["grenada"] = "XCD",
        ["guatemala"] = "GTQ",
        ["haiti"] = "HTG",
        ["honduras"] = "HNL",
        ["jamaica"] = "JMD",
        ["mexico"] = "MXN",
        ["montserrat"] = "XCD",
        ["nicaragua"] = "NIO",
        ["panama"] = "PAB",
        ["puerto rico"] = "USD",
        ["saint kitts and nevis"] = "XCD",
        ["saint lucia"] = "XCD",
        ["saint pierre and miquelon"] = "EUR",
        ["saint vincent and the grenadines"] = "XCD",
        ["trinidad and tobago"] = "TTD",
        ["turks and caicos islands"] = "USD",
        ["united states"] = "USD",
        ["us virgin islands"] = "USD",
        ["virgin islands"] = "USD",

        // South America
        ["argentina"] = "ARS",
        ["bolivia"] = "BOB",
        ["brazil"] = "BRL",
        ["chile"] = "CLP",
        ["colombia"] = "COP",
        ["ecuador"] = "USD",
        ["french guiana"] = "EUR",
        ["guyana"] = "GYD",
        ["paraguay"] = "PYG",
        ["peru"] = "PEN",
        ["suriname"] = "SRD",
        ["uruguay"] = "UYU",
        ["venezuela"] = "VES",

        // Common fallbacks / codes
        ["ca"] = "CAD",
        ["us"] = "USD",
        ["usa"] = "USD",
        ["mx"] = "MXN",
        ["ar"] = "ARS",
        ["br"] = "BRL",
        ["cl"] = "CLP",
        ["co"] = "COP",
        ["pe"] = "PEN",
        ["uy"] = "UYU",
        ["ve"] = "VES",
        ["cr"] = "CRC",
        ["gt"] = "GTQ",
        ["hn"] = "HNL",
        ["ni"] = "NIO",
        ["pa"] = "PAB",
        ["do"] = "DOP",
        ["jm"] = "JMD",
        ["tt"] = "TTD",
        ["bz"] = "BZD",
        ["bb"] = "BBD",
        ["bs"] = "BSD",
        ["dm"] = "XCD",
        ["gd"] = "XCD",
        ["lc"] = "XCD",
        ["vc"] = "XCD",
        ["ag"] = "XCD",
        ["kn"] = "XCD",
        ["ms"] = "XCD",
        ["gy"] = "GYD",
        ["sr"] = "SRD",
        ["bo"] = "BOB",
        ["py"] = "PYG",
        ["cu"] = "CUP",
        ["pr"] = "USD",
        ["vi"] = "USD",
        ["gl"] = "DKK"
    };

    private static readonly HashSet<string> SupportedCurrenciesInternal = new(StringComparer.OrdinalIgnoreCase)
    {
        "USD", "CAD", "MXN", "BSD", "BBD", "BZD", "BMD", "KYD", "CRC",
        "CUP", "XCD", "DOP", "DKK", "GTQ", "HTG", "HNL", "JMD", "NIO",
        "PAB", "EUR", "TTD", "ARS", "BOB", "BRL", "CLP", "COP", "GYD",
        "PEN", "PYG", "SRD", "UYU", "VES"
    };

    public static readonly ReadOnlyCollection<string> SupportedCurrencies =
        SupportedCurrenciesInternal.OrderBy(c => c).ToList().AsReadOnly();

    public static string ResolveCurrency(string? explicitCurrency, string? country)
    {
        if (!string.IsNullOrWhiteSpace(explicitCurrency) &&
            SupportedCurrenciesInternal.Contains(explicitCurrency.Trim().ToUpperInvariant()))
        {
            return explicitCurrency.Trim().ToUpperInvariant();
        }

        return GetCurrencyForCountry(country);
    }

    public static string GetCurrencyForCountry(string? country)
    {
        if (!string.IsNullOrWhiteSpace(country))
        {
            var key = country.Trim();
            if (CountryCurrencyMap.TryGetValue(key, out var currency))
            {
                return currency;
            }

            // Attempt with lowercase trimmed variant
            var normalized = key.Trim().ToLowerInvariant();
            if (CountryCurrencyMap.TryGetValue(normalized, out currency))
            {
                return currency;
            }
        }

        return "USD";
    }

    public static bool IsSupportedCurrency(string? currency) =>
        !string.IsNullOrWhiteSpace(currency) &&
        SupportedCurrenciesInternal.Contains(currency.Trim().ToUpperInvariant());
}

