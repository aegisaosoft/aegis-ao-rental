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

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;
using CarRental.Api.Models;

namespace CarRental.Api.Services;

public interface IMetaOAuthService
{
    string GenerateState(Guid companyId);
    Guid? ValidateAndExtractCompanyId(string state);
    string GetAuthorizationUrl(string state);
    Task<OAuthExchangeResult> ExchangeCodeAsync(string code, Guid companyId);
    Task<IEnumerable<FacebookPage>> GetUserPagesAsync(string userAccessToken);
    Task SelectPageForCompanyAsync(Guid companyId, string pageId);
    Task RevokeAccessAsync(Guid companyId);
    Task<string> RefreshLongLivedTokenAsync(Guid companyId);
}

public class MetaOAuthService : IMetaOAuthService
{
    private readonly HttpClient _httpClient;
    private readonly MetaOAuthSettings _settings;
    private readonly ICompanyMetaCredentialsRepository _credentialsRepo;
    private readonly IMemoryCache _stateCache;
    private readonly ILogger<MetaOAuthService> _logger;
    private const string GraphApiBaseUrl = "https://graph.facebook.com/v19.0";

    // Required permissions for rental company management
    private readonly string[] _requiredScopes =
    {
        // Basic
        "email",
        "public_profile",
        
        // Facebook Pages
        "pages_show_list",
        "pages_manage_posts",
        "pages_read_engagement",
        "pages_manage_metadata",
        "pages_messaging",
        
        // Instagram
        "instagram_basic",
        "instagram_content_publish",
        "instagram_manage_comments",
        "instagram_manage_insights",
        
        // Business
        "business_management"
    };

    public MetaOAuthService(
        HttpClient httpClient,
        IOptions<MetaOAuthSettings> settings,
        ICompanyMetaCredentialsRepository credentialsRepo,
        IMemoryCache stateCache,
        ILogger<MetaOAuthService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _credentialsRepo = credentialsRepo;
        _stateCache = stateCache;
        _logger = logger;
    }

    /// <summary>
    /// Generates a secure state parameter that encodes companyId
    /// </summary>
    public string GenerateState(Guid companyId)
    {
        var randomBytes = RandomNumberGenerator.GetBytes(32);
        var randomPart = Convert.ToBase64String(randomBytes);
        var state = $"{companyId}:{randomPart}";

        // Store in cache for validation (expires in 10 minutes)
        _stateCache.Set(
            $"meta_oauth_state:{state}",
            companyId,
            TimeSpan.FromMinutes(10));

        return Convert.ToBase64String(Encoding.UTF8.GetBytes(state));
    }

    /// <summary>
    /// Validates state and extracts companyId
    /// </summary>
    public Guid? ValidateAndExtractCompanyId(string state)
    {
        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(state));
            var cacheKey = $"meta_oauth_state:{decoded}";

            if (_stateCache.TryGetValue(cacheKey, out Guid companyId))
            {
                _stateCache.Remove(cacheKey); // One-time use
                return companyId;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Builds Facebook OAuth authorization URL
    /// </summary>
    public string GetAuthorizationUrl(string state)
    {
        var scopes = string.Join(",", _requiredScopes);

        var queryParams = new Dictionary<string, string>
        {
            ["client_id"] = _settings.AppId,
            ["redirect_uri"] = _settings.RedirectUri,
            ["state"] = state,
            ["scope"] = scopes,
            ["response_type"] = "code"
        };

        var queryString = string.Join("&",
            queryParams.Select(kvp =>
                $"{kvp.Key}={HttpUtility.UrlEncode(kvp.Value)}"));

        return $"https://www.facebook.com/v19.0/dialog/oauth?{queryString}";
    }

    /// <summary>
    /// Exchanges authorization code for access tokens
    /// </summary>
    public async Task<OAuthExchangeResult> ExchangeCodeAsync(string code, Guid companyId)
    {
        // Step 1: Exchange code for short-lived token
        var shortLivedToken = await GetShortLivedTokenAsync(code);

        // Step 2: Exchange for long-lived token (60 days)
        var longLivedToken = await GetLongLivedTokenAsync(shortLivedToken.AccessToken);

        // Step 3: Get user's Facebook Pages
        var pages = await GetUserPagesAsync(longLivedToken.AccessToken);
        var pagesList = pages.ToList();

        // Step 4: Save credentials
        var credentials = new CompanyMetaCredentials
        {
            CompanyId = companyId,
            UserAccessToken = longLivedToken.AccessToken,
            TokenExpiresAt = DateTime.UtcNow.AddSeconds(longLivedToken.ExpiresIn),
            CreatedAt = DateTime.UtcNow,
            Status = MetaCredentialStatus.PendingPageSelection,
            AvailablePages = JsonDocument.Parse(JsonSerializer.Serialize(pagesList))
        };

        // If only one page, auto-select it
        if (pagesList.Count == 1)
        {
            await SetupPageForCredentials(credentials, pagesList[0]);
        }

        // Set status to Active
        credentials.Status = MetaCredentialStatus.Active;

        await _credentialsRepo.SaveAsync(credentials);

        return new OAuthExchangeResult
        {
            CompanyId = companyId,
            PageId = credentials.PageId,
            PageName = credentials.PageName,
            AvailablePages = pagesList
        };
    }

    /// <summary>
    /// Gets all Facebook Pages the user manages
    /// </summary>
    public async Task<IEnumerable<FacebookPage>> GetUserPagesAsync(string userAccessToken)
    {
        var url = $"{GraphApiBaseUrl}/me/accounts?access_token={userAccessToken}&fields=id,name,access_token,category";

        var response = await _httpClient.GetAsync(url);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            var error = JsonSerializer.Deserialize<MetaErrorResponse>(content);
            throw new MetaOAuthException($"Failed to get pages: {error?.Error?.Message}");
        }

        var result = JsonSerializer.Deserialize<FacebookPagesResponse>(content);
        return result?.Data ?? Enumerable.Empty<FacebookPage>();
    }

    /// <summary>
    /// Selects a specific Page for company
    /// </summary>
    public async Task SelectPageForCompanyAsync(Guid companyId, string pageId)
    {
        var credentials = await _credentialsRepo.GetByCompanyIdAsync(companyId)
            ?? throw new MetaOAuthException("Company is not connected to Meta");

        var pagesJson = credentials.AvailablePages?.RootElement.GetRawText() ?? "[]";
        var pages = JsonSerializer.Deserialize<List<FacebookPage>>(pagesJson)
            ?? new List<FacebookPage>();

        var selectedPage = pages.FirstOrDefault(p => p.Id == pageId)
            ?? throw new MetaOAuthException("Page not found in available pages");

        await SetupPageForCredentials(credentials, selectedPage);
        await _credentialsRepo.UpdateAsync(credentials);
    }

    /// <summary>
    /// Sets up page details on credentials
    /// </summary>
    private async Task SetupPageForCredentials(CompanyMetaCredentials credentials, FacebookPage page)
    {
        credentials.PageId = page.Id;
        credentials.PageName = page.Name;
        credentials.PageAccessToken = page.AccessToken;
        credentials.Status = MetaCredentialStatus.Active;

        // Try to get Instagram Business Account linked to this page
        try
        {
            var igAccount = await GetInstagramBusinessAccountAsync(page.Id, page.AccessToken);
            if (igAccount != null)
            {
                credentials.InstagramAccountId = igAccount.Id;
                credentials.InstagramUsername = igAccount.Username;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get Instagram account for page {PageId}", page.Id);
        }
    }

    /// <summary>
    /// Gets Instagram Business Account linked to a Facebook Page
    /// </summary>
    private async Task<InstagramBusinessAccount?> GetInstagramBusinessAccountAsync(string pageId, string pageAccessToken)
    {
        var url = $"{GraphApiBaseUrl}/{pageId}?fields=instagram_business_account{{id,username,name}}&access_token={pageAccessToken}";

        var response = await _httpClient.GetAsync(url);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var result = JsonSerializer.Deserialize<InstagramBusinessAccountResponse>(content);
        return result?.InstagramBusinessAccount;
    }

    /// <summary>
    /// Revokes Meta access for company
    /// </summary>
    public async Task RevokeAccessAsync(Guid companyId)
    {
        var credentials = await _credentialsRepo.GetByCompanyIdAsync(companyId);

        if (credentials != null)
        {
            // Revoke token at Facebook
            try
            {
                var url = $"{GraphApiBaseUrl}/me/permissions?access_token={credentials.UserAccessToken}";
                await _httpClient.DeleteAsync(url);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to revoke token at Facebook");
            }

            // Delete local credentials
            await _credentialsRepo.DeleteAsync(companyId);
        }
    }

    /// <summary>
    /// Refreshes long-lived token before expiration
    /// </summary>
    public async Task<string> RefreshLongLivedTokenAsync(Guid companyId)
    {
        var credentials = await _credentialsRepo.GetByCompanyIdAsync(companyId)
            ?? throw new MetaOAuthException("Company is not connected to Meta");

        var newToken = await GetLongLivedTokenAsync(credentials.UserAccessToken);

        credentials.UserAccessToken = newToken.AccessToken;
        credentials.TokenExpiresAt = DateTime.UtcNow.AddSeconds(newToken.ExpiresIn);
        credentials.LastTokenRefresh = DateTime.UtcNow;

        await _credentialsRepo.UpdateAsync(credentials);

        return newToken.AccessToken;
    }

    #region Private Helper Methods

    private async Task<ShortLivedTokenResponse> GetShortLivedTokenAsync(string code)
    {
        var url = $"{GraphApiBaseUrl}/oauth/access_token";
        var queryParams = new Dictionary<string, string>
        {
            ["client_id"] = _settings.AppId,
            ["client_secret"] = _settings.AppSecret,
            ["redirect_uri"] = _settings.RedirectUri,
            ["code"] = code
        };

        var queryString = string.Join("&",
            queryParams.Select(kvp => $"{kvp.Key}={HttpUtility.UrlEncode(kvp.Value)}"));

        var response = await _httpClient.GetAsync($"{url}?{queryString}");
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            var error = JsonSerializer.Deserialize<MetaErrorResponse>(content);
            throw new MetaOAuthException($"Failed to exchange code: {error?.Error?.Message}");
        }

        return JsonSerializer.Deserialize<ShortLivedTokenResponse>(content)!;
    }

    private async Task<LongLivedTokenResponse> GetLongLivedTokenAsync(string shortLivedToken)
    {
        var url = $"{GraphApiBaseUrl}/oauth/access_token";
        var queryParams = new Dictionary<string, string>
        {
            ["grant_type"] = "fb_exchange_token",
            ["client_id"] = _settings.AppId,
            ["client_secret"] = _settings.AppSecret,
            ["fb_exchange_token"] = shortLivedToken
        };

        var queryString = string.Join("&",
            queryParams.Select(kvp => $"{kvp.Key}={HttpUtility.UrlEncode(kvp.Value)}"));

        var response = await _httpClient.GetAsync($"{url}?{queryString}");
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            var error = JsonSerializer.Deserialize<MetaErrorResponse>(content);
            throw new MetaOAuthException($"Failed to get long-lived token: {error?.Error?.Message}");
        }

        return JsonSerializer.Deserialize<LongLivedTokenResponse>(content)!;
    }

    #endregion
}

#region Response Models

public class ShortLivedTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = "";

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = "";
}

public class LongLivedTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = "";

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = "";

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }
}

public class FacebookPagesResponse
{
    [JsonPropertyName("data")]
    public List<FacebookPage>? Data { get; set; }
}

public class FacebookPage
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = "";

    [JsonPropertyName("category")]
    public string? Category { get; set; }
}

public class OAuthExchangeResult
{
    public Guid CompanyId { get; set; }
    public string? PageId { get; set; }
    public string? PageName { get; set; }
    public List<FacebookPage> AvailablePages { get; set; } = new();
}

public class InstagramBusinessAccountResponse
{
    [JsonPropertyName("instagram_business_account")]
    public InstagramBusinessAccount? InstagramBusinessAccount { get; set; }
}

public class InstagramBusinessAccount
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("profile_picture_url")]
    public string? ProfilePictureUrl { get; set; }

    [JsonPropertyName("followers_count")]
    public int? FollowersCount { get; set; }

    [JsonPropertyName("media_count")]
    public int? MediaCount { get; set; }
}

public class MetaErrorResponse
{
    [JsonPropertyName("error")]
    public MetaError? Error { get; set; }
}

public class MetaError
{
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("code")]
    public int? Code { get; set; }
}

#endregion

#region Exceptions

public class MetaOAuthException : Exception
{
    public MetaOAuthException(string message) : base(message) { }
}

#endregion
