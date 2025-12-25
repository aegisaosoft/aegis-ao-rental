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

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CarRental.Api.Data;
using CarRental.Api.Models;
using CarRental.Api.Services;
using ServiceBulkPublishRequest = CarRental.Api.Services.BulkPublishRequest;

namespace CarRental.Api.Controllers;

[ApiController]
[Route("api/instagram-campaign")]
[Authorize]
public class InstagramCampaignController : ControllerBase
{
    private readonly IInstagramCampaignService _campaignService;
    private readonly CarRentalDbContext _context;
    private readonly ILogger<InstagramCampaignController> _logger;

    public InstagramCampaignController(
        IInstagramCampaignService campaignService,
        CarRentalDbContext context,
        ILogger<InstagramCampaignController> logger)
    {
        _campaignService = campaignService;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Generate caption preview for a vehicle
    /// </summary>
    [HttpPost("preview-caption/{companyId}/{vehicleId}")]
    public async Task<IActionResult> PreviewCaption(
        Guid companyId,
        Guid vehicleId,
        [FromBody] CaptionPreviewRequest request)
    {
        var vehicle = await _context.Set<Vehicle>()
            .Include(v => v.VehicleModel)
                .ThenInclude(vm => vm!.Model)
                    .ThenInclude(m => m!.Category)
            .Include(v => v.LocationDetails)
            .FirstOrDefaultAsync(v => v.Id == vehicleId && v.CompanyId == companyId);

        if (vehicle == null)
        {
            return NotFound(new { error = "Vehicle not found" });
        }

        var caption = await _campaignService.GenerateCaptionAsync(vehicle, new CaptionOptions
        {
            IncludePrice = request.IncludePrice,
            DailyRate = request.DailyRate,
            Currency = request.Currency,
            IncludeHashtags = request.IncludeHashtags,
            CustomHashtags = request.CustomHashtags,
            MaxHashtags = request.MaxHashtags ?? 20,
            Location = vehicle.LocationDetails?.City ?? vehicle.Location,
            CallToAction = request.CallToAction
        });

        return Ok(caption);
    }

    /// <summary>
    /// Get recommended hashtags for a vehicle
    /// </summary>
    [HttpGet("hashtags/{companyId}/{vehicleId}")]
    public async Task<IActionResult> GetHashtags(Guid companyId, Guid vehicleId, [FromQuery] string? location = null)
    {
        var vehicle = await _context.Set<Vehicle>()
            .Include(v => v.VehicleModel)
                .ThenInclude(vm => vm!.Model)
                    .ThenInclude(m => m!.Category)
            .Include(v => v.LocationDetails)
            .FirstOrDefaultAsync(v => v.Id == vehicleId && v.CompanyId == companyId);

        if (vehicle == null)
        {
            return NotFound(new { error = "Vehicle not found" });
        }

        var hashtags = _campaignService.GetRecommendedHashtags(
            vehicle,
            location ?? vehicle.LocationDetails?.City ?? vehicle.Location
        );

        return Ok(new { hashtags });
    }

    /// <summary>
    /// Publish a vehicle to Instagram
    /// </summary>
    [HttpPost("publish/{companyId}")]
    public async Task<IActionResult> PublishVehicle(Guid companyId, [FromBody] PublishRequest request)
    {
        try
        {
            var result = await _campaignService.PublishVehicleAsync(companyId, request.VehicleId, new PublishOptions
            {
                Caption = request.Caption,
                ImageUrl = request.ImageUrl,
                IncludePrice = request.IncludePrice,
                DailyRate = request.DailyRate,
                Currency = request.Currency
            });

            return Ok(result);
        }
        catch (InstagramPublishException ex)
        {
            _logger.LogError(ex, "Instagram publish failed for vehicle {VehicleId}", request.VehicleId);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Publish multiple vehicles as carousel
    /// </summary>
    [HttpPost("publish-carousel/{companyId}")]
    public async Task<IActionResult> PublishCarousel(Guid companyId, [FromBody] CarouselPublishRequest request)
    {
        try
        {
            var result = await _campaignService.PublishCarouselAsync(companyId, request.VehicleIds, new CarouselOptions
            {
                Caption = request.Caption,
                IncludePrice = request.IncludePrice
            });

            return Ok(result);
        }
        catch (InstagramPublishException ex)
        {
            _logger.LogError(ex, "Carousel publish failed");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Bulk publish multiple vehicles
    /// </summary>
    [HttpPost("bulk-publish/{companyId}")]
    public async Task<IActionResult> BulkPublish(Guid companyId, [FromBody] ServiceBulkPublishRequest request)
    {
        var result = await _campaignService.BulkPublishAsync(companyId, request);
        return Ok(result);
    }

    /// <summary>
    /// Schedule a post for later
    /// </summary>
    [HttpPost("schedule/{companyId}")]
    public async Task<IActionResult> SchedulePost(Guid companyId, [FromBody] SchedulePostRequest request)
    {
        try
        {
            var post = await _campaignService.SchedulePostAsync(companyId, request);
            return Ok(new
            {
                success = true,
                scheduledPost = new
                {
                    post.Id,
                    post.VehicleId,
                    post.VehicleIds,
                    post.ScheduledFor,
                    post.Status
                }
            });
        }
        catch (InstagramPublishException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get scheduled posts for a company
    /// </summary>
    [HttpGet("scheduled/{companyId}")]
    public async Task<IActionResult> GetScheduledPosts(Guid companyId)
    {
        var posts = await _campaignService.GetScheduledPostsAsync(companyId);
        return Ok(posts.Select(p => new
        {
            p.Id,
            p.VehicleId,
            p.VehicleIds,
            p.PostType,
            p.Caption,
            p.ScheduledFor,
            p.Status,
            p.IncludePrice,
            p.CreatedAt
        }));
    }

    /// <summary>
    /// Cancel a scheduled post
    /// </summary>
    [HttpDelete("scheduled/{companyId}/{postId}")]
    public async Task<IActionResult> CancelScheduledPost(Guid companyId, Guid postId)
    {
        await _campaignService.CancelScheduledPostAsync(companyId, postId);
        return Ok(new { success = true });
    }

    /// <summary>
    /// Get post analytics
    /// </summary>
    [HttpGet("analytics/{companyId}/{postId}")]
    public async Task<IActionResult> GetAnalytics(Guid companyId, string postId)
    {
        try
        {
            var analytics = await _campaignService.GetPostAnalyticsAsync(companyId, postId);
            return Ok(analytics);
        }
        catch (InstagramPublishException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get analytics history for a post
    /// </summary>
    [HttpGet("analytics-history/{companyId}/{socialPostId}")]
    public async Task<IActionResult> GetAnalyticsHistory(Guid companyId, Guid socialPostId)
    {
        var history = await _context.Set<SocialPostAnalytics>()
            .Where(a => a.SocialPostId == socialPostId)
            .OrderByDescending(a => a.RecordedAt)
            .Take(30)
            .ToListAsync();

        return Ok(history);
    }

    #region Auto-Post Settings

    /// <summary>
    /// Get auto-post settings
    /// </summary>
    [HttpGet("auto-post-settings/{companyId}")]
    public async Task<IActionResult> GetAutoPostSettings(Guid companyId)
    {
        var settings = await _context.Set<CompanyAutoPostSettings>()
            .FirstOrDefaultAsync(s => s.CompanyId == companyId);

        if (settings == null)
        {
            return Ok(new CompanyAutoPostSettings
            {
                CompanyId = companyId,
                IsEnabled = false
            });
        }

        return Ok(settings);
    }

    /// <summary>
    /// Update auto-post settings
    /// </summary>
    [HttpPut("auto-post-settings/{companyId}")]
    public async Task<IActionResult> UpdateAutoPostSettings(
        Guid companyId,
        [FromBody] AutoPostSettingsRequest request)
    {
        var settings = await _context.Set<CompanyAutoPostSettings>()
            .FirstOrDefaultAsync(s => s.CompanyId == companyId);

        if (settings == null)
        {
            settings = new CompanyAutoPostSettings
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId
            };
            _context.Set<CompanyAutoPostSettings>().Add(settings);
        }

        settings.IsEnabled = request.IsEnabled;
        settings.PostOnVehicleAdded = request.PostOnVehicleAdded;
        settings.PostOnVehicleUpdated = request.PostOnVehicleUpdated;
        settings.PostOnVehicleAvailable = request.PostOnVehicleAvailable;
        settings.PostOnPriceChange = request.PostOnPriceChange;
        settings.IncludePriceInPosts = request.IncludePriceInPosts;
        settings.DefaultHashtags = request.DefaultHashtags;
        settings.DefaultCallToAction = request.DefaultCallToAction;
        settings.CrossPostToFacebook = request.CrossPostToFacebook;
        settings.MinHoursBetweenPosts = request.MinHoursBetweenPosts;
        settings.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(settings);
    }

    #endregion

    #region Templates

    /// <summary>
    /// Get post templates
    /// </summary>
    [HttpGet("templates/{companyId}")]
    public async Task<IActionResult> GetTemplates(Guid companyId)
    {
        var templates = await _context.Set<SocialPostTemplate>()
            .Where(t => t.CompanyId == companyId && t.IsActive)
            .OrderByDescending(t => t.IsDefault)
            .ThenBy(t => t.Name)
            .ToListAsync();

        return Ok(templates);
    }

    /// <summary>
    /// Create post template
    /// </summary>
    [HttpPost("templates/{companyId}")]
    public async Task<IActionResult> CreateTemplate(
        Guid companyId,
        [FromBody] TemplateRequest request)
    {
        var template = new SocialPostTemplate
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Name = request.Name,
            Description = request.Description,
            CaptionTemplate = request.CaptionTemplate,
            Hashtags = request.Hashtags,
            CallToAction = request.CallToAction,
            IncludePrice = request.IncludePrice,
            ApplicableCategories = request.ApplicableCategories,
            IsDefault = request.IsDefault
        };

        // If this is set as default, unset other defaults
        if (request.IsDefault)
        {
            var existingDefaults = await _context.Set<SocialPostTemplate>()
                .Where(t => t.CompanyId == companyId && t.IsDefault)
                .ToListAsync();

            foreach (var t in existingDefaults)
            {
                t.IsDefault = false;
            }
        }

        _context.Set<SocialPostTemplate>().Add(template);
        await _context.SaveChangesAsync();

        return Ok(template);
    }

    /// <summary>
    /// Update post template
    /// </summary>
    [HttpPut("templates/{companyId}/{templateId}")]
    public async Task<IActionResult> UpdateTemplate(
        Guid companyId,
        Guid templateId,
        [FromBody] TemplateRequest request)
    {
        var template = await _context.Set<SocialPostTemplate>()
            .FirstOrDefaultAsync(t => t.Id == templateId && t.CompanyId == companyId);

        if (template == null)
        {
            return NotFound(new { error = "Template not found" });
        }

        template.Name = request.Name;
        template.Description = request.Description;
        template.CaptionTemplate = request.CaptionTemplate;
        template.Hashtags = request.Hashtags;
        template.CallToAction = request.CallToAction;
        template.IncludePrice = request.IncludePrice;
        template.ApplicableCategories = request.ApplicableCategories;
        template.UpdatedAt = DateTime.UtcNow;

        if (request.IsDefault && !template.IsDefault)
        {
            var existingDefaults = await _context.Set<SocialPostTemplate>()
                .Where(t => t.CompanyId == companyId && t.IsDefault && t.Id != templateId)
                .ToListAsync();

            foreach (var t in existingDefaults)
            {
                t.IsDefault = false;
            }
        }
        template.IsDefault = request.IsDefault;

        await _context.SaveChangesAsync();

        return Ok(template);
    }

    /// <summary>
    /// Delete post template
    /// </summary>
    [HttpDelete("templates/{companyId}/{templateId}")]
    public async Task<IActionResult> DeleteTemplate(Guid companyId, Guid templateId)
    {
        var template = await _context.Set<SocialPostTemplate>()
            .FirstOrDefaultAsync(t => t.Id == templateId && t.CompanyId == companyId);

        if (template != null)
        {
            template.IsActive = false;
            template.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        return Ok(new { success = true });
    }

    /// <summary>
    /// Apply template to generate caption
    /// </summary>
    [HttpPost("templates/{companyId}/{templateId}/apply/{vehicleId}")]
    public async Task<IActionResult> ApplyTemplate(Guid companyId, Guid templateId, Guid vehicleId)
    {
        var template = await _context.Set<SocialPostTemplate>()
            .FirstOrDefaultAsync(t => t.Id == templateId && t.CompanyId == companyId);

        if (template == null)
        {
            return NotFound(new { error = "Template not found" });
        }

        var vehicle = await _context.Set<Vehicle>()
            .Include(v => v.VehicleModel)
                .ThenInclude(vm => vm!.Model)
                    .ThenInclude(m => m!.Category)
            .Include(v => v.LocationDetails)
            .FirstOrDefaultAsync(v => v.Id == vehicleId && v.CompanyId == companyId);

        if (vehicle == null)
        {
            return NotFound(new { error = "Vehicle not found" });
        }

        // Apply template placeholders
        var modelInfo = vehicle.VehicleModel?.Model;
        var caption = template.CaptionTemplate
            .Replace("{make}", modelInfo?.Make ?? "")
            .Replace("{model}", modelInfo?.ModelName ?? "")
            .Replace("{year}", modelInfo?.Year.ToString() ?? "")
            .Replace("{color}", vehicle.Color ?? "")
            .Replace("{transmission}", vehicle.Transmission ?? "")
            .Replace("{seats}", vehicle.Seats?.ToString() ?? "")
            .Replace("{location}", vehicle.LocationDetails?.City ?? vehicle.Location ?? "");

        // Add call to action
        if (!string.IsNullOrEmpty(template.CallToAction))
        {
            caption += "\n\n" + template.CallToAction;
        }

        // Add hashtags
        if (template.Hashtags?.Any() == true)
        {
            caption += "\n\n" + string.Join(" ", template.Hashtags);
        }

        return Ok(new
        {
            caption = caption.Trim(),
            templateName = template.Name,
            includePrice = template.IncludePrice
        });
    }

    #endregion

    #region Dashboard / Stats

    /// <summary>
    /// Get social media dashboard stats
    /// </summary>
    [HttpGet("dashboard/{companyId}")]
    public async Task<IActionResult> GetDashboard(Guid companyId)
    {
        var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);

        // Total posts
        var totalPosts = await _context.Set<VehicleSocialPost>()
            .CountAsync(p => p.CompanyId == companyId && p.IsActive);

        var postsThisMonth = await _context.Set<VehicleSocialPost>()
            .CountAsync(p => p.CompanyId == companyId && p.IsActive && p.CreatedAt >= thirtyDaysAgo);

        // Scheduled posts
        var scheduledCount = await _context.Set<ScheduledPost>()
            .CountAsync(p => p.CompanyId == companyId && p.Status == ScheduledPostStatus.Pending);

        // Recent analytics
        var recentAnalytics = await _context.Set<SocialPostAnalytics>()
            .Where(a => a.CompanyId == companyId && a.RecordedAt >= thirtyDaysAgo)
            .GroupBy(a => 1)
            .Select(g => new
            {
                TotalImpressions = g.Sum(a => a.Impressions),
                TotalReach = g.Sum(a => a.Reach),
                TotalEngagement = g.Sum(a => a.Engagement),
                TotalLikes = g.Sum(a => a.Likes),
                TotalComments = g.Sum(a => a.Comments)
            })
            .FirstOrDefaultAsync();

        // Top performing posts
        var topPosts = await _context.Set<VehicleSocialPost>()
            .Where(p => p.CompanyId == companyId && p.IsActive && p.Platform == SocialPlatform.Instagram)
            .OrderByDescending(p => p.CreatedAt)
            .Take(5)
            .Select(p => new
            {
                p.Id,
                p.VehicleId,
                p.PostId,
                p.Permalink,
                p.Caption,
                p.ImageUrl,
                p.CreatedAt
            })
            .ToListAsync();

        return Ok(new
        {
            totalPosts,
            postsThisMonth,
            scheduledCount,
            analytics = recentAnalytics ?? new
            {
                TotalImpressions = 0,
                TotalReach = 0,
                TotalEngagement = 0,
                TotalLikes = 0,
                TotalComments = 0
            },
            topPosts
        });
    }

    #endregion
}

#region Request DTOs

public class CaptionPreviewRequest
{
    public bool IncludePrice { get; set; }
    public decimal? DailyRate { get; set; }
    public string? Currency { get; set; }
    public bool IncludeHashtags { get; set; } = true;
    public List<string>? CustomHashtags { get; set; }
    public int? MaxHashtags { get; set; }
    public string? CallToAction { get; set; }
}

public class PublishRequest
{
    public Guid VehicleId { get; set; }
    public string? Caption { get; set; }
    public string? ImageUrl { get; set; }
    public bool IncludePrice { get; set; }
    public decimal? DailyRate { get; set; }
    public string? Currency { get; set; }
}

public class CarouselPublishRequest
{
    public List<Guid> VehicleIds { get; set; } = new();
    public string? Caption { get; set; }
    public bool IncludePrice { get; set; }
}

public class AutoPostSettingsRequest
{
    public bool IsEnabled { get; set; }
    public bool PostOnVehicleAdded { get; set; }
    public bool PostOnVehicleUpdated { get; set; }
    public bool PostOnVehicleAvailable { get; set; }
    public bool PostOnPriceChange { get; set; }
    public bool IncludePriceInPosts { get; set; }
    public List<string>? DefaultHashtags { get; set; }
    public string? DefaultCallToAction { get; set; }
    public bool CrossPostToFacebook { get; set; }
    public int MinHoursBetweenPosts { get; set; } = 4;
}

public class TemplateRequest
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string CaptionTemplate { get; set; } = "";
    public List<string>? Hashtags { get; set; }
    public string? CallToAction { get; set; }
    public bool IncludePrice { get; set; }
    public List<string>? ApplicableCategories { get; set; }
    public bool IsDefault { get; set; }
}

#endregion
