/*
 * CarRental.Api - Company Meta Integration Controller
 * Copyright (c) 2025 Alexander Orlov
 * 
 * This controller provides routes at /api/companies/{companyId}/meta/*
 * to match the frontend expectations.
 */

using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CarRental.Api.Data;
using CarRental.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Api.Controllers;

/// <summary>
/// Company Meta Integration Controller
/// Routes: /api/companies/{companyId}/meta/*
/// </summary>
[ApiController]
[Route("api/companies/{companyId}/meta")]
[Authorize]
public class CompanyMetaController : ControllerBase
{
    private readonly CarRentalDbContext _context;
    private readonly ILogger<CompanyMetaController> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public CompanyMetaController(
        CarRentalDbContext context,
        ILogger<CompanyMetaController> logger,
        IHttpClientFactory httpClientFactory)
    {
        _context = context;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// Get Meta connection status for a company
    /// GET /api/companies/{companyId}/meta/status
    /// </summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(Guid companyId)
    {
        _logger.LogInformation("GetStatus called for company {CompanyId}", companyId);

        try
        {
            var credentials = await _context.CompanyMetaCredentials
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.CompanyId == companyId);

            if (credentials == null)
            {
                return Ok(new MetaStatusResponse
                {
                    IsConnected = false,
                    Status = null,
                    PageId = null,
                    PageName = null,
                    InstagramAccountId = null,
                    InstagramUsername = null,
                    TokenExpiresAt = null,
                    TokenStatus = null
                });
            }

            var tokenStatus = credentials.TokenExpiresAt > DateTime.UtcNow ? "valid" : "expired";

            return Ok(new MetaStatusResponse
            {
                IsConnected = true,
                Status = credentials.Status.ToString(),
                PageId = credentials.PageId,
                PageName = credentials.PageName,
                InstagramAccountId = credentials.InstagramAccountId,
                InstagramUsername = credentials.InstagramUsername,
                CatalogId = credentials.CatalogId,
                PixelId = credentials.PixelId,
                TokenExpiresAt = credentials.TokenExpiresAt,
                TokenStatus = tokenStatus,
                LastTokenRefresh = credentials.LastTokenRefresh
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Meta status for company {CompanyId}", companyId);
            return StatusCode(500, new { error = "Failed to get Meta status" });
        }
    }

    /// <summary>
    /// Get available Facebook pages for a company
    /// GET /api/companies/{companyId}/meta/pages
    /// </summary>
    [HttpGet("pages")]
    public async Task<IActionResult> GetPages(Guid companyId)
    {
        _logger.LogInformation("GetPages called for company {CompanyId}", companyId);

        try
        {
            var credentials = await _context.CompanyMetaCredentials
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.CompanyId == companyId);

            if (credentials == null)
            {
                return NotFound(new { error = "Meta credentials not found for this company" });
            }

            var pages = ParseAvailablePages(credentials.AvailablePages);
            return Ok(pages ?? new List<MetaPageInfo>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pages for company {CompanyId}", companyId);
            return StatusCode(500, new { error = "Failed to get pages" });
        }
    }

    /// <summary>
    /// Disconnect company from Meta
    /// POST /api/companies/{companyId}/meta/disconnect
    /// </summary>
    [HttpPost("disconnect")]
    public async Task<IActionResult> Disconnect(Guid companyId)
    {
        _logger.LogInformation("Disconnect called for company {CompanyId}", companyId);

        try
        {
            var credentials = await _context.CompanyMetaCredentials
                .FirstOrDefaultAsync(c => c.CompanyId == companyId);

            if (credentials == null)
            {
                return NotFound(new { error = "Meta credentials not found for this company" });
            }

            _context.CompanyMetaCredentials.Remove(credentials);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Company {CompanyId} disconnected from Meta", companyId);
            return Ok(new { success = true, message = "Disconnected from Meta" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disconnecting company {CompanyId} from Meta", companyId);
            return StatusCode(500, new { error = "Failed to disconnect from Meta" });
        }
    }

    /// <summary>
    /// Select a Facebook Page for the company
    /// POST /api/companies/{companyId}/meta/select-page
    /// </summary>
    [HttpPost("select-page")]
    public async Task<IActionResult> SelectPage(Guid companyId, [FromBody] SelectPageRequest request)
    {
        _logger.LogInformation("SelectPage called for company {CompanyId}, page {PageId}", companyId, request.PageId);

        try
        {
            var credentials = await _context.CompanyMetaCredentials
                .FirstOrDefaultAsync(c => c.CompanyId == companyId);

            if (credentials == null)
            {
                return NotFound(new { error = "Meta credentials not found for this company" });
            }

            var pages = ParseAvailablePages(credentials.AvailablePages);
            var selectedPage = pages?.FirstOrDefault(p => p.Id == request.PageId);

            if (selectedPage == null)
            {
                return BadRequest(new { error = "Page not found in available pages" });
            }

            credentials.PageId = selectedPage.Id;
            credentials.PageName = selectedPage.Name;
            credentials.PageAccessToken = selectedPage.AccessToken;
            credentials.Status = MetaCredentialStatus.Active;
            credentials.UpdatedAt = DateTime.UtcNow;

            if (!string.IsNullOrEmpty(selectedPage.InstagramBusinessAccountId))
            {
                credentials.InstagramAccountId = selectedPage.InstagramBusinessAccountId;
                credentials.InstagramUsername = selectedPage.InstagramUsername;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Page {PageId} selected for company {CompanyId}", request.PageId, companyId);
            return Ok(new { success = true, message = "Page selected successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error selecting page for company {CompanyId}", companyId);
            return StatusCode(500, new { error = "Failed to select page" });
        }
    }

    /// <summary>
    /// Refresh Instagram connection for a company
    /// POST /api/companies/{companyId}/meta/refresh-instagram
    /// </summary>
    [HttpPost("refresh-instagram")]
    public async Task<IActionResult> RefreshInstagram(Guid companyId)
    {
        _logger.LogInformation("RefreshInstagram called for company {CompanyId}", companyId);

        try
        {
            var credentials = await _context.CompanyMetaCredentials
                .FirstOrDefaultAsync(c => c.CompanyId == companyId);

            if (credentials == null)
            {
                return NotFound(new { error = "Meta credentials not found for this company" });
            }

            if (string.IsNullOrEmpty(credentials.PageId) || string.IsNullOrEmpty(credentials.PageAccessToken))
            {
                return BadRequest(new { error = "Page must be selected before refreshing Instagram" });
            }

            var httpClient = _httpClientFactory.CreateClient();
            var url = $"https://graph.facebook.com/v19.0/{credentials.PageId}?fields=instagram_business_account{{id,username,name,profile_picture_url,followers_count,media_count}}&access_token={credentials.PageAccessToken}";

            var response = await httpClient.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to get Instagram account: {Response}", content);
                return BadRequest(new { error = "Failed to fetch Instagram account from Facebook API" });
            }

            var result = JsonSerializer.Deserialize<InstagramPageResponse>(content, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            });

            if (result?.InstagramBusinessAccount == null)
            {
                return Ok(new
                {
                    success = false,
                    message = "No Instagram Business Account linked to this Facebook Page.",
                    instagramAccountId = (string?)null,
                    instagramUsername = (string?)null
                });
            }

            credentials.InstagramAccountId = result.InstagramBusinessAccount.Id;
            credentials.InstagramUsername = result.InstagramBusinessAccount.Username;
            credentials.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                instagramAccountId = result.InstagramBusinessAccount.Id,
                instagramUsername = result.InstagramBusinessAccount.Username,
                followersCount = result.InstagramBusinessAccount.FollowersCount,
                mediaCount = result.InstagramBusinessAccount.MediaCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing Instagram for company {CompanyId}", companyId);
            return StatusCode(500, new { error = "Failed to refresh Instagram" });
        }
    }

    /// <summary>
    /// Get auto-publish settings for a company
    /// GET /api/companies/{companyId}/meta/auto-publish
    /// </summary>
    [HttpGet("auto-publish")]
    public async Task<IActionResult> GetAutoPublishSettings(Guid companyId)
    {
        _logger.LogInformation("GetAutoPublishSettings called for company {CompanyId}", companyId);

        try
        {
            var credentials = await _context.CompanyMetaCredentials
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.CompanyId == companyId);

            if (credentials == null)
            {
                return NotFound(new { error = "Meta credentials not found for this company" });
            }

            return Ok(new AutoPublishSettingsResponse
            {
                AutoPublishFacebook = credentials.AutoPublishFacebook,
                AutoPublishInstagram = credentials.AutoPublishInstagram,
                AutoPublishIncludePrice = credentials.AutoPublishIncludePrice,
                AutoPublishHashtags = credentials.AutoPublishHashtags
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting auto-publish settings for company {CompanyId}", companyId);
            return StatusCode(500, new { error = "Failed to get auto-publish settings" });
        }
    }

    /// <summary>
    /// Update auto-publish settings for a company
    /// POST /api/companies/{companyId}/meta/auto-publish
    /// </summary>
    [HttpPost("auto-publish")]
    public async Task<IActionResult> UpdateAutoPublishSettings(Guid companyId, [FromBody] UpdateAutoPublishRequest request)
    {
        _logger.LogInformation("UpdateAutoPublishSettings called for company {CompanyId}", companyId);

        try
        {
            var credentials = await _context.CompanyMetaCredentials
                .FirstOrDefaultAsync(c => c.CompanyId == companyId);

            if (credentials == null)
            {
                return NotFound(new { error = "Meta credentials not found for this company" });
            }

            credentials.AutoPublishFacebook = request.AutoPublishFacebook;
            credentials.AutoPublishInstagram = request.AutoPublishInstagram;
            credentials.AutoPublishIncludePrice = request.IncludePrice;
            credentials.AutoPublishHashtags = request.Hashtags != null ? string.Join(",", request.Hashtags) : null;
            credentials.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Auto-publish settings updated" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating auto-publish settings for company {CompanyId}", companyId);
            return StatusCode(500, new { error = "Failed to update auto-publish settings" });
        }
    }

    /// <summary>
    /// Get deep link settings for a company
    /// GET /api/companies/{companyId}/meta/deep-links
    /// </summary>
    [HttpGet("deep-links")]
    public async Task<IActionResult> GetDeepLinkSettings(Guid companyId)
    {
        _logger.LogInformation("GetDeepLinkSettings called for company {CompanyId}", companyId);

        try
        {
            var credentials = await _context.CompanyMetaCredentials
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.CompanyId == companyId);

            if (credentials == null)
            {
                return NotFound(new { error = "Meta credentials not found for this company" });
            }

            return Ok(new DeepLinkSettingsResponse
            {
                BaseUrl = credentials.DeepLinkBaseUrl,
                VehiclePattern = credentials.DeepLinkVehiclePattern ?? "/book?model={modelId}",
                BookingPattern = credentials.DeepLinkBookingPattern ?? "/booking/{bookingId}"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting deep link settings for company {CompanyId}", companyId);
            return StatusCode(500, new { error = "Failed to get deep link settings" });
        }
    }

    /// <summary>
    /// Update deep link settings for a company
    /// POST /api/companies/{companyId}/meta/deep-links
    /// </summary>
    [HttpPost("deep-links")]
    public async Task<IActionResult> UpdateDeepLinkSettings(Guid companyId, [FromBody] UpdateDeepLinkSettingsRequest request)
    {
        _logger.LogInformation("UpdateDeepLinkSettings called for company {CompanyId}", companyId);

        try
        {
            var credentials = await _context.CompanyMetaCredentials
                .FirstOrDefaultAsync(c => c.CompanyId == companyId);

            if (credentials == null)
            {
                return NotFound(new { error = "Meta credentials not found for this company" });
            }

            credentials.DeepLinkBaseUrl = request.BaseUrl;
            credentials.DeepLinkVehiclePattern = request.VehiclePattern;
            credentials.DeepLinkBookingPattern = request.BookingPattern;
            credentials.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Deep link settings updated" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating deep link settings for company {CompanyId}", companyId);
            return StatusCode(500, new { error = "Failed to update deep link settings" });
        }
    }

    /// <summary>
    /// Get Facebook domain verification code for a company
    /// GET /api/companies/{companyId}/meta/domain-verification
    /// This endpoint is also accessible without auth for proxy to inject meta-tag
    /// </summary>
    [HttpGet("domain-verification")]
    [AllowAnonymous]
    public async Task<IActionResult> GetDomainVerification(Guid companyId)
    {
        try
        {
            var credentials = await _context.CompanyMetaCredentials
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.CompanyId == companyId);

            return Ok(new { code = credentials?.FacebookDomainVerificationCode });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting domain verification for company {CompanyId}", companyId);
            return Ok(new { code = (string?)null });
        }
    }

    /// <summary>
    /// Save Facebook domain verification code for a company
    /// POST /api/companies/{companyId}/meta/domain-verification
    /// Tenant gets this code from: Facebook Business Settings > Brand Safety > Domains
    /// Creates a minimal credentials record if one doesn't exist yet.
    /// </summary>
    [HttpPost("domain-verification")]
    public async Task<IActionResult> SaveDomainVerification(Guid companyId, [FromBody] SaveDomainVerificationRequest request)
    {
        _logger.LogInformation("SaveDomainVerification called for company {CompanyId}", companyId);

        try
        {
            var credentials = await _context.CompanyMetaCredentials
                .FirstOrDefaultAsync(c => c.CompanyId == companyId);

            // Create a minimal credentials record if one doesn't exist
            // This allows domain verification before full Meta connection
            if (credentials == null)
            {
                credentials = new CompanyMetaCredentials
                {
                    CompanyId = companyId,
                    UserAccessToken = "", // Will be populated during OAuth
                    TokenExpiresAt = DateTime.MinValue,
                    Status = MetaCredentialStatus.PendingPageSelection,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.CompanyMetaCredentials.Add(credentials);
                await _context.SaveChangesAsync();
                _logger.LogInformation(
                    "Created minimal credentials record for domain verification - company {CompanyId}", 
                    companyId);
            }

            credentials.FacebookDomainVerificationCode = request.Code?.Trim();
            credentials.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Saved Facebook domain verification code for company {CompanyId}", 
                companyId);

            return Ok(new { 
                success = true, 
                message = "Domain verification code saved. The meta tag will be automatically added to your site pages.",
                code = credentials.FacebookDomainVerificationCode
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving domain verification for company {CompanyId}", companyId);
            return StatusCode(500, new { error = "Failed to save domain verification code" });
        }
    }

    #region Helper Methods

    private static List<MetaPageInfo>? ParseAvailablePages(JsonDocument? availablePages)
    {
        if (availablePages == null)
            return null;

        try
        {
            return JsonSerializer.Deserialize<List<MetaPageInfo>>(availablePages.RootElement.GetRawText());
        }
        catch
        {
            return null;
        }
    }

    #endregion
}

/// <summary>
/// Response DTO for Meta connection status
/// </summary>
public class MetaStatusResponse
{
    public bool IsConnected { get; set; }
    public string? Status { get; set; }
    public string? PageId { get; set; }
    public string? PageName { get; set; }
    public string? InstagramAccountId { get; set; }
    public string? InstagramUsername { get; set; }
    public string? CatalogId { get; set; }
    public string? PixelId { get; set; }
    public DateTime? TokenExpiresAt { get; set; }
    public string? TokenStatus { get; set; }
    public DateTime? LastTokenRefresh { get; set; }
}

/// <summary>
/// Response DTO for auto-publish settings
/// </summary>
public class AutoPublishSettingsResponse
{
    public bool AutoPublishFacebook { get; set; }
    public bool AutoPublishInstagram { get; set; }
    public bool AutoPublishIncludePrice { get; set; }
    public string? AutoPublishHashtags { get; set; }
}

/// <summary>
/// DTO for page info from AvailablePages JSON
/// </summary>
public class MetaPageInfo
{
    [System.Text.Json.Serialization.JsonPropertyName("id")]
    public string? Id { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("name")]
    public string? Name { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("accessToken")]
    public string? AccessToken { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("instagramBusinessAccountId")]
    public string? InstagramBusinessAccountId { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("instagramUsername")]
    public string? InstagramUsername { get; set; }
}

/// <summary>
/// Request DTO for saving Facebook domain verification code
/// </summary>
public class SaveDomainVerificationRequest
{
    public string? Code { get; set; }
}
