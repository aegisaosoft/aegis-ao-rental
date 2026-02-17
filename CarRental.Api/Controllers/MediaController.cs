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
using CarRental.Api.Services;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using SkiaSharp;

namespace CarRental.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MediaController : ControllerBase
{
    private readonly IWebHostEnvironment _environment;
    private readonly CarRentalDbContext _context;
    private readonly ILogger<MediaController> _logger;
    private readonly IAzureBlobStorageService _blobStorage;
    private readonly ImageOrientationService _orientationService;
    private const string CustomerLicensesContainer = "customer-licenses";

    public MediaController(
        IWebHostEnvironment environment,
        CarRentalDbContext context,
        ILogger<MediaController> logger,
        IAzureBlobStorageService blobStorage,
        ImageOrientationService orientationService)
    {
        _environment = environment;
        _context = context;
        _logger = logger;
        _blobStorage = blobStorage;
        _orientationService = orientationService;
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

            // Determine content type
            var contentType = fileExtension switch
            {
                ".mp4" => "video/mp4",
                ".webm" => "video/webm",
                ".mov" => "video/quicktime",
                ".avi" => "video/x-msvideo",
                _ => "application/octet-stream"
            };

            // Generate unique filename
            var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";
            var blobPath = $"{companyId}/videos/{uniqueFileName}";

            // Upload to blob storage
            using var stream = video.OpenReadStream();
            var videoUrl = await _blobStorage.UploadFileAsync(stream, "companies", blobPath, contentType);
            
            _logger.LogInformation("Video uploaded to blob storage: {VideoUrl}", videoUrl);

            // Update company with video link
            company.VideoLink = videoUrl;
            company.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();


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

            // Determine content type
            var contentType = fileExtension switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                _ => "application/octet-stream"
            };

            var uniqueFileName = $"banner{fileExtension}";
            var blobPath = $"{companyId}/{uniqueFileName}";

            // Upload to blob storage
            using var stream = banner.OpenReadStream();
            var bannerUrl = await _blobStorage.UploadFileAsync(stream, "companies", blobPath, contentType);
            
            _logger.LogInformation("Banner uploaded to blob storage: {BannerUrl}", bannerUrl);

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

            // Determine content type
            var contentType = fileExtension switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".svg" => "image/svg+xml",
                ".webp" => "image/webp",
                _ => "application/octet-stream"
            };

            var uniqueFileName = $"logo{fileExtension}";
            var blobPath = $"{companyId}/{uniqueFileName}";

            // Upload to blob storage
            using var stream = logo.OpenReadStream();
            var logoUrl = await _blobStorage.UploadFileAsync(stream, "companies", blobPath, contentType);
            
            _logger.LogInformation("Logo uploaded to blob storage: {LogoUrl}", logoUrl);

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

            string filePath;
            
            // Handle /public/ paths (use ContentRootPath) and /uploads/ paths (use WebRootPath)
            if (videoUrl.StartsWith("/public/", StringComparison.OrdinalIgnoreCase))
            {
                filePath = Path.Combine(_environment.ContentRootPath, "wwwroot", videoUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            }
            else
            {
                // Legacy /uploads/ paths
                filePath = Path.Combine(_environment.WebRootPath, videoUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            }
            
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
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
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

            // Validate file type (includes HEIC/HEIF for iPhone photos)
            if (!ImageOrientationService.IsSupportedImageFile(image))
                return BadRequest("Invalid file type. Supported formats: JPEG, PNG, BMP, TIFF, WebP, GIF, HEIC, HEIF.");

            // Validate file size (20 MB max — HEIC files can be larger before conversion)
            if (image.Length > 20_971_520)
                return BadRequest("File size exceeds 20 MB limit");

            // Full pipeline: HEIC→PNG + EXIF orientation + compress ≤ 1 MB
            var processedBytes = await _orientationService.ProcessImageAsync(image);

            // After processing, image is always JPEG (compressed) or PNG (from HEIC)
            var contentType = "image/jpeg";
            var fileName = $"{side}.jpg";
            var blobPath = $"{customerId}/licenses/{fileName}";

            _logger.LogInformation("Uploading license image to blob storage: {BlobPath} ({OrigSize} → {ProcessedSize} bytes)",
                blobPath, image.Length, processedBytes.Length);

            // Upload processed image to Azure Blob Storage (or local fallback)
            using var stream = new MemoryStream(processedBytes);
            var imageUrl = await _blobStorage.UploadFileAsync(stream, CustomerLicensesContainer, blobPath, contentType);

            _logger.LogInformation("License image uploaded successfully: {ImageUrl}", imageUrl);

            // Save blob URL to database
            try
            {
                var license = await _context.CustomerLicenses.FirstOrDefaultAsync(l => l.CustomerId == customerId);
                if (license != null)
                {
                    if (side == "front")
                        license.FrontImageUrl = imageUrl;
                    else
                        license.BackImageUrl = imageUrl;

                    license.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Saved {Side} image URL to database for customer {CustomerId}: {ImageUrl}", side, customerId, imageUrl);
                }
                else
                {
                    _logger.LogWarning("No customer_licenses record found for customer {CustomerId}, URL not saved to DB", customerId);
                }
            }
            catch (Exception dbEx)
            {
                _logger.LogWarning(dbEx, "Failed to save image URL to database (non-critical)");
            }

            return Ok(new
            {
                imageUrl,
                fileName,
                fileSize = processedBytes.Length,
                side,
                imageProcessed = processedBytes.Length != image.Length,
                message = $"Driver license {side} image uploaded successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading driver license {Side} image for customer {CustomerId}", side, customerId);
            return StatusCode(500, $"Error uploading driver license {side} image");
        }
    }

    /// <summary>
    /// Direct file serving endpoint for cross-server access
    /// GET /api/Media/customers/{customerId}/licenses/file/{fileName}
    /// This endpoint serves files directly from Azure Blob Storage or local disk
    /// </summary>
    [HttpGet("customers/{customerId}/licenses/file/{fileName}")]
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public async Task<IActionResult> GetCustomerLicenseImageFile(Guid customerId, string fileName)
    {
        try
        {
            var blobPath = $"{customerId}/licenses/{fileName}";
            
            _logger.LogInformation("Direct file serving - Checking blob storage: {BlobPath}", blobPath);
            
            // Determine content type from file extension
            var contentType = Path.GetExtension(fileName).ToLowerInvariant() switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                _ => "application/octet-stream"
            };

            // First, try to get from Azure Blob Storage
            var stream = await _blobStorage.DownloadFileAsync(CustomerLicensesContainer, blobPath);
            
            if (stream != null)
            {
                _logger.LogInformation("Serving file from blob storage: {BlobPath}", blobPath);
                
                // Set CORS headers
                Response.Headers.Append("Access-Control-Allow-Origin", "*");
                Response.Headers.Append("Access-Control-Allow-Methods", "GET, HEAD, OPTIONS");
                
                return File(stream, contentType);
            }
            
            // Fallback: try local file system
            var customersPath = Path.Combine(_environment.ContentRootPath, "wwwroot", "customers");
            var filePath = Path.Combine(customersPath, customerId.ToString(), "licenses", fileName);
            
            _logger.LogInformation("Blob not found, checking local file: {FilePath}", filePath);
            
            if (!System.IO.File.Exists(filePath))
            {
                _logger.LogWarning("File not found in blob storage or locally: {BlobPath} / {FilePath}", blobPath, filePath);
                return NotFound(new { error = "File not found", blobPath, localPath = filePath });
            }
            
            var fileInfo = new FileInfo(filePath);
            
            _logger.LogInformation("Serving file from local disk: {FilePath}, Size: {Size}, ContentType: {ContentType}", 
                filePath, fileInfo.Length, contentType);
            
            // Set CORS headers for cross-origin requests
            Response.Headers.Append("Access-Control-Allow-Origin", "*");
            Response.Headers.Append("Access-Control-Allow-Methods", "GET, HEAD, OPTIONS");
            
            // Use PhysicalFile to serve the file with proper content type
            return PhysicalFile(filePath, contentType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error serving license image file");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get list of driver license images for a customer
    /// Returns the actual filenames and URLs of existing images
    /// </summary>
    [HttpGet("customers/{customerId}/licenses")]
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public async Task<IActionResult> GetCustomerLicenseImages(Guid customerId)
    {
        try
        {
            // Validate customer exists
            var customer = _context.Customers.Find(customerId);
            if (customer == null)
                return NotFound("Customer not found");

            string? frontFile = null;
            string? backFile = null;
            string? frontUrl = null;
            string? backUrl = null;

            // First, check the database for stored URLs
            var license = await _context.CustomerLicenses.FirstOrDefaultAsync(l => l.CustomerId == customerId);
            if (license != null)
            {
                if (!string.IsNullOrEmpty(license.FrontImageUrl))
                {
                    frontUrl = license.FrontImageUrl;
                    frontFile = Path.GetFileName(frontUrl);
                    _logger.LogInformation("Found front image URL in database: {Url}", frontUrl);
                }
                if (!string.IsNullOrEmpty(license.BackImageUrl))
                {
                    backUrl = license.BackImageUrl;
                    backFile = Path.GetFileName(backUrl);
                    _logger.LogInformation("Found back image URL in database: {Url}", backUrl);
                }
            }

            // If not in database, try to find files in Azure Blob Storage
            if (frontFile == null || backFile == null)
            {
                var blobPrefix = $"{customerId}/licenses/";
                var blobs = await _blobStorage.ListFilesAsync(CustomerLicensesContainer, blobPrefix);
                var blobList = blobs.ToList();
                
                _logger.LogInformation("Checking blob storage for customer {CustomerId}, found {Count} blobs", customerId, blobList.Count);

                if (blobList.Any())
                {
                    // Find front and back images from blob storage
                    foreach (var blobPath in blobList)
                    {
                        var fileName = Path.GetFileName(blobPath);
                        if (fileName.StartsWith("front", StringComparison.OrdinalIgnoreCase))
                        {
                            frontFile = fileName;
                            frontUrl = _blobStorage.GetBlobUrl(CustomerLicensesContainer, blobPath);
                            _logger.LogInformation("Found front image in blob storage: {FileName}, URL: {Url}", fileName, frontUrl);
                        }
                        else if (fileName.StartsWith("back", StringComparison.OrdinalIgnoreCase))
                        {
                            backFile = fileName;
                            backUrl = _blobStorage.GetBlobUrl(CustomerLicensesContainer, blobPath);
                            _logger.LogInformation("Found back image in blob storage: {FileName}, URL: {Url}", fileName, backUrl);
                        }
                    }
                }
            }

            // Fallback: check local file system (for development or if blob storage not configured)
            if (frontFile == null || backFile == null)
            {
                var customersPath = Path.Combine(_environment.ContentRootPath, "wwwroot", "customers");
                var folderPath = Path.Combine(customersPath, customerId.ToString(), "licenses");
                
                _logger.LogInformation("Checking local file system for customer {CustomerId} in folder: {FolderPath}", customerId, folderPath);
                
                if (Directory.Exists(folderPath))
                {
                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".heic", ".heif" };
                    var files = Directory.GetFiles(folderPath)
                        .Where(f => allowedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                        .Select(Path.GetFileName)
                        .Where(f => f != null)
                        .Cast<string>()
                        .ToList();
                    
                    _logger.LogInformation("Found {Count} local image files", files.Count);

                    // Find front image if not already found
                    if (frontFile == null)
                    {
                        frontFile = files.FirstOrDefault(f => f.StartsWith("front", StringComparison.OrdinalIgnoreCase));
                        if (frontFile != null)
                        {
                            frontUrl = $"/customers/{customerId}/licenses/{frontFile}";
                            _logger.LogInformation("Found front image locally: {FrontFile}", frontFile);
                        }
                    }

                    // Find back image if not already found
                    if (backFile == null)
                    {
                        backFile = files.FirstOrDefault(f => f.StartsWith("back", StringComparison.OrdinalIgnoreCase));
                        if (backFile != null)
                        {
                            backUrl = $"/customers/{customerId}/licenses/{backFile}";
                            _logger.LogInformation("Found back image locally: {BackFile}", backFile);
                        }
                    }
                }
            }

            var result = new
            {
                customerId = customerId.ToString(),
                front = frontFile,
                back = backFile,
                frontUrl = frontUrl,
                backUrl = backUrl
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting license images for customer {CustomerId}", customerId);
            return StatusCode(500, "Error getting license images");
        }
    }

    /// <summary>
    /// Delete driver license image (front or back) for a customer
    /// </summary>
    [HttpDelete("customers/{customerId}/licenses/{side}")]
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public async Task<IActionResult> DeleteCustomerLicenseImage(Guid customerId, string side)
    {
        try
        {
            _logger.LogInformation("=== DeleteCustomerLicenseImage called - customerId: {CustomerId}, side: {Side} [AllowAnonymous] ===", customerId, side);

            if (side != "front" && side != "back")
            {
                _logger.LogWarning("Invalid side parameter: {Side}", side);
                return BadRequest("Side must be 'front' or 'back'");
            }

            var possibleExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            bool fileDeleted = false;
            
            // Delete from blob storage
            foreach (var ext in possibleExtensions)
            {
                var fileName = $"{side}{ext}";
                var blobPath = $"{customerId}/licenses/{fileName}";
                
                if (await _blobStorage.FileExistsAsync(CustomerLicensesContainer, blobPath))
                {
                    await _blobStorage.DeleteFileAsync(CustomerLicensesContainer, blobPath);
                    _logger.LogInformation("Deleted customer license {Side} image from blob: {BlobPath}", side, blobPath);
                    fileDeleted = true;
                    break;
                }
            }

            // Also clear from database
            var license = await _context.CustomerLicenses.FirstOrDefaultAsync(l => l.CustomerId == customerId);
            if (license != null)
            {
                if (side == "front")
                    license.FrontImageUrl = null;
                else
                    license.BackImageUrl = null;
                
                license.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                _logger.LogInformation("Cleared {Side} image URL from database for customer {CustomerId}", side, customerId);
            }

            if (!fileDeleted)
            {
                _logger.LogWarning("Customer license {Side} image not found for customer {CustomerId}", side, customerId);
                return NotFound($"Customer license {side} image not found");
            }

            return Ok(new
            {
                message = $"Customer license {side} image deleted successfully",
                customerId,
                side
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting customer license {Side} image for customer {CustomerId}", side, customerId);
            return StatusCode(500, $"Error deleting customer license {side} image");
        }
    }

    /// <summary>
    /// Upload driver license image (front or back) temporarily using wizardId (for new customers without customerId)
    /// </summary>
    [HttpPost("wizard/{wizardId}/licenses/{side}")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10_485_760)] // 10 MB limit for images
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public async Task<ActionResult<object>> UploadWizardLicenseImage(string wizardId, string side, IFormFile image)
    {
        try
        {
            _logger.LogInformation("UploadWizardLicenseImage called - wizardId: {WizardId}, side: {Side}, hasImage: {HasImage}, imageLength: {ImageLength}", 
                wizardId, side, image != null, image?.Length ?? 0);

            // Validate wizardId
            if (string.IsNullOrWhiteSpace(wizardId))
            {
                _logger.LogWarning("Wizard ID is required but was null or empty");
                return BadRequest("Wizard ID is required");
            }

            // Validate side parameter
            if (side != "front" && side != "back")
            {
                _logger.LogWarning("Invalid side parameter: {Side}", side);
                return BadRequest("Side must be 'front' or 'back'");
            }

            // Validate file
            if (image == null || image.Length == 0)
            {
                _logger.LogWarning("No file uploaded - image is null or empty");
                return BadRequest("No file uploaded");
            }

            // Validate file type (includes HEIC/HEIF for iPhone photos)
            if (!ImageOrientationService.IsSupportedImageFile(image))
                return BadRequest("Invalid file type. Supported formats: JPEG, PNG, BMP, TIFF, WebP, GIF, HEIC, HEIF.");

            // Validate file size (20 MB max — HEIC files can be larger before conversion)
            if (image.Length > 20_971_520)
                return BadRequest("File size exceeds 20 MB limit");

            // Sanitize wizardId to prevent directory traversal
            var sanitizedWizardId = string.Join("_", wizardId.Split(Path.GetInvalidFileNameChars()));

            // Full pipeline: HEIC→PNG + EXIF orientation + compress ≤ 1 MB
            var processedBytes = await _orientationService.ProcessImageAsync(image);

            // After processing, image is always JPEG
            var contentType = "image/jpeg";
            var fileName = $"{side}.jpg";
            var blobPath = $"wizard/{sanitizedWizardId}/licenses/{fileName}";

            // Delete old file if exists
            if (await _blobStorage.FileExistsAsync(CustomerLicensesContainer, blobPath))
            {
                await _blobStorage.DeleteFileAsync(CustomerLicensesContainer, blobPath);
            }

            _logger.LogInformation("Wizard image processed ({OrigSize} → {ProcessedSize} bytes), uploading to: {BlobPath}",
                image.Length, processedBytes.Length, blobPath);

            // Upload processed image to blob storage
            using var stream = new MemoryStream(processedBytes);
            var imageUrl = await _blobStorage.UploadFileAsync(stream, CustomerLicensesContainer, blobPath, contentType);

            _logger.LogInformation("Wizard license image uploaded to blob storage: {ImageUrl}", imageUrl);

            return Ok(new
            {
                imageUrl,
                fileName,
                fileSize = processedBytes.Length,
                side,
                wizardId,
                sanitizedWizardId,
                imageProcessed = processedBytes.Length != image.Length,
                message = $"Driver license {side} image uploaded successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading driver license {Side} image for wizard {WizardId}", side, wizardId);
            return StatusCode(500, $"Error uploading driver license {side} image");
        }
    }

    /// <summary>
    /// Delete driver license image (front or back) for a wizard
    /// </summary>
    [HttpDelete("wizard/{wizardId}/licenses/{side}")]
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public async Task<IActionResult> DeleteWizardLicenseImage(string wizardId, string side)
    {
        try
        {
            _logger.LogInformation("DeleteWizardLicenseImage called - wizardId: {WizardId}, side: {Side}", wizardId, side);

            // Validate wizardId
            if (string.IsNullOrWhiteSpace(wizardId))
            {
                _logger.LogWarning("Wizard ID is required but was null or empty");
                return BadRequest("Wizard ID is required");
            }

            // Validate side parameter
            if (side != "front" && side != "back")
            {
                _logger.LogWarning("Invalid side parameter: {Side}", side);
                return BadRequest("Side must be 'front' or 'back'");
            }

            // Sanitize wizardId to prevent directory traversal
            var sanitizedWizardId = string.Join("_", wizardId.Split(Path.GetInvalidFileNameChars()));

            // Try different file extensions
            var possibleExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            bool fileDeleted = false;
            
            foreach (var ext in possibleExtensions)
            {
                var fileName = $"{side}{ext}";
                var blobPath = $"wizard/{sanitizedWizardId}/licenses/{fileName}";
                
                if (await _blobStorage.FileExistsAsync(CustomerLicensesContainer, blobPath))
                {
                    await _blobStorage.DeleteFileAsync(CustomerLicensesContainer, blobPath);
                    _logger.LogInformation("Deleted wizard license {Side} image from blob: {BlobPath}", side, blobPath);
                    fileDeleted = true;
                    break;
                }
            }

            if (!fileDeleted)
            {
                _logger.LogWarning("Wizard license {Side} image not found for wizard {WizardId}", side, wizardId);
                return NotFound($"Wizard license {side} image not found");
            }

            return Ok(new
            {
                message = $"Wizard license {side} image deleted successfully",
                wizardId,
                side
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting wizard license {Side} image for wizard {WizardId}", side, wizardId);
            return StatusCode(500, $"Error deleting wizard license {side} image");
        }
    }
}
