/*
 * Copyright (c) 2025 Alexander Orlov.
 * 34 Middletown Ave Atlantic Highlands NJ 07716
 *
 * Driver License PDF417 Barcode Parser Service
 * Cross-platform implementation using SkiaSharp and ZXing.Net
 */

using CarRental.Api.Models;
using CarRental.Api.Services.Interfaces;
using IdParser.Core;
using SkiaSharp;
using ZXing;
using ZXing.Common;
using ZXing.SkiaSharp;

namespace CarRental.Api.Services;

/// <summary>
/// Service for parsing PDF417 barcodes from driver licenses
/// Uses ZXing.Net for barcode reading and IdParser.Core for AAMVA data parsing
/// Cross-platform implementation using SkiaSharp (works on Windows, Linux, macOS)
/// </summary>
public class BarcodeParserService : IBarcodeParserService
{
    private readonly BarcodeReader<SKBitmap> _barcodeReader;
    private readonly ILogger<BarcodeParserService> _logger;

    public BarcodeParserService(ILogger<BarcodeParserService> logger)
    {
        _logger = logger;
        _barcodeReader = new BarcodeReader<SKBitmap>(bitmap => new SKBitmapLuminanceSource(bitmap))
        {
            AutoRotate = true,
            Options = new DecodingOptions
            {
                TryHarder = true,
                TryInverted = true,
                PossibleFormats = new[] { BarcodeFormat.PDF_417 },
                PureBarcode = false
            }
        };

        _logger.LogInformation("BarcodeParserService initialized with SkiaSharp support");
    }

    public async Task<CarRental.Api.Services.Interfaces.BarcodeParseResult> ParseDriverLicenseBarcodeAsync(Stream imageStream, string mimeType)
    {
        using var memoryStream = new MemoryStream();
        await imageStream.CopyToAsync(memoryStream);
        return await ParseDriverLicenseBarcodeAsync(memoryStream.ToArray());
    }

    public Task<CarRental.Api.Services.Interfaces.BarcodeParseResult> ParseDriverLicenseBarcodeAsync(byte[] imageData)
    {
        return Task.Run(() =>
        {
            try
            {
                _logger.LogInformation("Starting PDF417 barcode parsing from image ({Size} bytes)", imageData.Length);

                using var bitmap = SKBitmap.Decode(imageData);
                if (bitmap == null)
                {
                    _logger.LogWarning("Could not decode image data");
                    return new CarRental.Api.Services.Interfaces.BarcodeParseResult
                    {
                        Success = false,
                        ErrorCode = "INVALID_IMAGE",
                        Error = "INVALID_IMAGE",
                        Message = "Could not decode image data. Please ensure the file is a valid image."
                    };
                }

                // Try to decode barcode
                var result = _barcodeReader.Decode(bitmap);

                // If not found, try with preprocessing
                if (result == null)
                {
                    _logger.LogInformation("Initial barcode detection failed, trying with preprocessing");
                    result = TryDecodeWithPreprocessing(bitmap);
                }

                if (result == null)
                {
                    _logger.LogWarning("No PDF417 barcode found in image");
                    return new CarRental.Api.Services.Interfaces.BarcodeParseResult
                    {
                        Success = false,
                        ErrorCode = "BARCODE_NOT_FOUND",
                        Error = "BARCODE_NOT_FOUND",
                        Message = "Could not find PDF417 barcode in the image. Please ensure the back of the license is clearly visible."
                    };
                }

                _logger.LogInformation("PDF417 barcode detected, raw data length: {Length}", result.Text?.Length ?? 0);

                var barcodeData = result.Text;
                var parseResult = Barcode.Parse(barcodeData);

                if (parseResult?.Card == null)
                {
                    _logger.LogWarning("Barcode found but could not parse driver license data");
                    return new CarRental.Api.Services.Interfaces.BarcodeParseResult
                    {
                        Success = false,
                        ErrorCode = "PARSE_FAILED",
                        Error = "PARSE_FAILED",
                        Message = "Barcode found but could not parse driver license data."
                    };
                }

                var licenseData = MapToLicenseData(parseResult.Card);
                _logger.LogInformation("Successfully parsed driver license data for {FirstName} {LastName} (License: {LicenseNumber})",
                    licenseData.FirstName, licenseData.LastName, licenseData.LicenseNumber);

                return new CarRental.Api.Services.Interfaces.BarcodeParseResult
                {
                    Success = true,
                    Data = licenseData,
                    Message = "Driver license barcode successfully parsed",
                    ConfidenceScore = 1.0, // High confidence for barcode parsing
                    RawData = barcodeData // Store raw barcode data for audit
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing PDF417 barcode");
                return new CarRental.Api.Services.Interfaces.BarcodeParseResult
                {
                    Success = false,
                    ErrorCode = "PARSE_ERROR",
                    Error = "PARSE_ERROR",
                    Message = $"Error parsing barcode: {ex.Message}"
                };
            }
        });
    }

    private Result? TryDecodeWithPreprocessing(SKBitmap originalBitmap)
    {
        // Try grayscale
        using var grayscale = ConvertToGrayscale(originalBitmap);
        var result = _barcodeReader.Decode(grayscale);
        if (result != null)
        {
            _logger.LogInformation("Barcode found with grayscale preprocessing");
            return result;
        }

        // Try with increased contrast
        using var contrasted = AdjustContrast(grayscale, 1.5f);
        result = _barcodeReader.Decode(contrasted);
        if (result != null)
        {
            _logger.LogInformation("Barcode found with contrast adjustment");
            return result;
        }

        // Try rotated 90 degrees
        using var rotated90 = RotateBitmap(originalBitmap, 90);
        result = _barcodeReader.Decode(rotated90);
        if (result != null)
        {
            _logger.LogInformation("Barcode found with 90° rotation");
            return result;
        }

        // Try rotated 270 degrees
        using var rotated270 = RotateBitmap(originalBitmap, 270);
        result = _barcodeReader.Decode(rotated270);
        if (result != null)
        {
            _logger.LogInformation("Barcode found with 270° rotation");
            return result;
        }

        // Try rotated 180 degrees
        using var rotated180 = RotateBitmap(originalBitmap, 180);
        result = _barcodeReader.Decode(rotated180);
        if (result != null)
        {
            _logger.LogInformation("Barcode found with 180° rotation");
        }

        return result;
    }

    private SKBitmap ConvertToGrayscale(SKBitmap original)
    {
        var grayscale = new SKBitmap(original.Width, original.Height);

        using var canvas = new SKCanvas(grayscale);
        using var paint = new SKPaint();

        // Grayscale color matrix
        paint.ColorFilter = SKColorFilter.CreateColorMatrix(new float[]
        {
            0.21f, 0.72f, 0.07f, 0, 0,
            0.21f, 0.72f, 0.07f, 0, 0,
            0.21f, 0.72f, 0.07f, 0, 0,
            0,     0,     0,     1, 0
        });

        canvas.DrawBitmap(original, 0, 0, paint);
        return grayscale;
    }

    private SKBitmap AdjustContrast(SKBitmap original, float contrast)
    {
        var adjusted = new SKBitmap(original.Width, original.Height);

        using var canvas = new SKCanvas(adjusted);
        using var paint = new SKPaint();

        float t = (1.0f - contrast) / 2.0f * 255;
        paint.ColorFilter = SKColorFilter.CreateColorMatrix(new float[]
        {
            contrast, 0, 0, 0, t,
            0, contrast, 0, 0, t,
            0, 0, contrast, 0, t,
            0, 0, 0, 1, 0
        });

        canvas.DrawBitmap(original, 0, 0, paint);
        return adjusted;
    }

    private SKBitmap RotateBitmap(SKBitmap original, float degrees)
    {
        var rotated = new SKBitmap(
            degrees == 90 || degrees == 270 ? original.Height : original.Width,
            degrees == 90 || degrees == 270 ? original.Width : original.Height);

        using var canvas = new SKCanvas(rotated);
        canvas.Translate(rotated.Width / 2f, rotated.Height / 2f);
        canvas.RotateDegrees(degrees);
        canvas.Translate(-original.Width / 2f, -original.Height / 2f);
        canvas.DrawBitmap(original, 0, 0);

        return rotated;
    }

    private DriverLicenseData MapToLicenseData(IdentificationCard card)
    {
        var data = new DriverLicenseData();

        // Personal info - Field<T> is a struct, access .Value directly
        try { data.FirstName = card.FirstName.Value ?? string.Empty; } catch { data.FirstName = string.Empty; }
        try { data.MiddleName = card.MiddleName.Value ?? string.Empty; } catch { data.MiddleName = string.Empty; }
        try { data.LastName = card.LastName.Value ?? string.Empty; } catch { data.LastName = string.Empty; }
        try { data.Suffix = card.Suffix.Value ?? string.Empty; } catch { data.Suffix = string.Empty; }

        // Dates
        try { data.DateOfBirth = card.DateOfBirth.Value?.ToString("yyyy-MM-dd") ?? string.Empty; } catch { data.DateOfBirth = string.Empty; }
        try { data.ExpirationDate = card.ExpirationDate.Value?.ToString("yyyy-MM-dd") ?? string.Empty; } catch { data.ExpirationDate = string.Empty; }
        try { data.IssueDate = card.IssueDate.Value?.ToString("yyyy-MM-dd") ?? string.Empty; } catch { data.IssueDate = string.Empty; }

        // License info
        try { data.LicenseNumber = card.IdNumber.Value ?? string.Empty; } catch { data.LicenseNumber = string.Empty; }
        try { data.DocumentNumber = card.IdNumber.Value ?? string.Empty; } catch { data.DocumentNumber = string.Empty; }
        try { data.IssuingState = card.JurisdictionCode.Value ?? string.Empty; } catch { data.IssuingState = string.Empty; }

        // Address
        try { data.AddressLine1 = card.StreetLine1.Value ?? string.Empty; } catch { data.AddressLine1 = string.Empty; }
        try { data.AddressLine2 = card.StreetLine2.Value ?? string.Empty; } catch { data.AddressLine2 = string.Empty; }
        try { data.City = card.City.Value ?? string.Empty; } catch { data.City = string.Empty; }
        try { data.State = card.JurisdictionCode.Value ?? string.Empty; } catch { data.State = string.Empty; }
        try { data.ZipCode = card.PostalCode.Value ?? string.Empty; } catch { data.ZipCode = string.Empty; }
        try { data.PostalCode = card.PostalCode.Value ?? string.Empty; } catch { data.PostalCode = string.Empty; }
        try { data.Country = card.Country.Value.ToString(); } catch { data.Country = "USA"; }

        // Physical description
        try { data.Gender = card.Sex.Value?.ToString() ?? string.Empty; } catch { data.Gender = string.Empty; }
        try { data.EyeColor = card.EyeColor.Value?.ToString() ?? string.Empty; } catch { data.EyeColor = string.Empty; }
        try { data.HairColor = card.HairColor.Value?.ToString() ?? string.Empty; } catch { data.HairColor = string.Empty; }
        try { data.Height = card.Height.Value?.ToString() ?? string.Empty; } catch { data.Height = string.Empty; }
        try { data.Weight = card.Weight.Value?.ToString() ?? string.Empty; } catch { data.Weight = string.Empty; }

        // AAMVA version info
        data.AamvaVersion = string.Empty;
        data.JurisdictionVersion = string.Empty;

        // Additional fields for backwards compatibility
        data.Address = data.AddressLine1; // Use primary address line
        data.Sex = data.Gender;           // Alias for gender

        return data;
    }
}

/// <summary>
/// SkiaSharp luminance source for ZXing barcode reading
/// </summary>
public class SKBitmapLuminanceSource : BaseLuminanceSource
{
    public SKBitmapLuminanceSource(SKBitmap bitmap)
        : base(bitmap.Width, bitmap.Height)
    {
        var pixels = bitmap.Pixels;
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                var pixel = pixels[y * bitmap.Width + x];
                // Calculate luminance using standard formula
                var luminance = (byte)((pixel.Red * 299 + pixel.Green * 587 + pixel.Blue * 114) / 1000);
                luminances[y * bitmap.Width + x] = luminance;
            }
        }
    }

    protected SKBitmapLuminanceSource(byte[] luminances, int width, int height)
        : base(luminances, width, height)
    {
    }

    protected override LuminanceSource CreateLuminanceSource(byte[] newLuminances, int width, int height)
    {
        return new SKBitmapLuminanceSource(newLuminances, width, height);
    }
}