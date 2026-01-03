/*
 * CarRental.Api - Instagram Webhook Controller
 * Copyright (c) 2025 Alexander Orlov
 * 
 * Handles Instagram Messaging API webhooks for DM booking assistant
 */

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CarRental.Api.Data;
using CarRental.Api.Models;
using CarRental.Api.Services;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;

namespace CarRental.Api.Controllers;

/// <summary>
/// Instagram Messaging API Webhook Controller
/// Handles incoming DM messages and webhook verification
/// </summary>
[ApiController]
[Route("api/instagram/webhook")]
public class InstagramWebhookController : ControllerBase
{
    private readonly CarRentalDbContext _context;
    private readonly ILogger<InstagramWebhookController> _logger;
    private readonly IConfiguration _configuration;
    private readonly IServiceProvider _serviceProvider;

    public InstagramWebhookController(
        CarRentalDbContext context,
        ILogger<InstagramWebhookController> logger,
        IConfiguration configuration,
        IServiceProvider serviceProvider)
    {
        _context = context;
        _logger = logger;
        _configuration = configuration;
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Webhook verification endpoint (GET)
    /// Facebook sends this to verify webhook URL
    /// </summary>
    [HttpGet]
    public IActionResult VerifyWebhook(
        [FromQuery(Name = "hub.mode")] string? mode,
        [FromQuery(Name = "hub.verify_token")] string? verifyToken,
        [FromQuery(Name = "hub.challenge")] string? challenge)
    {
        _logger.LogInformation("Instagram webhook verification: mode={Mode}, token={Token}", mode, verifyToken);

        var expectedToken = _configuration["Meta:WebhookVerifyToken"] ?? "aegis-rental-webhook-verify";

        if (mode == "subscribe" && verifyToken == expectedToken)
        {
            _logger.LogInformation("Instagram webhook verified successfully");
            return Ok(challenge);
        }

        _logger.LogWarning("Instagram webhook verification failed");
        return Forbid();
    }

    /// <summary>
    /// Webhook event handler (POST)
    /// Receives messages and other events from Instagram
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> HandleWebhook()
    {
        try
        {
            // Read request body
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();

            _logger.LogInformation("Instagram webhook received: {Length} bytes", body.Length);

            // Verify signature if app secret is configured
            var appSecret = _configuration["Meta:AppSecret"];
            if (!string.IsNullOrEmpty(appSecret))
            {
                var signature = Request.Headers["X-Hub-Signature-256"].FirstOrDefault();
                if (!VerifySignature(body, signature, appSecret))
                {
                    _logger.LogWarning("Instagram webhook signature verification failed");
                    return Unauthorized();
                }
            }

            // Parse webhook payload
            var payload = JsonSerializer.Deserialize<InstagramWebhookPayload>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (payload == null)
            {
                _logger.LogWarning("Failed to parse Instagram webhook payload");
                return Ok(); // Return 200 to prevent retries
            }

            // Process each entry
            foreach (var entry in payload.Entry ?? new List<WebhookEntry>())
            {
                await ProcessEntry(entry);
            }

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Instagram webhook");
            return Ok(); // Return 200 to prevent retries
        }
    }

    private async Task ProcessEntry(WebhookEntry entry)
    {
        // Process messaging events
        foreach (var messaging in entry.Messaging ?? new List<MessagingEvent>())
        {
            if (messaging.Message != null)
            {
                await HandleIncomingMessage(entry.Id, messaging);
            }
            else if (messaging.Postback != null)
            {
                await HandlePostback(entry.Id, messaging);
            }
        }
    }

    private async Task HandleIncomingMessage(string pageId, MessagingEvent messaging)
    {
        var senderId = messaging.Sender?.Id;
        var messageText = messaging.Message?.Text;
        var messageId = messaging.Message?.Mid;

        if (string.IsNullOrEmpty(senderId) || string.IsNullOrEmpty(messageText))
        {
            _logger.LogWarning("Invalid message: senderId={SenderId}, hasText={HasText}", 
                senderId, !string.IsNullOrEmpty(messageText));
            return;
        }

        _logger.LogInformation("Instagram DM received from {SenderId}: {MessagePreview}", 
            senderId, messageText.Length > 50 ? messageText.Substring(0, 50) + "..." : messageText);

        try
        {
            // Find company by Instagram Page ID
            var credentials = await _context.CompanyMetaCredentials
                .Include(c => c.Company)
                .FirstOrDefaultAsync(c => c.InstagramAccountId == pageId || c.PageId == pageId);

            if (credentials == null)
            {
                _logger.LogWarning("No company found for Instagram page {PageId}", pageId);
                return;
            }

            // Get or create conversation
            var conversation = await GetOrCreateConversation(credentials.CompanyId, senderId);

            // Check for duplicate message
            var existingMessage = await _context.Set<InstagramMessage>()
                .AnyAsync(m => m.InstagramMessageId == messageId);

            if (existingMessage)
            {
                _logger.LogInformation("Duplicate message ignored: {MessageId}", messageId);
                return;
            }

            // Save incoming message
            var incomingMessage = new InstagramMessage
            {
                ConversationId = conversation.Id,
                InstagramMessageId = messageId,
                Sender = MessageSender.User,
                Content = messageText,
                MessageType = "text",
                Timestamp = DateTime.UtcNow
            };
            _context.Set<InstagramMessage>().Add(incomingMessage);
            
            // Update conversation activity
            conversation.LastActivityAt = DateTime.UtcNow;
            conversation.ExpiresAt = DateTime.UtcNow.AddHours(24);

            await _context.SaveChangesAsync();

            // Process message with AI assistant (async - don't wait)
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var assistantService = scope.ServiceProvider.GetRequiredService<IBookingAssistantService>();
                    await assistantService.ProcessMessageAsync(conversation.Id, messageText);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing message with AI assistant");
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling incoming Instagram message");
        }
    }

    private async Task HandlePostback(string pageId, MessagingEvent messaging)
    {
        var senderId = messaging.Sender?.Id;
        var payload = messaging.Postback?.Payload;

        if (string.IsNullOrEmpty(senderId) || string.IsNullOrEmpty(payload))
            return;

        _logger.LogInformation("Instagram postback from {SenderId}: {Payload}", senderId, payload);

        // Handle quick reply payloads (e.g., "SELECT_VEHICLE_123")
        try
        {
            var credentials = await _context.CompanyMetaCredentials
                .FirstOrDefaultAsync(c => c.InstagramAccountId == pageId || c.PageId == pageId);

            if (credentials == null) return;

            var conversation = await GetOrCreateConversation(credentials.CompanyId, senderId);

            // Save postback as message
            var postbackMessage = new InstagramMessage
            {
                ConversationId = conversation.Id,
                Sender = MessageSender.User,
                Content = payload,
                MessageType = "postback",
                QuickReplyPayload = payload,
                Timestamp = DateTime.UtcNow
            };
            _context.Set<InstagramMessage>().Add(postbackMessage);

            conversation.LastActivityAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // Process with AI assistant
            _ = Task.Run(async () =>
            {
                using var scope = _serviceProvider.CreateScope();
                var assistantService = scope.ServiceProvider.GetRequiredService<IBookingAssistantService>();
                await assistantService.ProcessPostbackAsync(conversation.Id, payload);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling Instagram postback");
        }
    }

    private async Task<InstagramConversation> GetOrCreateConversation(Guid companyId, string instagramUserId)
    {
        var conversation = await _context.Set<InstagramConversation>()
            .FirstOrDefaultAsync(c => 
                c.CompanyId == companyId && 
                c.InstagramUserId == instagramUserId &&
                c.ExpiresAt > DateTime.UtcNow);

        if (conversation == null)
        {
            conversation = new InstagramConversation
            {
                CompanyId = companyId,
                InstagramUserId = instagramUserId,
                State = ConversationState.Initial,
                CreatedAt = DateTime.UtcNow,
                LastActivityAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(24)
            };
            _context.Set<InstagramConversation>().Add(conversation);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created new conversation {ConversationId} for user {UserId}", 
                conversation.Id, instagramUserId);
        }

        return conversation;
    }

    private bool VerifySignature(string payload, string? signature, string appSecret)
    {
        if (string.IsNullOrEmpty(signature) || !signature.StartsWith("sha256="))
            return false;

        var expectedSignature = signature.Substring(7);
        
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(appSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var computedSignature = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();

        return computedSignature == expectedSignature;
    }
}

#region Webhook Payload DTOs

public class InstagramWebhookPayload
{
    public string? Object { get; set; }
    public List<WebhookEntry>? Entry { get; set; }
}

public class WebhookEntry
{
    public string Id { get; set; } = "";
    public long Time { get; set; }
    public List<MessagingEvent>? Messaging { get; set; }
}

public class MessagingEvent
{
    public MessagingParticipant? Sender { get; set; }
    public MessagingParticipant? Recipient { get; set; }
    public long Timestamp { get; set; }
    public IncomingMessage? Message { get; set; }
    public Postback? Postback { get; set; }
}

public class MessagingParticipant
{
    public string? Id { get; set; }
}

public class IncomingMessage
{
    public string? Mid { get; set; }
    public string? Text { get; set; }
    public QuickReply? QuickReply { get; set; }
    public List<Attachment>? Attachments { get; set; }
}

public class QuickReply
{
    public string? Payload { get; set; }
}

public class Postback
{
    public string? Payload { get; set; }
    public string? Title { get; set; }
}

public class Attachment
{
    public string? Type { get; set; }
    public AttachmentPayload? Payload { get; set; }
}

public class AttachmentPayload
{
    public string? Url { get; set; }
}

#endregion
