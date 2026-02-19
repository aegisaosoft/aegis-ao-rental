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
using CarRental.Api.Services;
using CarRental.Api.Services.Interfaces;
using CarRental.Api.Models;
using CarRental.Api.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CarRental.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DriverLicenseController : ControllerBase
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<DriverLicenseController> _logger;
    private readonly IBarcodeParserService _barcodeParser;
    private readonly IFrontSideParserService _frontSideParser;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly CarRentalDbContext _context;
    private readonly ImageOrientationService _orientationService;

    public DriverLicenseController(
        IWebHostEnvironment env,
        ILogger<DriverLicenseController> logger,
        IBarcodeParserService barcodeParser,
        IFrontSideParserService frontSideParser,
        IHttpClientFactory httpClientFactory,
        CarRentalDbContext context,
        ImageOrientationService orientationService)
    {
        _env = env;
        _logger = logger;
        _barcodeParser = barcodeParser;
        _frontSideParser = frontSideParser;
        _httpClientFactory = httpClientFactory;
        _context = context;
        _orientationService = orientationService;
    }

    /// <summary>
    /// Parse PDF417 barcode from the back side of a driver license
    /// </summary>
    /// <param name="backSideImage">Image file containing PDF417 barcode</param>
    /// <param name="customerId">Optional customer ID to automatically save parsed data to database</param>
    /// <returns>Parsed driver license data</returns>
    [HttpPost("parse-back-side")]
    [DisableRequestSizeLimit]
    public async Task<ActionResult<BarcodeParseResult>> ParseBackSide(IFormFile backSideImage, [FromForm] Guid? customerId = null)
    {
        _logger.LogInformation("🔥 ParseBackSide ENDPOINT CALLED - START");
        _logger.LogInformation("🔥 Request received with file: {FileName}, size: {Size}, customerId: {CustomerId}",
            backSideImage?.FileName ?? "NULL", backSideImage?.Length ?? 0, customerId?.ToString() ?? "NULL");

        try
        {
            _logger.LogInformation("ParseBackSide called with customerId: {CustomerId}", customerId?.ToString() ?? "NULL");
            if (backSideImage == null || backSideImage.Length == 0)
            {
                return BadRequest(new { message = "No image file provided" });
            }

            // Validate image file type
            if (!IsValidImageFile(backSideImage))
            {
                return BadRequest(new { message = "Invalid image file. Supported formats: JPEG, PNG, BMP, TIFF, WebP, GIF, HEIC, HEIF." });
            }

            _logger.LogInformation("Processing PDF417 barcode from back side image: {FileName} ({Size} bytes)",
                backSideImage.FileName, backSideImage.Length);

            // Convert uploaded file to byte array
            using var memoryStream = new MemoryStream();
            await backSideImage.CopyToAsync(memoryStream);
            var imageBytes = memoryStream.ToArray();

            // Parse PDF417 barcode using ZXing.Net + IdParser (passes fileName/contentType for HEIC detection)
            var parseResult = await _barcodeParser.ParseDriverLicenseBarcodeAsync(imageBytes, backSideImage.FileName, backSideImage.ContentType);

            if (parseResult?.Success == true)
            {
                _logger.LogInformation("Successfully parsed driver license back side for license: {LicenseNumber}",
                    parseResult.Data?.LicenseNumber);

                // Auto-save to database if customerId is provided
                if (customerId.HasValue && parseResult.Data != null)
                {
                    _logger.LogInformation("Attempting to auto-save parsed license data for customer: {CustomerId}", customerId.Value);
                    try
                    {
                        var savedLicenseId = await SaveParsedLicenseToDatabase(
                            customerId.Value,
                            parseResult.Data,
                            "pdf417_barcode",
                            parseResult.ConfidenceScore,
                            parseResult.RawData
                        );

                        // Create license scan audit record
                        await CreateLicenseScanRecord(
                            customerId.Value,
                            savedLicenseId,
                            "back_side",
                            "pdf417_barcode",
                            parseResult.ConfidenceScore,
                            parseResult.Data,
                            parseResult.RawData
                        );

                        _logger.LogInformation("Auto-saved parsed license data to database for customer: {CustomerId}, license: {LicenseId}",
                            customerId.Value, savedLicenseId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to auto-save parsed license data for customer: {CustomerId}", customerId.Value);
                        // Continue without failing the parsing response
                    }
                }
                else
                {
                    _logger.LogWarning("Skipping auto-save - customerId: {CustomerId}, hasData: {HasData}",
                        customerId?.ToString() ?? "NULL", parseResult.Data != null);
                }

                return Ok(parseResult);
            }
            else
            {
                _logger.LogWarning("Failed to parse PDF417 barcode: {Error}", parseResult?.Error);

                // Create scan record for failed attempts if customerId provided
                if (customerId.HasValue)
                {
                    try
                    {
                        await CreateLicenseScanRecord(
                            customerId.Value,
                            null,
                            "back_side",
                            "pdf417_barcode",
                            0.0,
                            null,
                            null,
                            false,
                            new[] { parseResult?.Error ?? "Parse failed" }
                        );
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to create failed scan record for customer: {CustomerId}", customerId.Value);
                    }
                }

                return Ok(parseResult); // Return failed result with details
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing driver license back side image");
            return StatusCode(500, new { message = "Internal server error processing image", error = ex.Message });
        }
    }

    /// <summary>
    /// Process front side of driver license using Google Cloud Document AI
    /// </summary>
    /// <param name="frontSideImage">Image file of driver license front side</param>
    /// <param name="customerId">Optional customer ID to automatically save parsed data to database</param>
    /// <returns>Parsed driver license data from Document AI</returns>
    [HttpPost("parse-front-side")]
    [DisableRequestSizeLimit]
    public async Task<ActionResult<DocumentAiParseResult>> ParseFrontSide(IFormFile frontSideImage, [FromForm] Guid? customerId = null)
    {
        try
        {
            if (frontSideImage == null || frontSideImage.Length == 0)
            {
                return BadRequest(new { message = "No image file provided" });
            }

            if (!IsValidImageFile(frontSideImage))
            {
                return BadRequest(new { message = "Invalid image file. Supported formats: JPEG, PNG, BMP, TIFF, WebP, GIF, HEIC, HEIF." });
            }

            _logger.LogInformation("Processing front side image: {FileName} ({Size} bytes)",
                frontSideImage.FileName, frontSideImage.Length);

            // Convert uploaded file to byte array
            using var memoryStream = new MemoryStream();
            await frontSideImage.CopyToAsync(memoryStream);
            var imageBytes = memoryStream.ToArray();

            // Parse front side via singleton service (HEIC→PNG + EXIF + compress + Document AI inside)
            var parseResult = await _frontSideParser.ParseFrontSideAsync(imageBytes, frontSideImage.FileName, frontSideImage.ContentType);

            // Create scan records for Document AI processing
            if (customerId.HasValue && parseResult.Success && parseResult.Data != null)
            {
                try
                {
                    await CreateLicenseScanRecord(
                        customerId.Value,
                        null,
                        "front_side",
                        "document_ai_ocr",
                        parseResult.ConfidenceScore,
                        parseResult.Data,
                        null
                    );

                    _logger.LogInformation("Created scan record for Document AI processing for customer: {CustomerId}", customerId.Value);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create scan record for Document AI processing for customer: {CustomerId}", customerId.Value);
                }
            }
            else if (customerId.HasValue && !parseResult.Success)
            {
                // Create failed scan record
                try
                {
                    await CreateLicenseScanRecord(
                        customerId.Value,
                        null,
                        "front_side",
                        "document_ai_ocr",
                        0.0,
                        null,
                        null,
                        false,
                        new[] { parseResult.ErrorMessage ?? "Document AI processing failed" }
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create failed scan record for Document AI processing for customer: {CustomerId}", customerId.Value);
                }
            }

            return Ok(parseResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing driver license front side image with Document AI");
            return StatusCode(500, new { message = "Internal server error processing image", error = ex.Message });
        }
    }

    /// <summary>
    /// Process both sides of driver license and combine results
    /// </summary>
    /// <param name="frontSideImage">Front side image</param>
    /// <param name="backSideImage">Back side image</param>
    /// <param name="customerId">Optional customer ID to automatically save parsed data to database</param>
    /// <returns>Combined driver license data from both sides</returns>
    [HttpPost("parse-both-sides")]
    [DisableRequestSizeLimit]
    public async Task<ActionResult<CombinedLicenseParseResult>> ParseBothSides(
        IFormFile frontSideImage,
        IFormFile backSideImage,
        [FromForm] Guid? customerId = null)
    {
        try
        {
            if (frontSideImage == null || backSideImage == null)
            {
                return BadRequest(new { message = "Both front and back side images are required" });
            }

            _logger.LogInformation("Processing both sides of driver license");

            // Process both sides concurrently
            var frontTask = ProcessFrontSideInternal(frontSideImage);
            var backTask = ProcessBackSideInternal(backSideImage);

            await Task.WhenAll(frontTask, backTask);

            var frontResult = await frontTask;
            var backResult = await backTask;

            // Combine results from both sides
            var combinedResult = CombineLicenseData(frontResult, backResult);

            _logger.LogInformation("Successfully combined data from both sides of driver license");

            // Auto-save to database if customerId is provided and parsing was successful
            if (customerId.HasValue && combinedResult.Success && combinedResult.CombinedData != null)
            {
                try
                {
                    // Extract raw barcode data from back side result if available
                    var rawBarcodeData = combinedResult.BackSideResult?.RawData;

                    var savedLicenseId = await SaveCombinedLicenseToDatabase(
                        customerId.Value,
                        combinedResult.CombinedData,
                        combinedResult.CombinedData.PrimaryDataSource ?? "combined_parsing",
                        combinedResult.CombinedData.ConfidenceScore,
                        rawBarcodeData
                    );

                    // Create license scan audit record for both sides
                    await CreateLicenseScanRecord(
                        customerId.Value,
                        savedLicenseId,
                        "both_sides",
                        combinedResult.CombinedData.PrimaryDataSource ?? "combined_parsing",
                        combinedResult.CombinedData.ConfidenceScore,
                        combinedResult.CombinedData,
                        rawBarcodeData
                    );

                    _logger.LogInformation("Auto-saved combined license data to database for customer: {CustomerId}, license: {LicenseId}",
                        customerId.Value, savedLicenseId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to auto-save combined license data for customer: {CustomerId}", customerId.Value);
                    // Continue without failing the parsing response
                }
            }

            return Ok(combinedResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing both sides of driver license");
            return StatusCode(500, new { message = "Internal server error processing images", error = ex.Message });
        }
    }

    /// <summary>
    /// Health check endpoint for license parsing services.
    /// Resolves BarcodeParserService, does a quick dummy parse, returns pool stats.
    /// </summary>
    [HttpGet("health")]
    public async Task<ActionResult<object>> HealthCheck()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        string parserStatus;
        try
        {
            // Quick dummy parse to verify the pool and SkiaSharp are working
            using var dummyBitmap = new SkiaSharp.SKBitmap(10, 10);
            dummyBitmap.Erase(SkiaSharp.SKColors.White);
            using var ms = new MemoryStream();
            dummyBitmap.Encode(ms, SkiaSharp.SKEncodedImageFormat.Png, 100);
            var dummyResult = await _barcodeParser.ParseDriverLicenseBarcodeAsync(ms.ToArray());
            // Expected: no barcode found in white image — that's fine, service is alive
            parserStatus = "available";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Health check dummy parse failed");
            parserStatus = $"degraded: {ex.Message}";
        }
        sw.Stop();

        return Ok(new
        {
            status = parserStatus == "available" ? "healthy" : "degraded",
            services = new
            {
                pdf417Parser = parserStatus,
                pdf417ParserMode = "ObjectPool (thread-safe)",
                documentAi = "configured",
                healthCheckLatencyMs = sw.ElapsedMilliseconds,
                processorCount = Environment.ProcessorCount,
                timestamp = DateTime.UtcNow
            }
        });
    }

    #region Private Methods

    /// <summary>
    /// Validate image file type (JPEG, PNG, BMP, TIFF, WebP, GIF, HEIC, HEIF)
    /// </summary>
    private static bool IsValidImageFile(IFormFile file)
    {
        return ImageOrientationService.IsSupportedImageFile(file);
    }

    /// <summary>
    /// Process front side internally (HEIC→PNG + EXIF + compress pipeline applied inside FrontSideParserService)
    /// </summary>
    private async Task<DocumentAiParseResult> ProcessFrontSideInternal(IFormFile frontSideImage)
    {
        using var ms = new MemoryStream();
        await frontSideImage.CopyToAsync(ms);
        return await _frontSideParser.ParseFrontSideAsync(ms.ToArray());
    }

    /// <summary>
    /// Process back side internally (HEIC→PNG + EXIF + compress pipeline applied inside BarcodeParserService)
    /// </summary>
    private async Task<BarcodeParseResult> ProcessBackSideInternal(IFormFile backSideImage)
    {
        using var ms = new MemoryStream();
        await backSideImage.CopyToAsync(ms);
        var imageBytes = ms.ToArray();

        return await _barcodeParser.ParseDriverLicenseBarcodeAsync(imageBytes, backSideImage.FileName, backSideImage.ContentType) ?? new BarcodeParseResult
        {
            Success = false,
            Message = "Failed to parse back side barcode",
            Error = "PARSE_FAILED"
        };
    }

    /// <summary>
    /// Combine data from front and back side processing
    /// </summary>
    private CombinedLicenseParseResult CombineLicenseData(
        DocumentAiParseResult frontResult,
        BarcodeParseResult backResult)
    {
        var result = new CombinedLicenseParseResult
        {
            FrontSideResult = frontResult,
            BackSideResult = backResult,
            ProcessingTimestamp = DateTime.UtcNow
        };

        // Prioritize barcode data (more accurate) over OCR data
        if (backResult?.Success == true && backResult.Data != null)
        {
            result.CombinedData = new CombinedLicenseData
            {
                // Use barcode data as primary source
                FirstName = backResult.Data.FirstName,
                MiddleName = backResult.Data.MiddleName,
                LastName = backResult.Data.LastName,
                DateOfBirth = backResult.Data.DateOfBirth,
                LicenseNumber = backResult.Data.LicenseNumber,
                Address = backResult.Data.Address,
                City = backResult.Data.City,
                State = backResult.Data.State,
                PostalCode = backResult.Data.PostalCode,
                ExpirationDate = backResult.Data.ExpirationDate,
                Sex = backResult.Data.Sex,
                Height = backResult.Data.Height,
                EyeColor = backResult.Data.EyeColor,
                IssuingState = backResult.Data.IssuingState,

                // Metadata
                PrimaryDataSource = "pdf417_barcode",
                ProcessingTimestamp = DateTime.UtcNow,
                ConfidenceScore = backResult.ConfidenceScore
            };

            result.Success = true;
        }
        else if (frontResult?.Success == true && frontResult.Data != null)
        {
            // Fall back to Document AI data if barcode parsing failed
            result.CombinedData = new CombinedLicenseData
            {
                // Map Document AI data to combined structure
                // TODO: Implement mapping when Document AI is integrated
                PrimaryDataSource = "document_ai_ocr",
                ProcessingTimestamp = DateTime.UtcNow,
                ConfidenceScore = frontResult.ConfidenceScore
            };

            result.Success = true;
        }
        else
        {
            result.Success = false;
            result.ErrorMessage = "Failed to extract data from both front and back sides";
        }

        return result;
    }

    /// <summary>
    /// Save parsed license data to database
    /// </summary>
    private async Task<Guid> SaveParsedLicenseToDatabase(
        Guid customerId,
        DriverLicenseData licenseData,
        string verificationMethod,
        double confidenceScore,
        string? rawBarcodeData)
    {
        _logger.LogInformation("SaveParsedLicenseToDatabase called for customer: {CustomerId}", customerId);

        var customer = await _context.Customers.FindAsync(customerId);
        if (customer == null)
            throw new ArgumentException($"Customer not found: {customerId}");

        // Validate and sanitize data for database constraints
        var sanitizedSex = SanitizeSexField(licenseData.Sex);
        var sanitizedStateIssued = SanitizeStateCode(licenseData.IssuingState);
        var sanitizedCountryIssued = SanitizeCountryCode(licenseData.Country);

        _logger.LogInformation("Data sanitization: Sex '{0}' -> '{1}', State '{2}' -> '{3}', Country '{4}' -> '{5}'",
            licenseData.Sex ?? "NULL", sanitizedSex ?? "NULL",
            licenseData.IssuingState ?? "NULL", sanitizedStateIssued ?? "NULL",
            licenseData.Country ?? "NULL", sanitizedCountryIssued ?? "NULL");

        // First, check if license with the same license number already exists for this customer
        CustomerLicense? existingLicense = null;

        if (!string.IsNullOrEmpty(licenseData.LicenseNumber))
        {
            existingLicense = await _context.CustomerLicenses
                .FirstOrDefaultAsync(cl => cl.CustomerId == customerId && cl.LicenseNumber == licenseData.LicenseNumber);

            if (existingLicense != null)
            {
                _logger.LogInformation("Found existing license with same license number for customer: {CustomerId}, license: {LicenseId}, number: {LicenseNumber}",
                    customerId, existingLicense.Id, licenseData.LicenseNumber);
            }
        }

        // If no license with same license number found, check for any existing license for this customer
        if (existingLicense == null)
        {
            existingLicense = await _context.CustomerLicenses
                .FirstOrDefaultAsync(cl => cl.CustomerId == customerId);

            if (existingLicense != null)
            {
                _logger.LogInformation("Found existing license for customer (different license number): {CustomerId}, license: {LicenseId}",
                    customerId, existingLicense.Id);
            }
        }

        CustomerLicense license;
        if (existingLicense != null)
        {
            // Update existing license
            existingLicense.LicenseNumber = licenseData.LicenseNumber;
            existingLicense.StateIssued = sanitizedStateIssued ?? "";
            existingLicense.CountryIssued = sanitizedCountryIssued ?? "US";
            existingLicense.FirstName = SanitizeTextField(licenseData.FirstName, 100);
            existingLicense.LastName = SanitizeTextField(licenseData.LastName, 100);
            existingLicense.Sex = sanitizedSex;
            existingLicense.Height = SanitizeTextField(licenseData.Height, 20);
            existingLicense.EyeColor = SanitizeTextField(licenseData.EyeColor, 20);
            existingLicense.MiddleName = SanitizeTextField(licenseData.MiddleName, 100);
            existingLicense.ExpirationDate = ParseDateSafely(licenseData.ExpirationDate) ?? DateTime.UtcNow.AddYears(5);
            existingLicense.IssueDate = ParseDateSafely(licenseData.IssueDate);
            existingLicense.LicenseAddress = SanitizeTextField(licenseData.Address, 255);
            existingLicense.LicenseCity = SanitizeTextField(licenseData.City, 100);
            existingLicense.LicenseState = SanitizeTextField(licenseData.State, 100);
            existingLicense.LicensePostalCode = SanitizeTextField(licenseData.PostalCode, 20);
            existingLicense.LicenseCountry = SanitizeTextField(licenseData.Country, 100);
            existingLicense.RawBarcodeData = rawBarcodeData;
            existingLicense.VerificationMethod = verificationMethod;
            existingLicense.UpdatedAt = DateTime.UtcNow;

            license = existingLicense;
            _context.CustomerLicenses.Update(license);
        }
        else
        {
            // Create new license
            license = new CustomerLicense
            {
                CustomerId = customerId,
                LicenseNumber = licenseData.LicenseNumber,
                StateIssued = sanitizedStateIssued ?? "",
                CountryIssued = sanitizedCountryIssued ?? "US",
                FirstName = SanitizeTextField(licenseData.FirstName, 100),
                LastName = SanitizeTextField(licenseData.LastName, 100),
                Sex = sanitizedSex,
                Height = SanitizeTextField(licenseData.Height, 20),
                EyeColor = SanitizeTextField(licenseData.EyeColor, 20),
                MiddleName = SanitizeTextField(licenseData.MiddleName, 100),
                ExpirationDate = ParseDateSafely(licenseData.ExpirationDate) ?? DateTime.UtcNow.AddYears(5),
                IssueDate = ParseDateSafely(licenseData.IssueDate),
                LicenseAddress = SanitizeTextField(licenseData.Address, 255),
                LicenseCity = SanitizeTextField(licenseData.City, 100),
                LicenseState = SanitizeTextField(licenseData.State, 100),
                LicensePostalCode = SanitizeTextField(licenseData.PostalCode, 20),
                LicenseCountry = SanitizeTextField(licenseData.Country, 100),
                RawBarcodeData = rawBarcodeData,
                IsVerified = true,
                VerificationDate = DateTime.UtcNow,
                VerificationMethod = verificationMethod
            };

            _context.CustomerLicenses.Add(license);
        }

        await _context.SaveChangesAsync();
        return license.Id;
    }

    /// <summary>
    /// Save combined license data to database
    /// </summary>
    private async Task<Guid> SaveCombinedLicenseToDatabase(
        Guid customerId,
        CombinedLicenseData licenseData,
        string verificationMethod,
        double confidenceScore,
        string? rawBarcodeData = null)
    {
        var customer = await _context.Customers.FindAsync(customerId);
        if (customer == null)
            throw new ArgumentException($"Customer not found: {customerId}");

        // Validate and sanitize data for database constraints
        var sanitizedSex = SanitizeSexField(licenseData.Sex);
        var sanitizedStateIssued = SanitizeStateCode(licenseData.IssuingState);

        // First, check if license with the same license number already exists for this customer
        CustomerLicense? existingLicense = null;

        if (!string.IsNullOrEmpty(licenseData.LicenseNumber))
        {
            existingLicense = await _context.CustomerLicenses
                .FirstOrDefaultAsync(cl => cl.CustomerId == customerId && cl.LicenseNumber == licenseData.LicenseNumber);

            if (existingLicense != null)
            {
                _logger.LogInformation("Found existing license with same license number for customer: {CustomerId}, license: {LicenseId}, number: {LicenseNumber}",
                    customerId, existingLicense.Id, licenseData.LicenseNumber);
            }
        }

        // If no license with same license number found, check for any existing license for this customer
        if (existingLicense == null)
        {
            existingLicense = await _context.CustomerLicenses
                .FirstOrDefaultAsync(cl => cl.CustomerId == customerId);

            if (existingLicense != null)
            {
                _logger.LogInformation("Found existing license for customer (different license number): {CustomerId}, license: {LicenseId}",
                    customerId, existingLicense.Id);
            }
        }

        CustomerLicense license;
        if (existingLicense != null)
        {
            // Update existing license
            existingLicense.LicenseNumber = licenseData.LicenseNumber ?? existingLicense.LicenseNumber;
            existingLicense.StateIssued = sanitizedStateIssued ?? existingLicense.StateIssued;
            existingLicense.FirstName = SanitizeTextField(licenseData.FirstName, 100) ?? existingLicense.FirstName;
            existingLicense.LastName = SanitizeTextField(licenseData.LastName, 100) ?? existingLicense.LastName;
            existingLicense.Sex = sanitizedSex ?? existingLicense.Sex;
            existingLicense.Height = SanitizeTextField(licenseData.Height, 20) ?? existingLicense.Height;
            existingLicense.EyeColor = SanitizeTextField(licenseData.EyeColor, 20) ?? existingLicense.EyeColor;
            existingLicense.MiddleName = SanitizeTextField(licenseData.MiddleName, 100) ?? existingLicense.MiddleName;
            existingLicense.ExpirationDate = ParseDateSafely(licenseData.ExpirationDate) ?? existingLicense.ExpirationDate;
            existingLicense.IssueDate = ParseDateSafely(licenseData.IssueDate) ?? existingLicense.IssueDate;
            existingLicense.LicenseAddress = SanitizeTextField(licenseData.Address, 255) ?? existingLicense.LicenseAddress;
            existingLicense.LicenseCity = SanitizeTextField(licenseData.City, 100) ?? existingLicense.LicenseCity;
            existingLicense.LicenseState = SanitizeTextField(licenseData.State, 100) ?? existingLicense.LicenseState;
            existingLicense.LicensePostalCode = SanitizeTextField(licenseData.PostalCode, 20) ?? existingLicense.LicensePostalCode;
            existingLicense.RawBarcodeData = rawBarcodeData ?? existingLicense.RawBarcodeData;
            existingLicense.VerificationMethod = verificationMethod;
            existingLicense.UpdatedAt = DateTime.UtcNow;

            license = existingLicense;
            _context.CustomerLicenses.Update(license);
        }
        else
        {
            // Create new license
            license = new CustomerLicense
            {
                CustomerId = customerId,
                LicenseNumber = licenseData.LicenseNumber ?? "",
                StateIssued = sanitizedStateIssued ?? "",
                FirstName = SanitizeTextField(licenseData.FirstName, 100),
                LastName = SanitizeTextField(licenseData.LastName, 100),
                Sex = sanitizedSex,
                Height = SanitizeTextField(licenseData.Height, 20),
                EyeColor = SanitizeTextField(licenseData.EyeColor, 20),
                MiddleName = SanitizeTextField(licenseData.MiddleName, 100),
                ExpirationDate = ParseDateSafely(licenseData.ExpirationDate) ?? DateTime.UtcNow.AddYears(5),
                IssueDate = ParseDateSafely(licenseData.IssueDate),
                LicenseAddress = SanitizeTextField(licenseData.Address, 255),
                LicenseCity = SanitizeTextField(licenseData.City, 100),
                LicenseState = SanitizeTextField(licenseData.State, 100),
                LicensePostalCode = SanitizeTextField(licenseData.PostalCode, 20),
                RawBarcodeData = rawBarcodeData,
                IsVerified = true,
                VerificationDate = DateTime.UtcNow,
                VerificationMethod = verificationMethod
            };

            _context.CustomerLicenses.Add(license);
        }

        await _context.SaveChangesAsync();
        return license.Id;
    }

    /// <summary>
    /// Create license scan audit record
    /// </summary>
    private async Task CreateLicenseScanRecord(
        Guid customerId,
        Guid? customerLicenseId,
        string scanSource,
        string scanMethod,
        double confidenceScore,
        object? parsedData,
        string? rawBarcodeData,
        bool validationPassed = true,
        string[]? validationErrors = null)
    {
        _logger.LogInformation("CreateLicenseScanRecord called for customer: {CustomerId}, source: {ScanSource}", customerId, scanSource);

        var customer = await _context.Customers.FindAsync(customerId);
        if (customer == null)
            throw new ArgumentException($"Customer not found: {customerId}");

        // Check if scan record already exists for this customer and license
        var existingScan = await _context.LicenseScans
            .FirstOrDefaultAsync(ls => ls.CustomerId == customerId && ls.CustomerLicenseId == customerLicenseId);

        if (existingScan != null)
        {
            _logger.LogInformation("Scan record already exists for customer {CustomerId} and license {LicenseId}. Skipping duplicate.", customerId, customerLicenseId);
            return;
        }

        // Calculate age and expiration info if we have parsed data
        int? ageAtScan = null;
        bool wasExpired = false;
        int? daysUntilExpiration = null;

        if (parsedData is DriverLicenseData driverLicense)
        {
            var birthDate = ParseDateSafely(driverLicense.DateOfBirth);
            var expirationDate = ParseDateSafely(driverLicense.ExpirationDate);

            if (birthDate.HasValue)
                ageAtScan = (int)((DateTime.UtcNow - birthDate.Value).TotalDays / 365.25);

            if (expirationDate.HasValue)
            {
                wasExpired = expirationDate.Value < DateTime.UtcNow;
                daysUntilExpiration = (int)(expirationDate.Value - DateTime.UtcNow).TotalDays;
            }
        }
        else if (parsedData is CombinedLicenseData combinedLicense)
        {
            var birthDate = ParseDateSafely(combinedLicense.DateOfBirth);
            var expirationDate = ParseDateSafely(combinedLicense.ExpirationDate);

            if (birthDate.HasValue)
                ageAtScan = (int)((DateTime.UtcNow - birthDate.Value).TotalDays / 365.25);

            if (expirationDate.HasValue)
            {
                wasExpired = expirationDate.Value < DateTime.UtcNow;
                daysUntilExpiration = (int)(expirationDate.Value - DateTime.UtcNow).TotalDays;
            }
        }

        var licenseScan = new LicenseScan
        {
            CustomerId = customerId,
            CustomerLicenseId = customerLicenseId,
            ScanDate = DateTime.UtcNow,
            ScanSource = scanSource,
            ScanQuality = confidenceScore > 0.8 ? "high" : confidenceScore > 0.6 ? "medium" : "low",
            AllFieldsCaptured = validationPassed,
            CapturedData = parsedData != null ? JsonSerializer.Serialize(parsedData) : null,
            BarcodeData = rawBarcodeData,
            AgeAtScan = ageAtScan,
            WasExpired = wasExpired,
            DaysUntilExpiration = daysUntilExpiration,
            ValidationPassed = validationPassed,
            ValidationErrors = validationErrors
        };

        _context.LicenseScans.Add(licenseScan);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Sanitize sex field to ensure it fits database constraint (varchar(1))
    /// </summary>
    private string? SanitizeSexField(string? sexValue)
    {
        if (string.IsNullOrWhiteSpace(sexValue))
            return null;

        var normalized = sexValue.Trim().ToUpperInvariant();

        // Handle common sex field values
        return normalized switch
        {
            "MALE" or "M" or "1" => "M",
            "FEMALE" or "F" or "2" => "F",
            "X" or "NONBINARY" or "NON-BINARY" or "OTHER" => "X",
            _ => normalized.Length > 0 ? normalized.Substring(0, 1) : null
        };
    }

    /// <summary>
    /// Sanitize state code to ensure it fits database constraint (varchar(2))
    /// </summary>
    private string? SanitizeStateCode(string? stateValue)
    {
        if (string.IsNullOrWhiteSpace(stateValue))
            return null;

        var normalized = stateValue.Trim().ToUpperInvariant();

        // If it's already 2 characters, return as-is
        if (normalized.Length == 2)
            return normalized;

        // Handle common US state names to abbreviations mapping
        var stateMap = new Dictionary<string, string>
        {
            {"ALABAMA", "AL"}, {"ALASKA", "AK"}, {"ARIZONA", "AZ"}, {"ARKANSAS", "AR"}, {"CALIFORNIA", "CA"},
            {"COLORADO", "CO"}, {"CONNECTICUT", "CT"}, {"DELAWARE", "DE"}, {"FLORIDA", "FL"}, {"GEORGIA", "GA"},
            {"HAWAII", "HI"}, {"IDAHO", "ID"}, {"ILLINOIS", "IL"}, {"INDIANA", "IN"}, {"IOWA", "IA"},
            {"KANSAS", "KS"}, {"KENTUCKY", "KY"}, {"LOUISIANA", "LA"}, {"MAINE", "ME"}, {"MARYLAND", "MD"},
            {"MASSACHUSETTS", "MA"}, {"MICHIGAN", "MI"}, {"MINNESOTA", "MN"}, {"MISSISSIPPI", "MS"}, {"MISSOURI", "MO"},
            {"MONTANA", "MT"}, {"NEBRASKA", "NE"}, {"NEVADA", "NV"}, {"NEW HAMPSHIRE", "NH"}, {"NEW JERSEY", "NJ"},
            {"NEW MEXICO", "NM"}, {"NEW YORK", "NY"}, {"NORTH CAROLINA", "NC"}, {"NORTH DAKOTA", "ND"}, {"OHIO", "OH"},
            {"OKLAHOMA", "OK"}, {"OREGON", "OR"}, {"PENNSYLVANIA", "PA"}, {"RHODE ISLAND", "RI"}, {"SOUTH CAROLINA", "SC"},
            {"SOUTH DAKOTA", "SD"}, {"TENNESSEE", "TN"}, {"TEXAS", "TX"}, {"UTAH", "UT"}, {"VERMONT", "VT"},
            {"VIRGINIA", "VA"}, {"WASHINGTON", "WA"}, {"WEST VIRGINIA", "WV"}, {"WISCONSIN", "WI"}, {"WYOMING", "WY"},
            {"DISTRICT OF COLUMBIA", "DC"}
        };

        if (stateMap.TryGetValue(normalized, out var abbreviation))
            return abbreviation;

        // If we can't map it and it's longer than 2 characters, truncate to 2
        return normalized.Length > 2 ? normalized.Substring(0, 2) : normalized;
    }

    /// <summary>
    /// Sanitize country code to ensure it fits database constraint (varchar(2))
    /// </summary>
    private string? SanitizeCountryCode(string? countryValue)
    {
        if (string.IsNullOrWhiteSpace(countryValue))
            return "US"; // Default to US for driver licenses

        var normalized = countryValue.Trim().ToUpperInvariant();

        // If it's already 2 characters, return as-is
        if (normalized.Length == 2)
            return normalized;

        // Handle common country names to ISO codes mapping
        var countryMap = new Dictionary<string, string>
        {
            {"UNITED STATES", "US"}, {"UNITED STATES OF AMERICA", "US"}, {"USA", "US"}, {"AMERICA", "US"},
            {"CANADA", "CA"}, {"MEXICO", "MX"}
        };

        if (countryMap.TryGetValue(normalized, out var code))
            return code;

        // Default to US if we can't determine
        return "US";
    }

    /// <summary>
    /// Sanitize text field to ensure it fits database constraint
    /// </summary>
    private string? SanitizeTextField(string? textValue, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(textValue))
            return null;

        var trimmed = textValue.Trim();
        return trimmed.Length > maxLength ? trimmed.Substring(0, maxLength) : trimmed;
    }

    /// <summary>
    /// Safely parse date strings that might be in various formats
    /// </summary>
    private DateTime? ParseDateSafely(string? dateString)
    {
        if (string.IsNullOrEmpty(dateString))
            return null;

        // Try common date formats
        var formats = new[]
        {
            "MM/dd/yyyy",
            "yyyy-MM-dd",
            "MM-dd-yyyy",
            "dd/MM/yyyy",
            "yyyy/MM/dd",
            "MMddyyyy",
            "yyyyMMdd"
        };

        foreach (var format in formats)
        {
            if (DateTime.TryParseExact(dateString, format, null, System.Globalization.DateTimeStyles.None, out var result))
            {
                return result;
            }
        }

        // Fallback to general parsing
        if (DateTime.TryParse(dateString, out var generalResult))
        {
            return generalResult;
        }

        return null;
    }

    #endregion
}

#region DTOs for Document AI Integration

public class DocumentAiParseResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public DocumentAiLicenseData? Data { get; set; }
    public double ConfidenceScore { get; set; }
    public string? ProcessingMethod { get; set; }
    public DateTime ProcessingTimestamp { get; set; } = DateTime.UtcNow;
}

public class DocumentAiLicenseData
{
    // TODO: Define structure based on Google Cloud Document AI response
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? LicenseNumber { get; set; }
    public string? DateOfBirth { get; set; }
    public string? Address { get; set; }
    public string? ExpirationDate { get; set; }

    public DateTime ProcessingTimestamp { get; set; } = DateTime.UtcNow;
    public bool ExtractedFromOcr { get; set; }
    public bool RequiresManualEntry { get; set; }
}

public class CombinedLicenseParseResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public CombinedLicenseData? CombinedData { get; set; }
    public DocumentAiParseResult? FrontSideResult { get; set; }
    public BarcodeParseResult? BackSideResult { get; set; }
    public DateTime ProcessingTimestamp { get; set; } = DateTime.UtcNow;
}

public class CombinedLicenseData
{
    // Personal Information
    public string? FirstName { get; set; }
    public string? MiddleName { get; set; }
    public string? LastName { get; set; }
    public string? DateOfBirth { get; set; }
    public string? Sex { get; set; }

    // License Information
    public string? LicenseNumber { get; set; }
    public string? IssuingState { get; set; }
    public string? ExpirationDate { get; set; }
    public string? IssueDate { get; set; }

    // Address Information
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }

    // Physical Characteristics
    public string? Height { get; set; }
    public string? EyeColor { get; set; }

    // Processing Metadata
    public string? PrimaryDataSource { get; set; } // "pdf417_barcode" or "document_ai_ocr"
    public double ConfidenceScore { get; set; }
    public DateTime ProcessingTimestamp { get; set; } = DateTime.UtcNow;
}

#endregion
