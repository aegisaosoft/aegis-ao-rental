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
using CarRental.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MediaController : ControllerBase
{
    private readonly IWebHostEnvironment _environment;
    private readonly CarRentalDbContext _context;
    private readonly ILogger<MediaController> _logger;

    public MediaController(
        IWebHostEnvironment environment,
        CarRentalDbContext context,
        ILogger<MediaController> logger)
    {
        _environment = environment;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Upload a video for a company
    /// </summary>
    [HttpPost("companies/{companyId}/video")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(524_288_000)] // 500 MB limit
    public async Task<ActionResult<object>> UploadCompanyVideo(Guid companyId, IFormFile video)
    {
        try
        {
            // Validate company exists
            var company = await _context.Companies.FindAsync(companyId);
            if (company == null)
                return NotFound("Company not found");

            // Validate file
            if (video == null || video.Length == 0)
                return BadRequest("No file uploaded");

            // Validate file type
            var allowedExtensions = new[] { ".mp4", ".avi", ".mov", ".wmv", ".flv", ".webm", ".mkv" };
            var fileExtension = Path.GetExtension(video.FileName).ToLowerInvariant();
            
            if (!allowedExtensions.Contains(fileExtension))
                return BadRequest($"Invalid file type. Allowed types: {string.Join(", ", allowedExtensions)}");

            // Validate file size (500 MB max)
            if (video.Length > 524_288_000)
                return BadRequest("File size exceeds 500 MB limit");

            // Delete old video if exists
            if (!string.IsNullOrEmpty(company.VideoLink))
            {
                await DeleteVideoFile(company.VideoLink);
            }

            // Create folder structure: /<YYYY-MM-DD>/<company id>/videos
            var date = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var folderPath = Path.Combine(_environment.WebRootPath, "uploads", date, companyId.ToString(), "videos");
            
            // Create directory if it doesn't exist
            Directory.CreateDirectory(folderPath);

            // Generate unique filename
            var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";
            var filePath = Path.Combine(folderPath, uniqueFileName);

            // Save file
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await video.CopyToAsync(stream);
            }

            // Generate URL path
            var videoUrl = $"/uploads/{date}/{companyId}/videos/{uniqueFileName}";

            // Update company with video link
            company.VideoLink = videoUrl;
            company.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Video uploaded successfully for company {CompanyId}: {VideoUrl}", companyId, videoUrl);

            return Ok(new
            {
                videoUrl,
                fileName = uniqueFileName,
                fileSize = video.Length,
                message = "Video uploaded successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading video for company {CompanyId}", companyId);
            return StatusCode(500, "Error uploading video");
        }
    }

    /// <summary>
    /// Delete a company's video
    /// </summary>
    [HttpDelete("companies/{companyId}/video")]
    public async Task<IActionResult> DeleteCompanyVideo(Guid companyId)
    {
        try
        {
            var company = await _context.Companies.FindAsync(companyId);
            if (company == null)
                return NotFound("Company not found");

            if (string.IsNullOrEmpty(company.VideoLink))
                return NotFound("No video found for this company");

            // Delete file
            await DeleteVideoFile(company.VideoLink);

            // Remove link from database
            company.VideoLink = null;
            company.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Video deleted successfully for company {CompanyId}", companyId);

            return Ok(new { message = "Video deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting video for company {CompanyId}", companyId);
            return StatusCode(500, "Error deleting video");
        }
    }

    /// <summary>
    /// Upload a banner image for a company
    /// </summary>
    [HttpPost("companies/{companyId}/banner")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10_485_760)] // 10 MB limit for images
    public async Task<ActionResult<object>> UploadCompanyBanner(Guid companyId, IFormFile banner)
    {
        try
        {
            var company = await _context.Companies.FindAsync(companyId);
            if (company == null)
                return NotFound("Company not found");

            if (banner == null || banner.Length == 0)
                return BadRequest("No file uploaded");

            // Validate file type
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var fileExtension = Path.GetExtension(banner.FileName).ToLowerInvariant();
            
            if (!allowedExtensions.Contains(fileExtension))
                return BadRequest($"Invalid file type. Allowed types: {string.Join(", ", allowedExtensions)}");

            // Validate file size (10 MB max)
            if (banner.Length > 10_485_760)
                return BadRequest("File size exceeds 10 MB limit");

            // Delete old banner if exists
            if (!string.IsNullOrEmpty(company.BannerLink))
            {
                await DeleteImageFile(company.BannerLink);
            }

            // Create folder structure
            var date = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var folderPath = Path.Combine(_environment.WebRootPath, "uploads", date, companyId.ToString(), "banners");
            Directory.CreateDirectory(folderPath);

            var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";
            var filePath = Path.Combine(folderPath, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await banner.CopyToAsync(stream);
            }

            var bannerUrl = $"/uploads/{date}/{companyId}/banners/{uniqueFileName}";

            company.BannerLink = bannerUrl;
            company.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                bannerUrl,
                fileName = uniqueFileName,
                fileSize = banner.Length,
                message = "Banner uploaded successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading banner for company {CompanyId}", companyId);
            return StatusCode(500, "Error uploading banner");
        }
    }

    /// <summary>
    /// Upload a logo for a company
    /// </summary>
    [HttpPost("companies/{companyId}/logo")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(5_242_880)] // 5 MB limit for logos
    public async Task<ActionResult<object>> UploadCompanyLogo(Guid companyId, IFormFile logo)
    {
        try
        {
            var company = await _context.Companies.FindAsync(companyId);
            if (company == null)
                return NotFound("Company not found");

            if (logo == null || logo.Length == 0)
                return BadRequest("No file uploaded");

            // Validate file type
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".svg", ".webp" };
            var fileExtension = Path.GetExtension(logo.FileName).ToLowerInvariant();
            
            if (!allowedExtensions.Contains(fileExtension))
                return BadRequest($"Invalid file type. Allowed types: {string.Join(", ", allowedExtensions)}");

            // Validate file size (5 MB max)
            if (logo.Length > 5_242_880)
                return BadRequest("File size exceeds 5 MB limit");

            // Delete old logo if exists
            if (!string.IsNullOrEmpty(company.LogoLink))
            {
                await DeleteImageFile(company.LogoLink);
            }

            // Create folder structure
            var date = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var folderPath = Path.Combine(_environment.WebRootPath, "uploads", date, companyId.ToString(), "logos");
            Directory.CreateDirectory(folderPath);

            var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";
            var filePath = Path.Combine(folderPath, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await logo.CopyToAsync(stream);
            }

            var logoUrl = $"/uploads/{date}/{companyId}/logos/{uniqueFileName}";

            company.LogoLink = logoUrl;
            company.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                logoUrl,
                fileName = uniqueFileName,
                fileSize = logo.Length,
                message = "Logo uploaded successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading logo for company {CompanyId}", companyId);
            return StatusCode(500, "Error uploading logo");
        }
    }

    /// <summary>
    /// Delete banner image
    /// </summary>
    [HttpDelete("companies/{companyId}/banner")]
    public async Task<IActionResult> DeleteCompanyBanner(Guid companyId)
    {
        try
        {
            var company = await _context.Companies.FindAsync(companyId);
            if (company == null)
                return NotFound("Company not found");

            if (string.IsNullOrEmpty(company.BannerLink))
                return NotFound("No banner found for this company");

            await DeleteImageFile(company.BannerLink);

            company.BannerLink = null;
            company.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Banner deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting banner for company {CompanyId}", companyId);
            return StatusCode(500, "Error deleting banner");
        }
    }

    /// <summary>
    /// Delete logo
    /// </summary>
    [HttpDelete("companies/{companyId}/logo")]
    public async Task<IActionResult> DeleteCompanyLogo(Guid companyId)
    {
        try
        {
            var company = await _context.Companies.FindAsync(companyId);
            if (company == null)
                return NotFound("Company not found");

            if (string.IsNullOrEmpty(company.LogoLink))
                return NotFound("No logo found for this company");

            await DeleteImageFile(company.LogoLink);

            company.LogoLink = null;
            company.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Logo deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting logo for company {CompanyId}", companyId);
            return StatusCode(500, "Error deleting logo");
        }
    }

    private async Task DeleteVideoFile(string videoUrl)
    {
        try
        {
            if (string.IsNullOrEmpty(videoUrl))
                return;

            // Convert URL to file path
            var filePath = Path.Combine(_environment.WebRootPath, videoUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
                _logger.LogInformation("Deleted video file: {FilePath}", filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete video file: {VideoUrl}", videoUrl);
        }
        
        await Task.CompletedTask;
    }

    private async Task DeleteImageFile(string imageUrl)
    {
        try
        {
            if (string.IsNullOrEmpty(imageUrl))
                return;

            var filePath = Path.Combine(_environment.WebRootPath, imageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
                _logger.LogInformation("Deleted image file: {FilePath}", filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete image file: {ImageUrl}", imageUrl);
        }
        
        await Task.CompletedTask;
    }

    /// <summary>
    /// Upload driver license image (front or back) for a customer
    /// </summary>
    [HttpPost("customers/{customerId}/licenses/{side}")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10_485_760)] // 10 MB limit for images
    public async Task<ActionResult<object>> UploadCustomerLicenseImage(Guid customerId, string side, IFormFile image)
    {
        try
        {
            // Validate customer exists
            var customer = await _context.Customers.FindAsync(customerId);
            if (customer == null)
                return NotFound("Customer not found");

            // Validate side parameter
            if (side != "front" && side != "back")
                return BadRequest("Side must be 'front' or 'back'");

            // Validate file
            if (image == null || image.Length == 0)
                return BadRequest("No file uploaded");

            // Validate file type
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var fileExtension = Path.GetExtension(image.FileName).ToLowerInvariant();
            
            if (!allowedExtensions.Contains(fileExtension))
                return BadRequest($"Invalid file type. Allowed types: {string.Join(", ", allowedExtensions)}");

            // Validate file size (10 MB max)
            if (image.Length > 10_485_760)
                return BadRequest("File size exceeds 10 MB limit");

            // Create folder structure: /customers/{customerId}/licenses
            var folderPath = Path.Combine(_environment.WebRootPath, "customers", customerId.ToString(), "licenses");
            Directory.CreateDirectory(folderPath);

            // Determine file extension based on image format
            var imageExtension = fileExtension; // Use original extension
            if (string.IsNullOrEmpty(imageExtension))
            {
                // Determine from content type if extension is missing
                imageExtension = image.ContentType switch
                {
                    "image/jpeg" => ".jpg",
                    "image/jpg" => ".jpg",
                    "image/png" => ".png",
                    "image/gif" => ".gif",
                    "image/webp" => ".webp",
                    _ => ".jpg" // Default to jpg
                };
            }

            // Use fixed filename: front.jpg or back.jpg (or appropriate extension)
            var fileName = $"{side}{imageExtension}";
            var filePath = Path.Combine(folderPath, fileName);

            // Delete old file if exists
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }

            // Save file
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await image.CopyToAsync(stream);
            }

            // Generate URL path
            var imageUrl = $"/customers/{customerId}/licenses/{fileName}";

            _logger.LogInformation("Driver license {Side} image uploaded successfully for customer {CustomerId}: {ImageUrl}", side, customerId, imageUrl);

            return Ok(new
            {
                imageUrl,
                fileName,
                fileSize = image.Length,
                side,
                message = $"Driver license {side} image uploaded successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading driver license {Side} image for customer {CustomerId}", side, customerId);
            return StatusCode(500, $"Error uploading driver license {side} image");
        }
    }
}

