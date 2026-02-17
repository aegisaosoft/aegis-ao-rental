/*
 * Copyright (c) 2025 Alexander Orlov.
 * 34 Middletown Ave Atlantic Highlands NJ 07716
 *
 * Driver License PDF417 Barcode Parser Service
 * Thread-safe implementation using ObjectPool + SkiaSharp + ZXing.Net
 *
 * CRITICAL: BarcodeReader<SKBitmap> is NOT thread-safe.
 * Every parse call gets its own reader from the pool and returns it in finally.
 * NEVER store a pooled reader as a class field.
 */

using CarRental.Api.Models;
using CarRental.Api.Services.Interfaces;
using IdParser.Core;
using Microsoft.Extensions.ObjectPool;
using SkiaSharp;
using ZXing;
using ZXing.Common;
using ZXing.SkiaSharp;
using BarcodeParseResult = CarRental.Api.Services.Interfaces.BarcodeParseResult;

namespace CarRental.Api.Services;

/// <summary>
/// Singleton service for parsing PDF417 barcodes from driver licenses.
/// Uses ObjectPool for thread-safe concurrent barcode reading.
/// </summary>
public class BarcodeParserService : IBarcodeParserService
{
    private readonly ObjectPool<BarcodeReader<SKBitmap>> _readerPool;
    private readonly ImageOrientationService _orientationService;
    private readonly ILogger<BarcodeParserService> _logger;

    public BarcodeParserService(
        ObjectPool<BarcodeReader<SKBitmap>> readerPool,
        ImageOrientationService orientationService,
        ILogger<BarcodeParserService> logger)
    {
        _readerPool = readerPool;
        _orientationService = orientationService;
        _logger = logger;

        // Verify SkiaSharp is properly loaded (especially important on Azure)
        try
        {
            var testBitmap = new SKBitmap(1, 1);
            testBitmap.Dispose();
            _logger.LogInformation("BarcodeParserService initialized with SkiaSharp support and ObjectPool - Azure native assets loaded successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SkiaSharp initialization failed - native assets may be missing. Error: {Error}", ex.Message);
            throw new InvalidOperationException($"SkiaSharp not properly initialized: {ex.Message}", ex);
        }
    }

    public async Task<BarcodeParseResult> ParseDriverLicenseBarcodeAsync(Stream imageStream, string mimeType)
    {
        using var memoryStream = new MemoryStream();
        await imageStream.CopyToAsync(memoryStream);
        return await ParseDriverLicenseBarcodeAsync(memoryStream.ToArray());
    }

    public Task<BarcodeParseResult> ParseDriverLicenseBarcodeAsync(byte[] imageData, string? fileName = null, string? contentType = null)
    {
        return Task.Run(() =>
        {
            try
            {
                _logger.LogInformation("Starting PDF417 barcode parsing from image ({Size} bytes, file={FileName}, type={ContentType})",
                    imageData.Length, fileName ?? "unknown", contentType ?? "unknown");

                // Full pipeline: HEIC→PNG + EXIF orientation (fileName/contentType needed for HEIC detection)
                SKBitmap? bitmap;
                try
                {
                    bitmap = _orientationService.DecodeAndCorrectOrientation(imageData, fileName, contentType);
                }
                catch (InvalidOperationException)
                {
                    bitmap = null;
                }

                if (bitmap == null)
                {
                    _logger.LogWarning("Could not decode image data");
                    return new BarcodeParseResult
                    {
                        Success = false,
                        ErrorCode = "INVALID_IMAGE",
                        Error = "INVALID_IMAGE",
                        Message = "Could not decode image data. Please ensure the file is a valid image."
                    };
                }

                using (bitmap)
                {
                    // Get reader from pool, decode, return reader — thread-safe pattern
                    var reader = _readerPool.Get();
                    Result? result;
                    try
                    {
                        result = reader.Decode(bitmap);
                    }
                    finally
                    {
                        _readerPool.Return(reader);
                    }

                    // If not found, try with preprocessing
                    if (result == null)
                    {
                        _logger.LogInformation("Initial barcode detection failed, trying with preprocessing");
                        result = TryDecodeWithPreprocessing(bitmap);
                    }

                    if (result == null)
                    {
                        _logger.LogWarning("No PDF417 barcode found in image");
                        return new BarcodeParseResult
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
                        return new BarcodeParseResult
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

                    return new BarcodeParseResult
                    {
                        Success = true,
                        Data = licenseData,
                        Message = "Driver license barcode successfully parsed",
                        ConfidenceScore = 1.0,
                        RawData = barcodeData
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing PDF417 barcode");
                return new BarcodeParseResult
                {
                    Success = false,
                    ErrorCode = "PARSE_ERROR",
                    Error = "PARSE_ERROR",
                    Message = $"Error parsing barcode: {ex.Message}"
                };
            }
        });
    }

    /// <summary>
    /// Decode a single bitmap using a pooled reader. Thread-safe: Get → Decode → Return.
    /// </summary>
    private Result? DecodeWithPooledReader(SKBitmap bitmap)
    {
        var reader = _readerPool.Get();
        try
        {
            return reader.Decode(bitmap);
        }
        finally
        {
            _readerPool.Return(reader);
        }
    }

    private Result? TryDecodeWithPreprocessing(SKBitmap originalBitmap)
    {
        // Try grayscale
        using var grayscale = ConvertToGrayscale(originalBitmap);
        var result = DecodeWithPooledReader(grayscale);
        if (result != null)
        {
            _logger.LogInformation("Barcode found with grayscale preprocessing");
            return result;
        }

        // Try with increased contrast
        using var contrasted = AdjustContrast(grayscale, 1.5f);
        result = DecodeWithPooledReader(contrasted);
        if (result != null)
        {
            _logger.LogInformation("Barcode found with contrast adjustment");
            return result;
        }

        // Try rotated 90 degrees
        using var rotated90 = RotateBitmap(originalBitmap, 90);
        result = DecodeWithPooledReader(rotated90);
        if (result != null)
        {
            _logger.LogInformation("Barcode found with 90 degree rotation");
            return result;
        }

        // Try rotated 270 degrees
        using var rotated270 = RotateBitmap(originalBitmap, 270);
        result = DecodeWithPooledReader(rotated270);
        if (result != null)
        {
            _logger.LogInformation("Barcode found with 270 degree rotation");
            return result;
        }

        // Try rotated 180 degrees
        using var rotated180 = RotateBitmap(originalBitmap, 180);
        result = DecodeWithPooledReader(rotated180);
        if (result != null)
        {
            _logger.LogInformation("Barcode found with 180 degree rotation");
        }

        return result;
    }

    private SKBitmap ConvertToGrayscale(SKBitmap original)
    {
        var grayscale = new SKBitmap(original.Width, original.Height);

        using var canvas = new SKCanvas(grayscale);
        using var paint = new SKPaint();

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
        data.Address = data.AddressLine1;
        data.Sex = data.Gender;

        return data;
    }
}

/// <summary>
/// SkiaSharp luminance source for ZXing barcode reading.
/// Optimised: reads raw pixel bytes via GetPixelSpan() — avoids
/// allocating a managed SKColor[] copy (bitmap.Pixels).
/// </summary>
public class SKBitmapLuminanceSource : BaseLuminanceSource
{
    public SKBitmapLuminanceSource(SKBitmap bitmap)
        : base(bitmap.Width, bitmap.Height)
    {
        var w = bitmap.Width;
        var h = bitmap.Height;
        var span = bitmap.GetPixelSpan(); // ReadOnlySpan<byte>, no managed copy
        int bytesPerPixel = bitmap.BytesPerPixel;

        // Most common: BGRA8888 (4 bytes/pixel) or RGBA8888
        // Both have R/G/B in the first 3 bytes (order differs but luminance is symmetric enough)
        if (bytesPerPixel >= 3)
        {
            bool isBgra = bitmap.ColorType == SKColorType.Bgra8888;
            for (int i = 0; i < w * h; i++)
            {
                int offset = i * bytesPerPixel;
                byte r, g, b;
                if (isBgra)
                {
                    b = span[offset];
                    g = span[offset + 1];
                    r = span[offset + 2];
                }
                else
                {
                    r = span[offset];
                    g = span[offset + 1];
                    b = span[offset + 2];
                }
                luminances[i] = (byte)((r * 299 + g * 587 + b * 114) / 1000);
            }
        }
        else
        {
            // Fallback for unusual color types — use SKColor API
            var pixels = bitmap.Pixels;
            for (int i = 0; i < w * h; i++)
            {
                var p = pixels[i];
                luminances[i] = (byte)((p.Red * 299 + p.Green * 587 + p.Blue * 114) / 1000);
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
