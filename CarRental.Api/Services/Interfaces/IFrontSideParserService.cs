using CarRental.Api.Controllers;

namespace CarRental.Api.Services.Interfaces;

public interface IFrontSideParserService
{
    /// <summary>
    /// Parse the front side of a driver license from raw image bytes.
    /// Full pipeline: HEIC→PNG + EXIF orientation + compress ≤ 1 MB.
    /// </summary>
    Task<DocumentAiParseResult> ParseFrontSideAsync(byte[] imageData);

    /// <summary>
    /// Parse the front side with fileName/contentType for HEIC detection.
    /// Full pipeline: HEIC→PNG + EXIF orientation + compress ≤ 1 MB.
    /// </summary>
    Task<DocumentAiParseResult> ParseFrontSideAsync(byte[] imageData, string? fileName, string? contentType);

    /// <summary>
    /// Parse the front side of a driver license from an uploaded file stream.
    /// Full pipeline: HEIC→PNG + EXIF orientation + compress ≤ 1 MB.
    /// </summary>
    Task<DocumentAiParseResult> ParseFrontSideAsync(Stream imageStream);
}
