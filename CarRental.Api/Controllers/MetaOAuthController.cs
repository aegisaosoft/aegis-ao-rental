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
using Microsoft.Extensions.Caching.Memory;
using System.Web;
using System.Text.Json;
using CarRental.Api.Models;
using CarRental.Api.Services;

namespace CarRental.Api.Controllers;

[ApiController]
[Route("api/meta/oauth")]
public class MetaOAuthController : ControllerBase
{
    private readonly IMetaOAuthService _oauthService;
    private readonly ICompanyMetaCredentialsRepository _credentialsRepo;
    private readonly ILogger<MetaOAuthController> _logger;
    private readonly IMemoryCache _cache;

    public MetaOAuthController(
        IMetaOAuthService oauthService,
        ICompanyMetaCredentialsRepository credentialsRepo,
        IMemoryCache cache,
        ILogger<MetaOAuthController> logger)
    {
        _oauthService = oauthService;
        _credentialsRepo = credentialsRepo;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Initiates OAuth flow - redirects company admin to Facebook login
    /// </summary>
    [HttpGet("connect/{companyId}")]
    [AllowAnonymous]
    public async Task<IActionResult> Connect(Guid companyId, [FromQuery] string? lang)
    {
        var origin = GetOriginFromRequest();
        var language = lang ?? GetLanguageFromRequest() ?? "en";

        var state = _oauthService.GenerateState(companyId);

        // Store origin and language in cache for later retrieval
        if (!string.IsNullOrEmpty(origin))
        {
            _cache.Set(
                $"meta_oauth_origin:{state}",
                origin,
                TimeSpan.FromMinutes(10));
        }

        _cache.Set(
            $"meta_oauth_lang:{state}",
            language,
            TimeSpan.FromMinutes(10));

        _logger.LogInformation(
            "Stored OAuth origin for company {CompanyId}: {Origin}, lang: {Language}",
            companyId, origin, language);

        var authUrl = await _oauthService.GetAuthorizationUrlAsync(state);

        _logger.LogInformation(
            "Initiating Meta OAuth for company {CompanyId}",
            companyId);

        return Redirect(authUrl);
    }

    /// <summary>
    /// OAuth callback - Facebook redirects here after user grants permission
    /// </summary>
    [HttpGet("callback")]
    [AllowAnonymous]
    public async Task<IActionResult> Callback(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error,
        [FromQuery] string? error_description)
    {
        var redirectUrl = await GetRedirectUrlAsync(state);
        var language = GetCachedLanguage(state);

        string BuildRedirect(string baseUrl, string queryParams)
        {
            var langParam = !string.IsNullOrEmpty(language) ? $"&lang={language}" : "";
            return $"{baseUrl}?{queryParams}{langParam}";
        }

        // Handle user cancelled or denied permission
        if (string.IsNullOrEmpty(code))
        {
            _logger.LogWarning("Meta OAuth callback received without code - user likely cancelled");
            return Redirect(BuildRedirect(redirectUrl, "error=authorization_cancelled"));
        }

        if (string.IsNullOrEmpty(state))
        {
            _logger.LogWarning("Meta OAuth callback received without state");
            return Redirect(BuildRedirect(redirectUrl, "error=invalid_state"));
        }

        // Handle user denied permission
        if (!string.IsNullOrEmpty(error))
        {
            _logger.LogWarning(
                "Meta OAuth denied: {Error} - {Description}",
                error, error_description);

            return Redirect(BuildRedirect(redirectUrl, $"error={HttpUtility.UrlEncode(error_description ?? error)}"));
        }

        // Validate state and extract companyId
        var companyId = _oauthService.ValidateAndExtractCompanyId(state);
        if (companyId == null)
        {
            _logger.LogWarning("Invalid OAuth state: {State}", state);
            return Redirect(BuildRedirect(redirectUrl, "error=invalid_state"));
        }

        try
        {
            // Exchange code for tokens
            var result = await _oauthService.ExchangeCodeAsync(code, companyId.Value);

            _logger.LogInformation(
                "Meta OAuth completed for company {CompanyId}. PageId: {PageId}",
                companyId, result.PageId);

            // Clean up cached data
            _cache.Remove($"meta_oauth_origin:{state}");
            _cache.Remove($"meta_oauth_lang:{state}");

            return Redirect(BuildRedirect(redirectUrl, $"success=true&company={companyId}"));
        }
        catch (MetaOAuthException ex)
        {
            _logger.LogError(ex,
                "Meta OAuth failed for company {CompanyId}: {Error}",
                companyId, ex.Message);

            return Redirect(BuildRedirect(redirectUrl, $"error={HttpUtility.UrlEncode(ex.Message)}"));
        }
    }

    /// <summary>
    /// Disconnects company from Meta (revokes access)
    /// </summary>
    [HttpPost("disconnect/{companyId}")]
    [Authorize]
    public async Task<IActionResult> Disconnect(Guid companyId)
    {
        try
        {
            await _oauthService.RevokeAccessAsync(companyId);

            _logger.LogInformation(
                "Disconnected company {CompanyId} from Meta",
                companyId);

            return Ok(new { message = "Disconnected successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to disconnect company {CompanyId} from Meta",
                companyId);

            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Returns current Meta connection status for company
    /// </summary>
    [HttpGet("status/{companyId}")]
    [Authorize]
    public async Task<IActionResult> GetStatus(Guid companyId)
    {
        var credentials = await _credentialsRepo.GetByCompanyIdAsync(companyId);

        if (credentials == null)
        {
            return Ok(new MetaConnectionStatus
            {
                IsConnected = false
            });
        }

        return Ok(new MetaConnectionStatus
        {
            IsConnected = true,
            PageId = credentials.PageId,
            PageName = credentials.PageName,
            CatalogId = credentials.CatalogId,
            PixelId = credentials.PixelId,
            ConnectedAt = credentials.CreatedAt,
            TokenExpiresAt = credentials.TokenExpiresAt,
            TokenStatus = credentials.TokenExpiresAt > DateTime.UtcNow
                ? "valid"
                : "expired",
            InstagramAccountId = credentials.InstagramAccountId,
            InstagramUsername = credentials.InstagramUsername
        });
    }

    /// <summary>
    /// Refreshes Instagram Business Account info for company
    /// </summary>
    [HttpPost("refresh-instagram/{companyId}")]
    [Authorize]
    public async Task<IActionResult> RefreshInstagram(Guid companyId)
    {
        try
        {
            var credentials = await _credentialsRepo.GetByCompanyIdAsync(companyId);

            if (credentials == null || string.IsNullOrEmpty(credentials.PageId))
            {
                return BadRequest(new { error = "Company not connected to Meta or no page selected" });
            }

            // Fetch Instagram Business Account from Facebook Page
            using var httpClient = new HttpClient();
            var url = $"https://graph.facebook.com/v19.0/{credentials.PageId}?fields=instagram_business_account{{id,username,name,profile_picture_url,followers_count,media_count}}&access_token={credentials.PageAccessToken}";

            var response = await httpClient.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to get Instagram account: {Response}", content);
                return BadRequest(new { error = "Failed to fetch Instagram account from Facebook API" });
            }

            var result = JsonSerializer.Deserialize<InstagramPageResponse>(content);

            if (result?.InstagramBusinessAccount == null)
            {
                return Ok(new
                {
                    success = false,
                    message = "No Instagram Business Account linked to this Facebook Page. Please link your Instagram Business account to your Facebook Page first.",
                    instagramAccountId = (string?)null,
                    instagramUsername = (string?)null
                });
            }

            // Update credentials
            credentials.InstagramAccountId = result.InstagramBusinessAccount.Id;
            credentials.InstagramUsername = result.InstagramBusinessAccount.Username;
            await _credentialsRepo.UpdateAsync(credentials);

            _logger.LogInformation(
                "Refreshed Instagram for company {CompanyId}: @{Username} (ID: {Id})",
                companyId, result.InstagramBusinessAccount.Username, result.InstagramBusinessAccount.Id);

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
            _logger.LogError(ex, "Failed to refresh Instagram for company {CompanyId}", companyId);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Gets a list of user's Facebook Pages (for page selection UI)
    /// </summary>
    [HttpGet("pages/{companyId}")]
    [Authorize]
    public async Task<IActionResult> GetPages(Guid companyId)
    {
        var credentials = await _credentialsRepo.GetByCompanyIdAsync(companyId);

        if (credentials == null)
        {
            return NotFound(new { error = "Company not connected to Meta" });
        }

        return Ok(new
        {
            pages = credentials.AvailablePages,
            selectedPageId = credentials.PageId
        });
    }

    /// <summary>
    /// Selects a Facebook Page for the company
    /// </summary>
    [HttpPost("select-page/{companyId}")]
    [Authorize]
    public async Task<IActionResult> SelectPage(Guid companyId, [FromBody] SelectPageRequest request)
    {
        try
        {
            await _oauthService.SelectPageForCompanyAsync(companyId, request.PageId);

            return Ok(new { message = "Page selected successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to select page for company {CompanyId}",
                companyId);

            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Manually refresh the access token
    /// </summary>
    [HttpPost("refresh-token/{companyId}")]
    [Authorize]
    public async Task<IActionResult> RefreshToken(Guid companyId)
    {
        try
        {
            await _oauthService.RefreshLongLivedTokenAsync(companyId);

            _logger.LogInformation(
                "Manually refreshed Meta token for company {CompanyId}",
                companyId);

            return Ok(new { message = "Token refreshed successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to refresh token for company {CompanyId}",
                companyId);

            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get auto-publish settings for company
    /// </summary>
    [HttpGet("auto-publish/{companyId}")]
    [Authorize]
    public async Task<IActionResult> GetAutoPublishSettings(Guid companyId)
    {
        var credentials = await _credentialsRepo.GetByCompanyIdAsync(companyId);
        if (credentials == null)
        {
            return NotFound(new { error = "Company not connected to Meta" });
        }

        List<string>? hashtags = null;
        if (!string.IsNullOrEmpty(credentials.AutoPublishHashtags))
        {
            try
            {
                hashtags = System.Text.Json.JsonSerializer.Deserialize<List<string>>(credentials.AutoPublishHashtags);
            }
            catch { /* ignore */ }
        }

        return Ok(new
        {
            autoPublishFacebook = credentials.AutoPublishFacebook,
            autoPublishInstagram = credentials.AutoPublishInstagram,
            includePrice = credentials.AutoPublishIncludePrice,
            hashtags = hashtags
        });
    }

    /// <summary>
    /// Update auto-publish settings for company
    /// </summary>
    [HttpPost("auto-publish/{companyId}")]
    [Authorize]
    public async Task<IActionResult> UpdateAutoPublishSettings(Guid companyId, [FromBody] UpdateAutoPublishRequest request)
    {
        var credentials = await _credentialsRepo.GetByCompanyIdAsync(companyId);
        if (credentials == null)
        {
            return NotFound(new { error = "Company not connected to Meta" });
        }

        credentials.AutoPublishFacebook = request.AutoPublishFacebook;
        credentials.AutoPublishInstagram = request.AutoPublishInstagram;
        credentials.AutoPublishIncludePrice = request.IncludePrice;
        credentials.AutoPublishHashtags = request.Hashtags != null
            ? System.Text.Json.JsonSerializer.Serialize(request.Hashtags)
            : null;

        await _credentialsRepo.UpdateAsync(credentials);

        _logger.LogInformation(
            "Updated auto-publish settings for company {CompanyId}: FB={Facebook}, IG={Instagram}",
            companyId, request.AutoPublishFacebook, request.AutoPublishInstagram);

        return Ok(new { message = "Auto-publish settings updated" });
    }

    /// <summary>
    /// Get deep link settings for company
    /// </summary>
    [HttpGet("deep-links/{companyId}")]
    [Authorize]
    public async Task<IActionResult> GetDeepLinkSettings(Guid companyId)
    {
        var credentials = await _credentialsRepo.GetByCompanyIdAsync(companyId);
        if (credentials == null)
        {
            return NotFound(new { error = "Company not connected to Meta" });
        }

        return Ok(new DeepLinkSettingsResponse
        {
            BaseUrl = credentials.DeepLinkBaseUrl,
            VehiclePattern = credentials.DeepLinkVehiclePattern ?? "/book?modelId={modelId}",
            BookingPattern = credentials.DeepLinkBookingPattern ?? "/booking/{bookingId}",
            PreviewUrl = GeneratePreviewUrl(credentials, companyId)
        });
    }

    /// <summary>
    /// Update deep link settings for company
    /// </summary>
    [HttpPost("deep-links/{companyId}")]
    [Authorize]
    public async Task<IActionResult> UpdateDeepLinkSettings(Guid companyId, [FromBody] UpdateDeepLinkSettingsRequest request)
    {
        var credentials = await _credentialsRepo.GetByCompanyIdAsync(companyId);
        if (credentials == null)
        {
            return NotFound(new { error = "Company not connected to Meta" });
        }

        // Validate base URL format
        if (!string.IsNullOrEmpty(request.BaseUrl))
        {
            if (!Uri.TryCreate(request.BaseUrl, UriKind.Absolute, out var uri) || 
                (uri.Scheme != "http" && uri.Scheme != "https"))
            {
                return BadRequest(new { error = "Invalid base URL format. Must be a valid HTTP/HTTPS URL." });
            }
            credentials.DeepLinkBaseUrl = request.BaseUrl.TrimEnd('/');
        }
        else
        {
            credentials.DeepLinkBaseUrl = null;
        }

        // Validate vehicle pattern has required placeholder
        if (!string.IsNullOrEmpty(request.VehiclePattern))
        {
            if (!request.VehiclePattern.StartsWith("/"))
            {
                return BadRequest(new { error = "Vehicle pattern must start with /" });
            }
            credentials.DeepLinkVehiclePattern = request.VehiclePattern;
        }
        else
        {
            credentials.DeepLinkVehiclePattern = null;
        }

        // Validate booking pattern
        if (!string.IsNullOrEmpty(request.BookingPattern))
        {
            if (!request.BookingPattern.StartsWith("/"))
            {
                return BadRequest(new { error = "Booking pattern must start with /" });
            }
            credentials.DeepLinkBookingPattern = request.BookingPattern;
        }
        else
        {
            credentials.DeepLinkBookingPattern = null;
        }

        await _credentialsRepo.UpdateAsync(credentials);

        _logger.LogInformation(
            "Updated deep link settings for company {CompanyId}: BaseUrl={BaseUrl}",
            companyId, credentials.DeepLinkBaseUrl);

        return Ok(new { 
            message = "Deep link settings updated",
            previewUrl = GeneratePreviewUrl(credentials, companyId)
        });
    }

    private string GeneratePreviewUrl(CompanyMetaCredentials credentials, Guid companyId)
    {
        var baseUrl = credentials.DeepLinkBaseUrl ?? "https://{subdomain}.aegis-rental.com";
        var pattern = credentials.DeepLinkVehiclePattern ?? "/book?modelId={modelId}";
        
        // Replace placeholders with example values
        var exampleUrl = pattern
            .Replace("{modelId}", "example-model-id")
            .Replace("{vehicleId}", "example-vehicle-id")
            .Replace("{make}", "Toyota")
            .Replace("{model}", "Camry")
            .Replace("{companyId}", companyId.ToString())
            .Replace("{category}", "sedan");
        
        return $"{baseUrl}{exampleUrl}";
    }

    #region Private Helper Methods

    private string? GetOriginFromRequest()
    {
        // Try X-Forwarded-Host first
        var forwardedHost = Request.Headers["X-Forwarded-Host"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedHost))
        {
            var protocol = forwardedHost.Contains("localhost") ? "https" : "https";
            return $"{protocol}://{forwardedHost}";
        }

        // Try Referer header
        var referer = Request.Headers["Referer"].FirstOrDefault();
        if (!string.IsNullOrEmpty(referer))
        {
            try
            {
                var uri = new Uri(referer);
                return $"{uri.Scheme}://{uri.Authority}";
            }
            catch
            {
                // Invalid referer, ignore
            }
        }

        // Try Origin header
        var origin = Request.Headers["Origin"].FirstOrDefault();
        if (!string.IsNullOrEmpty(origin))
        {
            return origin;
        }

        return null;
    }

    private async Task<string> GetRedirectUrlAsync(string? state)
    {
        if (!string.IsNullOrEmpty(state))
        {
            if (_cache.TryGetValue($"meta_oauth_origin:{state}", out string? cachedOrigin)
                && !string.IsNullOrEmpty(cachedOrigin))
            {
                _logger.LogInformation("Using cached origin for redirect: {Origin}", cachedOrigin);
                return $"{cachedOrigin}/admin/social";
            }
        }

        var settings = await _oauthService.GetSettingsAsync();
        return settings.FrontendRedirectUrl;
    }

    private string? GetCachedLanguage(string? state)
    {
        if (!string.IsNullOrEmpty(state))
        {
            if (_cache.TryGetValue($"meta_oauth_lang:{state}", out string? language))
            {
                return language;
            }
        }
        return null;
    }

    private string? GetLanguageFromRequest()
    {
        var acceptLang = Request.Headers["Accept-Language"].FirstOrDefault();
        if (!string.IsNullOrEmpty(acceptLang))
        {
            var lang = acceptLang.Split(',')[0].Split('-')[0].Split(';')[0].Trim().ToLower();
            if (lang.Length == 2)
            {
                return lang;
            }
        }
        return null;
    }

    #endregion
}

public class SelectPageRequest
{
    public string PageId { get; set; } = "";
}

public class UpdateAutoPublishRequest
{
    public bool AutoPublishFacebook { get; set; }
    public bool AutoPublishInstagram { get; set; }
    public bool IncludePrice { get; set; } = true;
    public List<string>? Hashtags { get; set; }
}

public class MetaConnectionStatus
{
    [System.Text.Json.Serialization.JsonPropertyName("isConnected")]
    public bool IsConnected { get; set; } = false;

    public string? PageId { get; set; }
    public string? PageName { get; set; }
    public string? CatalogId { get; set; }
    public string? PixelId { get; set; }
    public DateTime? ConnectedAt { get; set; }
    public DateTime? TokenExpiresAt { get; set; }
    public string? TokenStatus { get; set; }
    public string? InstagramAccountId { get; set; }
    public string? InstagramUsername { get; set; }
}

public class InstagramPageResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("instagram_business_account")]
    public InstagramAccountInfo? InstagramBusinessAccount { get; set; }
}

public class InstagramAccountInfo
{
    [System.Text.Json.Serialization.JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [System.Text.Json.Serialization.JsonPropertyName("username")]
    public string? Username { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("name")]
    public string? Name { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("profile_picture_url")]
    public string? ProfilePictureUrl { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("followers_count")]
    public int? FollowersCount { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("media_count")]
    public int? MediaCount { get; set; }
}

public class DeepLinkSettingsResponse
{
    /// <summary>
    /// Base URL for deep links (e.g., https://mycompany.aegis-rental.com)
    /// </summary>
    public string? BaseUrl { get; set; }
    
    /// <summary>
    /// URL pattern for vehicle/model pages
    /// Available placeholders: {modelId}, {vehicleId}, {make}, {model}, {companyId}, {category}
    /// </summary>
    public string VehiclePattern { get; set; } = "/book?modelId={modelId}";
    
    /// <summary>
    /// URL pattern for booking pages
    /// Available placeholders: {bookingId}, {companyId}
    /// </summary>
    public string BookingPattern { get; set; } = "/booking/{bookingId}";
    
    /// <summary>
    /// Preview URL showing how the deep link will look
    /// </summary>
    public string? PreviewUrl { get; set; }
}

public class UpdateDeepLinkSettingsRequest
{
    /// <summary>
    /// Base URL for deep links. Leave empty to use default subdomain URL.
    /// Example: https://mycompany.aegis-rental.com or https://custom-domain.com
    /// </summary>
    public string? BaseUrl { get; set; }
    
    /// <summary>
    /// URL pattern for vehicle pages. Must start with /
    /// Available placeholders: {modelId}, {vehicleId}, {make}, {model}, {companyId}, {category}
    /// Example: /book?modelId={modelId}&make={make}
    /// </summary>
    public string? VehiclePattern { get; set; }
    
    /// <summary>
    /// URL pattern for booking pages. Must start with /
    /// Available placeholders: {bookingId}, {companyId}
    /// Example: /booking/{bookingId}
    /// </summary>
    public string? BookingPattern { get; set; }
}
