/*
 * Copyright (c) 2025 Alexander Orlov.
 * 34 Middletown Ave Atlantic Highlands NJ 07716
 *
 * Image processing service (singleton) — performance-optimized.
 *
 * Full pipeline:
 *   1. HEIC/HEIF → JPEG conversion  (Magick.NET / ImageMagick + AutoOrient)
 *   2. EXIF orientation correction   (SKCodec → rotate/flip in-memory)
 *   3. Compression to ≤ 1 MB         (binary-search JPEG quality + scale)
 *
 * Key optimisations:
 *   • HEIC converts to JPEG (not PNG) — 3-5× smaller intermediate.
 *   • Magick.AutoOrient() handles HEIC EXIF; no second SKCodec pass.
 *   • Single SKBitmap.Decode per image — orient + compress in-memory.
 *   • Binary-search quality selection — ~3 encodes instead of 6.
 *   • Static readonly HashSets — zero per-call allocation.
 *
 * No mutable state — safe for concurrent use.
 */

using System.Diagnostics;
using ImageMagick;
using SkiaSharp;

namespace CarRental.Api.Services;

/// <summary>
/// Singleton service: HEIC conversion + EXIF orientation + compression.
/// Thread-safe, zero mutable state.
/// </summary>
public class ImageOrientationService
{
    private const int MaxFileSizeBytes = 1_048_576; // 1 MB
    private const int HeicJpegQuality  = 92;        // Magick HEIC → JPEG quality
    private const int MaxJpegQuality   = 90;
    private const int MinJpegQuality   = 40;

    private readonly ILogger<ImageOrientationService> _logger;

    // ── Static readonly sets (allocated once at class load) ─────────
    private static readonly HashSet<string> HeicMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/heic", "image/heif", "image/x-heic", "image/x-heif"
    };

    private static readonly HashSet<string> HeicExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".heic", ".heif"
    };

    private static readonly HashSet<string> SupportedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/bmp", "image/tiff",
        "image/webp", "image/gif",
        "image/heic", "image/heif", "image/x-heic", "image/x-heif"
    };

    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".tiff", ".tif",
        ".webp", ".gif", ".heic", ".heif"
    };

    public ImageOrientationService(ILogger<ImageOrientationService> logger)
    {
        _logger = logger;
    }

    // ──────────────────────────────────────────────────────────────────
    //  Public: detection helpers
    // ──────────────────────────────────────────────────────────────────

    /// <summary>Check whether the file is HEIC/HEIF by MIME type or extension.</summary>
    public static bool IsHeicFile(string? fileName, string? contentType)
    {
        if (!string.IsNullOrEmpty(contentType) && HeicMimeTypes.Contains(contentType))
            return true;

        if (!string.IsNullOrEmpty(fileName))
        {
            var ext = Path.GetExtension(fileName);
            if (!string.IsNullOrEmpty(ext) && HeicExtensions.Contains(ext))
                return true;
        }

        return false;
    }

    /// <summary>Check whether the file is any supported image. Uses static sets — zero allocation.</summary>
    public static bool IsSupportedImageFile(IFormFile file)
    {
        if (!string.IsNullOrEmpty(file.ContentType) && SupportedMimeTypes.Contains(file.ContentType))
            return true;

        var ext = Path.GetExtension(file.FileName);
        return !string.IsNullOrEmpty(ext) && SupportedExtensions.Contains(ext);
    }

    // ──────────────────────────────────────────────────────────────────
    //  Public: full pipeline  (HEIC → orient → compress)
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Full pipeline from IFormFile:
    /// HEIC→JPEG, EXIF orient, compress ≤ 1 MB.
    /// Single decode, single encode.
    /// </summary>
    public async Task<byte[]> ProcessImageAsync(IFormFile file)
    {
        using var ms = new MemoryStream((int)file.Length);
        await file.CopyToAsync(ms);
        return ProcessImageBytes(ms.ToArray(), file.FileName, file.ContentType);
    }

    /// <summary>
    /// Full pipeline from raw bytes:
    /// HEIC→JPEG (Magick+AutoOrient), EXIF orient, compress ≤ 1 MB.
    /// Optimised: single decode + single encode path.
    /// </summary>
    public byte[] ProcessImageBytes(byte[] imageData, string? fileName = null, string? contentType = null)
    {
        var sw = Stopwatch.StartNew();
        bool isHeic = IsHeicFile(fileName, contentType) || IsHeicBySignature(imageData);

        // ── HEIC fast-path: Magick handles decode + orient + JPEG encode ──
        if (isHeic)
        {
            var jpegBytes = ConvertHeicToJpeg(imageData);
            _logger.LogInformation("HEIC→JPEG done ({OrigKB} KB → {NewKB} KB) in {Ms}ms",
                imageData.Length / 1024, jpegBytes.Length / 1024, sw.ElapsedMilliseconds);

            // If already ≤ 1 MB, we're done
            if (jpegBytes.Length <= MaxFileSizeBytes)
            {
                _logger.LogInformation("Pipeline complete (HEIC fast-path) in {Ms}ms, {Size} KB",
                    sw.ElapsedMilliseconds, jpegBytes.Length / 1024);
                return jpegBytes;
            }

            // Need further compression — single decode from JPEG
            var compressed = CompressFromBytes(jpegBytes);
            _logger.LogInformation("Pipeline complete (HEIC+compress) in {Ms}ms, {OrigKB} KB → {ResultKB} KB",
                sw.ElapsedMilliseconds, imageData.Length / 1024, compressed.Length / 1024);
            return compressed;
        }

        // ── Non-HEIC: single-decode pipeline ──────────────────────────
        // 1. Read EXIF origin (header-only, lightweight)
        var origin = ReadExifOrigin(imageData);

        // 2. If no orientation needed and already ≤ 1 MB → return as-is
        if (origin == SKEncodedOrigin.TopLeft && imageData.Length <= MaxFileSizeBytes)
        {
            _logger.LogInformation("Pipeline complete (no-op) in {Ms}ms, {Size} KB",
                sw.ElapsedMilliseconds, imageData.Length / 1024);
            return imageData;
        }

        // 3. Single decode
        using var bitmap = SKBitmap.Decode(imageData);
        if (bitmap == null)
        {
            _logger.LogWarning("Failed to decode image, returning original");
            return imageData;
        }

        // 4. Apply orientation in-memory (no encode/decode cycle)
        SKBitmap oriented;
        bool wasOriented = false;
        if (origin != SKEncodedOrigin.TopLeft)
        {
            oriented = ApplyOrientation(bitmap, origin);
            wasOriented = true;
        }
        else
        {
            oriented = bitmap;
        }

        try
        {
            // 5. Encode with compression (binary-search quality)
            var result = CompressFromBitmap(oriented, MaxFileSizeBytes);
            _logger.LogInformation(
                "Pipeline complete in {Ms}ms: {W}x{H}, oriented={Oriented}, {OrigKB} KB → {ResultKB} KB",
                sw.ElapsedMilliseconds, oriented.Width, oriented.Height, wasOriented,
                imageData.Length / 1024, result.Length / 1024);
            return result;
        }
        finally
        {
            if (wasOriented)
                oriented.Dispose();
        }
    }

    /// <summary>
    /// Decode + orient into an SKBitmap (for barcode parsing).
    /// Single decode path. Caller must dispose the returned bitmap.
    /// </summary>
    public SKBitmap DecodeAndCorrectOrientation(byte[] imageData, string? fileName = null, string? contentType = null)
    {
        var sw = Stopwatch.StartNew();
        bool isHeic = IsHeicFile(fileName, contentType) || IsHeicBySignature(imageData);

        byte[] decodableBytes;
        bool heicOriented = false;

        if (isHeic)
        {
            // Magick: HEIC → JPEG + AutoOrient (handles HEIC EXIF natively)
            decodableBytes = ConvertHeicToJpeg(imageData);
            heicOriented = true;
            _logger.LogInformation("HEIC→JPEG for bitmap decode ({OrigKB} KB → {NewKB} KB) in {Ms}ms",
                imageData.Length / 1024, decodableBytes.Length / 1024, sw.ElapsedMilliseconds);
        }
        else
        {
            decodableBytes = imageData;
        }

        // Read EXIF (skip if Magick already oriented the HEIC)
        var origin = heicOriented ? SKEncodedOrigin.TopLeft : ReadExifOrigin(decodableBytes);

        // Single decode
        var bitmap = SKBitmap.Decode(decodableBytes);
        if (bitmap == null)
            throw new InvalidOperationException("Failed to decode image data");

        if (origin == SKEncodedOrigin.TopLeft)
        {
            _logger.LogInformation("Bitmap decode complete in {Ms}ms, {W}x{H}",
                sw.ElapsedMilliseconds, bitmap.Width, bitmap.Height);
            return bitmap;
        }

        // Apply orientation in-memory
        var corrected = ApplyOrientation(bitmap, origin);
        _logger.LogInformation("Bitmap decode+orient in {Ms}ms: {Origin}, {W}x{H} → {CW}x{CH}",
            sw.ElapsedMilliseconds, origin, bitmap.Width, bitmap.Height, corrected.Width, corrected.Height);
        bitmap.Dispose();
        return corrected;
    }

    /// <summary>Read EXIF orientation from image header. Lightweight — does not fully decode pixels.</summary>
    public SKEncodedOrigin GetExifOrientation(byte[] imageData) => ReadExifOrigin(imageData);

    // ──────────────────────────────────────────────────────────────────
    //  Private: HEIC → JPEG conversion (Magick.NET)
    // ──────────────────────────────────────────────────────────────────

    /// <summary>HEIC → JPEG with AutoOrient. EXIF is handled by Magick natively.</summary>
    private byte[] ConvertHeicToJpeg(byte[] heicData)
    {
        try
        {
            using var image = new MagickImage(heicData);
            image.AutoOrient();              // Magick reads HEIC EXIF natively
            image.Format = MagickFormat.Jpeg;
            image.Quality = HeicJpegQuality;

            using var output = new MemoryStream();
            image.Write(output);
            return output.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Magick.NET HEIC→JPEG conversion failed");
            throw new InvalidOperationException($"HEIC conversion failed: {ex.Message}", ex);
        }
    }

    /// <summary>Detect HEIC by file signature (ftyp box at offset 4).</summary>
    private static bool IsHeicBySignature(byte[] data)
    {
        if (data.Length < 12) return false;
        if (data[4] != (byte)'f' || data[5] != (byte)'t' ||
            data[6] != (byte)'y' || data[7] != (byte)'p')
            return false;

        // brand at offset 8 (4 ASCII chars)
        Span<char> brand = stackalloc char[4];
        brand[0] = (char)data[8];
        brand[1] = (char)data[9];
        brand[2] = (char)data[10];
        brand[3] = (char)data[11];

        return brand.SequenceEqual("heic") || brand.SequenceEqual("heix") ||
               brand.SequenceEqual("hevc") || brand.SequenceEqual("hevx") ||
               brand.SequenceEqual("mif1") || brand.SequenceEqual("msf1") ||
               brand.SequenceEqual("avif");
    }

    // ──────────────────────────────────────────────────────────────────
    //  Private: EXIF orientation (header-only read)
    // ──────────────────────────────────────────────────────────────────

    private SKEncodedOrigin ReadExifOrigin(byte[] imageData)
    {
        try
        {
            using var stream = new MemoryStream(imageData, writable: false);
            using var codec = SKCodec.Create(stream);
            if (codec == null) return SKEncodedOrigin.TopLeft;
            var origin = codec.EncodedOrigin;
            if (origin != SKEncodedOrigin.TopLeft)
                _logger.LogInformation("EXIF orientation: {Origin} ({Value})", origin, (int)origin);
            return origin;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "EXIF read failed, defaulting to TopLeft");
            return SKEncodedOrigin.TopLeft;
        }
    }

    // ──────────────────────────────────────────────────────────────────
    //  Private: compression (works on already-decoded bitmap)
    // ──────────────────────────────────────────────────────────────────

    /// <summary>Decode bytes once, then compress the bitmap.</summary>
    private byte[] CompressFromBytes(byte[] imageData)
    {
        using var bitmap = SKBitmap.Decode(imageData);
        if (bitmap == null)
        {
            _logger.LogWarning("Cannot decode for compression, returning as-is");
            return imageData;
        }

        return CompressFromBitmap(bitmap, MaxFileSizeBytes);
    }

    /// <summary>
    /// Encode bitmap to JPEG ≤ maxSize using binary-search quality,
    /// then scale-down if quality alone isn't enough.
    /// </summary>
    private byte[] CompressFromBitmap(SKBitmap bitmap, int maxSize)
    {
        // Fast check: encode at max quality first
        using var image = SKImage.FromBitmap(bitmap);
        using var maxQData = image.Encode(SKEncodedImageFormat.Jpeg, MaxJpegQuality);
        if (maxQData.Size <= maxSize)
            return maxQData.ToArray();

        _logger.LogInformation("Image {W}x{H} is {KB} KB at q{Q}, compressing...",
            bitmap.Width, bitmap.Height, maxQData.Size / 1024, MaxJpegQuality);

        // Binary search for optimal quality
        var result = BinarySearchQuality(image, maxSize, MinJpegQuality, MaxJpegQuality - 1);
        if (result != null)
            return result;

        // Quality alone didn't work — scale down progressively
        for (float scale = 0.75f; scale >= 0.2f; scale -= 0.1f)
        {
            int newW = Math.Max((int)(bitmap.Width * scale), 100);
            int newH = Math.Max((int)(bitmap.Height * scale), 100);

            using var resized = bitmap.Resize(new SKImageInfo(newW, newH), SKFilterQuality.Medium);
            if (resized == null) continue;

            using var resizedImage = SKImage.FromBitmap(resized);

            // Try binary search on the resized image
            result = BinarySearchQuality(resizedImage, maxSize, MinJpegQuality, MaxJpegQuality);
            if (result != null)
            {
                _logger.LogInformation("Compressed at scale {Scale:P0}, {NewW}x{NewH}", scale, newW, newH);
                return result;
            }
        }

        // Last resort: 20% scale at minimum quality
        {
            int newW = Math.Max((int)(bitmap.Width * 0.2f), 100);
            int newH = Math.Max((int)(bitmap.Height * 0.2f), 100);
            using var resized = bitmap.Resize(new SKImageInfo(newW, newH), SKFilterQuality.Low);
            using var img = SKImage.FromBitmap(resized ?? bitmap);
            using var encoded = img.Encode(SKEncodedImageFormat.Jpeg, MinJpegQuality);
            _logger.LogWarning("Last-resort compression: {KB} KB at 20% scale + q{Q}", encoded.Size / 1024, MinJpegQuality);
            return encoded.ToArray();
        }
    }

    /// <summary>Binary search for JPEG quality that fits within maxSize. Returns null if min quality still too large.</summary>
    private static byte[]? BinarySearchQuality(SKImage image, int maxSize, int lo, int hi)
    {
        byte[]? bestFit = null;
        while (lo <= hi)
        {
            int mid = (lo + hi) / 2;
            using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, mid);
            if (encoded.Size <= maxSize)
            {
                bestFit = encoded.ToArray();
                lo = mid + 1; // try higher quality
            }
            else
            {
                hi = mid - 1; // try lower quality
            }
        }
        return bestFit;
    }

    // ──────────────────────────────────────────────────────────────────
    //  Private: orientation transforms (in-memory, no encode/decode)
    // ──────────────────────────────────────────────────────────────────

    private SKBitmap ApplyOrientation(SKBitmap bitmap, SKEncodedOrigin origin)
    {
        switch (origin)
        {
            case SKEncodedOrigin.TopLeft:     return bitmap.Copy();
            case SKEncodedOrigin.TopRight:    return FlipHorizontal(bitmap);
            case SKEncodedOrigin.BottomRight: return Rotate(bitmap, 180);
            case SKEncodedOrigin.BottomLeft:  return FlipVertical(bitmap);
            case SKEncodedOrigin.LeftTop:
            {
                using var rotated = Rotate(bitmap, 90);
                return FlipHorizontal(rotated);
            }
            case SKEncodedOrigin.RightTop:    return Rotate(bitmap, 90);
            case SKEncodedOrigin.RightBottom:
            {
                using var rotated = Rotate(bitmap, 270);
                return FlipHorizontal(rotated);
            }
            case SKEncodedOrigin.LeftBottom:  return Rotate(bitmap, 270);
            default:
                _logger.LogWarning("Unknown EXIF orientation {Origin}", origin);
                return bitmap.Copy();
        }
    }

    private static SKBitmap Rotate(SKBitmap original, float degrees)
    {
        bool swap = degrees is 90 or 270;
        int newW = swap ? original.Height : original.Width;
        int newH = swap ? original.Width : original.Height;

        var rotated = new SKBitmap(newW, newH);
        using var canvas = new SKCanvas(rotated);
        canvas.Translate(newW / 2f, newH / 2f);
        canvas.RotateDegrees(degrees);
        canvas.Translate(-original.Width / 2f, -original.Height / 2f);
        canvas.DrawBitmap(original, 0, 0);
        return rotated;
    }

    private static SKBitmap FlipHorizontal(SKBitmap original)
    {
        var flipped = new SKBitmap(original.Width, original.Height);
        using var canvas = new SKCanvas(flipped);
        canvas.Scale(-1, 1, original.Width / 2f, 0);
        canvas.DrawBitmap(original, 0, 0);
        return flipped;
    }

    private static SKBitmap FlipVertical(SKBitmap original)
    {
        var flipped = new SKBitmap(original.Width, original.Height);
        using var canvas = new SKCanvas(flipped);
        canvas.Scale(1, -1, 0, original.Height / 2f);
        canvas.DrawBitmap(original, 0, 0);
        return flipped;
    }
}
