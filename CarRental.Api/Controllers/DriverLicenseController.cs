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
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using CarRental.Api.Extensions;
using Microsoft.AspNetCore.StaticFiles;

namespace CarRental.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DriverLicenseController : ControllerBase
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<DriverLicenseController> _logger;

    public DriverLicenseController(
        IWebHostEnvironment env,
        ILogger<DriverLicenseController> logger)
    {
        _env = env;
        _logger = logger;
    }

    /// <summary>
    /// Upload driver license image and save it to public/<company id>/<user id>/driverlicense.png
    /// </summary>
    [HttpPost("upload")]
    [Authorize]
    public async Task<IActionResult> UploadDriverLicense(IFormFile file)
    {
        try
        {
            // Get company ID from context (set by CompanyMiddleware)
            var companyId = HttpContext.GetCompanyIdAsGuid();
            if (!companyId.HasValue)
            {
                return BadRequest(new { message = "Company ID not found in request context" });
            }

            // Get user ID from JWT token
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new { message = "Invalid user token" });
            }

            // Validate file
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "No file uploaded" });
            }

            // Validate file type (only images)
            var allowedExtensions = new[] { ".png", ".jpg", ".jpeg", ".gif", ".webp" };
            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(fileExtension))
            {
                return BadRequest(new { message = "Invalid file type. Only images are allowed." });
            }

            // Validate file size (max 10MB)
            const long maxFileSize = 10 * 1024 * 1024; // 10MB
            if (file.Length > maxFileSize)
            {
                return BadRequest(new { message = "File size exceeds maximum limit of 10MB" });
            }

            // Create directory structure: wwwroot/public/<company id>/<user id>/
            // Match the static file serving path configured in Program.cs
            var publicDir = Path.Combine(_env.ContentRootPath, "wwwroot", "public");
            var companyDir = Path.Combine(publicDir, companyId.Value.ToString());
            var userDir = Path.Combine(companyDir, userId.ToString());

            if (!Directory.Exists(userDir))
            {
                Directory.CreateDirectory(userDir);
                _logger.LogInformation("Created directory: {Directory}", userDir);
            }

            // Save file as driverlicense.png (always use .png extension)
            var fileName = "driverlicense.png";
            var filePath = Path.Combine(userDir, fileName);

            // Delete existing file if it exists
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
                _logger.LogInformation("Deleted existing file: {FilePath}", filePath);
            }

            // Save the new file
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            _logger.LogInformation("Driver license image uploaded successfully. Company: {CompanyId}, User: {UserId}, Path: {FilePath}",
                companyId.Value, userId, filePath);
            
            // Log the actual physical path for debugging
            _logger.LogInformation("Physical file location: {FilePath}", filePath);
            _logger.LogInformation("ContentRootPath: {ContentRootPath}", _env.ContentRootPath);
            _logger.LogInformation("WebRootPath: {WebRootPath}", _env.WebRootPath);

            // Return the URL path (relative to wwwroot/public)
            var urlPath = $"/public/{companyId.Value}/{userId}/{fileName}";
            
            // Also return the full physical path for local debugging
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var fullUrl = $"{baseUrl}{urlPath}";

            return Ok(new
            {
                message = "Driver license uploaded successfully",
                filePath = urlPath,
                fullUrl = fullUrl,
                physicalPath = filePath,
                companyId = companyId.Value,
                userId = userId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading driver license image");
            return StatusCode(500, new { message = "An error occurred while uploading the file" });
        }
    }

    /// <summary>
    /// Get driver license image for the current user
    /// </summary>
    [HttpGet("image")]
    [Authorize]
    public IActionResult GetDriverLicenseImage()
    {
        try
        {
            // Get company ID from context (set by CompanyMiddleware)
            var companyId = HttpContext.GetCompanyIdAsGuid();
            if (!companyId.HasValue)
            {
                return BadRequest(new { message = "Company ID not found in request context" });
            }

            // Get user ID from JWT token
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new { message = "Invalid user token" });
            }

            // Build file path
            var publicDir = Path.Combine(_env.ContentRootPath, "wwwroot", "public");
            var companyDir = Path.Combine(publicDir, companyId.Value.ToString());
            var userDir = Path.Combine(companyDir, userId.ToString());
            var fileName = "driverlicense.png";
            var filePath = Path.Combine(userDir, fileName);

            _logger.LogInformation("Looking for driver license image at: {FilePath}", filePath);

            // Check if file exists
            if (!System.IO.File.Exists(filePath))
            {
                _logger.LogWarning("Driver license image not found at: {FilePath}", filePath);
                return NotFound(new { message = "Driver license image not found" });
            }

            // Return the file
            var fileBytes = System.IO.File.ReadAllBytes(filePath);
            var contentType = "image/png";
            
            _logger.LogInformation("Returning driver license image from: {FilePath}", filePath);
            
            return File(fileBytes, contentType, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving driver license image");
            return StatusCode(500, new { message = "An error occurred while retrieving the file" });
        }
    }
}
