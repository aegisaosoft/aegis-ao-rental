/*
 * Copyright (c) 2025 Alexander Orlov.
 * 34 Middletown Ave Atlantic Highlands NJ 07716
 *
 * Thread-safe ObjectPool policy for ZXing BarcodeReader instances.
 * BarcodeReaderGeneric is NOT thread-safe, so each concurrent parse
 * must use its own reader obtained from the pool via Get/Return.
 */

using Microsoft.Extensions.ObjectPool;
using SkiaSharp;
using ZXing;
using ZXing.Common;
using ZXing.SkiaSharp;

namespace CarRental.Api.Services;

/// <summary>
/// Pool policy that creates and manages BarcodeReader instances for PDF417 parsing.
/// Each reader is configured for driver license barcode scanning with optimal settings.
/// </summary>
public class BarcodeReaderPoolPolicy : IPooledObjectPolicy<BarcodeReader<SKBitmap>>
{
    public BarcodeReader<SKBitmap> Create()
    {
        return new BarcodeReader<SKBitmap>(bitmap => new SKBitmapLuminanceSource(bitmap))
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
    }

    public bool Return(BarcodeReader<SKBitmap> obj)
    {
        // BarcodeReader is stateless between Decode calls, safe to reuse
        return true;
    }
}
