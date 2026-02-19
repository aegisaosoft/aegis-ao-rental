using System.Text.Json;
using System.Web;
using CarRental.Api.DTOs;

namespace CarRental.Api.Services;

/// <summary>
/// Search car photos via SerpAPI (Google Images)
/// </summary>
public class CarsScraper : ICarsScraper
{
    private readonly HttpClient _httpClient;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<CarsScraper> _logger;

    private const string SerpApiKeySettingKey = "serpapi.apiKey";

    public CarsScraper(HttpClient httpClient, ISettingsService settingsService, ILogger<CarsScraper> logger)
    {
        _httpClient = httpClient;
        _settingsService = settingsService;
        _logger = logger;
    }

    public async Task<List<CarSearchResultDto>> SearchAsync(
        string make, string model, int maxResults,
        Action<string, int>? onProgress = null, CancellationToken ct = default)
    {
        var results = new List<CarSearchResultDto>();

        onProgress?.Invoke("searching_google", 0);

        try
        {
            var apiKey = await _settingsService.GetValueAsync(SerpApiKeySettingKey, ct);

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogError("CarsScraper: SerpAPI key not configured in settings table (key: {Key})", SerpApiKeySettingKey);
                onProgress?.Invoke("completed", 0);
                return results;
            }

            var seenUrls = new HashSet<string>();
            var query = $"2025 {make} {model} car photo exterior white background";
            var encodedQuery = HttpUtility.UrlEncode(query);

            // SerpAPI Google Images endpoint
            var url = $"https://serpapi.com/search.json?engine=google_images&q={encodedQuery}&ijn=0&num={maxResults}&api_key={apiKey}";

            _logger.LogInformation("CarsScraper: SerpAPI request for '{Query}'", query);

            var response = await _httpClient.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("CarsScraper: SerpAPI HTTP {StatusCode}: {Error}", response.StatusCode, errorBody);
                onProgress?.Invoke("completed", 0);
                return results;
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("images_results", out var imagesResults))
            {
                _logger.LogWarning("CarsScraper: SerpAPI returned no images_results");
                onProgress?.Invoke("completed", 0);
                return results;
            }

            foreach (var item in imagesResults.EnumerateArray())
            {
                if (results.Count >= maxResults) break;

                var originalUrl = item.TryGetProperty("original", out var origProp) ? origProp.GetString() : null;
                if (string.IsNullOrEmpty(originalUrl)) continue;

                // Skip Google/YouTube domains
                if (originalUrl.Contains("gstatic.com") || originalUrl.Contains("google.com") ||
                    originalUrl.Contains("youtube.com") || originalUrl.Contains("ggpht.com"))
                    continue;

                if (!seenUrls.Add(originalUrl)) continue;

                var thumbnail = item.TryGetProperty("thumbnail", out var thumbProp) ? thumbProp.GetString() : originalUrl;

                results.Add(new CarSearchResultDto
                {
                    Id = Guid.NewGuid().ToString(),
                    ThumbnailUrl = thumbnail ?? originalUrl,
                    SourceUrl = originalUrl,
                    Make = make,
                    Model = model,
                    Source = "google"
                });
            }

            onProgress?.Invoke("searching_google", results.Count);
            _logger.LogInformation("CarsScraper: SerpAPI returned {Count} images for {Make} {Model}", results.Count, make, model);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CarsScraper: SerpAPI search failed for {Make} {Model}", make, model);
        }

        onProgress?.Invoke("completed", results.Count);
        _logger.LogInformation("CarsScraper: Total {Count} images for {Make} {Model}", results.Count, make, model);
        return results;
    }
}
