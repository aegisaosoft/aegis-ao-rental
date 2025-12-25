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
    /// Publish a vehicle to configured social platforms based on company settings
    /// </summary>
    Task PublishVehicleAsync(Guid companyId, Guid vehicleId);
    
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

    public async Task PublishVehicleAsync(Guid companyId, Guid vehicleId)
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

            // Get vehicle with model info
            var vehicle = await _dbContext.Vehicles
                .Include(v => v.VehicleModel)
                    .ThenInclude(vm => vm.Model)
                .Include(v => v.Company)
                .FirstOrDefaultAsync(v => v.Id == vehicleId && v.CompanyId == companyId);

            if (vehicle == null)
            {
                _logger.LogWarning("Auto-publish failed: vehicle {VehicleId} not found", vehicleId);
                return;
            }

            // Check if has image
            var imageUrl = vehicle.ImageUrl ?? vehicle.VehicleModel?.ImageUrl;
            if (string.IsNullOrEmpty(imageUrl))
            {
                _logger.LogDebug("Auto-publish skipped: vehicle {VehicleId} has no image", vehicleId);
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

            // Build caption
            var caption = BuildCaption(vehicle, credentials.AutoPublishIncludePrice, hashtags);

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
                        VehicleId = vehicleId,
                        Platform = SocialPlatform.Facebook,
                        PostId = postId,
                        Caption = caption,
                        ImageUrl = imageUrl,
                        DailyRate = vehicle.VehicleModel?.DailyRate,
                        IsActive = true
                    };
                    await _postRepo.SaveAsync(post);
                    
                    _logger.LogInformation("Auto-published vehicle {VehicleId} to Facebook: {PostId}", vehicleId, postId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Auto-publish to Facebook failed for vehicle {VehicleId}", vehicleId);
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
                        VehicleId = vehicleId,
                        Platform = SocialPlatform.Instagram,
                        PostId = postId,
                        Caption = caption,
                        ImageUrl = imageUrl,
                        DailyRate = vehicle.VehicleModel?.DailyRate,
                        IsActive = true
                    };
                    await _postRepo.SaveAsync(post);
                    
                    _logger.LogInformation("Auto-published vehicle {VehicleId} to Instagram: {PostId}", vehicleId, postId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Auto-publish to Instagram failed for vehicle {VehicleId}", vehicleId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Auto-publish failed for vehicle {VehicleId}", vehicleId);
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

    private string BuildCaption(Vehicle vehicle, bool includePrice, List<string>? hashtags)
    {
        var vehicleModel = vehicle.VehicleModel;
        var catalogModel = vehicleModel?.Model;
        var parts = new List<string>();

        // Vehicle name
        var vehicleName = catalogModel != null
            ? $"{catalogModel.Year} {catalogModel.Make} {catalogModel.ModelName}"
            : $"Vehicle {vehicle.LicensePlate}";
        parts.Add($"🚗 {vehicleName}");

        // Features
        var features = new List<string>();
        if (!string.IsNullOrEmpty(vehicle.Transmission))
            features.Add(vehicle.Transmission);
        if (vehicle.Seats.HasValue)
            features.Add($"{vehicle.Seats} seats");
        if (!string.IsNullOrEmpty(vehicle.Color))
            features.Add(vehicle.Color);

        if (features.Count > 0)
            parts.Add(string.Join(" • ", features));

        // Price
        if (includePrice && vehicleModel?.DailyRate > 0)
        {
            parts.Add($"💰 ${vehicleModel.DailyRate}/day");
        }

        // Company info
        if (vehicle.Company != null)
        {
            parts.Add($"📍 {vehicle.Company.CompanyName}");
        }

        // Booking URL
        var bookingUrl = GetBookingUrl(vehicle);
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

    private string? GetBookingUrl(Vehicle vehicle)
    {
        if (vehicle.Company == null || vehicle.VehicleModel?.Model == null)
            return null;

        var catalogModel = vehicle.VehicleModel.Model;
        var subdomain = vehicle.Company.Subdomain;
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        
        // Build query parameters
        var queryParams = new List<string>();
        
        if (catalogModel.CategoryId != null)
            queryParams.Add($"category={catalogModel.CategoryId}");
        
        if (!string.IsNullOrEmpty(catalogModel.Make))
            queryParams.Add($"make={Uri.EscapeDataString(catalogModel.Make)}");
        
        if (!string.IsNullOrEmpty(catalogModel.ModelName))
            queryParams.Add($"model={Uri.EscapeDataString(catalogModel.ModelName)}");
        
        queryParams.Add($"companyId={vehicle.CompanyId}");
        queryParams.Add($"startDate={today}");
        queryParams.Add($"endDate={today}");
        
        var query = string.Join("&", queryParams);
        
        // Use company subdomain if available
        if (!string.IsNullOrEmpty(subdomain))
        {
            return $"https://{subdomain}.aegis-rental.com/book?{query}";
        }

        // Fallback to main site
        return $"https://aegis-rental.com/book?{query}";
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
