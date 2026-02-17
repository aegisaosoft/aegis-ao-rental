using CarRental.Api.Models;

namespace CarRental.Api.Services.Interfaces;

public interface IBarcodeParserService
{
    Task<BarcodeParseResult> ParseDriverLicenseBarcodeAsync(Stream imageStream, string mimeType);
    Task<BarcodeParseResult> ParseDriverLicenseBarcodeAsync(byte[] imageData, string? fileName = null, string? contentType = null);
}

public class BarcodeParseResult
{
    public bool Success { get; set; }
    public DriverLicenseData? Data { get; set; }
    public string? Message { get; set; }
    public string? Error { get; set; }
    public string ErrorCode { get; set; } = string.Empty;
    public double ConfidenceScore { get; set; } = 1.0;  // Default confidence score
    public string? RawData { get; set; } // Raw barcode data for audit purposes
}