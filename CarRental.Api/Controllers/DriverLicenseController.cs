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

            // Create directory structure: public/<company id>/<user id>/
            var publicDir = Path.Combine(_env.WebRootPath ?? _env.ContentRootPath, "public");
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

            // Return the URL path (relative to wwwroot/public)
            var urlPath = $"/public/{companyId.Value}/{userId}/{fileName}";

            return Ok(new
            {
                message = "Driver license uploaded successfully",
                filePath = urlPath,
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
}
