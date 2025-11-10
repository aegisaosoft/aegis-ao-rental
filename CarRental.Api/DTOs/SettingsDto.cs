using System.ComponentModel.DataAnnotations;

namespace CarRental.Api.DTOs;

public class StripeSettingsResponseDto
{
    public string PublishableKey { get; set; } = string.Empty;
    public bool HasSecretKey { get; set; }
    public string? SecretKeyPreview { get; set; }
    public bool HasWebhookSecret { get; set; }
    public string? WebhookSecretPreview { get; set; }
}

public class UpdateStripeSettingsRequestDto
{
    public string? SecretKey { get; set; }
    public string? PublishableKey { get; set; }
    public string? WebhookSecret { get; set; }

    public bool RemoveSecretKey { get; set; }
    public bool RemovePublishableKey { get; set; }
    public bool RemoveWebhookSecret { get; set; }
}

public class AiSettingsResponseDto
{
    public bool HasAnthropicKey { get; set; }
    public string? AnthropicKeyPreview { get; set; }
    public bool HasClaudeKey { get; set; }
    public string? ClaudeKeyPreview { get; set; }
    public bool HasOpenAiKey { get; set; }
    public string? OpenAiKeyPreview { get; set; }
}

public class UpdateAiSettingsRequestDto
{
    public string? AnthropicApiKey { get; set; }
    public string? ClaudeApiKey { get; set; }
    public string? OpenAiApiKey { get; set; }

    public bool RemoveAnthropicApiKey { get; set; }
    public bool RemoveClaudeApiKey { get; set; }
    public bool RemoveOpenAiApiKey { get; set; }
}
