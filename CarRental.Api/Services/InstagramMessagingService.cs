/*
 * CarRental.Api - Instagram Messaging Service
 * Copyright (c) 2025 Alexander Orlov
 * 
 * Service for sending messages via Instagram Messaging API
 */

using System.Text;
using System.Text.Json;
using CarRental.Api.Data;
using CarRental.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Api.Services;

public interface IInstagramMessagingService
{
    /// <summary>
    /// Send a text message to an Instagram user
    /// </summary>
    Task<bool> SendTextMessageAsync(Guid companyId, string recipientId, string text);

    /// <summary>
    /// Send a message with quick reply buttons
    /// </summary>
    Task<bool> SendQuickRepliesAsync(Guid companyId, string recipientId, string text, List<QuickReplyButton> buttons);

    /// <summary>
    /// Send a generic template (carousel of items)
    /// </summary>
    Task<bool> SendGenericTemplateAsync(Guid companyId, string recipientId, List<TemplateElement> elements);

    /// <summary>
    /// Send a button template
    /// </summary>
    Task<bool> SendButtonTemplateAsync(Guid companyId, string recipientId, string text, List<TemplateButton> buttons);

    /// <summary>
    /// Send typing indicator
    /// </summary>
    Task SendTypingIndicatorAsync(Guid companyId, string recipientId, bool typing = true);
}

public class InstagramMessagingService : IInstagramMessagingService
{
    private readonly CarRentalDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<InstagramMessagingService> _logger;
    private const string GraphApiBaseUrl = "https://graph.facebook.com/v18.0";

    public InstagramMessagingService(
        CarRentalDbContext context,
        IHttpClientFactory httpClientFactory,
        ILogger<InstagramMessagingService> logger)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<bool> SendTextMessageAsync(Guid companyId, string recipientId, string text)
    {
        var credentials = await GetCredentialsAsync(companyId);
        if (credentials == null) return false;

        var payload = new
        {
            recipient = new { id = recipientId },
            message = new { text = text }
        };

        return await SendMessageAsync(credentials, payload);
    }

    public async Task<bool> SendQuickRepliesAsync(Guid companyId, string recipientId, string text, List<QuickReplyButton> buttons)
    {
        var credentials = await GetCredentialsAsync(companyId);
        if (credentials == null) return false;

        var quickReplies = buttons.Select(b => new
        {
            content_type = "text",
            title = b.Title.Length > 20 ? b.Title.Substring(0, 20) : b.Title,
            payload = b.Payload
        }).ToList();

        var payload = new
        {
            recipient = new { id = recipientId },
            message = new
            {
                text = text,
                quick_replies = quickReplies
            }
        };

        return await SendMessageAsync(credentials, payload);
    }

    public async Task<bool> SendGenericTemplateAsync(Guid companyId, string recipientId, List<TemplateElement> elements)
    {
        var credentials = await GetCredentialsAsync(companyId);
        if (credentials == null) return false;

        var templateElements = elements.Select(e => new
        {
            title = e.Title.Length > 80 ? e.Title.Substring(0, 80) : e.Title,
            subtitle = e.Subtitle?.Length > 80 ? e.Subtitle.Substring(0, 80) : e.Subtitle,
            image_url = e.ImageUrl,
            default_action = e.ActionUrl != null ? new
            {
                type = "web_url",
                url = e.ActionUrl
            } : null,
            buttons = e.Buttons?.Select(b => CreateButton(b)).ToList()
        }).ToList();

        var payload = new
        {
            recipient = new { id = recipientId },
            message = new
            {
                attachment = new
                {
                    type = "template",
                    payload = new
                    {
                        template_type = "generic",
                        elements = templateElements
                    }
                }
            }
        };

        return await SendMessageAsync(credentials, payload);
    }

    public async Task<bool> SendButtonTemplateAsync(Guid companyId, string recipientId, string text, List<TemplateButton> buttons)
    {
        var credentials = await GetCredentialsAsync(companyId);
        if (credentials == null) return false;

        var payload = new
        {
            recipient = new { id = recipientId },
            message = new
            {
                attachment = new
                {
                    type = "template",
                    payload = new
                    {
                        template_type = "button",
                        text = text,
                        buttons = buttons.Select(b => CreateButton(b)).ToList()
                    }
                }
            }
        };

        return await SendMessageAsync(credentials, payload);
    }

    public async Task SendTypingIndicatorAsync(Guid companyId, string recipientId, bool typing = true)
    {
        var credentials = await GetCredentialsAsync(companyId);
        if (credentials == null) return;

        var payload = new
        {
            recipient = new { id = recipientId },
            sender_action = typing ? "typing_on" : "typing_off"
        };

        await SendMessageAsync(credentials, payload);
    }

    private object CreateButton(TemplateButton button)
    {
        return button.Type switch
        {
            ButtonType.WebUrl => new
            {
                type = "web_url",
                title = button.Title.Length > 20 ? button.Title.Substring(0, 20) : button.Title,
                url = button.Value
            },
            ButtonType.Postback => new
            {
                type = "postback",
                title = button.Title.Length > 20 ? button.Title.Substring(0, 20) : button.Title,
                payload = button.Value
            },
            ButtonType.PhoneNumber => new
            {
                type = "phone_number",
                title = button.Title.Length > 20 ? button.Title.Substring(0, 20) : button.Title,
                payload = button.Value
            },
            _ => new
            {
                type = "postback",
                title = button.Title,
                payload = button.Value
            }
        };
    }

    private async Task<CompanyMetaCredentials?> GetCredentialsAsync(Guid companyId)
    {
        return await _context.CompanyMetaCredentials
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CompanyId == companyId && c.Status == MetaCredentialStatus.Active);
    }

    private async Task<bool> SendMessageAsync(CompanyMetaCredentials credentials, object payload)
    {
        try
        {
            var accessToken = credentials.PageAccessToken;
            if (string.IsNullOrEmpty(accessToken))
            {
                _logger.LogWarning("No page access token for company {CompanyId}", credentials.CompanyId);
                return false;
            }

            var pageId = credentials.InstagramAccountId ?? credentials.PageId;
            if (string.IsNullOrEmpty(pageId))
            {
                _logger.LogWarning("No Instagram/Page ID for company {CompanyId}", credentials.CompanyId);
                return false;
            }

            var client = _httpClientFactory.CreateClient();
            var url = $"{GraphApiBaseUrl}/{pageId}/messages?access_token={accessToken}";

            var content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            var response = await client.PostAsync(url, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Instagram API error: {StatusCode} - {Response}", 
                    response.StatusCode, responseBody);
                return false;
            }

            _logger.LogInformation("Message sent successfully to Instagram");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending Instagram message");
            return false;
        }
    }
}

#region DTOs

public class QuickReplyButton
{
    public string Title { get; set; } = "";
    public string Payload { get; set; } = "";
}

public class TemplateElement
{
    public string Title { get; set; } = "";
    public string? Subtitle { get; set; }
    public string? ImageUrl { get; set; }
    public string? ActionUrl { get; set; }
    public List<TemplateButton>? Buttons { get; set; }
}

public class TemplateButton
{
    public ButtonType Type { get; set; }
    public string Title { get; set; } = "";
    public string Value { get; set; } = "";
}

public enum ButtonType
{
    WebUrl,
    Postback,
    PhoneNumber
}

#endregion
