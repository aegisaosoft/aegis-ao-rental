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

namespace CarRental.Api.DTOs;

public class TranslateRequest
{
    public string Text { get; set; } = string.Empty;
    public string TargetLanguage { get; set; } = string.Empty;
    public string? SourceLanguage { get; set; }
}

public class TranslateAllRequest
{
    public string Text { get; set; } = string.Empty;
    public string? SourceLanguage { get; set; }
}

public class TranslateResponse
{
    public string Translation { get; set; } = string.Empty;
}

public class TranslateAllResponse
{
    public Dictionary<string, string> Translations { get; set; } = new();
}

