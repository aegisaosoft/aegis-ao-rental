using CarRental.Api.Models;

namespace CarRental.Api.Services.Interfaces;

public interface IDocumentAiService
{
    Task<DocumentAiResult> ProcessDriverLicenseAsync(Stream imageStream, string mimeType);
    Task<DocumentAiResult> ProcessDriverLicenseAsync(byte[] imageData, string mimeType);
}

public class DocumentAiResult
{
    public bool Success { get; set; }
    public LicenseOcrData? Data { get; set; }
    public string? Message { get; set; }
    public string? Error { get; set; }
    public ValidationResult? Validation { get; set; }
}

public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}