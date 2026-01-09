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
using CarRental.Api.Services;
using System.Collections.Generic;

namespace CarRental.Api.Controllers;

[ApiController]
[Route("{containerName}")]
public class StaticFilesController : ControllerBase
{
    private readonly IAzureBlobStorageService _blobStorageService;
    private readonly ILogger<StaticFilesController> _logger;

    public StaticFilesController(
        IAzureBlobStorageService blobStorageService,
        ILogger<StaticFilesController> logger)
    {
        _blobStorageService = blobStorageService;
        _logger = logger;
    }

    // Valid container names
    private static readonly HashSet<string> ValidContainers = new()
    {
        "agreements", "companies", "customer-licenses", "models"
    };

    /// <summary>
    /// Determine content type from file extension
    /// </summary>
    private static string GetContentType(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".svg" => "image/svg+xml",
            ".pdf" => "application/pdf",
            ".mp4" => "video/mp4",
            ".webm" => "video/webm",
            ".mov" => "video/quicktime",
            ".avi" => "video/x-msvideo",
            ".wmv" => "video/x-ms-wmv",
            ".flv" => "video/x-flv",
            ".mkv" => "video/x-matroska",
            _ => "application/octet-stream"
        };
    }

    /// <summary>
    /// Serves files from Azure Blob Storage for different container types
    /// Examples:
    /// - /agreements/{companyId}/{fileName}.pdf
    /// - /companies/{companyId}/logos/{fileName}.png
    /// - /customer-licenses/{customerId}/licenses/{fileName}.jpg
    /// </summary>
    /// <param name="containerName">Container name (agreements, companies, customer-licenses, models)</param>
    /// <param name="filePath">File path within container (can include subfolders)</param>
    /// <returns>File stream</returns>
    [HttpGet("{**filePath}")]
    [ResponseCache(Duration = 3600)] // Cache for 1 hour
    public async Task<IActionResult> GetFile(string containerName, string filePath)
    {
        try
        {
            _logger.LogInformation("Requesting file: {ContainerName}/{FilePath}", containerName, filePath);

            // Validate container name
            if (!ValidContainers.Contains(containerName))
            {
                _logger.LogWarning("Invalid container requested: {ContainerName}", containerName);
                return BadRequest($"Invalid container. Valid containers: {string.Join(", ", ValidContainers)}");
            }

            // Validate file path
            if (string.IsNullOrWhiteSpace(filePath) || filePath.Contains(".."))
            {
                _logger.LogWarning("Invalid file path: {FilePath}", filePath);
                return BadRequest("Invalid file path");
            }

            // Extract file name to determine content type
            var fileName = Path.GetFileName(filePath);
            if (string.IsNullOrEmpty(fileName))
            {
                _logger.LogWarning("No file name in path: {FilePath}", filePath);
                return BadRequest("Invalid file path - no file name");
            }

            // Check if file exists in storage
            if (!await _blobStorageService.FileExistsAsync(containerName, filePath))
            {
                _logger.LogWarning("File not found: {ContainerName}/{FilePath}", containerName, filePath);
                return NotFound("File not found");
            }

            // Download file from storage
            var fileStream = await _blobStorageService.DownloadFileAsync(containerName, filePath);

            if (fileStream == null)
            {
                _logger.LogError("Failed to download file: {ContainerName}/{FilePath}", containerName, filePath);
                return StatusCode(500, "Failed to retrieve file");
            }

            // Determine content type
            var contentType = GetContentType(fileName);

            _logger.LogInformation("Successfully serving file: {ContainerName}/{FilePath} as {ContentType}",
                containerName, filePath, contentType);

            // Set CORS headers
            Response.Headers.Append("Access-Control-Allow-Origin", "*");
            Response.Headers.Append("Access-Control-Allow-Methods", "GET, HEAD, OPTIONS");

            // Return file with proper headers
            return File(fileStream, contentType, fileName, enableRangeProcessing: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error serving file {ContainerName}/{FilePath}: {ErrorMessage}",
                containerName, filePath, ex.Message);
            return StatusCode(500, "Internal server error");
        }
    }
}