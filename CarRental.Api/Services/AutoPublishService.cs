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
using CarRental.Api.Data;
using CarRental.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Api.Services;

public interface IAutoPublishService
{
    /// <summary>
    /// Publish a vehicle model to configured social platforms based on company settings
    /// </summary>
    Task PublishModelAsync(Guid companyId, Guid vehicleModelId);
    
    /// <summary>
    /// Update auto-publish settings for a company
    /// </summary>
    Task UpdateSettingsAsync(Guid companyId, AutoPublishSettings settings);
    
    /// <summary>
    /// Get auto-publish settings for a company
    /// </summary>
    Task<AutoPublishSettings?> GetSettingsAsync(Guid companyId);
}

public class AutoPublishSettings
{
    public bool AutoPublishFacebook { get; set; }
    public bool AutoPublishInstagram { get; set; }
    public bool IncludePrice { get; set; } = true;
    public List<string>? Hashtags { get; set; }
}

public class AutoPublishService : IAutoPublishService
{
    private readonly CarRentalDbContext _dbContext;
    private readonly ICompanyMetaCredentialsRepository _credentialsRepo;
    private readonly IVehicleSocialPostRepository _postRepo;
    private readonly ILogger<AutoPublishService> _logger;
    private readonly HttpClient _httpClient;
    private readonly string _apiBaseUrl;
    private const string GraphApiBaseUrl = "https://graph.facebook.com/v19.0";

    public AutoPublishService(
        CarRentalDbContext dbContext,
        ICompanyMetaCredentialsRepository credentialsRepo,
        IVehicleSocialPostRepository postRepo,
        IConfiguration configuration,
        ILogger<AutoPublishService> logger)
    {
        _dbContext = dbContext;
        _credentialsRepo = credentialsRepo;
        _postRepo = postRepo;
        _logger = logger;
        _httpClient = new HttpClient();
        _apiBaseUrl = configuration["ApiBaseUrl"] ?? "https://aegis-ao-rental.azurewebsites.net";
    }

    public async Task PublishModelAsync(Guid companyId, Guid vehicleModelId)
    {
        try
        {
            var credentials = await _credentialsRepo.GetByCompanyIdAsync(companyId);
            if (credentials == null)
            {
                _logger.LogDebug("Auto-publish skipped: company {CompanyId} not connected to Meta", companyId);
                return;
            }

            // Check if auto-publish is enabled
            if (!credentials.AutoPublishFacebook && !credentials.AutoPublishInstagram)
            {
                _logger.LogDebug("Auto-publish skipped: disabled for company {CompanyId}", companyId);
                return;
            }

            // Get vehicle model with catalog info
            var vehicleModel = await _dbContext.VehicleModels
                .Include(vm => vm.Model)
                .Include(vm => vm.Company)
                .FirstOrDefaultAsync(vm => vm.Id == vehicleModelId && vm.CompanyId == companyId);

            if (vehicleModel == null)
            {
                _logger.LogWarning("Auto-publish failed: vehicle model {VehicleModelId} not found", vehicleModelId);
                return;
            }

            // Get image from one of the vehicles with this model, or use a default
            var imageUrl = await _dbContext.Vehicles
                .Where(v => v.VehicleModelId == vehicleModelId && !string.IsNullOrEmpty(v.ImageUrl))
                .Select(v => v.ImageUrl)
                .FirstOrDefaultAsync();

            if (string.IsNullOrEmpty(imageUrl))
            {
                _logger.LogDebug("Auto-publish skipped: model {VehicleModelId} has no image", vehicleModelId);
                return;
            }

            // Parse hashtags
            List<string>? hashtags = null;
            if (!string.IsNullOrEmpty(credentials.AutoPublishHashtags))
            {
                try
                {
                    hashtags = JsonSerializer.Deserialize<List<string>>(credentials.AutoPublishHashtags);
                }
                catch { /* ignore */ }
            }

            // Build caption for model with Deep Link support
            var caption = BuildModelCaption(vehicleModel, credentials.AutoPublishIncludePrice, hashtags, credentials);

            // Publish to Facebook
            if (credentials.AutoPublishFacebook && !string.IsNullOrEmpty(credentials.PageId) && !string.IsNullOrEmpty(credentials.PageAccessToken))
            {
                try
                {
                    var postId = await PublishToFacebook(credentials.PageId, credentials.PageAccessToken, imageUrl, caption);
                    
                    var post = new VehicleSocialPost
                    {
                        Id = Guid.NewGuid(),
                        CompanyId = companyId,
                        VehicleModelId = vehicleModelId,
                        Platform = SocialPlatform.Facebook,
                        PostId = postId,
                        Caption = caption,
                        ImageUrl = imageUrl,
                        DailyRate = vehicleModel.DailyRate,
                        IsActive = true
                    };
                    await _postRepo.SaveAsync(post);
                    
                    _logger.LogInformation("Auto-published model {VehicleModelId} ({Make} {Model}) to Facebook: {PostId}", 
                        vehicleModelId, vehicleModel.Model?.Make, vehicleModel.Model?.ModelName, postId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Auto-publish to Facebook failed for model {VehicleModelId}", vehicleModelId);
                }
            }

            // Publish to Instagram
            if (credentials.AutoPublishInstagram && !string.IsNullOrEmpty(credentials.InstagramAccountId) && !string.IsNullOrEmpty(credentials.PageAccessToken))
            {
                try
                {
                    var postId = await PublishToInstagram(credentials.InstagramAccountId, credentials.PageAccessToken, imageUrl, caption);
                    
                    var post = new VehicleSocialPost
                    {
                        Id = Guid.NewGuid(),
                        CompanyId = companyId,
                        VehicleModelId = vehicleModelId,
                        Platform = SocialPlatform.Instagram,
                        PostId = postId,
                        Caption = caption,
                        ImageUrl = imageUrl,
                        DailyRate = vehicleModel.DailyRate,
                        IsActive = true
                    };
                    await _postRepo.SaveAsync(post);
                    
                    _logger.LogInformation("Auto-published model {VehicleModelId} ({Make} {Model}) to Instagram: {PostId}", 
                        vehicleModelId, vehicleModel.Model?.Make, vehicleModel.Model?.ModelName, postId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Auto-publish to Instagram failed for model {VehicleModelId}", vehicleModelId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Auto-publish failed for model {VehicleModelId}", vehicleModelId);
        }
    }

    public async Task UpdateSettingsAsync(Guid companyId, AutoPublishSettings settings)
    {
        var credentials = await _credentialsRepo.GetByCompanyIdAsync(companyId);
        if (credentials == null)
        {
            throw new InvalidOperationException("Company not connected to Meta");
        }

        credentials.AutoPublishFacebook = settings.AutoPublishFacebook;
        credentials.AutoPublishInstagram = settings.AutoPublishInstagram;
        credentials.AutoPublishIncludePrice = settings.IncludePrice;
        credentials.AutoPublishHashtags = settings.Hashtags != null 
            ? JsonSerializer.Serialize(settings.Hashtags) 
            : null;

        await _credentialsRepo.UpdateAsync(credentials);
        
        _logger.LogInformation(
            "Updated auto-publish settings for company {CompanyId}: FB={Facebook}, IG={Instagram}",
            companyId, settings.AutoPublishFacebook, settings.AutoPublishInstagram);
    }

    public async Task<AutoPublishSettings?> GetSettingsAsync(Guid companyId)
    {
        var credentials = await _credentialsRepo.GetByCompanyIdAsync(companyId);
        if (credentials == null)
        {
            return null;
        }

        List<string>? hashtags = null;
        if (!string.IsNullOrEmpty(credentials.AutoPublishHashtags))
        {
            try
            {
                hashtags = JsonSerializer.Deserialize<List<string>>(credentials.AutoPublishHashtags);
            }
            catch { /* ignore */ }
        }

        return new AutoPublishSettings
        {
            AutoPublishFacebook = credentials.AutoPublishFacebook,
            AutoPublishInstagram = credentials.AutoPublishInstagram,
            IncludePrice = credentials.AutoPublishIncludePrice,
            Hashtags = hashtags
        };
    }

    private string BuildModelCaption(VehicleModel vehicleModel, bool includePrice, List<string>? hashtags, CompanyMetaCredentials? credentials = null)
    {
        var catalogModel = vehicleModel.Model;
        var parts = new List<string>();

        // Model name (Make + Model, without Year)
        var modelName = catalogModel != null
            ? $"{catalogModel.Make} {catalogModel.ModelName}"
            : "New Vehicle Available";
        parts.Add($"🚗 {modelName}");

        // Features from catalog model
        if (catalogModel != null)
        {
            var features = new List<string>();
            if (!string.IsNullOrEmpty(catalogModel.Transmission))
                features.Add(catalogModel.Transmission);
            if (catalogModel.Seats.HasValue)
                features.Add($"{catalogModel.Seats} seats");
            if (!string.IsNullOrEmpty(catalogModel.FuelType))
                features.Add(catalogModel.FuelType);

            if (features.Count > 0)
                parts.Add(string.Join(" • ", features));
        }

        // Price
        if (includePrice && vehicleModel.DailyRate > 0)
        {
            parts.Add($"💰 ${vehicleModel.DailyRate}/day");
        }

        // Company info
        if (vehicleModel.Company != null)
        {
            parts.Add($"📍 {vehicleModel.Company.CompanyName}");
        }

        // Booking URL with Deep Link support
        var bookingUrl = GetModelBookingUrl(vehicleModel, credentials);
        if (!string.IsNullOrEmpty(bookingUrl))
        {
            parts.Add($"🔗 Book now: {bookingUrl}");
        }
        else
        {
            parts.Add("Book now! Link in bio 👆");
        }

        // Hashtags
        var allHashtags = new List<string> { "#carrental", "#rentalcar" };
        if (catalogModel != null)
        {
            if (!string.IsNullOrEmpty(catalogModel.Make))
                allHashtags.Add($"#{catalogModel.Make.ToLower().Replace(" ", "")}");
            if (!string.IsNullOrEmpty(catalogModel.ModelName))
                allHashtags.Add($"#{catalogModel.ModelName.ToLower().Replace(" ", "")}");
        }
        if (hashtags != null)
            allHashtags.AddRange(hashtags);

        parts.Add(string.Join(" ", allHashtags.Distinct()));

        return string.Join("\n\n", parts);
    }

    private string? GetModelBookingUrl(VehicleModel vehicleModel, CompanyMetaCredentials? credentials = null)
    {
        if (vehicleModel.Company == null || vehicleModel.Model == null)
            return null;

        var catalogModel = vehicleModel.Model;
        var company = vehicleModel.Company;
        
        // Determine base URL
        string baseUrl;
        if (!string.IsNullOrEmpty(credentials?.DeepLinkBaseUrl))
        {
            baseUrl = credentials.DeepLinkBaseUrl.TrimEnd('/');
        }
        else if (!string.IsNullOrEmpty(company.Subdomain))
        {
            baseUrl = $"https://{company.Subdomain}.aegis-rental.com";
        }
        else
        {
            baseUrl = "https://aegis-rental.com";
        }
        
        // Determine URL pattern
        string urlPattern = credentials?.DeepLinkVehiclePattern ?? "/book?modelId={modelId}";
        
        // Replace placeholders
        var url = urlPattern
            .Replace("{modelId}", vehicleModel.Id.ToString())
            .Replace("{vehicleId}", vehicleModel.Id.ToString())
            .Replace("{make}", Uri.EscapeDataString(catalogModel.Make ?? ""))
            .Replace("{model}", Uri.EscapeDataString(catalogModel.ModelName ?? ""))
            .Replace("{companyId}", vehicleModel.CompanyId.ToString())
            .Replace("{category}", catalogModel.CategoryId?.ToString() ?? "");
        
        // Clean up empty query params
        url = System.Text.RegularExpressions.Regex.Replace(url, @"[&?][^=]+=(?=&|$)", "");
        url = url.TrimEnd('&', '?');
        
        return $"{baseUrl}{url}";
    }

    private string NormalizeImageUrl(string imageUrl)
    {
        if (imageUrl.StartsWith("http://") || imageUrl.StartsWith("https://"))
            return imageUrl;
        if (imageUrl.StartsWith("/"))
            return $"{_apiBaseUrl}{imageUrl}";
        return $"{_apiBaseUrl}/{imageUrl}";
    }

    private async Task<string> PublishToFacebook(string pageId, string accessToken, string imageUrl, string caption)
    {
        var absoluteImageUrl = NormalizeImageUrl(imageUrl);
        var endpoint = $"{GraphApiBaseUrl}/{pageId}/photos";
        
        var formData = new Dictionary<string, string>
        {
            ["url"] = absoluteImageUrl,
            ["caption"] = caption,
            ["access_token"] = accessToken
        };
        
        var response = await _httpClient.PostAsync(endpoint, new FormUrlEncodedContent(formData));
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Facebook API error: {content}");
        }

        var result = JsonSerializer.Deserialize<JsonElement>(content);
        return result.TryGetProperty("id", out var id) ? id.GetString() ?? "" :
               result.TryGetProperty("post_id", out var postId) ? postId.GetString() ?? "" : "";
    }

    private async Task<string> PublishToInstagram(string igAccountId, string accessToken, string imageUrl, string caption)
    {
        var absoluteImageUrl = NormalizeImageUrl(imageUrl);
        
        // Create container
        var containerEndpoint = $"{GraphApiBaseUrl}/{igAccountId}/media";
        var containerData = new Dictionary<string, string>
        {
            ["image_url"] = absoluteImageUrl,
            ["caption"] = caption,
            ["access_token"] = accessToken
        };
        
        var containerResponse = await _httpClient.PostAsync(containerEndpoint, new FormUrlEncodedContent(containerData));
        var containerContent = await containerResponse.Content.ReadAsStringAsync();

        if (!containerResponse.IsSuccessStatusCode)
        {
            throw new Exception($"Instagram container error: {containerContent}");
        }

        var containerResult = JsonSerializer.Deserialize<JsonElement>(containerContent);
        var containerId = containerResult.GetProperty("id").GetString() ?? "";

        // Wait for processing
        await Task.Delay(2000);

        // Publish
        var publishEndpoint = $"{GraphApiBaseUrl}/{igAccountId}/media_publish";
        var publishData = new Dictionary<string, string>
        {
            ["creation_id"] = containerId,
            ["access_token"] = accessToken
        };
        
        var publishResponse = await _httpClient.PostAsync(publishEndpoint, new FormUrlEncodedContent(publishData));
        var publishContent = await publishResponse.Content.ReadAsStringAsync();

        if (!publishResponse.IsSuccessStatusCode)
        {
            throw new Exception($"Instagram publish error: {publishContent}");
        }

        var publishResult = JsonSerializer.Deserialize<JsonElement>(publishContent);
        return publishResult.GetProperty("id").GetString() ?? "";
    }
}
