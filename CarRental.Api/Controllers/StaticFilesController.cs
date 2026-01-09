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

namespace CarRental.Api.Controllers;

[ApiController]
[Route("agreements")]
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

    /// <summary>
    /// Serves PDF files from Azure Blob Storage
    /// </summary>
    /// <param name="companyId">Company ID</param>
    /// <param name="fileName">PDF file name (e.g., AGR-2025-000001.pdf)</param>
    /// <returns>PDF file stream</returns>
    [HttpGet("{companyId}/{fileName}")]
    [ResponseCache(Duration = 3600)] // Cache for 1 hour
    public async Task<IActionResult> GetAgreementPdf(string companyId, string fileName)
    {
        try
        {
            _logger.LogInformation("Requesting PDF file: {CompanyId}/{FileName}", companyId, fileName);

            // Validate file extension
            if (!fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Invalid file extension requested: {FileName}", fileName);
                return BadRequest("Only PDF files are supported");
            }

            // Validate company ID format
            if (!Guid.TryParse(companyId, out _))
            {
                _logger.LogWarning("Invalid company ID format: {CompanyId}", companyId);
                return BadRequest("Invalid company ID format");
            }

            // Check if Azure Blob Storage is configured
            if (!await _blobStorageService.IsConfiguredAsync())
            {
                _logger.LogError("Azure Blob Storage is not configured");
                return StatusCode(503, "File storage is not available");
            }

            // Build blob path: {companyId}/{fileName}
            var blobPath = $"{companyId}/{fileName}";
            const string containerName = "agreements";

            // Check if file exists
            if (!await _blobStorageService.FileExistsAsync(containerName, blobPath))
            {
                _logger.LogWarning("PDF file not found: {ContainerName}/{BlobPath}", containerName, blobPath);
                return NotFound("PDF file not found");
            }

            // Download file from Azure Blob Storage
            var fileStream = await _blobStorageService.DownloadFileAsync(containerName, blobPath);

            if (fileStream == null)
            {
                _logger.LogError("Failed to download PDF file: {ContainerName}/{BlobPath}", containerName, blobPath);
                return StatusCode(500, "Failed to retrieve PDF file");
            }

            _logger.LogInformation("Successfully serving PDF file: {CompanyId}/{FileName}", companyId, fileName);

            // Return PDF file with proper headers
            return File(fileStream, "application/pdf", fileName, enableRangeProcessing: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error serving PDF file {CompanyId}/{FileName}: {ErrorMessage}",
                companyId, fileName, ex.Message);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Serves customer-specific PDF files from Azure Blob Storage
    /// Path: /agreements/{customerId}/agreements/{dateFolder}/{fileName}
    /// </summary>
    /// <param name="customerId">Customer ID</param>
    /// <param name="dateFolder">Date folder (YYYY-MM-DD)</param>
    /// <param name="fileName">PDF file name</param>
    /// <returns>PDF file stream</returns>
    [HttpGet("{customerId}/agreements/{dateFolder}/{fileName}")]
    [ResponseCache(Duration = 3600)] // Cache for 1 hour
    public async Task<IActionResult> GetCustomerAgreementPdf(string customerId, string dateFolder, string fileName)
    {
        try
        {
            _logger.LogInformation("Requesting customer PDF file: {CustomerId}/agreements/{DateFolder}/{FileName}",
                customerId, dateFolder, fileName);

            // Validate file extension
            if (!fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Invalid file extension requested: {FileName}", fileName);
                return BadRequest("Only PDF files are supported");
            }

            // Validate customer ID format
            if (!Guid.TryParse(customerId, out _))
            {
                _logger.LogWarning("Invalid customer ID format: {CustomerId}", customerId);
                return BadRequest("Invalid customer ID format");
            }

            // Validate date folder format (YYYY-MM-DD)
            if (!System.Text.RegularExpressions.Regex.IsMatch(dateFolder, @"^\d{4}-\d{2}-\d{2}$"))
            {
                _logger.LogWarning("Invalid date folder format: {DateFolder}", dateFolder);
                return BadRequest("Invalid date folder format");
            }

            // Check if Azure Blob Storage is configured
            if (!await _blobStorageService.IsConfiguredAsync())
            {
                _logger.LogError("Azure Blob Storage is not configured");
                return StatusCode(503, "File storage is not available");
            }

            // Build blob path: {customerId}/agreements/{dateFolder}/{fileName}
            var blobPath = $"{customerId}/agreements/{dateFolder}/{fileName}";
            const string containerName = "agreements";

            // Check if file exists
            if (!await _blobStorageService.FileExistsAsync(containerName, blobPath))
            {
                _logger.LogWarning("Customer PDF file not found: {ContainerName}/{BlobPath}", containerName, blobPath);
                return NotFound("PDF file not found");
            }

            // Download file from Azure Blob Storage
            var fileStream = await _blobStorageService.DownloadFileAsync(containerName, blobPath);

            if (fileStream == null)
            {
                _logger.LogError("Failed to download customer PDF file: {ContainerName}/{BlobPath}", containerName, blobPath);
                return StatusCode(500, "Failed to retrieve PDF file");
            }

            _logger.LogInformation("Successfully serving customer PDF file: {CustomerId}/agreements/{DateFolder}/{FileName}",
                customerId, dateFolder, fileName);

            // Return PDF file with proper headers
            return File(fileStream, "application/pdf", fileName, enableRangeProcessing: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error serving customer PDF file {CustomerId}/agreements/{DateFolder}/{FileName}: {ErrorMessage}",
                customerId, dateFolder, fileName, ex.Message);
            return StatusCode(500, "Internal server error");
        }
    }
}