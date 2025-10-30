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

public enum VehicleStatus
{
    Available,
    Rented,
    Maintenance,
    OutOfService,
    Cleaning
}

public static class VehicleStatusConstants
{
    public const string Available = "Available";
    public const string Rented = "Rented";
    public const string Maintenance = "Maintenance";
    public const string OutOfService = "OutOfService";
    public const string Cleaning = "Cleaning";
}
