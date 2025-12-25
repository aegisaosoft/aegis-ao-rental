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
using System.Text.Json;
using CarRental.Api.Data;
using CarRental.Api.Models;
using CarRental.Api.Services;

namespace CarRental.Api.Controllers;

[ApiController]
[Route("api/social-posts")]
[Authorize]
public class SocialPostController : ControllerBase
{
    private readonly IVehicleSocialPostRepository _postRepo;
    private readonly ICompanyMetaCredentialsRepository _credentialsRepo;
    private readonly ILogger<SocialPostController> _logger;
    private readonly HttpClient _httpClient;
    private readonly string _apiBaseUrl;
    private const string GraphApiBaseUrl = "https://graph.facebook.com/v19.0";

    public SocialPostController(
        IVehicleSocialPostRepository postRepo,
        ICompanyMetaCredentialsRepository credentialsRepo,
        IConfiguration configuration,
        ILogger<SocialPostController> logger)
    {
        _postRepo = postRepo;
        _credentialsRepo = credentialsRepo;
        _logger = logger;
        _httpClient = new HttpClient();
        _apiBaseUrl = configuration["ApiBaseUrl"] ?? "https://aegis-ao-rental.azurewebsites.net";
    }

    /// <summary>
    /// Normalize image URL - convert relative paths to absolute URLs
    /// </summary>
    private string NormalizeImageUrl(string? imageUrl)
    {
        if (string.IsNullOrEmpty(imageUrl))
            return imageUrl ?? "";

        if (imageUrl.StartsWith("http://") || imageUrl.StartsWith("https://"))
            return imageUrl;

        if (imageUrl.StartsWith("/"))
            return $"{_apiBaseUrl}{imageUrl}";

        return $"{_apiBaseUrl}/{imageUrl}";
    }

    /// <summary>
    /// Get post status for a single vehicle
    /// </summary>
    [HttpGet("status/{companyId}/{vehicleId}")]
    public async Task<IActionResult> GetVehiclePostStatus(Guid companyId, Guid vehicleId)
    {
        var posts = await _postRepo.GetByVehicleAsync(companyId, vehicleId);

        return Ok(new
        {
            facebookPost = posts.FirstOrDefault(p => p.Platform == SocialPlatform.Facebook),
            instagramPost = posts.FirstOrDefault(p => p.Platform == SocialPlatform.Instagram)
        });
    }

    /// <summary>
    /// Get post status for multiple vehicles (bulk)
    /// </summary>
    [HttpPost("status/bulk/{companyId}")]
    public async Task<IActionResult> GetBulkPostStatus(Guid companyId, [FromBody] BulkStatusRequest request)
    {
        var vehicleGuids = request.VehicleIds
            .Where(id => Guid.TryParse(id, out _))
            .Select(id => Guid.Parse(id))
            .ToList();

        var posts = await _postRepo.GetByVehicleIdsAsync(companyId, vehicleGuids);

        var result = vehicleGuids.ToDictionary(
            id => id.ToString(),
            id => new
            {
                facebookPostId = posts.FirstOrDefault(p => p.VehicleId == id && p.Platform == SocialPlatform.Facebook)?.PostId,
                facebookPermalink = posts.FirstOrDefault(p => p.VehicleId == id && p.Platform == SocialPlatform.Facebook)?.Permalink,
                facebookUpdatedAt = posts.FirstOrDefault(p => p.VehicleId == id && p.Platform == SocialPlatform.Facebook)?.UpdatedAt,
                instagramPostId = posts.FirstOrDefault(p => p.VehicleId == id && p.Platform == SocialPlatform.Instagram)?.PostId,
                instagramPermalink = posts.FirstOrDefault(p => p.VehicleId == id && p.Platform == SocialPlatform.Instagram)?.Permalink,
                instagramUpdatedAt = posts.FirstOrDefault(p => p.VehicleId == id && p.Platform == SocialPlatform.Instagram)?.UpdatedAt,
            }
        );

        return Ok(result);
    }

    /// <summary>
    /// Publish or update a vehicle to Facebook
    /// </summary>
    [HttpPost("publish/facebook/{companyId}")]
    public async Task<IActionResult> PublishToFacebook(Guid companyId, [FromBody] PublishVehicleRequest request)
    {
        if (!Guid.TryParse(request.VehicleId, out var vehicleId))
        {
            return BadRequest(new { error = "Invalid vehicle ID" });
        }

        try
        {
            var credentials = await _credentialsRepo.GetByCompanyIdAsync(companyId);
            if (credentials == null || string.IsNullOrEmpty(credentials.PageId) || string.IsNullOrEmpty(credentials.PageAccessToken))
            {
                return BadRequest(new { error = "Not connected to Facebook" });
            }

            // Check if already published
            var existingPost = await _postRepo.GetByVehicleAndPlatformAsync(companyId, vehicleId, SocialPlatform.Facebook);

            string postId;
            string? permalink = null;

            if (existingPost != null && request.UpdateMode != "republish")
            {
                // Update existing post
                if (existingPost.ImageUrl != request.ImageUrl)
                {
                    // Delete old post and create new one (can't update images)
                    await DeleteFacebookPost(credentials.PageAccessToken, existingPost.PostId);

                    var result = await PublishFacebookPost(credentials.PageId, credentials.PageAccessToken, request);
                    postId = result.PostId;
                    permalink = result.Permalink;

                    existingPost.PostId = postId;
                    existingPost.Permalink = permalink;
                    existingPost.Caption = request.Caption;
                    existingPost.ImageUrl = request.ImageUrl;
                    existingPost.DailyRate = request.DailyRate;
                    await _postRepo.UpdateAsync(existingPost);
                }
                else
                {
                    // Just update the text
                    await UpdateFacebookPostText(credentials.PageAccessToken, existingPost.PostId, request.Caption);
                    postId = existingPost.PostId;
                    permalink = existingPost.Permalink;

                    existingPost.Caption = request.Caption;
                    existingPost.DailyRate = request.DailyRate;
                    await _postRepo.UpdateAsync(existingPost);
                }
            }
            else
            {
                // Delete old if republishing
                if (existingPost != null)
                {
                    await DeleteFacebookPost(credentials.PageAccessToken, existingPost.PostId);
                    await _postRepo.MarkAsDeletedAsync(companyId, vehicleId, SocialPlatform.Facebook);
                }

                // Create new post
                var result = await PublishFacebookPost(credentials.PageId, credentials.PageAccessToken, request);
                postId = result.PostId;
                permalink = result.Permalink;

                // Save to database
                var newPost = new VehicleSocialPost
                {
                    Id = Guid.NewGuid(),
                    CompanyId = companyId,
                    VehicleId = vehicleId,
                    Platform = SocialPlatform.Facebook,
                    PostId = postId,
                    Permalink = permalink,
                    Caption = request.Caption,
                    ImageUrl = request.ImageUrl,
                    DailyRate = request.DailyRate,
                    IsActive = true
                };

                await _postRepo.SaveAsync(newPost);
            }

            _logger.LogInformation(
                "Published vehicle {VehicleId} to Facebook. PostId: {PostId}",
                vehicleId, postId);

            return Ok(new
            {
                success = true,
                postId,
                permalink
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish vehicle {VehicleId} to Facebook", vehicleId);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Publish or update a vehicle to Instagram
    /// </summary>
    [HttpPost("publish/instagram/{companyId}")]
    public async Task<IActionResult> PublishToInstagram(Guid companyId, [FromBody] PublishVehicleRequest request)
    {
        if (!Guid.TryParse(request.VehicleId, out var vehicleId))
        {
            return BadRequest(new { error = "Invalid vehicle ID" });
        }

        try
        {
            var credentials = await _credentialsRepo.GetByCompanyIdAsync(companyId);
            if (credentials == null ||
                string.IsNullOrEmpty(credentials.InstagramAccountId) ||
                string.IsNullOrEmpty(credentials.PageAccessToken))
            {
                return BadRequest(new { error = "Not connected to Instagram" });
            }

            if (string.IsNullOrEmpty(request.ImageUrl))
            {
                return BadRequest(new { error = "Instagram posts require an image" });
            }

            // Check if already published
            var existingPost = await _postRepo.GetByVehicleAndPlatformAsync(companyId, vehicleId, SocialPlatform.Instagram);

            // Instagram doesn't support updating posts, always create new
            if (existingPost != null)
            {
                // Instagram API doesn't support deleting posts, just mark as inactive
                await _postRepo.MarkAsDeletedAsync(companyId, vehicleId, SocialPlatform.Instagram);
            }

            // Create Instagram container
            var absoluteImageUrl = NormalizeImageUrl(request.ImageUrl);
            var containerId = await CreateInstagramContainer(
                credentials.InstagramAccountId,
                credentials.PageAccessToken,
                absoluteImageUrl,
                request.Caption);

            // Wait for container processing
            await Task.Delay(2000);

            // Publish the container
            var postId = await PublishInstagramContainer(
                credentials.InstagramAccountId,
                credentials.PageAccessToken,
                containerId);

            // Get permalink
            string? permalink = null;
            try
            {
                var permalinkUrl = $"{GraphApiBaseUrl}/{postId}?fields=permalink&access_token={credentials.PageAccessToken}";
                var response = await _httpClient.GetAsync(permalinkUrl);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<JsonElement>(content);
                    permalink = result.TryGetProperty("permalink", out var pl) ? pl.GetString() : null;
                }
            }
            catch { /* Ignore permalink errors */ }

            // Save to database
            var newPost = new VehicleSocialPost
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                VehicleId = vehicleId,
                Platform = SocialPlatform.Instagram,
                PostId = postId,
                Permalink = permalink,
                Caption = request.Caption,
                ImageUrl = request.ImageUrl,
                DailyRate = request.DailyRate,
                IsActive = true
            };

            await _postRepo.SaveAsync(newPost);

            _logger.LogInformation(
                "Published vehicle {VehicleId} to Instagram. PostId: {PostId}",
                vehicleId, postId);

            return Ok(new
            {
                success = true,
                postId,
                permalink
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish vehicle {VehicleId} to Instagram", vehicleId);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Publish to both Facebook and Instagram
    /// </summary>
    [HttpPost("publish/both/{companyId}")]
    public async Task<IActionResult> PublishToBoth(Guid companyId, [FromBody] PublishVehicleRequest request)
    {
        var results = new Dictionary<string, object>();
        var errors = new List<string>();

        // Try Facebook
        try
        {
            var fbResult = await PublishToFacebook(companyId, request);
            if (fbResult is OkObjectResult okResult)
            {
                results["facebook"] = okResult.Value ?? new { };
            }
            else if (fbResult is BadRequestObjectResult badResult)
            {
                errors.Add($"Facebook: {badResult.Value}");
            }
        }
        catch (Exception ex)
        {
            errors.Add($"Facebook: {ex.Message}");
        }

        // Try Instagram
        try
        {
            var igResult = await PublishToInstagram(companyId, request);
            if (igResult is OkObjectResult okResult)
            {
                results["instagram"] = okResult.Value ?? new { };
            }
            else if (igResult is BadRequestObjectResult badResult)
            {
                errors.Add($"Instagram: {badResult.Value}");
            }
        }
        catch (Exception ex)
        {
            errors.Add($"Instagram: {ex.Message}");
        }

        return Ok(new
        {
            success = errors.Count == 0,
            results,
            errors
        });
    }

    /// <summary>
    /// Delete a post from a platform
    /// </summary>
    [HttpDelete("{companyId}/{vehicleId}/{platform}")]
    public async Task<IActionResult> DeletePost(Guid companyId, Guid vehicleId, string platform)
    {
        if (!Enum.TryParse<SocialPlatform>(platform, true, out var socialPlatform))
        {
            return BadRequest(new { error = "Invalid platform. Use 'facebook' or 'instagram'" });
        }

        try
        {
            var credentials = await _credentialsRepo.GetByCompanyIdAsync(companyId);
            if (credentials == null)
            {
                return BadRequest(new { error = "Not connected to Meta" });
            }

            var existingPost = await _postRepo.GetByVehicleAndPlatformAsync(companyId, vehicleId, socialPlatform);
            if (existingPost == null)
            {
                return NotFound(new { error = "Post not found" });
            }

            // Delete from platform
            var accessToken = credentials.PageAccessToken ?? "";
            if (socialPlatform == SocialPlatform.Facebook)
            {
                await DeleteFacebookPost(accessToken, existingPost.PostId);
            }
            // Note: Instagram API doesn't support deleting posts programmatically

            // Mark as deleted in DB
            await _postRepo.MarkAsDeletedAsync(companyId, vehicleId, socialPlatform);

            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete post for vehicle {VehicleId}", vehicleId);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get all posts for a company
    /// </summary>
    [HttpGet("company/{companyId}")]
    public async Task<IActionResult> GetCompanyPosts(Guid companyId)
    {
        var posts = await _postRepo.GetByCompanyAsync(companyId);
        return Ok(posts);
    }

    /// <summary>
    /// Bulk publish all vehicles to Facebook and/or Instagram
    /// </summary>
    [HttpPost("publish/bulk/{companyId}")]
    public async Task<IActionResult> BulkPublish(Guid companyId, [FromBody] BulkPublishRequest request, [FromServices] CarRentalDbContext dbContext)
    {
        var credentials = await _credentialsRepo.GetByCompanyIdAsync(companyId);
        if (credentials == null || string.IsNullOrEmpty(credentials.PageAccessToken))
        {
            return BadRequest(new { error = "Not connected to Meta" });
        }

        // Get company for branding info
        var company = await dbContext.Companies.FindAsync(companyId);
        if (company == null)
        {
            return NotFound(new { error = "Company not found" });
        }

        // Get all available vehicles for company
        var vehiclesQuery = dbContext.Vehicles
            .Include(v => v.VehicleModel)
                .ThenInclude(vm => vm.Model)
            .Where(v => v.CompanyId == companyId && v.Status == VehicleStatus.Available);

        // If specific vehicle IDs provided, filter to those
        if (request.VehicleIds != null && request.VehicleIds.Count > 0)
        {
            var vehicleGuids = request.VehicleIds
                .Where(id => Guid.TryParse(id, out _))
                .Select(id => Guid.Parse(id))
                .ToList();
            vehiclesQuery = vehiclesQuery.Where(v => vehicleGuids.Contains(v.Id));
        }

        var vehicles = await vehiclesQuery.ToListAsync();

        if (vehicles.Count == 0)
        {
            return Ok(new { success = true, message = "No vehicles to publish", published = 0 });
        }

        // Get already published vehicles to skip (unless force republish)
        var existingPosts = await _postRepo.GetByCompanyAsync(companyId);
        var publishedFacebookIds = existingPosts
            .Where(p => p.Platform == SocialPlatform.Facebook && p.IsActive)
            .Select(p => p.VehicleId)
            .ToHashSet();
        var publishedInstagramIds = existingPosts
            .Where(p => p.Platform == SocialPlatform.Instagram && p.IsActive)
            .Select(p => p.VehicleId)
            .ToHashSet();

        var results = new List<BulkPublishResult>();
        var publishedCount = 0;
        var skippedCount = 0;
        var errorCount = 0;

        foreach (var vehicle in vehicles)
        {
            var result = new BulkPublishResult
            {
                VehicleId = vehicle.Id.ToString(),
                VehicleName = $"{vehicle.VehicleModel?.Make} {vehicle.VehicleModel?.Model} {vehicle.VehicleModel?.Year}"
            };

            // Build caption
            var caption = BuildVehicleCaption(vehicle, company, request.IncludePrice, request.Hashtags);

            var publishRequest = new PublishVehicleRequest
            {
                VehicleId = vehicle.Id.ToString(),
                ImageUrl = vehicle.ImageUrl ?? vehicle.VehicleModel?.ImageUrl,
                Caption = caption,
                DailyRate = vehicle.VehicleModel?.DailyRate,
                IncludePrice = request.IncludePrice,
                Hashtags = request.Hashtags
            };

            // Skip if no image
            if (string.IsNullOrEmpty(publishRequest.ImageUrl))
            {
                result.Skipped = true;
                result.SkipReason = "No image";
                skippedCount++;
                results.Add(result);
                continue;
            }

            // Publish to Facebook
            if (request.Platforms.Contains("facebook", StringComparer.OrdinalIgnoreCase))
            {
                if (!request.ForceRepublish && publishedFacebookIds.Contains(vehicle.Id))
                {
                    result.FacebookSkipped = true;
                }
                else
                {
                    try
                    {
                        publishRequest.UpdateMode = request.ForceRepublish ? "republish" : "update";
                        var fbResult = await PublishToFacebook(companyId, publishRequest);
                        if (fbResult is OkObjectResult okResult)
                        {
                            result.FacebookSuccess = true;
                            result.FacebookPostId = (okResult.Value as dynamic)?.postId;
                        }
                        else
                        {
                            result.FacebookError = "Failed to publish";
                        }
                    }
                    catch (Exception ex)
                    {
                        result.FacebookError = ex.Message;
                    }
                }
            }

            // Publish to Instagram
            if (request.Platforms.Contains("instagram", StringComparer.OrdinalIgnoreCase))
            {
                if (!request.ForceRepublish && publishedInstagramIds.Contains(vehicle.Id))
                {
                    result.InstagramSkipped = true;
                }
                else
                {
                    try
                    {
                        var igResult = await PublishToInstagram(companyId, publishRequest);
                        if (igResult is OkObjectResult okResult)
                        {
                            result.InstagramSuccess = true;
                            result.InstagramPostId = (okResult.Value as dynamic)?.postId;
                        }
                        else
                        {
                            result.InstagramError = "Failed to publish";
                        }
                    }
                    catch (Exception ex)
                    {
                        result.InstagramError = ex.Message;
                    }
                }
            }

            // Count results
            if (result.FacebookSuccess || result.InstagramSuccess)
                publishedCount++;
            else if (result.FacebookSkipped && result.InstagramSkipped)
                skippedCount++;
            else if (!string.IsNullOrEmpty(result.FacebookError) || !string.IsNullOrEmpty(result.InstagramError))
                errorCount++;

            results.Add(result);

            // Rate limiting - wait between posts to avoid API limits
            await Task.Delay(request.DelayBetweenPosts ?? 1000);
        }

        _logger.LogInformation(
            "Bulk publish completed for company {CompanyId}. Published: {Published}, Skipped: {Skipped}, Errors: {Errors}",
            companyId, publishedCount, skippedCount, errorCount);

        return Ok(new
        {
            success = true,
            totalVehicles = vehicles.Count,
            published = publishedCount,
            skipped = skippedCount,
            errors = errorCount,
            results
        });
    }

    /// <summary>
    /// Get unpublished vehicles (not yet posted to social media)
    /// </summary>
    [HttpGet("unpublished/{companyId}")]
    public async Task<IActionResult> GetUnpublishedVehicles(Guid companyId, [FromServices] CarRentalDbContext dbContext)
    {
        var allVehicles = await dbContext.Vehicles
            .Include(v => v.VehicleModel)
                .ThenInclude(vm => vm.Model)
            .Where(v => v.CompanyId == companyId && v.Status == VehicleStatus.Available)
            .ToListAsync();

        var existingPosts = await _postRepo.GetByCompanyAsync(companyId);
        var publishedIds = existingPosts
            .Where(p => p.IsActive)
            .Select(p => p.VehicleId)
            .Distinct()
            .ToHashSet();

        var unpublished = allVehicles
            .Where(v => !publishedIds.Contains(v.Id))
            .Select(v => new
            {
                id = v.Id,
                name = v.VehicleModel?.Model != null 
                    ? $"{v.VehicleModel.Model.Make} {v.VehicleModel.Model.ModelName} {v.VehicleModel.Model.Year}"
                    : v.LicensePlate,
                licensePlate = v.LicensePlate,
                imageUrl = v.ImageUrl,
                dailyRate = v.VehicleModel?.DailyRate,
                hasImage = !string.IsNullOrEmpty(v.ImageUrl)
            })
            .ToList();

        return Ok(new
        {
            total = allVehicles.Count,
            published = publishedIds.Count,
            unpublished = unpublished.Count,
            vehicles = unpublished
        });
    }

    /// <summary>
    /// Build caption for vehicle post
    /// </summary>
    private string BuildVehicleCaption(Vehicle vehicle, Company company, bool includePrice, List<string>? hashtags)
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
        parts.Add($"📍 {company.CompanyName}");

        // Booking URL
        var bookingUrl = GetBookingUrl(vehicle, company);
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

    /// <summary>
    /// Get booking URL for vehicle
    /// </summary>
    private string? GetBookingUrl(Vehicle vehicle, Company company)
    {
        if (vehicle.VehicleModel?.Model == null)
            return null;

        var catalogModel = vehicle.VehicleModel.Model;
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        
        // Build query parameters
        var queryParams = new List<string>();
        
        if (catalogModel.CategoryId != null)
            queryParams.Add($"category={catalogModel.CategoryId}");
        
        if (!string.IsNullOrEmpty(catalogModel.Make))
            queryParams.Add($"make={Uri.EscapeDataString(catalogModel.Make)}");
        
        if (!string.IsNullOrEmpty(catalogModel.ModelName))
            queryParams.Add($"model={Uri.EscapeDataString(catalogModel.ModelName)}");
        
        queryParams.Add($"companyId={company.Id}");
        queryParams.Add($"startDate={today}");
        queryParams.Add($"endDate={today}");
        
        var query = string.Join("&", queryParams);
        
        // Use company subdomain if available
        if (!string.IsNullOrEmpty(company.Subdomain))
        {
            return $"https://{company.Subdomain}.aegis-rental.com/book?{query}";
        }

        // Fallback to main site
        return $"https://aegis-rental.com/book?{query}";
    }

    #region Private Methods

    private async Task<FacebookPostResult> PublishFacebookPost(string pageId, string pageAccessToken, PublishVehicleRequest request)
    {
        string endpoint;
        HttpContent content;

        if (!string.IsNullOrEmpty(request.ImageUrl))
        {
            var absoluteImageUrl = NormalizeImageUrl(request.ImageUrl);
            _logger.LogInformation("Publishing photo to Facebook. Original URL: {Original}, Normalized: {Normalized}",
                request.ImageUrl, absoluteImageUrl);

            endpoint = $"{GraphApiBaseUrl}/{pageId}/photos";
            var formData = new Dictionary<string, string>
            {
                ["url"] = absoluteImageUrl,
                ["caption"] = request.Caption,
                ["access_token"] = pageAccessToken
            };
            content = new FormUrlEncodedContent(formData);
        }
        else if (!string.IsNullOrEmpty(request.VehicleUrl))
        {
            endpoint = $"{GraphApiBaseUrl}/{pageId}/feed";
            var formData = new Dictionary<string, string>
            {
                ["message"] = request.Caption,
                ["link"] = request.VehicleUrl,
                ["access_token"] = pageAccessToken
            };
            content = new FormUrlEncodedContent(formData);
        }
        else
        {
            endpoint = $"{GraphApiBaseUrl}/{pageId}/feed";
            var formData = new Dictionary<string, string>
            {
                ["message"] = request.Caption,
                ["access_token"] = pageAccessToken
            };
            content = new FormUrlEncodedContent(formData);
        }

        var response = await _httpClient.PostAsync(endpoint, content);
        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to publish to Facebook: {Response}", responseContent);
            throw new Exception($"Failed to publish to Facebook: {responseContent}");
        }

        var result = JsonSerializer.Deserialize<JsonElement>(responseContent);
        var postId = result.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" :
                     result.TryGetProperty("post_id", out var postIdProp) ? postIdProp.GetString() ?? "" : "";

        // Get permalink
        string? permalink = null;
        try
        {
            var permalinkResponse = await _httpClient.GetAsync(
                $"{GraphApiBaseUrl}/{postId}?fields=permalink_url&access_token={pageAccessToken}");
            if (permalinkResponse.IsSuccessStatusCode)
            {
                var permalinkContent = await permalinkResponse.Content.ReadAsStringAsync();
                var permalinkResult = JsonSerializer.Deserialize<JsonElement>(permalinkContent);
                permalink = permalinkResult.TryGetProperty("permalink_url", out var pl) ? pl.GetString() : null;
            }
        }
        catch { /* Ignore permalink errors */ }

        return new FacebookPostResult { PostId = postId, Permalink = permalink };
    }

    private async Task DeleteFacebookPost(string accessToken, string postId)
    {
        var url = $"{GraphApiBaseUrl}/{postId}?access_token={accessToken}";
        var response = await _httpClient.DeleteAsync(url);

        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Failed to delete Facebook post {PostId}: {Response}", postId, content);
        }
    }

    private async Task UpdateFacebookPostText(string accessToken, string postId, string message)
    {
        var url = $"{GraphApiBaseUrl}/{postId}";
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("message", message),
            new KeyValuePair<string, string>("access_token", accessToken)
        });

        var response = await _httpClient.PostAsync(url, content);

        if (!response.IsSuccessStatusCode)
        {
            var responseContent = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Failed to update Facebook post {PostId}: {Response}", postId, responseContent);
            throw new Exception($"Failed to update post: {responseContent}");
        }
    }

    private async Task<string> CreateInstagramContainer(string igAccountId, string accessToken, string imageUrl, string caption)
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
            throw new Exception($"Failed to create Instagram container: {content}");
        }

        var result = JsonSerializer.Deserialize<JsonElement>(content);
        return result.GetProperty("id").GetString() ?? "";
    }

    private async Task<string> PublishInstagramContainer(string igAccountId, string accessToken, string containerId)
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
            throw new Exception($"Failed to publish Instagram post: {content}");
        }

        var result = JsonSerializer.Deserialize<JsonElement>(content);
        return result.GetProperty("id").GetString() ?? "";
    }

    #endregion
}

#region Request/Response Models

public class BulkStatusRequest
{
    public List<string> VehicleIds { get; set; } = new();
}

public class BulkPublishRequest
{
    /// <summary>
    /// Optional: specific vehicle IDs to publish. If empty, publishes all available vehicles.
    /// </summary>
    public List<string>? VehicleIds { get; set; }
    
    /// <summary>
    /// Platforms to publish to: "facebook", "instagram", or both
    /// </summary>
    public List<string> Platforms { get; set; } = new() { "facebook", "instagram" };
    
    /// <summary>
    /// Whether to include price in caption
    /// </summary>
    public bool IncludePrice { get; set; } = true;
    
    /// <summary>
    /// Additional hashtags to include
    /// </summary>
    public List<string>? Hashtags { get; set; }
    
    /// <summary>
    /// Force republish even if already published
    /// </summary>
    public bool ForceRepublish { get; set; } = false;
    
    /// <summary>
    /// Delay between posts in milliseconds (default 1000ms to avoid rate limits)
    /// </summary>
    public int? DelayBetweenPosts { get; set; } = 1000;
}

public class BulkPublishResult
{
    public string VehicleId { get; set; } = "";
    public string? VehicleName { get; set; }
    public bool Skipped { get; set; }
    public string? SkipReason { get; set; }
    public bool FacebookSuccess { get; set; }
    public bool FacebookSkipped { get; set; }
    public string? FacebookPostId { get; set; }
    public string? FacebookError { get; set; }
    public bool InstagramSuccess { get; set; }
    public bool InstagramSkipped { get; set; }
    public string? InstagramPostId { get; set; }
    public string? InstagramError { get; set; }
}

public class PublishVehicleRequest
{
    public string VehicleId { get; set; } = "";
    public string? ImageUrl { get; set; }
    public string Caption { get; set; } = "";
    public string? VehicleUrl { get; set; }
    public decimal? DailyRate { get; set; }
    public string? Currency { get; set; }
    public bool IncludePrice { get; set; }
    public List<string>? Hashtags { get; set; }
    /// <summary>
    /// "update" (default) or "republish"
    /// </summary>
    public string? UpdateMode { get; set; }
}

public class FacebookPostResult
{
    public string PostId { get; set; } = "";
    public string? Permalink { get; set; }
}

#endregion
