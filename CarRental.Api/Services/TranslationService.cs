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

using System.Text;
using System.Text.Json;

namespace CarRental.Api.Services;

public interface ITranslationService
{
    Task<string> TranslateTextAsync(string text, string targetLanguage, string? sourceLanguage = null, CancellationToken cancellationToken = default);
    Task<Dictionary<string, string>> TranslateToAllLanguagesAsync(string text, string? sourceLanguage = null, CancellationToken cancellationToken = default);
}

public class GoogleTranslationService : ITranslationService
{
    private const string GoogleTranslateApiKeySetting = "google.translate.key";
    
    private readonly HttpClient _httpClient;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<GoogleTranslationService> _logger;
    private readonly string[] _supportedLanguages = { "en", "es", "pt", "fr", "de", "ru" };

    public GoogleTranslationService(
        IHttpClientFactory httpClientFactory,
        ISettingsService settingsService,
        ILogger<GoogleTranslationService> logger)
    {
        _httpClient = httpClientFactory.CreateClient();
        _settingsService = settingsService;
        _logger = logger;
    }

    public async Task<string> TranslateTextAsync(
        string text,
        string targetLanguage,
        string? sourceLanguage = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Text cannot be empty", nameof(text));
        }

        var apiKey = await _settingsService.GetValueAsync(GoogleTranslateApiKeySetting, cancellationToken);
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException("Google Translate API key is not configured. Please set it in Settings.");
        }

        var url = $"https://translation.googleapis.com/language/translate/v2?key={apiKey}";

        var requestBody = new
        {
            q = text,
            target = targetLanguage,
            source = sourceLanguage,
            format = "text"
        };

        var content = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json"
        );

        try
        {
            var response = await _httpClient.PostAsync(url, content, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Google Translate API error: {StatusCode} - {Error}", 
                    response.StatusCode, errorContent);
                throw new HttpRequestException($"Translation API returned {response.StatusCode}");
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<GoogleTranslateResponse>(
                responseContent,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );

            if (result?.Data?.Translations == null || !result.Data.Translations.Any())
            {
                throw new InvalidOperationException("No translation returned from API");
            }

            return result.Data.Translations[0].TranslatedText;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error translating text to {TargetLanguage}", targetLanguage);
            throw;
        }
    }

    public async Task<Dictionary<string, string>> TranslateToAllLanguagesAsync(
        string text,
        string? sourceLanguage = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Text cannot be empty", nameof(text));
        }

        var translations = new Dictionary<string, string>();
        var tasks = new List<Task<(string language, string translation)>>();

        foreach (var language in _supportedLanguages)
        {
            // Skip if it's the source language
            if (!string.IsNullOrEmpty(sourceLanguage) && language == sourceLanguage)
            {
                translations[language] = text;
                continue;
            }

            tasks.Add(TranslateWithLanguageAsync(text, language, sourceLanguage, cancellationToken));
        }

        try
        {
            var results = await Task.WhenAll(tasks);
            
            foreach (var (language, translation) in results)
            {
                translations[language] = translation;
            }

            return translations;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error translating to all languages");
            throw;
        }
    }

    private async Task<(string language, string translation)> TranslateWithLanguageAsync(
        string text,
        string targetLanguage,
        string? sourceLanguage,
        CancellationToken cancellationToken)
    {
        var translation = await TranslateTextAsync(text, targetLanguage, sourceLanguage, cancellationToken);
        return (targetLanguage, translation);
    }
}

// Google Translate API Response Models
internal class GoogleTranslateResponse
{
    public GoogleTranslateData Data { get; set; } = new();
}

internal class GoogleTranslateData
{
    public List<GoogleTranslation> Translations { get; set; } = new();
}

internal class GoogleTranslation
{
    public string TranslatedText { get; set; } = string.Empty;
    public string DetectedSourceLanguage { get; set; } = string.Empty;
}

