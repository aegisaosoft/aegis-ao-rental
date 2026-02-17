/*
 * Copyright (c) 2025 Alexander Orlov.
 * 34 Middletown Ave Atlantic Highlands NJ 07716
 *
 * Front-side driver license parser service.
 * Singleton with EXIF orientation correction — same pattern as BarcodeParserService.
 * Uses Google Cloud Document AI for OCR (placeholder until full integration).
 */

using CarRental.Api.Controllers;
using CarRental.Api.Services.Interfaces;

namespace CarRental.Api.Services;

/// <summary>
/// Singleton service for parsing the front side of driver licenses.
/// Applies EXIF orientation correction before processing,
/// then delegates to Google Cloud Document AI for OCR.
/// </summary>
public class FrontSideParserService : IFrontSideParserService
{
    private readonly ImageOrientationService _orientationService;
    private readonly ILogger<FrontSideParserService> _logger;

    public FrontSideParserService(
        ImageOrientationService orientationService,
        ILogger<FrontSideParserService> logger)
    {
        _orientationService = orientationService;
        _logger = logger;
    }

    public async Task<DocumentAiParseResult> ParseFrontSideAsync(Stream imageStream)
    {
        using var ms = new MemoryStream();
        await imageStream.CopyToAsync(ms);
        return await ParseFrontSideAsync(ms.ToArray(), null, null);
    }

    public async Task<DocumentAiParseResult> ParseFrontSideAsync(byte[] imageData)
    {
        return await ParseFrontSideAsync(imageData, null, null);
    }

    /// <summary>
    /// Parse front side with fileName/contentType for HEIC detection.
    /// Full pipeline: HEIC→PNG + EXIF orientation + compress ≤ 1 MB.
    /// </summary>
    public async Task<DocumentAiParseResult> ParseFrontSideAsync(byte[] imageData, string? fileName, string? contentType)
    {
        try
        {
            _logger.LogInformation("Starting front-side license parsing from image ({Size} bytes, file={FileName}, type={ContentType})",
                imageData.Length, fileName ?? "unknown", contentType ?? "unknown");

            // Full pipeline: HEIC→PNG + EXIF orientation + compress ≤ 1 MB
            var processedBytes = _orientationService.ProcessImageBytes(imageData, fileName, contentType);
            if (processedBytes.Length != imageData.Length)
            {
                _logger.LogInformation("Front-side image processed ({OrigSize} → {NewSize} bytes)",
                    imageData.Length, processedBytes.Length);
            }

            // Process with Google Cloud Document AI
            return await ProcessWithDocumentAi(processedBytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing front side of driver license");
            return new DocumentAiParseResult
            {
                Success = false,
                ErrorMessage = $"Front-side parsing failed: {ex.Message}",
                ProcessingMethod = "front_side_error"
            };
        }
    }

    /// <summary>
    /// Process image with Google Cloud Document AI.
    /// TODO: Replace placeholder with actual Document AI integration.
    /// </summary>
    private async Task<DocumentAiParseResult> ProcessWithDocumentAi(byte[] imageBytes)
    {
        try
        {
            _logger.LogInformation("Processing with Google Cloud Document AI (placeholder implementation), image size: {Size} bytes", imageBytes.Length);

            // TODO: Replace with actual Google Cloud Document AI implementation
            await Task.Delay(1000);

            return new DocumentAiParseResult
            {
                Success = false,
                ErrorMessage = "Google Cloud Document AI integration pending implementation",
                ProcessingMethod = "document_ai_placeholder",
                ConfidenceScore = 0.0,
                ProcessingTimestamp = DateTime.UtcNow,
                Data = new DocumentAiLicenseData
                {
                    ProcessingTimestamp = DateTime.UtcNow,
                    ExtractedFromOcr = false,
                    RequiresManualEntry = true
                }
            };

            /*
            TODO: Actual Google Cloud Document AI implementation:

            using Google.Cloud.DocumentAI.V1;

            var client = DocumentProcessorServiceClient.Create();
            var request = new ProcessRequest
            {
                Name = "projects/{project}/locations/{location}/processors/{processor}",
                RawDocument = new RawDocument
                {
                    Content = Google.Protobuf.ByteString.CopyFrom(imageBytes),
                    MimeType = "image/jpeg"
                }
            };

            var response = await client.ProcessDocumentAsync(request);
            return ParseDocumentAiResponse(response);
            */
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing with Google Cloud Document AI");
            return new DocumentAiParseResult
            {
                Success = false,
                ErrorMessage = $"Document AI processing failed: {ex.Message}",
                ProcessingMethod = "document_ai_error"
            };
        }
    }
}
