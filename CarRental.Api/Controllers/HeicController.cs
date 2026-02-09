/*
 *
 * Copyright (c) 2025 Alexander Orlov.
 * 34 Middletown Ave Atlantic Highlands NJ 07716
 *
 * THIS SOFTWARE IS THE CONFIDENTIAL AND PROPRIETARY INFORMATION OF
 * Alexander Orlov. ("CONFIDENTIAL INFORMATION"). YOU SHALL NOT DISCLOSE
 * SUCH CONFIDENTIAL INFORMATION AND SHALL USE IT ONLY IN ACCORDANCE
 * WITH THE TERMS OF THE LICENSE AGREEMENT YOU ENTERED INTO WITH
 * Alexander Orlov.
 *
 * Author: Alexander Orlov
 *
 */

using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace CarRental.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HeicController : ControllerBase
{
    private readonly ILogger<HeicController> _logger;

    public HeicController(ILogger<HeicController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Convert HEIC file to JPEG/PNG (delegates to Node.js service)
    /// </summary>
    [HttpPost("convert")]
    public async Task<IActionResult> ConvertHeic(IFormFile file, [FromForm] string? quality = "85", [FromForm] string format = "image/jpeg")
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { success = false, message = "No file provided", error = "NO_FILE_PROVIDED" });
            }

            // Validate file extension
            var fileName = file.FileName?.ToLower();
            if (!IsHeicFile(fileName, file.ContentType))
            {
                return BadRequest(new { success = false, message = "File is not a HEIC/HEIF format", error = "INVALID_FILE_TYPE" });
            }

            // Call Node.js HEIC conversion service
            var convertedFile = await CallNodeHeicService(file, quality, format);

            if (convertedFile == null)
            {
                return StatusCode(422, new { success = false, message = "HEIC conversion failed", error = "CONVERSION_FAILED" });
            }

            var outputFormat = format == "image/png" ? "png" : "jpg";
            var convertedFileName = fileName?.Replace(".heic", $".{outputFormat}").Replace(".heif", $".{outputFormat}") ?? $"converted.{outputFormat}";

            return File(convertedFile, format, convertedFileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error converting HEIC file: {FileName}", file?.FileName);
            return StatusCode(500, new { success = false, message = "Internal server error during conversion", error = "SERVER_ERROR" });
        }
    }

    /// <summary>
    /// Check if server supports HEIC conversion
    /// </summary>
    [HttpGet("support")]
    public async Task<IActionResult> CheckHeicSupport()
    {
        try
        {
            // Call Node.js service to check Sharp support
            var supported = await CheckNodeHeicSupport();

            return Ok(new {
                supported = supported,
                method = "node_sharp",
                version = "via_nodejs_service"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking HEIC support");
            return Ok(new { supported = false, error = "Failed to check support" });
        }
    }

    /// <summary>
    /// Get HEIC conversion statistics
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetHeicStats()
    {
        try
        {
            // Call Node.js service for stats
            var stats = await GetNodeHeicStats();

            return Ok(stats ?? new {
                conversionsToday = 0,
                averageConversionTime = 0,
                serverLoad = "low"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting HEIC stats");
            return Ok(new { conversionsToday = 0, averageConversionTime = 0, serverLoad = "unknown" });
        }
    }

    #region Private Methods

    private static bool IsHeicFile(string? fileName, string? contentType)
    {
        if (string.IsNullOrEmpty(fileName) && string.IsNullOrEmpty(contentType))
            return false;

        // Check content type first
        var heicMimeTypes = new[] { "image/heic", "image/heif", "image/x-heic", "image/x-heif" };
        if (!string.IsNullOrEmpty(contentType) && heicMimeTypes.Contains(contentType.ToLower()))
            return true;

        // Check file extension
        if (!string.IsNullOrEmpty(fileName))
        {
            var lowerFileName = fileName.ToLower();
            return lowerFileName.EndsWith(".heic") || lowerFileName.EndsWith(".heif");
        }

        return false;
    }

    private async Task<byte[]?> CallNodeHeicService(IFormFile file, string? quality, string format)
    {
        try
        {
            // Create multipart form data
            using var content = new MultipartFormDataContent();
            using var fileStream = file.OpenReadStream();
            using var streamContent = new StreamContent(fileStream);
            streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType ?? "image/heic");
            content.Add(streamContent, "file", file.FileName ?? "image.heic");
            content.Add(new StringContent(quality ?? "85"), "quality");
            content.Add(new StringContent(format), "format");

            // Call Node.js HEIC service (assuming it runs on port 3001)
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromMinutes(2); // Allow time for conversion

            var nodeServiceUrl = GetNodeServiceUrl();
            var response = await httpClient.PostAsync($"{nodeServiceUrl}/api/heic/convert", content);

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsByteArrayAsync();
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Node HEIC service returned error: {StatusCode}, {Content}", response.StatusCode, errorContent);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call Node HEIC service");
            return null;
        }
    }

    private async Task<bool> CheckNodeHeicSupport()
    {
        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);

            var nodeServiceUrl = GetNodeServiceUrl();
            var response = await httpClient.GetAsync($"{nodeServiceUrl}/api/heic/support");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                // Simple check - if we get a response, assume supported
                return content.Contains("\"supported\"") && content.Contains("true");
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private async Task<object?> GetNodeHeicStats()
    {
        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);

            var nodeServiceUrl = GetNodeServiceUrl();
            var response = await httpClient.GetAsync($"{nodeServiceUrl}/api/heic/stats");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return System.Text.Json.JsonSerializer.Deserialize<object>(content);
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private string GetNodeServiceUrl()
    {
        // Default to localhost:3001, but can be configured via environment variable
        return Environment.GetEnvironmentVariable("NODE_HEIC_SERVICE_URL") ?? "http://localhost:3001";
    }

    #endregion
}