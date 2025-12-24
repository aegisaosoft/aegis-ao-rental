/*
 *
 * Copyright (c) 2025 Alexander Orlov.
 * 34 Middletown Ave, Atlantic Highlands, NJ 07716
 *
 * THIS SOFTWARE IS THE CONFIDENTIAL AND PROPRIETARY INFORMATION OF
 * Alexander Orlov ("CONFIDENTIAL INFORMATION").
 *
 * Author: Alexander Orlov
 * Aegis AO Soft
 *
 */

namespace CarRental.Api.Models;

/// <summary>
/// Tracks vehicles posted to social media platforms (Facebook/Instagram)
/// </summary>
public class VehicleSocialPost
{
    public Guid Id { get; set; }

    /// <summary>
    /// Reference to the rental company
    /// </summary>
    public Guid CompanyId { get; set; }

    /// <summary>
    /// Reference to the vehicle
    /// </summary>
    public Guid VehicleId { get; set; }

    /// <summary>
    /// Social platform (Facebook or Instagram)
    /// </summary>
    public SocialPlatform Platform { get; set; }

    /// <summary>
    /// ID of the post on the social platform
    /// </summary>
    public string PostId { get; set; } = "";

    /// <summary>
    /// Permanent link to the post
    /// </summary>
    public string? Permalink { get; set; }

    /// <summary>
    /// Caption used when posting
    /// </summary>
    public string? Caption { get; set; }

    /// <summary>
    /// Image URL used when posting
    /// </summary>
    public string? ImageUrl { get; set; }

    /// <summary>
    /// Daily rate at time of posting
    /// </summary>
    public decimal? DailyRate { get; set; }

    /// <summary>
    /// Whether this post record is active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// When the post was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the post was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public Company? Company { get; set; }
    public Vehicle? Vehicle { get; set; }
}

/// <summary>
/// Social media platforms for posting
/// </summary>
public enum SocialPlatform
{
    Facebook = 0,
    Instagram = 1
}
