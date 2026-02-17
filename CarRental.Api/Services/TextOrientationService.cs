/*
 * Copyright (c) 2025 Alexander Orlov.
 * 34 Middletown Ave Atlantic Highlands NJ 07716
 *
 * Text Orientation Detection Service
 * Uses Google Cloud Vision API to detect text orientation in images.
 * Returns the rotation angle needed to make text horizontal.
 */

using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace CarRental.Api.Services;

/// <summary>
/// Singleton service: detects text orientation in images using Google Cloud Vision API.
/// Returns the angle (0, 90, 180, 270) needed to rotate the image
/// so that text lines become horizontal.
/// Uses IServiceScopeFactory to resolve scoped ISettingsService.
/// </summary>
public class TextOrientationService
{
    private const string VisionApiUrl = "https://vision.googleapis.com/v1/images:annotate";
    private const string GoogleVisionApiKeySetting = "google.vision.key";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TextOrientationService> _logger;

    // API key cached on first use — avoids DB hit on every call
    private string? _cachedApiKey;
    private bool _apiKeyLoaded;

    public TextOrientationService(
        IHttpClientFactory httpClientFactory,
        IServiceScopeFactory scopeFactory,
        ILogger<TextOrientationService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Detect the rotation angle needed to make text horizontal.
    /// Returns 0 if text is already horizontal, 90/180/270 for other orientations.
    /// Returns null if detection failed (caller should fall back to simple heuristic).
    /// </summary>
    public async Task<int?> DetectTextRotationAsync(byte[] imageData)
    {
        try
        {
            var apiKey = await GetApiKeyAsync();
            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogWarning("Google Vision API key not configured (setting: {Setting})", GoogleVisionApiKeySetting);
                return null;
            }

            var base64Image = Convert.ToBase64String(imageData);

            var requestBody = new
            {
                requests = new[]
                {
                    new
                    {
                        image = new { content = base64Image },
                        features = new[]
                        {
                            new { type = "TEXT_DETECTION", maxResults = 10 }
                        }
                    }
                }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var httpClient = _httpClientFactory.CreateClient();
            var url = $"{VisionApiUrl}?key={apiKey}";

            var response = await httpClient.PostAsync(url,
                new StringContent(json, System.Text.Encoding.UTF8, "application/json"));

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Vision API returned {StatusCode}: {Error}", response.StatusCode, errorBody);
                return null;
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            var visionResponse = JsonSerializer.Deserialize<JsonElement>(responseJson);

            // Log the detected text (first annotation = full text) for debugging
            try
            {
                var firstAnnotation = visionResponse.GetProperty("responses")[0]
                    .GetProperty("textAnnotations")[0]
                    .GetProperty("description").GetString();
                var textPreview = firstAnnotation?.Length > 200 ? firstAnnotation[..200] + "..." : firstAnnotation;
                _logger.LogInformation("Vision API detected text: {Text}", textPreview);
            }
            catch { /* ignore logging errors */ }

            return AnalyzeTextOrientation(visionResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to detect text orientation via Vision API");
            return null;
        }
    }

    /// <summary>
    /// Analyze Vision API response to determine text rotation angle.
    /// Uses the bounding polygon vertices of detected text blocks to calculate
    /// the dominant text line angle.
    /// </summary>
    private int? AnalyzeTextOrientation(JsonElement response)
    {
        try
        {
            // Navigate: responses[0].textAnnotations[0].description (full text)
            //           responses[0].fullTextAnnotation.pages[0].blocks[*]
            var responses = response.GetProperty("responses");
            if (responses.GetArrayLength() == 0)
            {
                _logger.LogWarning("Vision API: empty responses array");
                return null;
            }

            var firstResponse = responses[0];

            // Check for errors
            if (firstResponse.TryGetProperty("error", out var error))
            {
                _logger.LogWarning("Vision API error: {Error}", error.GetRawText());
                return null;
            }

            // Use fullTextAnnotation for structured block-level data
            if (!firstResponse.TryGetProperty("fullTextAnnotation", out var fullText))
            {
                _logger.LogWarning("Vision API: no text detected in image");
                return null;
            }

            var pages = fullText.GetProperty("pages");
            if (pages.GetArrayLength() == 0)
                return null;

            var page = pages[0];

            // Collect angles from word-level bounding boxes
            var angles = new List<double>();

            if (page.TryGetProperty("blocks", out var blocks))
            {
                foreach (var block in blocks.EnumerateArray())
                {
                    if (!block.TryGetProperty("paragraphs", out var paragraphs))
                        continue;

                    foreach (var paragraph in paragraphs.EnumerateArray())
                    {
                        if (!paragraph.TryGetProperty("words", out var words))
                            continue;

                        foreach (var word in words.EnumerateArray())
                        {
                            if (!word.TryGetProperty("boundingBox", out var bbox))
                                continue;

                            if (!bbox.TryGetProperty("vertices", out var vertices))
                                continue;

                            var angle = CalculateTextAngle(vertices);
                            if (angle.HasValue)
                                angles.Add(angle.Value);
                        }
                    }
                }
            }

            if (angles.Count == 0)
            {
                _logger.LogWarning("Vision API: no word bounding boxes found");
                return null;
            }

            // Log first 10 individual angles for debugging
            var sample = angles.Take(10).Select(a => $"{a:F1}°");
            _logger.LogInformation("Text orientation: first angles = [{Angles}]", string.Join(", ", sample));

            // Calculate median angle to reduce outlier impact
            angles.Sort();
            var medianAngle = angles[angles.Count / 2];

            _logger.LogInformation("Text orientation analysis: {Count} words, median angle = {Angle:F1}°, min = {Min:F1}°, max = {Max:F1}°",
                angles.Count, medianAngle, angles[0], angles[^1]);

            // Snap to nearest 90° — this is the angle the text is currently at
            var textAngle = SnapToRotation(medianAngle);

            // To make text horizontal, rotate the image by the SAME angle
            // (rotating CW by textAngle undoes a CCW tilt of textAngle)
            // But we need to think of it differently:
            //   textAngle=0   → text is horizontal → no rotation needed
            //   textAngle=90  → top edge points DOWN → image needs 270° CW rotation (= 90° CCW)
            //   textAngle=180 → text is upside down  → image needs 180° rotation
            //   textAngle=270 → top edge points UP   → image needs 90° CW rotation
            var correctionAngle = textAngle == 0 ? 0 : (360 - textAngle);

            _logger.LogInformation("Text angle: {TextAngle}° → correction: {Correction}° CW",
                textAngle, correctionAngle);

            return correctionAngle;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze text orientation from Vision API response");
            return null;
        }
    }

    /// <summary>
    /// Calculate the angle of a text line from its bounding box vertices.
    /// Vertices are: [top-left, top-right, bottom-right, bottom-left].
    /// The angle of the top edge (top-left → top-right) gives the text direction.
    /// </summary>
    private static double? CalculateTextAngle(JsonElement vertices)
    {
        if (vertices.GetArrayLength() < 2)
            return null;

        var v0 = vertices[0]; // top-left
        var v1 = vertices[1]; // top-right

        if (!v0.TryGetProperty("x", out var x0Elem) || !v0.TryGetProperty("y", out var y0Elem) ||
            !v1.TryGetProperty("x", out var x1Elem) || !v1.TryGetProperty("y", out var y1Elem))
            return null;

        double x0 = x0Elem.GetDouble();
        double y0 = y0Elem.GetDouble();
        double x1 = x1Elem.GetDouble();
        double y1 = y1Elem.GetDouble();

        // Calculate angle in degrees
        var angle = Math.Atan2(y1 - y0, x1 - x0) * 180.0 / Math.PI;

        // Normalize to [0, 360)
        if (angle < 0) angle += 360;

        return angle;
    }

    /// <summary>
    /// Snap a text angle to the nearest 90° rotation.
    /// 0° = horizontal (normal), 90° = rotated CW, 180° = upside down, 270° = rotated CCW.
    /// </summary>
    private static int SnapToRotation(double angle)
    {
        // Normalize to [0, 360)
        angle = ((angle % 360) + 360) % 360;

        // Snap to nearest 90°
        if (angle < 45 || angle >= 315) return 0;
        if (angle >= 45 && angle < 135) return 90;
        if (angle >= 135 && angle < 225) return 180;
        return 270;
    }

    /// <summary>
    /// Get API key from DB, cached after first load.
    /// </summary>
    private async Task<string?> GetApiKeyAsync()
    {
        if (_apiKeyLoaded)
            return _cachedApiKey;

        using var scope = _scopeFactory.CreateScope();
        var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        _cachedApiKey = await settingsService.GetValueAsync(GoogleVisionApiKeySetting);
        _apiKeyLoaded = true;

        if (!string.IsNullOrEmpty(_cachedApiKey))
            _logger.LogInformation("Google Vision API key loaded from database");

        return _cachedApiKey;
    }
}
