/*
 * Copyright (c) 2025 Alexander Orlov.
 * 34 Middletown Ave Atlantic Highlands NJ 07716
 *
 * ZXing BarcodeReader warmup service.
 * Pre-creates pooled readers and forces JIT compilation at startup
 * to prevent cold-start failures on Azure App Service.
 */

using System.Diagnostics;
using Microsoft.Extensions.ObjectPool;
using SkiaSharp;
using ZXing;
using ZXing.SkiaSharp;

namespace CarRental.Api.HostedServices;

/// <summary>
/// Hosted service that warms up the BarcodeReader ObjectPool at application startup.
/// Pre-creates N readers (N = ProcessorCount) and decodes a dummy image to force
/// JIT compilation of all code paths, eliminating cold-start latency.
/// </summary>
public class ZXingWarmupService : IHostedService
{
    private readonly ObjectPool<BarcodeReader<SKBitmap>> _readerPool;
    private readonly ILogger<ZXingWarmupService> _logger;

    public ZXingWarmupService(
        ObjectPool<BarcodeReader<SKBitmap>> readerPool,
        ILogger<ZXingWarmupService> logger)
    {
        _readerPool = readerPool;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var readerCount = Environment.ProcessorCount;

        _logger.LogInformation("ZXing warmup starting — pre-warming {Count} readers...", readerCount);

        // Pre-create readers and decode a dummy white image to force JIT compilation
        var readers = new BarcodeReader<SKBitmap>[readerCount];
        try
        {
            for (int i = 0; i < readerCount; i++)
            {
                readers[i] = _readerPool.Get();
            }

            // Decode a dummy 100x100 white bitmap to force JIT of Decode path
            using var dummyBitmap = new SKBitmap(100, 100);
            dummyBitmap.Erase(SKColors.White);

            foreach (var reader in readers)
            {
                try
                {
                    // Decode will return null (no barcode in white image) but forces JIT
                    reader.Decode(dummyBitmap);
                }
                catch
                {
                    // Expected — no barcode in dummy image
                }
            }
        }
        finally
        {
            // Return all readers to the pool
            foreach (var reader in readers)
            {
                if (reader != null)
                {
                    _readerPool.Return(reader);
                }
            }
        }

        sw.Stop();
        _logger.LogInformation(
            "ZXing warmup complete — {Count} readers pre-warmed in {ElapsedMs}ms",
            readerCount, sw.ElapsedMilliseconds);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
