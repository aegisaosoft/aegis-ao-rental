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

/// <summary>
/// Standard API response format used across all endpoints
/// </summary>
public class ApiResponseDto<T>
{
    public T Result { get; set; } = default!;
    public int Reason { get; set; }
    public string? Message { get; set; }
    public string? StackTrace { get; set; }
}

/// <summary>
/// Non-generic version for use when result type varies
/// </summary>
public class ApiResponseDto
{
    public object Result { get; set; } = new();
    public int Reason { get; set; }
    public string? Message { get; set; }
    public string? StackTrace { get; set; }
}

