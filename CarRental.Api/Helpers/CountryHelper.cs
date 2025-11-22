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

/// <summary>
/// Helper class for ISO 3166-1 alpha-2 country code conversion and validation.
/// All country values in the system should use ISO 3166-1 alpha-2 codes (e.g., "US", "BR", "GB").
/// </summary>
public static class CountryHelper
{
    /// <summary>
    /// Maps ISO 3166-1 alpha-2 country codes to country names
    /// </summary>
    private static readonly Dictionary<string, string> CodeToNameMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // North America
        { "US", "United States" },
        { "CA", "Canada" },
        { "MX", "Mexico" },
        { "GT", "Guatemala" },
        { "BZ", "Belize" },
        { "SV", "El Salvador" },
        { "HN", "Honduras" },
        { "NI", "Nicaragua" },
        { "CR", "Costa Rica" },
        { "PA", "Panama" },
        { "CU", "Cuba" },
        { "JM", "Jamaica" },
        { "HT", "Haiti" },
        { "DO", "Dominican Republic" },
        { "PR", "Puerto Rico" },
        { "TT", "Trinidad and Tobago" },
        { "BS", "Bahamas" },
        { "BB", "Barbados" },
        { "AG", "Antigua and Barbuda" },
        { "DM", "Dominica" },
        { "GD", "Grenada" },
        { "LC", "Saint Lucia" },
        { "VC", "Saint Vincent and the Grenadines" },
        { "KN", "Saint Kitts and Nevis" },
        { "AI", "Anguilla" },
        { "MS", "Montserrat" },
        { "KY", "Cayman Islands" },
        { "TC", "Turks and Caicos Islands" },
        { "VG", "British Virgin Islands" },
        { "VI", "US Virgin Islands" },
        { "BM", "Bermuda" },
        { "GL", "Greenland" },
        { "PM", "Saint Pierre and Miquelon" },
        
        // South America
        { "BR", "Brazil" },
        { "AR", "Argentina" },
        { "CL", "Chile" },
        { "CO", "Colombia" },
        { "PE", "Peru" },
        { "VE", "Venezuela" },
        { "EC", "Ecuador" },
        { "BO", "Bolivia" },
        { "PY", "Paraguay" },
        { "UY", "Uruguay" },
        { "GY", "Guyana" },
        { "SR", "Suriname" },
        { "GF", "French Guiana" },
        { "FK", "Falkland Islands" },
        
        // Europe
        { "GB", "United Kingdom" },
        { "IE", "Ireland" },
        { "FR", "France" },
        { "DE", "Germany" },
        { "IT", "Italy" },
        { "ES", "Spain" },
        { "PT", "Portugal" },
        { "NL", "Netherlands" },
        { "BE", "Belgium" },
        { "CH", "Switzerland" },
        { "AT", "Austria" },
        { "SE", "Sweden" },
        { "NO", "Norway" },
        { "DK", "Denmark" },
        { "FI", "Finland" },
        { "PL", "Poland" },
        { "GR", "Greece" },
        { "CZ", "Czech Republic" },
        { "RO", "Romania" },
        { "HU", "Hungary" },
        { "BG", "Bulgaria" },
        { "HR", "Croatia" },
        { "SK", "Slovakia" },
        { "SI", "Slovenia" },
        { "LT", "Lithuania" },
        { "LV", "Latvia" },
        { "EE", "Estonia" },
        { "LU", "Luxembourg" },
        { "MT", "Malta" },
        { "CY", "Cyprus" },
        { "IS", "Iceland" },
        { "RU", "Russia" },
        { "UA", "Ukraine" },
        { "TR", "Turkey" },
        { "RS", "Serbia" },
        { "BA", "Bosnia and Herzegovina" },
        { "MK", "North Macedonia" },
        { "AL", "Albania" },
        { "ME", "Montenegro" },
        { "XK", "Kosovo" },
        { "MD", "Moldova" },
        { "BY", "Belarus" },
        
        // Asia
        { "CN", "China" },
        { "JP", "Japan" },
        { "IN", "India" },
        { "KR", "South Korea" },
        { "SG", "Singapore" },
        { "MY", "Malaysia" },
        { "TH", "Thailand" },
        { "ID", "Indonesia" },
        { "PH", "Philippines" },
        { "VN", "Vietnam" },
        { "TW", "Taiwan" },
        { "HK", "Hong Kong" },
        { "MO", "Macau" },
        { "AE", "United Arab Emirates" },
        { "SA", "Saudi Arabia" },
        { "IL", "Israel" },
        { "PK", "Pakistan" },
        { "BD", "Bangladesh" },
        { "LK", "Sri Lanka" },
        { "NP", "Nepal" },
        { "MM", "Myanmar" },
        { "KH", "Cambodia" },
        { "LA", "Laos" },
        { "MN", "Mongolia" },
        { "KZ", "Kazakhstan" },
        { "UZ", "Uzbekistan" },
        { "AF", "Afghanistan" },
        { "IR", "Iran" },
        { "IQ", "Iraq" },
        { "JO", "Jordan" },
        { "LB", "Lebanon" },
        { "SY", "Syria" },
        { "YE", "Yemen" },
        { "OM", "Oman" },
        { "QA", "Qatar" },
        { "KW", "Kuwait" },
        { "BH", "Bahrain" },
        { "BN", "Brunei" },
        { "MV", "Maldives" },
        { "BT", "Bhutan" },
        
        // Africa
        { "EG", "Egypt" },
        { "ZA", "South Africa" },
        { "NG", "Nigeria" },
        { "KE", "Kenya" },
        { "MA", "Morocco" },
        { "DZ", "Algeria" },
        { "TN", "Tunisia" },
        { "GH", "Ghana" },
        { "ET", "Ethiopia" },
        { "TZ", "Tanzania" },
        { "UG", "Uganda" },
        { "SN", "Senegal" },
        { "CI", "Ivory Coast" },
        { "CM", "Cameroon" },
        { "MG", "Madagascar" },
        { "AO", "Angola" },
        { "MZ", "Mozambique" },
        { "ZM", "Zambia" },
        { "ZW", "Zimbabwe" },
        { "BW", "Botswana" },
        { "NA", "Namibia" },
        { "MU", "Mauritius" },
        { "RW", "Rwanda" },
        { "BJ", "Benin" },
        { "BF", "Burkina Faso" },
        { "ML", "Mali" },
        { "NE", "Niger" },
        { "TD", "Chad" },
        { "SD", "Sudan" },
        { "LY", "Libya" },
        { "SO", "Somalia" },
        { "ER", "Eritrea" },
        { "DJ", "Djibouti" },
        { "LR", "Liberia" },
        { "SL", "Sierra Leone" },
        { "GN", "Guinea" },
        { "GW", "Guinea-Bissau" },
        { "CV", "Cape Verde" },
        { "ST", "São Tomé and Príncipe" },
        { "GA", "Gabon" },
        { "CG", "Republic of the Congo" },
        { "CD", "Democratic Republic of the Congo" },
        { "CF", "Central African Republic" },
        { "SS", "South Sudan" },
        { "MW", "Malawi" },
        { "LS", "Lesotho" },
        { "SZ", "Eswatini" },
        
        // Oceania
        { "AU", "Australia" },
        { "NZ", "New Zealand" },
        { "FJ", "Fiji" },
        { "PG", "Papua New Guinea" },
        { "WS", "Samoa" },
        { "TO", "Tonga" },
        { "VU", "Vanuatu" },
        { "SB", "Solomon Islands" },
        { "PW", "Palau" },
        { "FM", "Micronesia" },
        { "MH", "Marshall Islands" },
        { "KI", "Kiribati" },
        { "TV", "Tuvalu" },
        { "NR", "Nauru" },
        { "NC", "New Caledonia" },
        { "PF", "French Polynesia" },
        { "GU", "Guam" },
        { "AS", "American Samoa" },
        { "MP", "Northern Mariana Islands" },
        { "CK", "Cook Islands" },
        { "NU", "Niue" },
        { "TK", "Tokelau" }
    };

    /// <summary>
    /// Maps country names (various formats) to ISO 3166-1 alpha-2 codes
    /// </summary>
    private static readonly Dictionary<string, string> NameToCodeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // North America - common variations
        { "United States", "US" },
        { "United States of America", "US" },
        { "USA", "US" },
        { "Canada", "CA" },
        { "Mexico", "MX" },
        { "Guatemala", "GT" },
        { "Belize", "BZ" },
        { "El Salvador", "SV" },
        { "Honduras", "HN" },
        { "Nicaragua", "NI" },
        { "Costa Rica", "CR" },
        { "Panama", "PA" },
        { "Cuba", "CU" },
        { "Jamaica", "JM" },
        { "Haiti", "HT" },
        { "Dominican Republic", "DO" },
        { "Puerto Rico", "PR" },
        { "Trinidad and Tobago", "TT" },
        { "Bahamas", "BS" },
        { "Barbados", "BB" },
        { "Antigua and Barbuda", "AG" },
        { "Dominica", "DM" },
        { "Grenada", "GD" },
        { "Saint Lucia", "LC" },
        { "Saint Vincent and the Grenadines", "VC" },
        { "Saint Kitts and Nevis", "KN" },
        { "Anguilla", "AI" },
        { "Montserrat", "MS" },
        { "Cayman Islands", "KY" },
        { "Turks and Caicos Islands", "TC" },
        { "British Virgin Islands", "VG" },
        { "US Virgin Islands", "VI" },
        { "Bermuda", "BM" },
        { "Greenland", "GL" },
        { "Saint Pierre and Miquelon", "PM" },
        
        // South America
        { "Brazil", "BR" },
        { "Argentina", "AR" },
        { "Chile", "CL" },
        { "Colombia", "CO" },
        { "Peru", "PE" },
        { "Venezuela", "VE" },
        { "Ecuador", "EC" },
        { "Bolivia", "BO" },
        { "Paraguay", "PY" },
        { "Uruguay", "UY" },
        { "Guyana", "GY" },
        { "Suriname", "SR" },
        { "French Guiana", "GF" },
        { "Falkland Islands", "FK" },
        
        // Europe
        { "United Kingdom", "GB" },
        { "UK", "GB" },
        { "Great Britain", "GB" },
        { "Ireland", "IE" },
        { "France", "FR" },
        { "Germany", "DE" },
        { "Italy", "IT" },
        { "Spain", "ES" },
        { "Portugal", "PT" },
        { "Netherlands", "NL" },
        { "Belgium", "BE" },
        { "Switzerland", "CH" },
        { "Austria", "AT" },
        { "Sweden", "SE" },
        { "Norway", "NO" },
        { "Denmark", "DK" },
        { "Finland", "FI" },
        { "Poland", "PL" },
        { "Greece", "GR" },
        { "Czech Republic", "CZ" },
        { "Romania", "RO" },
        { "Hungary", "HU" },
        { "Bulgaria", "BG" },
        { "Croatia", "HR" },
        { "Slovakia", "SK" },
        { "Slovenia", "SI" },
        { "Lithuania", "LT" },
        { "Latvia", "LV" },
        { "Estonia", "EE" },
        { "Luxembourg", "LU" },
        { "Malta", "MT" },
        { "Cyprus", "CY" },
        { "Iceland", "IS" },
        { "Russia", "RU" },
        { "Ukraine", "UA" },
        { "Turkey", "TR" },
        
        // Asia
        { "China", "CN" },
        { "Japan", "JP" },
        { "India", "IN" },
        { "South Korea", "KR" },
        { "Korea", "KR" },
        { "Singapore", "SG" },
        { "Malaysia", "MY" },
        { "Thailand", "TH" },
        { "Indonesia", "ID" },
        { "Philippines", "PH" },
        { "Vietnam", "VN" },
        { "Taiwan", "TW" },
        { "Hong Kong", "HK" },
        { "Macau", "MO" },
        { "United Arab Emirates", "AE" },
        { "UAE", "AE" },
        { "Saudi Arabia", "SA" },
        { "Israel", "IL" },
        { "Pakistan", "PK" },
        { "Bangladesh", "BD" },
        { "Sri Lanka", "LK" },
        { "Nepal", "NP" },
        { "Myanmar", "MM" },
        { "Cambodia", "KH" },
        { "Laos", "LA" },
        { "Mongolia", "MN" },
        { "Kazakhstan", "KZ" },
        
        // Africa
        { "Egypt", "EG" },
        { "South Africa", "ZA" },
        { "Nigeria", "NG" },
        { "Kenya", "KE" },
        { "Morocco", "MA" },
        { "Algeria", "DZ" },
        { "Tunisia", "TN" },
        { "Ghana", "GH" },
        { "Ethiopia", "ET" },
        { "Tanzania", "TZ" },
        { "Uganda", "UG" },
        { "Senegal", "SN" },
        { "Ivory Coast", "CI" },
        { "Côte d'Ivoire", "CI" },
        { "Cameroon", "CM" },
        { "Madagascar", "MG" },
        { "Angola", "AO" },
        { "Mozambique", "MZ" },
        { "Zambia", "ZM" },
        { "Zimbabwe", "ZW" },
        { "Botswana", "BW" },
        { "Namibia", "NA" },
        { "Mauritius", "MU" },
        { "Rwanda", "RW" },
        
        // Oceania
        { "Australia", "AU" },
        { "New Zealand", "NZ" },
        { "Fiji", "FJ" },
        { "Papua New Guinea", "PG" },
        { "Samoa", "WS" },
        { "Tonga", "TO" },
        { "Vanuatu", "VU" },
        { "Solomon Islands", "SB" },
        { "Palau", "PW" },
        { "Micronesia", "FM" },
        { "Marshall Islands", "MH" },
        { "Kiribati", "KI" },
        { "Tuvalu", "TV" },
        { "Nauru", "NR" }
    };

    /// <summary>
    /// Gets all supported ISO 3166-1 alpha-2 country codes
    /// </summary>
    public static readonly ReadOnlyCollection<string> SupportedCountryCodes =
        CodeToNameMap.Keys.OrderBy(k => k).ToList().AsReadOnly();

    /// <summary>
    /// Gets all country names sorted alphabetically
    /// </summary>
    public static readonly ReadOnlyCollection<string> SupportedCountryNames =
        CodeToNameMap.Values.OrderBy(v => v).ToList().AsReadOnly();

    /// <summary>
    /// Converts a country name or code to ISO 3166-1 alpha-2 code.
    /// If the input is already a valid 2-character code, it returns it as-is (uppercase).
    /// If the input is a country name, it converts it to the ISO code.
    /// Returns "US" as default if conversion fails.
    /// </summary>
    /// <param name="country">Country name or ISO code</param>
    /// <returns>ISO 3166-1 alpha-2 country code (e.g., "US", "BR", "GB")</returns>
    public static string NormalizeToIsoCode(string? country)
    {
        if (string.IsNullOrWhiteSpace(country))
        {
            return "US"; // Default to US
        }

        var trimmed = country.Trim();

        // If it's already a 2-character code, validate and return uppercase
        if (trimmed.Length == 2 && trimmed.All(char.IsLetter))
        {
            var upperCode = trimmed.ToUpperInvariant();
            if (CodeToNameMap.ContainsKey(upperCode))
            {
                return upperCode;
            }
        }

        // Try to find in name-to-code map
        if (NameToCodeMap.TryGetValue(trimmed, out var code))
        {
            return code;
        }

        // If not found, log a warning and default to US
        // In production, you might want to throw an exception or log this for manual review
        return "US";
    }

    /// <summary>
    /// Converts an ISO 3166-1 alpha-2 code to country name.
    /// Returns the code itself if conversion fails.
    /// </summary>
    /// <param name="isoCode">ISO 3166-1 alpha-2 country code (e.g., "US", "BR", "GB")</param>
    /// <returns>Country name (e.g., "United States", "Brazil", "United Kingdom")</returns>
    public static string GetCountryName(string? isoCode)
    {
        if (string.IsNullOrWhiteSpace(isoCode))
        {
            return "United States"; // Default
        }

        var trimmed = isoCode.Trim().ToUpperInvariant();
        
        if (CodeToNameMap.TryGetValue(trimmed, out var name))
        {
            return name;
        }

        // If not found, return the code itself
        return trimmed;
    }

    /// <summary>
    /// Validates if a string is a valid ISO 3166-1 alpha-2 country code
    /// </summary>
    /// <param name="isoCode">The code to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    public static bool IsValidIsoCode(string? isoCode)
    {
        if (string.IsNullOrWhiteSpace(isoCode))
        {
            return false;
        }

        var trimmed = isoCode.Trim().ToUpperInvariant();
        return trimmed.Length == 2 && 
               trimmed.All(char.IsLetter) && 
               CodeToNameMap.ContainsKey(trimmed);
    }

    /// <summary>
    /// Gets all countries as a dictionary of ISO codes to names
    /// </summary>
    /// <returns>Dictionary with ISO codes as keys and country names as values</returns>
    public static Dictionary<string, string> GetAllCountries()
    {
        return new Dictionary<string, string>(CodeToNameMap);
    }
}

