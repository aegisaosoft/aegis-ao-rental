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

using System.Text.Json;
using System.Text.Json.Serialization;

namespace CarRental.Api.Models;

/// <summary>
/// Stores Meta (Facebook/Instagram) OAuth credentials for each company
/// </summary>
public class CompanyMetaCredentials
{
    public int Id { get; set; }

    /// <summary>
    /// Reference to the rental company
    /// </summary>
    public Guid CompanyId { get; set; }

    /// <summary>
    /// Long-lived user access token (60 days expiration)
    /// Used for refreshing page tokens and API calls
    /// </summary>
    public string UserAccessToken { get; set; } = "";

    /// <summary>
    /// When the user access token expires
    /// </summary>
    public DateTime TokenExpiresAt { get; set; }

    /// <summary>
    /// Facebook Page ID selected for this company
    /// </summary>
    public string? PageId { get; set; }

    /// <summary>
    /// Facebook Page name
    /// </summary>
    public string? PageName { get; set; }

    /// <summary>
    /// Page access token (never expires as long as user token is valid)
    /// Used for posting, catalog management, etc.
    /// </summary>
    public string? PageAccessToken { get; set; }

    /// <summary>
    /// Facebook Product Catalog ID
    /// </summary>
    public string? CatalogId { get; set; }

    /// <summary>
    /// Facebook Pixel ID for tracking
    /// </summary>
    public string? PixelId { get; set; }

    /// <summary>
    /// Instagram Business Account ID (if connected)
    /// </summary>
    public string? InstagramAccountId { get; set; }

    /// <summary>
    /// Instagram username (if connected)
    /// </summary>
    public string? InstagramUsername { get; set; }

    /// <summary>
    /// JSON serialized list of available pages (for page selection UI)
    /// Stored as JSONB in database
    /// </summary>
    public JsonDocument? AvailablePages { get; set; }

    /// <summary>
    /// Current status of the integration
    /// </summary>
    public MetaCredentialStatus Status { get; set; } = MetaCredentialStatus.PendingPageSelection;

    /// <summary>
    /// When the OAuth connection was established
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Last time the record was updated (auto-updated by trigger)
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Last time the token was refreshed
    /// </summary>
    public DateTime? LastTokenRefresh { get; set; }

    /// <summary>
    /// Auto-publish new vehicles to Facebook
    /// </summary>
    public bool AutoPublishFacebook { get; set; } = false;

    /// <summary>
    /// Auto-publish new vehicles to Instagram
    /// </summary>
    public bool AutoPublishInstagram { get; set; } = false;

    /// <summary>
    /// Include price in auto-published posts
    /// </summary>
    public bool AutoPublishIncludePrice { get; set; } = true;

    /// <summary>
    /// Custom hashtags for auto-published posts (JSON array)
    /// </summary>
    public string? AutoPublishHashtags { get; set; }

    // Navigation property
    public Company? Company { get; set; }
}

/// <summary>
/// Status values for Meta OAuth credentials
/// </summary>
public enum MetaCredentialStatus
{
    /// <summary>
    /// OAuth completed but user needs to select a Page
    /// </summary>
    PendingPageSelection = 0,

    /// <summary>
    /// Fully configured and active
    /// </summary>
    Active = 1,

    /// <summary>
    /// Token expired, needs re-authentication
    /// </summary>
    TokenExpired = 2,

    /// <summary>
    /// User revoked access via Facebook settings
    /// </summary>
    Revoked = 3,

    /// <summary>
    /// Error state
    /// </summary>
    Error = 4
}

/// <summary>
/// OAuth configuration settings for Meta integration
/// </summary>
public class MetaOAuthSettings
{
    /// <summary>
    /// Facebook App ID
    /// </summary>
    public string AppId { get; set; } = "";

    /// <summary>
    /// Facebook App Secret
    /// </summary>
    public string AppSecret { get; set; } = "";

    /// <summary>
    /// OAuth callback URL (must match Facebook App settings)
    /// Example: https://yourapp.com/api/meta/oauth/callback
    /// </summary>
    public string RedirectUri { get; set; } = "";

    /// <summary>
    /// Frontend URL to redirect after OAuth completion
    /// Example: https://yourapp.com/admin/social
    /// </summary>
    public string FrontendRedirectUrl { get; set; } = "";
}
