/*
 *
 * Copyright (c) 2025 Alexander Orlov.
 * 34 Middletown Ave Atlantic Highlands NJ 07716
 *
 * THIS SOFTWARE IS THE CONFIDENTIAL AND PROPRIETARY INFORMATION OF
 * Alexander Orlov. ("CONFIDENTIAL INFORMATION"). YOU SHALL NOT DISCLOSE
 * SUCH CONFIDENTIAL INFORMATION AND SHALL USE IT ONLY IN ACCORDANCE
 * WITH THE TERMS OF THE LICENSE AGREEMENT YOU ENTERED INTO WITH
 * Alexander Orlov.
 *
 * Author: Alexander Orlov
 *
 */

using Microsoft.AspNetCore.Mvc;
using CarRental.Api.DTOs;
using CarRental.Api.Services;

namespace CarRental.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TranslationController : ControllerBase
{
    private readonly ITranslationService _translationService;
    private readonly ILogger<TranslationController> _logger;

    public TranslationController(
        ITranslationService translationService,
        ILogger<TranslationController> logger)
    {
        _translationService = translationService;
        _logger = logger;
    }

    /// <summary>
    /// Translate text to a single target language
    /// </summary>
    [HttpPost("translate")]
    public async Task<ActionResult<TranslateResponse>> Translate([FromBody] TranslateRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
        {
            return BadRequest("Text is required");
        }

        if (string.IsNullOrWhiteSpace(request.TargetLanguage))
        {
            return BadRequest("Target language is required");
        }

        try
        {
            var translation = await _translationService.TranslateTextAsync(
                request.Text,
                request.TargetLanguage,
                request.SourceLanguage,
                cancellationToken
            );

            return Ok(new TranslateResponse { Translation = translation });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid translation request");
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Translation configuration error");
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during translation");
            return StatusCode(500, "Translation failed. Please try again later.");
        }
    }

    /// <summary>
    /// Translate text to all supported languages
    /// </summary>
    [HttpPost("translate-all")]
    public async Task<ActionResult<TranslateAllResponse>> TranslateAll([FromBody] TranslateAllRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
        {
            return BadRequest("Text is required");
        }

        try
        {
            var translations = await _translationService.TranslateToAllLanguagesAsync(
                request.Text,
                request.SourceLanguage,
                cancellationToken
            );

            return Ok(new TranslateAllResponse { Translations = translations });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid translation request");
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Translation configuration error");
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during batch translation");
            return StatusCode(500, "Translation failed. Please try again later.");
        }
    }

    /// <summary>
    /// Get list of supported languages
    /// </summary>
    [HttpGet("languages")]
    public ActionResult<object> GetSupportedLanguages()
    {
        var languages = new[]
        {
            new { code = "en", name = "English" },
            new { code = "es", name = "Spanish" },
            new { code = "pt", name = "Portuguese" },
            new { code = "fr", name = "French" },
            new { code = "de", name = "German" },
            new { code = "ru", name = "Russian" }
        };

        return Ok(languages);
    }

    /// <summary>
    /// Health check endpoint
    /// </summary>
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
    }
}

