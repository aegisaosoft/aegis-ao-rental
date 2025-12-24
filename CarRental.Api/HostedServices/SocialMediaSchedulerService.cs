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

using CarRental.Api.Data;
using CarRental.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Api.HostedServices;

/// <summary>
/// Background service that processes scheduled social media posts
/// </summary>
public class SocialMediaSchedulerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SocialMediaSchedulerService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1);
    private const int MaxRetries = 3;

    public SocialMediaSchedulerService(
        IServiceScopeFactory scopeFactory,
        ILogger<SocialMediaSchedulerService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Social Media Scheduler Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessScheduledPostsAsync(stoppingToken);
                await CollectAnalyticsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Social Media Scheduler Service");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("Social Media Scheduler Service stopped");
    }

    private async Task ProcessScheduledPostsAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CarRentalDbContext>();
        var campaignService = scope.ServiceProvider.GetRequiredService<Services.IInstagramCampaignService>();

        // Get posts that are due
        var now = DateTime.UtcNow;
        var duePosts = await context.Set<ScheduledPost>()
            .Where(p => p.Status == ScheduledPostStatus.Pending
                && p.ScheduledFor <= now
                && p.RetryCount < MaxRetries)
            .OrderBy(p => p.ScheduledFor)
            .Take(10) // Process in batches
            .ToListAsync(stoppingToken);

        foreach (var post in duePosts)
        {
            if (stoppingToken.IsCancellationRequested) break;

            try
            {
                _logger.LogInformation(
                    "Processing scheduled post {PostId} for company {CompanyId}",
                    post.Id, post.CompanyId);

                post.Status = ScheduledPostStatus.Processing;
                post.UpdatedAt = DateTime.UtcNow;
                await context.SaveChangesAsync(stoppingToken);

                Services.InstagramPostResult result;

                if (post.PostType == ScheduledPostType.Carousel && post.VehicleIds?.Any() == true)
                {
                    result = await campaignService.PublishCarouselAsync(
                        post.CompanyId,
                        post.VehicleIds,
                        new Services.CarouselOptions
                        {
                            Caption = post.Caption,
                            IncludePrice = post.IncludePrice
                        });
                }
                else if (post.VehicleId.HasValue)
                {
                    result = await campaignService.PublishVehicleAsync(
                        post.CompanyId,
                        post.VehicleId.Value,
                        new Services.PublishOptions
                        {
                            Caption = post.Caption,
                            IncludePrice = post.IncludePrice,
                            DailyRate = post.DailyRate,
                            Currency = post.Currency
                        });
                }
                else
                {
                    throw new InvalidOperationException("No vehicle specified for scheduled post");
                }

                // Update post with success
                post.Status = ScheduledPostStatus.Published;
                post.PostId = result.PostId;
                post.Permalink = result.Permalink;
                post.PublishedAt = DateTime.UtcNow;
                post.UpdatedAt = DateTime.UtcNow;

                _logger.LogInformation(
                    "Successfully published scheduled post {ScheduledPostId} as {PostId}",
                    post.Id, result.PostId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to publish scheduled post {PostId}",
                    post.Id);

                post.RetryCount++;
                post.ErrorMessage = ex.Message;
                post.UpdatedAt = DateTime.UtcNow;

                if (post.RetryCount >= MaxRetries)
                {
                    post.Status = ScheduledPostStatus.Failed;
                    _logger.LogWarning(
                        "Scheduled post {PostId} failed after {Retries} retries",
                        post.Id, post.RetryCount);
                }
                else
                {
                    post.Status = ScheduledPostStatus.Pending;
                    // Exponential backoff for retry
                    post.ScheduledFor = DateTime.UtcNow.AddMinutes(Math.Pow(2, post.RetryCount) * 5);
                }
            }

            await context.SaveChangesAsync(stoppingToken);

            // Rate limiting between posts
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private async Task CollectAnalyticsAsync(CancellationToken stoppingToken)
    {
        // Only collect analytics every hour
        if (DateTime.UtcNow.Minute != 0) return;

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CarRentalDbContext>();
        var campaignService = scope.ServiceProvider.GetRequiredService<Services.IInstagramCampaignService>();

        // Get recent posts that need analytics update
        var oneDayAgo = DateTime.UtcNow.AddDays(-1);
        var recentPosts = await context.Set<VehicleSocialPost>()
            .Where(p => p.Platform == SocialPlatform.Instagram
                && p.IsActive
                && p.CreatedAt >= oneDayAgo)
            .Take(20)
            .ToListAsync(stoppingToken);

        foreach (var post in recentPosts)
        {
            if (stoppingToken.IsCancellationRequested) break;

            try
            {
                var analytics = await campaignService.GetPostAnalyticsAsync(post.CompanyId, post.PostId);

                var analyticsRecord = new SocialPostAnalytics
                {
                    Id = Guid.NewGuid(),
                    CompanyId = post.CompanyId,
                    SocialPostId = post.Id,
                    PostId = post.PostId,
                    Platform = SocialPlatform.Instagram,
                    Impressions = analytics.Impressions,
                    Reach = analytics.Reach,
                    Engagement = analytics.Engagement,
                    Likes = analytics.Likes,
                    Comments = analytics.Comments,
                    Shares = analytics.Shares,
                    Saves = analytics.Saves,
                    RecordedAt = DateTime.UtcNow
                };

                context.Set<SocialPostAnalytics>().Add(analyticsRecord);
                await context.SaveChangesAsync(stoppingToken);

                // Rate limiting for API calls
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to collect analytics for post {PostId}",
                    post.PostId);
            }
        }
    }
}
