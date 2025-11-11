using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CarRental.Api.Services;
using CarRental.Api.DTOs;

namespace CarRental.Api.Controllers;

[ApiController]
[Route("api/settings")]
[Authorize(Roles = "mainadmin,admin")]
public class SettingsController : ControllerBase
{
    private readonly ISettingsService _settingsService;
    private readonly ILogger<SettingsController> _logger;

    private const string StripeSecretKeySetting = "stripe.secretKey";
    private const string StripePublishableKeySetting = "stripe.publishableKey";
    private const string StripeWebhookSecretSetting = "stripe.webhookSecret";

    private const string AnthropicApiKeySetting = "anthropic.apiKey";
    private const string ClaudeApiKeySetting = "claude.apiKey";
    private const string OpenAIApiKeySetting = "openai.apiKey";

    public SettingsController(ISettingsService settingsService, ILogger<SettingsController> logger)
    {
        _settingsService = settingsService;
        _logger = logger;
    }

    [HttpGet("stripe")]
    public async Task<ActionResult<StripeSettingsResponseDto>> GetStripeSettings()
    {
        var secret = await _settingsService.GetValueAsync(StripeSecretKeySetting);
        var publishable = await _settingsService.GetValueAsync(StripePublishableKeySetting);
        var webhook = await _settingsService.GetValueAsync(StripeWebhookSecretSetting);

        var response = new StripeSettingsResponseDto
        {
            PublishableKey = publishable ?? string.Empty,
            HasSecretKey = !string.IsNullOrWhiteSpace(secret),
            SecretKeyPreview = MaskSecret(secret),
            SecretKey = secret,
            HasWebhookSecret = !string.IsNullOrWhiteSpace(webhook),
            WebhookSecretPreview = MaskSecret(webhook),
            WebhookSecret = webhook
        };

        return Ok(response);
    }

    [HttpPut("stripe")]
    public async Task<IActionResult> UpdateStripeSettings([FromBody] UpdateStripeSettingsRequestDto request)
    {
        try
        {
            if (request.RemoveSecretKey)
            {
                await _settingsService.SetValueAsync(StripeSecretKeySetting, null);
            }
            else if (!string.IsNullOrWhiteSpace(request.SecretKey))
            {
                await _settingsService.SetValueAsync(StripeSecretKeySetting, request.SecretKey.Trim());
            }

            if (request.RemovePublishableKey)
            {
                await _settingsService.SetValueAsync(StripePublishableKeySetting, null);
            }
            else if (request.PublishableKey != null)
            {
                var trimmed = request.PublishableKey.Trim();
                await _settingsService.SetValueAsync(StripePublishableKeySetting, string.IsNullOrWhiteSpace(trimmed) ? null : trimmed);
            }

            if (request.RemoveWebhookSecret)
            {
                await _settingsService.SetValueAsync(StripeWebhookSecretSetting, null);
            }
            else if (!string.IsNullOrWhiteSpace(request.WebhookSecret))
            {
                await _settingsService.SetValueAsync(StripeWebhookSecretSetting, request.WebhookSecret.Trim());
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update Stripe settings");
            return StatusCode(500, new { error = "Failed to update Stripe settings." });
        }
    }

    [HttpGet("ai")]
    public async Task<ActionResult<AiSettingsResponseDto>> GetAiSettings()
    {
        var anthropic = await _settingsService.GetValueAsync(AnthropicApiKeySetting);
        var claude = await _settingsService.GetValueAsync(ClaudeApiKeySetting);
        var openAi = await _settingsService.GetValueAsync(OpenAIApiKeySetting);

        var response = new AiSettingsResponseDto
        {
            HasAnthropicKey = !string.IsNullOrWhiteSpace(anthropic),
            AnthropicKeyPreview = MaskSecret(anthropic),
            AnthropicApiKey = anthropic,
            HasClaudeKey = !string.IsNullOrWhiteSpace(claude),
            ClaudeKeyPreview = MaskSecret(claude),
            ClaudeApiKey = claude,
            HasOpenAiKey = !string.IsNullOrWhiteSpace(openAi),
            OpenAiKeyPreview = MaskSecret(openAi),
            OpenAiApiKey = openAi
        };

        return Ok(response);
    }

    [HttpPut("ai")]
    public async Task<IActionResult> UpdateAiSettings([FromBody] UpdateAiSettingsRequestDto request)
    {
        try
        {
            if (request.RemoveAnthropicApiKey)
            {
                await _settingsService.SetValueAsync(AnthropicApiKeySetting, null);
            }
            else if (!string.IsNullOrWhiteSpace(request.AnthropicApiKey))
            {
                await _settingsService.SetValueAsync(AnthropicApiKeySetting, request.AnthropicApiKey.Trim());
            }

            if (request.RemoveClaudeApiKey)
            {
                await _settingsService.SetValueAsync(ClaudeApiKeySetting, null);
            }
            else if (!string.IsNullOrWhiteSpace(request.ClaudeApiKey))
            {
                await _settingsService.SetValueAsync(ClaudeApiKeySetting, request.ClaudeApiKey.Trim());
            }

            if (request.RemoveOpenAiApiKey)
            {
                await _settingsService.SetValueAsync(OpenAIApiKeySetting, null);
            }
            else if (!string.IsNullOrWhiteSpace(request.OpenAiApiKey))
            {
                await _settingsService.SetValueAsync(OpenAIApiKeySetting, request.OpenAiApiKey.Trim());
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update AI settings");
            return StatusCode(500, new { error = "Failed to update AI settings." });
        }
    }

    private static string? MaskSecret(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        if (trimmed.Length <= 8)
            return new string('*', Math.Max(trimmed.Length - 2, 0)) + trimmed[^Math.Min(2, trimmed.Length)..];

        return $"{trimmed[..4]}********{trimmed[^4..]}";
    }
}
