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

/// <summary>
/// Constants for valid booking status values
/// </summary>
public static class BookingStatus
{
    /// <summary>
    /// Booking has been created but not yet confirmed
    /// </summary>
    public const string Pending = "Pending";
    
    /// <summary>
    /// Booking has been confirmed by the rental company
    /// </summary>
    public const string Confirmed = "Confirmed";
    
    /// <summary>
    /// Vehicle has been picked up by the customer
    /// </summary>
    public const string PickedUp = "PickedUp";
    
    /// <summary>
    /// Vehicle has been returned by the customer
    /// </summary>
    public const string Returned = "Returned";
    
    /// <summary>
    /// Booking has been cancelled
    /// </summary>
    public const string Cancelled = "Cancelled";
    
    /// <summary>
    /// Customer did not show up for the booking
    /// </summary>
    public const string NoShow = "NoShow";
    
    /// <summary>
    /// Booking is currently active (vehicle picked up and in use)
    /// </summary>
    public const string Active = "Active";
    
    /// <summary>
    /// Booking has been completed (vehicle returned and booking closed)
    /// </summary>
    public const string Completed = "Completed";
    
    /// <summary>
    /// Get all valid status values
    /// </summary>
    public static readonly string[] AllStatuses = new[]
    {
        Pending,
        Confirmed,
        PickedUp,
        Returned,
        Cancelled,
        NoShow,
        Active,
        Completed
    };
    
    /// <summary>
    /// Check if a status value is valid
    /// </summary>
    public static bool IsValid(string status)
    {
        return AllStatuses.Contains(status);
    }
}

