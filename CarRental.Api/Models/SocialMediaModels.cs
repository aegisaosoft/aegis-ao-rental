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

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRental.Api.Models;

/// <summary>
/// Scheduled social media post
/// </summary>
[Table("scheduled_posts")]
public class ScheduledPost
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("company_id")]
    public Guid CompanyId { get; set; }

    [Column("vehicle_id")]
    public Guid? VehicleId { get; set; }

    /// <summary>
    /// For carousel posts - multiple vehicle IDs
    /// </summary>
    [Column("vehicle_ids", TypeName = "uuid[]")]
    public List<Guid>? VehicleIds { get; set; }

    [Column("post_type")]
    public ScheduledPostType PostType { get; set; } = ScheduledPostType.Single;

    [Column("platform")]
    public SocialPlatform Platform { get; set; } = SocialPlatform.Instagram;

    [Column("caption")]
    public string? Caption { get; set; }

    [Column("scheduled_for")]
    public DateTime ScheduledFor { get; set; }

    [Column("include_price")]
    public bool IncludePrice { get; set; }

    [Column("daily_rate")]
    public decimal? DailyRate { get; set; }

    [Column("currency")]
    [MaxLength(3)]
    public string? Currency { get; set; }

    [Column("custom_hashtags", TypeName = "text[]")]
    public List<string>? CustomHashtags { get; set; }

    [Column("status")]
    public ScheduledPostStatus Status { get; set; } = ScheduledPostStatus.Pending;

    [Column("error_message")]
    public string? ErrorMessage { get; set; }

    [Column("post_id")]
    public string? PostId { get; set; }

    [Column("permalink")]
    public string? Permalink { get; set; }

    [Column("published_at")]
    public DateTime? PublishedAt { get; set; }

    [Column("retry_count")]
    public int RetryCount { get; set; } = 0;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey("CompanyId")]
    public virtual Company? Company { get; set; }

    [ForeignKey("VehicleId")]
    public virtual Vehicle? Vehicle { get; set; }
}

public enum ScheduledPostType
{
    Single = 0,
    Carousel = 1,
    Reel = 2,
    Story = 3
}

public enum ScheduledPostStatus
{
    Pending = 0,
    Processing = 1,
    Published = 2,
    Failed = 3,
    Cancelled = 4
}

/// <summary>
/// Company-level auto-posting settings
/// </summary>
[Table("company_auto_post_settings")]
public class CompanyAutoPostSettings
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("company_id")]
    public Guid CompanyId { get; set; }

    [Column("is_enabled")]
    public bool IsEnabled { get; set; } = false;

    /// <summary>
    /// Auto-post when new vehicle is added
    /// </summary>
    [Column("post_on_vehicle_added")]
    public bool PostOnVehicleAdded { get; set; } = true;

    /// <summary>
    /// Auto-post when vehicle is updated
    /// </summary>
    [Column("post_on_vehicle_updated")]
    public bool PostOnVehicleUpdated { get; set; } = false;

    /// <summary>
    /// Auto-post when vehicle becomes available after rental
    /// </summary>
    [Column("post_on_vehicle_available")]
    public bool PostOnVehicleAvailable { get; set; } = false;

    /// <summary>
    /// Auto-post when price changes
    /// </summary>
    [Column("post_on_price_change")]
    public bool PostOnPriceChange { get; set; } = false;

    /// <summary>
    /// Include price in auto-posts
    /// </summary>
    [Column("include_price_in_posts")]
    public bool IncludePriceInPosts { get; set; } = true;

    /// <summary>
    /// Default hashtags for all posts
    /// </summary>
    [Column("default_hashtags", TypeName = "text[]")]
    public List<string>? DefaultHashtags { get; set; }

    /// <summary>
    /// Default call-to-action text
    /// </summary>
    [Column("default_call_to_action")]
    public string? DefaultCallToAction { get; set; }

    /// <summary>
    /// Post to Facebook as well
    /// </summary>
    [Column("cross_post_to_facebook")]
    public bool CrossPostToFacebook { get; set; } = false;

    /// <summary>
    /// Minimum hours between auto-posts (rate limiting)
    /// </summary>
    [Column("min_hours_between_posts")]
    public int MinHoursBetweenPosts { get; set; } = 4;

    /// <summary>
    /// Last auto-post timestamp
    /// </summary>
    [Column("last_auto_post_at")]
    public DateTime? LastAutoPostAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey("CompanyId")]
    public virtual Company? Company { get; set; }
}

/// <summary>
/// Campaign template for quick posting
/// </summary>
[Table("social_post_templates")]
public class SocialPostTemplate
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("company_id")]
    public Guid CompanyId { get; set; }

    [Column("name")]
    [MaxLength(100)]
    public string Name { get; set; } = "";

    [Column("description")]
    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Caption template with placeholders like {make}, {model}, {year}, {price}
    /// </summary>
    [Column("caption_template")]
    public string CaptionTemplate { get; set; } = "";

    [Column("hashtags", TypeName = "text[]")]
    public List<string>? Hashtags { get; set; }

    [Column("call_to_action")]
    public string? CallToAction { get; set; }

    [Column("include_price")]
    public bool IncludePrice { get; set; } = true;

    /// <summary>
    /// Vehicle categories this template applies to
    /// </summary>
    [Column("applicable_categories", TypeName = "text[]")]
    public List<string>? ApplicableCategories { get; set; }

    [Column("is_default")]
    public bool IsDefault { get; set; } = false;

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey("CompanyId")]
    public virtual Company? Company { get; set; }
}

/// <summary>
/// Tracks social post performance metrics
/// </summary>
[Table("social_post_analytics")]
public class SocialPostAnalytics
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("company_id")]
    public Guid CompanyId { get; set; }

    [Column("social_post_id")]
    public Guid SocialPostId { get; set; }

    [Column("post_id")]
    public string PostId { get; set; } = "";

    [Column("platform")]
    public SocialPlatform Platform { get; set; }

    [Column("impressions")]
    public int Impressions { get; set; }

    [Column("reach")]
    public int Reach { get; set; }

    [Column("engagement")]
    public int Engagement { get; set; }

    [Column("likes")]
    public int Likes { get; set; }

    [Column("comments")]
    public int Comments { get; set; }

    [Column("shares")]
    public int Shares { get; set; }

    [Column("saves")]
    public int Saves { get; set; }

    [Column("clicks")]
    public int Clicks { get; set; }

    [Column("profile_visits")]
    public int ProfileVisits { get; set; }

    [Column("recorded_at")]
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey("SocialPostId")]
    public virtual VehicleSocialPost? SocialPost { get; set; }
}
