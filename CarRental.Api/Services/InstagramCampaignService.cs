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

using System.Text;
using System.Text.Json;
using CarRental.Api.Data;
using CarRental.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Api.Services;

/// <summary>
/// Main service for Instagram campaign automation
/// </summary>
public interface IInstagramCampaignService
{
    /// <summary>
    /// Generate smart caption for a vehicle
    /// </summary>
    Task<GeneratedCaption> GenerateCaptionAsync(Vehicle vehicle, CaptionOptions options);
    
    /// <summary>
    /// Get recommended hashtags based on vehicle attributes
    /// </summary>
    List<string> GetRecommendedHashtags(Vehicle vehicle, string? location = null);
    
    /// <summary>
    /// Publish a single vehicle to Instagram
    /// </summary>
    Task<InstagramPostResult> PublishVehicleAsync(Guid companyId, Guid vehicleId, PublishOptions options);
    
    /// <summary>
    /// Publish multiple vehicles as carousel post
    /// </summary>
    Task<InstagramPostResult> PublishCarouselAsync(Guid companyId, List<Guid> vehicleIds, CarouselOptions options);
    
    /// <summary>
    /// Schedule a post for later
    /// </summary>
    Task<ScheduledPost> SchedulePostAsync(Guid companyId, SchedulePostRequest request);
    
    /// <summary>
    /// Get scheduled posts for a company
    /// </summary>
    Task<List<ScheduledPost>> GetScheduledPostsAsync(Guid companyId);
    
    /// <summary>
    /// Cancel a scheduled post
    /// </summary>
    Task CancelScheduledPostAsync(Guid companyId, Guid scheduledPostId);
    
    /// <summary>
    /// Bulk publish vehicles based on criteria
    /// </summary>
    Task<BulkPublishResult> BulkPublishAsync(Guid companyId, BulkPublishRequest request);
    
    /// <summary>
    /// Get post analytics
    /// </summary>
    Task<PostAnalytics> GetPostAnalyticsAsync(Guid companyId, string postId);
    
    /// <summary>
    /// Auto-post new vehicles (called when vehicle is added/updated)
    /// </summary>
    Task<bool> TryAutoPostVehicleAsync(Guid companyId, Guid vehicleId, AutoPostTrigger trigger);
}

public class InstagramCampaignService : IInstagramCampaignService
{
    private readonly CarRentalDbContext _context;
    private readonly ICompanyMetaCredentialsRepository _credentialsRepo;
    private readonly IVehicleSocialPostRepository _postRepo;
    private readonly IAzureBlobStorageService _blobService;
    private readonly ILogger<InstagramCampaignService> _logger;
    private readonly HttpClient _httpClient;
    private readonly string _apiBaseUrl;
    private const string GraphApiBaseUrl = "https://graph.facebook.com/v21.0";

    public InstagramCampaignService(
        CarRentalDbContext context,
        ICompanyMetaCredentialsRepository credentialsRepo,
        IVehicleSocialPostRepository postRepo,
        IAzureBlobStorageService blobService,
        IConfiguration configuration,
        ILogger<InstagramCampaignService> logger)
    {
        _context = context;
        _credentialsRepo = credentialsRepo;
        _postRepo = postRepo;
        _blobService = blobService;
        _logger = logger;
        _httpClient = new HttpClient();
        _apiBaseUrl = configuration["ApiBaseUrl"] ?? "https://aegis-ao-rental.azurewebsites.net";
    }

    public Task<GeneratedCaption> GenerateCaptionAsync(Vehicle vehicle, CaptionOptions options)
    {
        var vehicleModel = vehicle.VehicleModel;
        var modelInfo = vehicleModel?.Model;
        var sb = new StringBuilder();

        // Emoji based on vehicle type
        var emoji = GetVehicleEmoji(modelInfo?.Category?.CategoryName ?? "");

        // Main line with vehicle info
        if (modelInfo != null)
        {
            sb.AppendLine($"{emoji} {modelInfo.Year} {modelInfo.Make} {modelInfo.ModelName}");
        }
        else
        {
            sb.AppendLine($"{emoji} Premium Rental Vehicle");
        }

        // Features line
        if (vehicle.Features?.Any() == true)
        {
            var topFeatures = vehicle.Features.Take(3);
            sb.AppendLine($"✨ {string.Join(" • ", topFeatures)}");
        }

        // Price line
        if (options.IncludePrice && options.DailyRate.HasValue)
        {
            var currency = options.Currency ?? "USD";
            var currencySymbol = GetCurrencySymbol(currency);
            sb.AppendLine($"💰 From {currencySymbol}{options.DailyRate:N0}/day");
        }

        // Call to action
        sb.AppendLine();
        sb.AppendLine(options.CallToAction ?? "📲 Book now - link in bio!");

        // Hashtags
        if (options.IncludeHashtags)
        {
            sb.AppendLine();
            var hashtags = GetRecommendedHashtags(vehicle, options.Location);
            if (options.CustomHashtags?.Any() == true)
            {
                hashtags.AddRange(options.CustomHashtags);
            }
            hashtags = hashtags.Distinct().Take(options.MaxHashtags).ToList();
            sb.AppendLine(string.Join(" ", hashtags));
        }

        return Task.FromResult(new GeneratedCaption
        {
            Text = sb.ToString().Trim(),
            Hashtags = GetRecommendedHashtags(vehicle, options.Location),
            CharacterCount = sb.Length,
            IsWithinLimit = sb.Length <= 2200 // Instagram caption limit
        });
    }

    public List<string> GetRecommendedHashtags(Vehicle vehicle, string? location = null)
    {
        var hashtags = new List<string>
        {
            "#carrental",
            "#rentacar",
            "#luxuryrentals"
        };

        var vehicleModel = vehicle.VehicleModel;
        var modelInfo = vehicleModel?.Model;
        if (modelInfo != null)
        {
            // Brand hashtags
            if (!string.IsNullOrEmpty(modelInfo.Make))
            {
                hashtags.Add($"#{modelInfo.Make.ToLower().Replace(" ", "")}");
                hashtags.Add($"#{modelInfo.Make.ToLower().Replace(" ", "")}rental");
            }

            // Model hashtags
            if (!string.IsNullOrEmpty(modelInfo.ModelName))
            {
                hashtags.Add($"#{modelInfo.ModelName.ToLower().Replace(" ", "").Replace("-", "")}");
            }

            // Category hashtags
            var categoryName = modelInfo.Category?.CategoryName;
            if (!string.IsNullOrEmpty(categoryName))
            {
                var categoryHashtags = GetCategoryHashtags(categoryName);
                hashtags.AddRange(categoryHashtags);
            }
        }

        // Location hashtags
        if (!string.IsNullOrEmpty(location))
        {
            var locationClean = location.ToLower()
                .Replace(" ", "")
                .Replace(",", "")
                .Replace(".", "");
            hashtags.Add($"#{locationClean}");
            hashtags.Add($"#{locationClean}rental");
        }

        // Transmission hashtags
        if (!string.IsNullOrEmpty(vehicle.Transmission))
        {
            if (vehicle.Transmission.ToLower().Contains("automatic"))
                hashtags.Add("#automatictransmission");
        }

        // Add some popular rental hashtags
        hashtags.AddRange(new[]
        {
            "#travelcar",
            "#roadtrip",
            "#driveinstyle",
            "#rentals",
            "#carlovers"
        });

        return hashtags.Distinct().ToList();
    }

    public async Task<InstagramPostResult> PublishVehicleAsync(Guid companyId, Guid vehicleId, PublishOptions options)
    {
        var credentials = await _credentialsRepo.GetByCompanyIdAsync(companyId);
        if (credentials == null || string.IsNullOrEmpty(credentials.InstagramAccountId))
        {
            throw new InstagramPublishException("Instagram account not connected");
        }

        var vehicle = await _context.Set<Vehicle>()
            .Include(v => v.VehicleModel)
                .ThenInclude(vm => vm!.Model)
                    .ThenInclude(m => m!.Category)
            .Include(v => v.LocationDetails)
            .FirstOrDefaultAsync(v => v.Id == vehicleId && v.CompanyId == companyId);

        if (vehicle == null)
        {
            throw new InstagramPublishException("Vehicle not found");
        }

        // Generate caption if not provided
        var caption = options.Caption;
        if (string.IsNullOrEmpty(caption))
        {
            var captionResult = await GenerateCaptionAsync(vehicle, new CaptionOptions
            {
                IncludePrice = options.IncludePrice,
                DailyRate = options.DailyRate,
                Currency = options.Currency,
                IncludeHashtags = true,
                Location = vehicle.LocationDetails?.City ?? vehicle.Location
            });
            caption = captionResult.Text;
        }

        // Get image URL
        var imageUrl = NormalizeImageUrl(vehicle.ImageUrl ?? options.ImageUrl);
        if (string.IsNullOrEmpty(imageUrl))
        {
            throw new InstagramPublishException("No image available for this vehicle");
        }

        // Step 1: Create media container
        var containerId = await CreateInstagramContainerAsync(
            credentials.InstagramAccountId,
            credentials.PageAccessToken!,
            imageUrl,
            caption
        );

        // Step 2: Wait for container to be ready (Instagram needs time to process)
        await WaitForContainerReadyAsync(credentials.InstagramAccountId, credentials.PageAccessToken!, containerId);

        // Step 3: Publish
        var postId = await PublishInstagramContainerAsync(
            credentials.InstagramAccountId,
            credentials.PageAccessToken!,
            containerId
        );

        // Get permalink
        var permalink = await GetPostPermalinkAsync(postId, credentials.PageAccessToken!);

        // Save to database
        var post = new VehicleSocialPost
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            VehicleId = vehicleId,
            Platform = SocialPlatform.Instagram,
            PostId = postId,
            Permalink = permalink,
            Caption = caption,
            ImageUrl = imageUrl,
            DailyRate = options.DailyRate,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _postRepo.SaveAsync(post);

        _logger.LogInformation(
            "Published vehicle {VehicleId} to Instagram. PostId: {PostId}",
            vehicleId, postId);

        return new InstagramPostResult
        {
            Success = true,
            PostId = postId,
            Permalink = permalink,
            ContainerId = containerId
        };
    }

    public async Task<InstagramPostResult> PublishCarouselAsync(Guid companyId, List<Guid> vehicleIds, CarouselOptions options)
    {
        if (vehicleIds.Count < 2 || vehicleIds.Count > 10)
        {
            throw new InstagramPublishException("Carousel must have 2-10 items");
        }

        var credentials = await _credentialsRepo.GetByCompanyIdAsync(companyId);
        if (credentials == null || string.IsNullOrEmpty(credentials.InstagramAccountId))
        {
            throw new InstagramPublishException("Instagram account not connected");
        }

        var vehicles = await _context.Set<Vehicle>()
            .Include(v => v.VehicleModel)
                .ThenInclude(vm => vm!.Model)
                    .ThenInclude(m => m!.Category)
            .Where(v => vehicleIds.Contains(v.Id) && v.CompanyId == companyId)
            .ToListAsync();

        if (vehicles.Count != vehicleIds.Count)
        {
            throw new InstagramPublishException("Some vehicles not found");
        }

        // Create child containers for each image
        var childContainerIds = new List<string>();
        foreach (var vehicle in vehicles)
        {
            var imageUrl = NormalizeImageUrl(vehicle.ImageUrl);
            if (!string.IsNullOrEmpty(imageUrl))
            {
                var childId = await CreateCarouselItemContainerAsync(
                    credentials.InstagramAccountId,
                    credentials.PageAccessToken!,
                    imageUrl
                );
                childContainerIds.Add(childId);
            }
        }

        if (childContainerIds.Count < 2)
        {
            throw new InstagramPublishException("Not enough valid images for carousel");
        }

        // Wait for all children to be ready
        foreach (var childId in childContainerIds)
        {
            await WaitForContainerReadyAsync(
                credentials.InstagramAccountId,
                credentials.PageAccessToken!,
                childId
            );
        }

        // Create carousel container
        var carouselContainerId = await CreateCarouselContainerAsync(
            credentials.InstagramAccountId,
            credentials.PageAccessToken!,
            childContainerIds,
            options.Caption ?? "Check out our fleet! 🚗"
        );

        // Wait for carousel container
        await WaitForContainerReadyAsync(
            credentials.InstagramAccountId,
            credentials.PageAccessToken!,
            carouselContainerId
        );

        // Publish
        var postId = await PublishInstagramContainerAsync(
            credentials.InstagramAccountId,
            credentials.PageAccessToken!,
            carouselContainerId
        );

        var permalink = await GetPostPermalinkAsync(postId, credentials.PageAccessToken!);

        // Save posts for each vehicle
        foreach (var vehicle in vehicles)
        {
            var post = new VehicleSocialPost
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                VehicleId = vehicle.Id,
                Platform = SocialPlatform.Instagram,
                PostId = postId,
                Permalink = permalink,
                Caption = options.Caption,
                ImageUrl = vehicle.ImageUrl,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await _postRepo.SaveAsync(post);
        }

        return new InstagramPostResult
        {
            Success = true,
            PostId = postId,
            Permalink = permalink,
            ContainerId = carouselContainerId
        };
    }

    public async Task<ScheduledPost> SchedulePostAsync(Guid companyId, SchedulePostRequest request)
    {
        if (request.ScheduledFor <= DateTime.UtcNow)
        {
            throw new InstagramPublishException("Scheduled time must be in the future");
        }

        var scheduledPost = new ScheduledPost
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            VehicleId = request.VehicleId,
            VehicleIds = request.VehicleIds,
            PostType = request.IsCarousel ? ScheduledPostType.Carousel : ScheduledPostType.Single,
            Caption = request.Caption,
            ScheduledFor = request.ScheduledFor,
            IncludePrice = request.IncludePrice,
            DailyRate = request.DailyRate,
            Currency = request.Currency,
            CustomHashtags = request.CustomHashtags,
            Status = ScheduledPostStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _context.Set<ScheduledPost>().Add(scheduledPost);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Scheduled post for company {CompanyId} at {ScheduledFor}",
            companyId, request.ScheduledFor);

        return scheduledPost;
    }

    public async Task<List<ScheduledPost>> GetScheduledPostsAsync(Guid companyId)
    {
        return await _context.Set<ScheduledPost>()
            .Where(p => p.CompanyId == companyId && p.Status == ScheduledPostStatus.Pending)
            .OrderBy(p => p.ScheduledFor)
            .ToListAsync();
    }

    public async Task CancelScheduledPostAsync(Guid companyId, Guid scheduledPostId)
    {
        var post = await _context.Set<ScheduledPost>()
            .FirstOrDefaultAsync(p => p.Id == scheduledPostId && p.CompanyId == companyId);

        if (post != null)
        {
            post.Status = ScheduledPostStatus.Cancelled;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<BulkPublishResult> BulkPublishAsync(Guid companyId, BulkPublishRequest request)
    {
        var result = new BulkPublishResult
        {
            TotalRequested = request.VehicleIds.Count,
            Results = new List<VehiclePublishResult>()
        };

        foreach (var vehicleId in request.VehicleIds)
        {
            try
            {
                var publishResult = await PublishVehicleAsync(companyId, vehicleId, new PublishOptions
                {
                    IncludePrice = request.IncludePrice,
                    DailyRate = request.DailyRate,
                    Currency = request.Currency
                });

                result.Results.Add(new VehiclePublishResult
                {
                    VehicleId = vehicleId,
                    Success = true,
                    PostId = publishResult.PostId,
                    Permalink = publishResult.Permalink
                });
                result.SuccessCount++;

                // Rate limiting - wait between posts
                await Task.Delay(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish vehicle {VehicleId}", vehicleId);
                result.Results.Add(new VehiclePublishResult
                {
                    VehicleId = vehicleId,
                    Success = false,
                    Error = ex.Message
                });
                result.FailedCount++;
            }
        }

        return result;
    }

    public async Task<PostAnalytics> GetPostAnalyticsAsync(Guid companyId, string postId)
    {
        var credentials = await _credentialsRepo.GetByCompanyIdAsync(companyId);
        if (credentials == null)
        {
            throw new InstagramPublishException("Not connected to Instagram");
        }

        var url = $"{GraphApiBaseUrl}/{postId}/insights" +
            $"?metric=engagement,impressions,reach,saved,likes,comments,shares" +
            $"&access_token={credentials.PageAccessToken}";

        var response = await _httpClient.GetAsync(url);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Failed to get analytics for post {PostId}: {Response}", postId, content);
            return new PostAnalytics { PostId = postId };
        }

        var result = JsonSerializer.Deserialize<JsonElement>(content);
        var analytics = new PostAnalytics { PostId = postId };

        if (result.TryGetProperty("data", out var dataArray))
        {
            foreach (var metric in dataArray.EnumerateArray())
            {
                var name = metric.GetProperty("name").GetString();
                var values = metric.GetProperty("values").EnumerateArray().FirstOrDefault();
                var value = values.TryGetProperty("value", out var v) ? v.GetInt32() : 0;

                switch (name)
                {
                    case "engagement": analytics.Engagement = value; break;
                    case "impressions": analytics.Impressions = value; break;
                    case "reach": analytics.Reach = value; break;
                    case "saved": analytics.Saves = value; break;
                    case "likes": analytics.Likes = value; break;
                    case "comments": analytics.Comments = value; break;
                    case "shares": analytics.Shares = value; break;
                }
            }
        }

        return analytics;
    }

    public async Task<bool> TryAutoPostVehicleAsync(Guid companyId, Guid vehicleId, AutoPostTrigger trigger)
    {
        // Check if auto-posting is enabled for this company
        var settings = await _context.Set<CompanyAutoPostSettings>()
            .FirstOrDefaultAsync(s => s.CompanyId == companyId);

        if (settings == null || !settings.IsEnabled)
        {
            return false;
        }

        // Check trigger conditions
        var shouldPost = trigger switch
        {
            AutoPostTrigger.VehicleAdded => settings.PostOnVehicleAdded,
            AutoPostTrigger.VehicleUpdated => settings.PostOnVehicleUpdated,
            AutoPostTrigger.VehicleAvailable => settings.PostOnVehicleAvailable,
            AutoPostTrigger.PriceChanged => settings.PostOnPriceChange,
            _ => false
        };

        if (!shouldPost)
        {
            return false;
        }

        try
        {
            await PublishVehicleAsync(companyId, vehicleId, new PublishOptions
            {
                IncludePrice = settings.IncludePriceInPosts
            });
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Auto-post failed for vehicle {VehicleId}", vehicleId);
            return false;
        }
    }

    #region Private Helper Methods

    private string NormalizeImageUrl(string? imageUrl)
    {
        if (string.IsNullOrEmpty(imageUrl)) return "";
        if (imageUrl.StartsWith("http://") || imageUrl.StartsWith("https://")) return imageUrl;
        if (imageUrl.StartsWith("/")) return $"{_apiBaseUrl}{imageUrl}";
        return $"{_apiBaseUrl}/{imageUrl}";
    }

    private string GetVehicleEmoji(string category)
    {
        return category.ToLower() switch
        {
            "suv" or "crossover" => "🚙",
            "sedan" => "🚗",
            "sports" or "luxury" => "🏎️",
            "truck" or "pickup" => "🛻",
            "van" or "minivan" => "🚐",
            "convertible" => "🏎️",
            "electric" or "ev" => "⚡",
            "hybrid" => "🍃",
            _ => "🚗"
        };
    }

    private string GetCurrencySymbol(string currency)
    {
        return currency.ToUpper() switch
        {
            "USD" => "$",
            "EUR" => "€",
            "GBP" => "£",
            "BRL" => "R$",
            "JPY" => "¥",
            _ => "$"
        };
    }

    private List<string> GetCategoryHashtags(string category)
    {
        return category.ToLower() switch
        {
            "suv" or "crossover" => new List<string> { "#suvrental", "#suvlife", "#crossover" },
            "sedan" => new List<string> { "#sedanrental", "#sedanlife" },
            "sports" or "luxury" => new List<string> { "#luxurycar", "#sportscar", "#exoticcars" },
            "truck" or "pickup" => new List<string> { "#truckrental", "#pickup" },
            "van" or "minivan" => new List<string> { "#vanrental", "#familycar", "#minivan" },
            "convertible" => new List<string> { "#convertible", "#droptoprental" },
            "electric" or "ev" => new List<string> { "#electriccar", "#evrental", "#sustainable" },
            _ => new List<string>()
        };
    }

    private async Task<string> CreateInstagramContainerAsync(
        string igAccountId, string accessToken, string imageUrl, string caption)
    {
        var endpoint = $"{GraphApiBaseUrl}/{igAccountId}/media";
        var formData = new Dictionary<string, string>
        {
            ["image_url"] = imageUrl,
            ["caption"] = caption,
            ["access_token"] = accessToken
        };

        var response = await _httpClient.PostAsync(endpoint, new FormUrlEncodedContent(formData));
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to create Instagram container: {Response}", content);
            throw new InstagramPublishException($"Failed to create container: {content}");
        }

        var result = JsonSerializer.Deserialize<JsonElement>(content);
        return result.GetProperty("id").GetString() ?? "";
    }

    private async Task<string> CreateCarouselItemContainerAsync(
        string igAccountId, string accessToken, string imageUrl)
    {
        var endpoint = $"{GraphApiBaseUrl}/{igAccountId}/media";
        var formData = new Dictionary<string, string>
        {
            ["image_url"] = imageUrl,
            ["is_carousel_item"] = "true",
            ["access_token"] = accessToken
        };

        var response = await _httpClient.PostAsync(endpoint, new FormUrlEncodedContent(formData));
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InstagramPublishException($"Failed to create carousel item: {content}");
        }

        var result = JsonSerializer.Deserialize<JsonElement>(content);
        return result.GetProperty("id").GetString() ?? "";
    }

    private async Task<string> CreateCarouselContainerAsync(
        string igAccountId, string accessToken, List<string> childIds, string caption)
    {
        var endpoint = $"{GraphApiBaseUrl}/{igAccountId}/media";
        var formData = new Dictionary<string, string>
        {
            ["media_type"] = "CAROUSEL",
            ["children"] = string.Join(",", childIds),
            ["caption"] = caption,
            ["access_token"] = accessToken
        };

        var response = await _httpClient.PostAsync(endpoint, new FormUrlEncodedContent(formData));
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InstagramPublishException($"Failed to create carousel: {content}");
        }

        var result = JsonSerializer.Deserialize<JsonElement>(content);
        return result.GetProperty("id").GetString() ?? "";
    }

    private async Task WaitForContainerReadyAsync(string igAccountId, string accessToken, string containerId)
    {
        var maxAttempts = 30;
        var delay = TimeSpan.FromSeconds(2);

        for (var i = 0; i < maxAttempts; i++)
        {
            var url = $"{GraphApiBaseUrl}/{containerId}?fields=status_code&access_token={accessToken}";
            var response = await _httpClient.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<JsonElement>(content);
                if (result.TryGetProperty("status_code", out var statusCode))
                {
                    var status = statusCode.GetString();
                    if (status == "FINISHED")
                    {
                        return;
                    }
                    if (status == "ERROR")
                    {
                        throw new InstagramPublishException("Container processing failed");
                    }
                }
            }

            await Task.Delay(delay);
        }

        throw new InstagramPublishException("Timeout waiting for container to be ready");
    }

    private async Task<string> PublishInstagramContainerAsync(
        string igAccountId, string accessToken, string containerId)
    {
        var endpoint = $"{GraphApiBaseUrl}/{igAccountId}/media_publish";
        var formData = new Dictionary<string, string>
        {
            ["creation_id"] = containerId,
            ["access_token"] = accessToken
        };

        var response = await _httpClient.PostAsync(endpoint, new FormUrlEncodedContent(formData));
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to publish Instagram container: {Response}", content);
            throw new InstagramPublishException($"Failed to publish: {content}");
        }

        var result = JsonSerializer.Deserialize<JsonElement>(content);
        return result.GetProperty("id").GetString() ?? "";
    }

    private async Task<string?> GetPostPermalinkAsync(string postId, string accessToken)
    {
        try
        {
            var url = $"{GraphApiBaseUrl}/{postId}?fields=permalink&access_token={accessToken}";
            var response = await _httpClient.GetAsync(url);
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<JsonElement>(content);
                return result.TryGetProperty("permalink", out var pl) ? pl.GetString() : null;
            }
        }
        catch { /* Ignore */ }
        
        return null;
    }

    #endregion
}

#region Models and DTOs

public class GeneratedCaption
{
    public string Text { get; set; } = "";
    public List<string> Hashtags { get; set; } = new();
    public int CharacterCount { get; set; }
    public bool IsWithinLimit { get; set; }
}

public class CaptionOptions
{
    public bool IncludePrice { get; set; }
    public decimal? DailyRate { get; set; }
    public string? Currency { get; set; }
    public bool IncludeHashtags { get; set; } = true;
    public List<string>? CustomHashtags { get; set; }
    public int MaxHashtags { get; set; } = 20;
    public string? Location { get; set; }
    public string? CallToAction { get; set; }
}

public class PublishOptions
{
    public string? Caption { get; set; }
    public string? ImageUrl { get; set; }
    public bool IncludePrice { get; set; }
    public decimal? DailyRate { get; set; }
    public string? Currency { get; set; }
}

public class CarouselOptions
{
    public string? Caption { get; set; }
    public bool IncludePrice { get; set; }
}

public class InstagramPostResult
{
    public bool Success { get; set; }
    public string PostId { get; set; } = "";
    public string? Permalink { get; set; }
    public string? ContainerId { get; set; }
    public string? Error { get; set; }
}

public class SchedulePostRequest
{
    public Guid? VehicleId { get; set; }
    public List<Guid>? VehicleIds { get; set; }
    public bool IsCarousel { get; set; }
    public string? Caption { get; set; }
    public DateTime ScheduledFor { get; set; }
    public bool IncludePrice { get; set; }
    public decimal? DailyRate { get; set; }
    public string? Currency { get; set; }
    public List<string>? CustomHashtags { get; set; }
}

public class BulkPublishRequest
{
    public List<Guid> VehicleIds { get; set; } = new();
    public bool IncludePrice { get; set; }
    public decimal? DailyRate { get; set; }
    public string? Currency { get; set; }
}

public class BulkPublishResult
{
    public int TotalRequested { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public List<VehiclePublishResult> Results { get; set; } = new();
}

public class VehiclePublishResult
{
    public Guid VehicleId { get; set; }
    public bool Success { get; set; }
    public string? PostId { get; set; }
    public string? Permalink { get; set; }
    public string? Error { get; set; }
}

public class PostAnalytics
{
    public string PostId { get; set; } = "";
    public int Impressions { get; set; }
    public int Reach { get; set; }
    public int Engagement { get; set; }
    public int Likes { get; set; }
    public int Comments { get; set; }
    public int Shares { get; set; }
    public int Saves { get; set; }
}

public enum AutoPostTrigger
{
    VehicleAdded,
    VehicleUpdated,
    VehicleAvailable,
    PriceChanged
}

public class InstagramPublishException : Exception
{
    public InstagramPublishException(string message) : base(message) { }
}

#endregion
