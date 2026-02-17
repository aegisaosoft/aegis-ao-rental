using SkiaSharp;

// Find original test image
var testPaths = new[]
{
    @"C:\temp\original.jpg",
    @"C:\temp\test.jpg",
    @"C:\temp\front.jpg",
};

string? foundPath = null;
foreach (var p in testPaths)
{
    if (File.Exists(p)) { foundPath = p; break; }
}

// Also scan C:\temp for any jpg
if (foundPath == null && Directory.Exists(@"C:\temp"))
{
    var jpgs = Directory.GetFiles(@"C:\temp", "*.jpg");
    if (jpgs.Length > 0) foundPath = jpgs[0];
    var jpegs = Directory.GetFiles(@"C:\temp", "*.jpeg");
    if (jpegs.Length > 0) foundPath ??= jpegs[0];
}

if (foundPath == null)
{
    Console.WriteLine("No test image found. Copy your license photo to C:\\temp\\original.jpg and re-run.");
    return;
}

Console.WriteLine($"=== Testing: {foundPath} ===");
var origBytes = File.ReadAllBytes(foundPath);
AnalyzeImage("ORIGINAL", origBytes);

// Simulate pipeline: orient and save
Console.WriteLine("\n=== Simulating pipeline (orient) ===");
SimulateOrient(origBytes);

void AnalyzeImage(string label, byte[] data)
{
    Console.WriteLine($"[{label}] File size: {data.Length} bytes ({data.Length/1024} KB)");

    // SKCodec - header only
    using var stream1 = new MemoryStream(data);
    using var codec = SKCodec.Create(stream1);
    if (codec != null)
    {
        Console.WriteLine($"[{label}] SKCodec: {codec.Info.Width}x{codec.Info.Height}, EXIF origin={codec.EncodedOrigin} ({(int)codec.EncodedOrigin})");
    }
    else
    {
        Console.WriteLine($"[{label}] SKCodec: NULL");
    }

    // SKBitmap.Decode
    using var bitmap = SKBitmap.Decode(data);
    if (bitmap != null)
    {
        Console.WriteLine($"[{label}] SKBitmap.Decode: {bitmap.Width}x{bitmap.Height}");

        if (codec != null)
        {
            bool swapped = bitmap.Width == codec.Info.Height && bitmap.Height == codec.Info.Width;
            bool same = bitmap.Width == codec.Info.Width && bitmap.Height == codec.Info.Height;
            Console.WriteLine($"[{label}] Dimensions vs codec: swapped={swapped}, same={same}");

            if (swapped)
                Console.WriteLine($"[{label}] >>> SKBitmap.Decode AUTO-ROTATED (dimensions swapped)");
            else if (same)
                Console.WriteLine($"[{label}] >>> SKBitmap.Decode did NOT auto-rotate (dimensions match codec)");
        }
    }
}
