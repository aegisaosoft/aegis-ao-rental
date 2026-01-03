/*
 * CarRental.Api - Booking Assistant Service
 * Copyright (c) 2025 Alexander Orlov
 * 
 * AI-powered booking assistant using Claude (Anthropic) for Instagram DM conversations
 */

using System.Text;
using System.Text.Json;
using CarRental.Api.Data;
using CarRental.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Api.Services;

public interface IBookingAssistantService
{
    /// <summary>
    /// Process incoming text message from user
    /// </summary>
    Task ProcessMessageAsync(int conversationId, string userMessage);

    /// <summary>
    /// Process postback/quick reply from user
    /// </summary>
    Task ProcessPostbackAsync(int conversationId, string payload);
}

public class BookingAssistantService : IBookingAssistantService
{
    private readonly CarRentalDbContext _context;
    private readonly IInstagramMessagingService _messagingService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<BookingAssistantService> _logger;

    private const string ClaudeApiUrl = "https://api.anthropic.com/v1/messages";
    // Use Haiku for speed (3x faster than Sonnet, good enough for chat)
    private const string ClaudeModel = "claude-3-5-haiku-20241022";

    public BookingAssistantService(
        CarRentalDbContext context,
        IInstagramMessagingService messagingService,
        IHttpClientFactory httpClientFactory,
        ILogger<BookingAssistantService> logger)
    {
        _context = context;
        _messagingService = messagingService;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task ProcessMessageAsync(int conversationId, string userMessage)
    {
        try
        {
            var conversation = await _context.Set<InstagramConversation>()
                .Include(c => c.Company)
                .Include(c => c.Messages.OrderByDescending(m => m.Timestamp).Take(10))
                .FirstOrDefaultAsync(c => c.Id == conversationId);

            if (conversation == null)
            {
                _logger.LogWarning("Conversation {ConversationId} not found", conversationId);
                return;
            }

            // Send typing indicator immediately (don't wait)
            _ = _messagingService.SendTypingIndicatorAsync(
                conversation.CompanyId, 
                conversation.InstagramUserId);

            // Get AI response
            var response = await GetAIResponseAsync(conversation, userMessage);

            if (response == null)
            {
                await SendFallbackMessage(conversation);
                return;
            }

            // Parse and execute AI response
            await ExecuteAIResponse(conversation, response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message for conversation {ConversationId}", conversationId);
        }
    }

    public async Task ProcessPostbackAsync(int conversationId, string payload)
    {
        try
        {
            var conversation = await _context.Set<InstagramConversation>()
                .Include(c => c.Company)
                .FirstOrDefaultAsync(c => c.Id == conversationId);

            if (conversation == null) return;

            // Handle specific payloads directly (no AI needed = faster)
            if (payload.StartsWith("SELECT_MODEL_"))
            {
                var modelIdStr = payload.Replace("SELECT_MODEL_", "");
                if (Guid.TryParse(modelIdStr, out var modelId))
                {
                    await HandleModelSelection(conversation, modelId);
                }
            }
            else if (payload == "SHOW_MORE_VEHICLES")
            {
                await ShowAvailableVehicles(conversation);
            }
            else if (payload == "TALK_TO_HUMAN")
            {
                conversation.State = ConversationState.HandoffToHuman;
                await _context.SaveChangesAsync();
                await _messagingService.SendTextMessageAsync(
                    conversation.CompanyId,
                    conversation.InstagramUserId,
                    "I'll connect you with our team. Someone will respond shortly! 🙋‍♂️");
            }
            else if (payload == "I want to rent a car" || payload == "Show me available cars")
            {
                // Quick responses without AI
                await _messagingService.SendTextMessageAsync(
                    conversation.CompanyId,
                    conversation.InstagramUserId,
                    "Great! 🚗 When do you need the car? Please tell me pickup and return dates.");
                conversation.State = ConversationState.AskingDates;
                await _context.SaveChangesAsync();
            }
            else
            {
                // Only use AI for complex messages
                await ProcessMessageAsync(conversationId, payload);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing postback for conversation {ConversationId}", conversationId);
        }
    }

    private async Task<AIResponse?> GetAIResponseAsync(InstagramConversation conversation, string userMessage)
    {
        try
        {
            var apiKey = await GetClaudeApiKeyAsync(conversation.CompanyId);
            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogWarning("No Claude API key configured for company {CompanyId}", conversation.CompanyId);
                return null;
            }

            // Build compact context (smaller = faster)
            var context = await BuildConversationContext(conversation);
            var systemPrompt = BuildSystemPrompt(conversation.Company!, context);
            var messages = BuildMessageHistory(conversation, userMessage);

            // Call Claude API with optimized settings
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("x-api-key", apiKey);
            client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

            var requestBody = new
            {
                model = ClaudeModel,
                max_tokens = 512, // Reduced for speed
                messages = messages,
                system = systemPrompt
            };

            var content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");

            var response = await client.PostAsync(ClaudeApiUrl, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Claude API error: {StatusCode} - {Response}", 
                    response.StatusCode, responseBody);
                return null;
            }

            var claudeResponse = JsonSerializer.Deserialize<ClaudeApiResponse>(responseBody, 
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var textContent = claudeResponse?.Content?.FirstOrDefault(c => c.Type == "text")?.Text;
            if (string.IsNullOrEmpty(textContent))
            {
                return null;
            }

            return ParseAIResponse(textContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting AI response");
            return null;
        }
    }

    private async Task<string?> GetClaudeApiKeyAsync(Guid companyId)
    {
        // Settings are global (no CompanyId in this system)
        // Try claude.apiKey first, then anthropic.apiKey
        var claudeSetting = await _context.Settings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == "claude.apiKey");

        if (claudeSetting != null && !string.IsNullOrEmpty(claudeSetting.Value))
        {
            return claudeSetting.Value;
        }

        var anthropicSetting = await _context.Settings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == "anthropic.apiKey");

        return anthropicSetting?.Value;
    }

    private async Task<ConversationContext> BuildConversationContext(InstagramConversation conversation)
    {
        var context = new ConversationContext
        {
            CompanyName = conversation.Company?.CompanyName ?? "Our rental company",
            Currency = conversation.Company?.Currency ?? "USD",
            Language = conversation.Language
        };

        // Get available locations (cached in memory would be faster)
        context.Locations = await _context.CompanyLocations
            .AsNoTracking()
            .Where(l => l.CompanyId == conversation.CompanyId && l.IsActive)
            .Select(l => l.LocationName)
            .Take(10)
            .ToListAsync();

        // Only fetch vehicles if dates are set
        if (conversation.PickupDate.HasValue && conversation.ReturnDate.HasValue)
        {
            context.AvailableModels = await GetAvailableModels(
                conversation.CompanyId,
                conversation.PickupDate.Value,
                conversation.ReturnDate.Value,
                conversation.PickupLocation);
        }

        return context;
    }

    private async Task<List<AvailableModel>> GetAvailableModels(
        Guid companyId, DateTime pickup, DateTime returnDate, string? location)
    {
        // Query available vehicles and group by model
        var vehicles = await _context.Vehicles
            .AsNoTracking()
            .Include(v => v.VehicleModel)
                .ThenInclude(vm => vm!.Model)
                    .ThenInclude(m => m!.Category)
            .Where(v => v.CompanyId == companyId && 
                       v.Status == VehicleStatus.Available &&
                       v.VehicleModel != null &&
                       v.VehicleModel.Model != null)
            .ToListAsync();

        // Group by model and create AvailableModel list
        var models = vehicles
            .GroupBy(v => v.VehicleModel!.ModelId)
            .Select(g => {
                var first = g.First();
                var model = first.VehicleModel!.Model!;
                return new AvailableModel
                {
                    ModelId = model.Id,
                    Make = model.Make,
                    ModelName = model.ModelName,
                    Year = model.Year,
                    Category = model.Category?.CategoryName,
                    DailyRate = first.VehicleModel!.DailyRate ?? 0,
                    ImageUrl = first.ImageUrl,
                    AvailableCount = g.Count()
                };
            })
            .Take(8)
            .ToList();

        return models;
    }

    private string BuildSystemPrompt(Company company, ConversationContext context)
    {
        // Compact prompt for Haiku (smaller = faster)
        var sb = new StringBuilder();
        
        sb.AppendLine($"You're a booking assistant for {context.CompanyName} car rental.");
        sb.AppendLine("Be friendly, concise. Use emojis sparingly. Keep responses under 200 chars.");
        sb.AppendLine($"Currency: {context.Currency}. Language: {context.Language}");
        sb.AppendLine();
        sb.AppendLine("FLOW: 1) Ask dates 2) Ask location 3) Show vehicles 4) Send booking link");

        if (context.Locations.Any())
        {
            sb.AppendLine($"LOCATIONS: {string.Join(", ", context.Locations.Take(5))}");
        }

        if (context.AvailableModels.Any())
        {
            sb.AppendLine("VEHICLES:");
            foreach (var model in context.AvailableModels.Take(5))
            {
                sb.AppendLine($"- {model.Make} {model.ModelName}: ${model.DailyRate}/day [ID:{model.ModelId}]");
            }
        }

        sb.AppendLine();
        sb.AppendLine("RESPOND IN JSON:");
        sb.AppendLine("{\"message\":\"text\",\"action\":\"none|show_vehicles|send_booking_link|handoff\",");
        sb.AppendLine("\"extracted_data\":{\"pickup_date\":\"YYYY-MM-DD\",\"return_date\":\"YYYY-MM-DD\",\"location\":\"name\",\"selected_model_id\":123}}");

        return sb.ToString();
    }

    private List<object> BuildMessageHistory(InstagramConversation conversation, string currentMessage)
    {
        var messages = new List<object>();

        // Add only last 6 messages for speed
        foreach (var msg in conversation.Messages.OrderBy(m => m.Timestamp).TakeLast(6))
        {
            messages.Add(new
            {
                role = msg.Sender == MessageSender.User ? "user" : "assistant",
                content = msg.Content
            });
        }

        // Add current message
        messages.Add(new
        {
            role = "user",
            content = currentMessage
        });

        return messages;
    }

    private AIResponse? ParseAIResponse(string responseText)
    {
        try
        {
            // Try to extract JSON from the response
            var jsonStart = responseText.IndexOf('{');
            var jsonEnd = responseText.LastIndexOf('}');
            
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = responseText.Substring(jsonStart, jsonEnd - jsonStart + 1);
                return JsonSerializer.Deserialize<AIResponse>(json, 
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }

            // If no JSON, treat entire response as message
            return new AIResponse { Message = responseText, Action = "none" };
        }
        catch
        {
            return new AIResponse { Message = responseText, Action = "none" };
        }
    }

    private async Task ExecuteAIResponse(InstagramConversation conversation, AIResponse response)
    {
        // Update conversation with extracted data
        if (response.ExtractedData != null)
        {
            if (!string.IsNullOrEmpty(response.ExtractedData.PickupDate) && 
                DateTime.TryParse(response.ExtractedData.PickupDate, out var pickupDate))
            {
                conversation.PickupDate = pickupDate;
            }

            if (!string.IsNullOrEmpty(response.ExtractedData.ReturnDate) && 
                DateTime.TryParse(response.ExtractedData.ReturnDate, out var returnDate))
            {
                conversation.ReturnDate = returnDate;
            }

            if (!string.IsNullOrEmpty(response.ExtractedData.Location))
            {
                conversation.PickupLocation = response.ExtractedData.Location;
            }

            if (response.ExtractedData.SelectedModelId.HasValue)
            {
                conversation.SelectedModelId = response.ExtractedData.SelectedModelId;
            }
        }

        // Save assistant message
        var assistantMessage = new InstagramMessage
        {
            ConversationId = conversation.Id,
            Sender = MessageSender.Assistant,
            Content = response.Message ?? "",
            MessageType = "text",
            Timestamp = DateTime.UtcNow
        };
        _context.Set<InstagramMessage>().Add(assistantMessage);
        await _context.SaveChangesAsync();

        // Execute action
        switch (response.Action?.ToLower())
        {
            case "show_vehicles":
                await ShowAvailableVehicles(conversation);
                break;

            case "send_booking_link":
                await SendBookingLink(conversation);
                break;

            case "handoff":
                conversation.State = ConversationState.HandoffToHuman;
                await _context.SaveChangesAsync();
                await _messagingService.SendTextMessageAsync(
                    conversation.CompanyId,
                    conversation.InstagramUserId,
                    response.Message ?? "Connecting you with our team...");
                break;

            default:
                // Just send the message
                await _messagingService.SendTextMessageAsync(
                    conversation.CompanyId,
                    conversation.InstagramUserId,
                    response.Message ?? "How can I help you today?");
                break;
        }
    }

    private async Task ShowAvailableVehicles(InstagramConversation conversation)
    {
        if (!conversation.PickupDate.HasValue || !conversation.ReturnDate.HasValue)
        {
            await _messagingService.SendTextMessageAsync(
                conversation.CompanyId,
                conversation.InstagramUserId,
                "I'd love to show you our cars! 🚗 When do you need it? (pickup & return dates)");
            return;
        }

        var models = await GetAvailableModels(
            conversation.CompanyId,
            conversation.PickupDate.Value,
            conversation.ReturnDate.Value,
            conversation.PickupLocation);

        if (!models.Any())
        {
            await _messagingService.SendTextMessageAsync(
                conversation.CompanyId,
                conversation.InstagramUserId,
                "Sorry, no cars available for those dates. Try different dates?");
            return;
        }

        var days = (conversation.ReturnDate.Value - conversation.PickupDate.Value).Days;
        if (days < 1) days = 1;

        // Build carousel of vehicles
        var elements = models.Take(5).Select(m => new TemplateElement
        {
            Title = $"{m.Make} {m.ModelName}",
            Subtitle = $"${m.DailyRate}/day • {days}d = ${m.DailyRate * days}",
            ImageUrl = m.ImageUrl,
            Buttons = new List<TemplateButton>
            {
                new TemplateButton
                {
                    Type = ButtonType.Postback,
                    Title = "Book This",
                    Value = $"SELECT_MODEL_{m.ModelId}"
                }
            }
        }).ToList();

        await _messagingService.SendGenericTemplateAsync(
            conversation.CompanyId,
            conversation.InstagramUserId,
            elements);

        conversation.State = ConversationState.ShowingVehicles;
        await _context.SaveChangesAsync();
    }

    private async Task HandleModelSelection(InstagramConversation conversation, Guid modelId)
    {
        conversation.SelectedModelId = modelId;
        conversation.State = ConversationState.VehicleSelected;
        await _context.SaveChangesAsync();

        await SendBookingLink(conversation);
    }

    private async Task SendBookingLink(InstagramConversation conversation)
    {
        if (!conversation.SelectedModelId.HasValue)
        {
            await _messagingService.SendTextMessageAsync(
                conversation.CompanyId,
                conversation.InstagramUserId,
                "Please select a car first! 🚗");
            return;
        }

        var model = await _context.Set<Model>()
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == conversation.SelectedModelId.Value);
            
        if (model == null)
        {
            await _messagingService.SendTextMessageAsync(
                conversation.CompanyId,
                conversation.InstagramUserId,
                "Sorry, that car is no longer available. Please select another.");
            return;
        }

        // Build booking URL
        var subdomain = conversation.Company?.Subdomain ?? "";
        var baseUrl = $"https://{subdomain}.aegis-rental.com";
        
        var bookingParams = new List<string>
        {
            $"model={conversation.SelectedModelId}"
        };

        if (conversation.PickupDate.HasValue)
            bookingParams.Add($"pickup={conversation.PickupDate.Value:yyyy-MM-dd}");
        if (conversation.ReturnDate.HasValue)
            bookingParams.Add($"return={conversation.ReturnDate.Value:yyyy-MM-dd}");
        if (!string.IsNullOrEmpty(conversation.PickupLocation))
            bookingParams.Add($"location={Uri.EscapeDataString(conversation.PickupLocation)}");

        var bookingUrl = $"{baseUrl}/book?{string.Join("&", bookingParams)}";

        var message = $"🎉 {model.Make} {model.ModelName} - great choice!";

        await _messagingService.SendButtonTemplateAsync(
            conversation.CompanyId,
            conversation.InstagramUserId,
            message,
            new List<TemplateButton>
            {
                new TemplateButton
                {
                    Type = ButtonType.WebUrl,
                    Title = "Complete Booking",
                    Value = bookingUrl
                },
                new TemplateButton
                {
                    Type = ButtonType.Postback,
                    Title = "Other Cars",
                    Value = "SHOW_MORE_VEHICLES"
                }
            });

        conversation.State = ConversationState.BookingLinkSent;
        await _context.SaveChangesAsync();
    }

    private async Task SendFallbackMessage(InstagramConversation conversation)
    {
        await _messagingService.SendQuickRepliesAsync(
            conversation.CompanyId,
            conversation.InstagramUserId,
            "How can I help? 🚗",
            new List<QuickReplyButton>
            {
                new QuickReplyButton { Title = "Book a Car", Payload = "I want to rent a car" },
                new QuickReplyButton { Title = "See Cars", Payload = "Show me available cars" },
                new QuickReplyButton { Title = "Human Help", Payload = "TALK_TO_HUMAN" }
            });
    }
}

#region Internal DTOs

internal class ConversationContext
{
    public string CompanyName { get; set; } = "";
    public string Currency { get; set; } = "USD";
    public string Language { get; set; } = "en";
    public List<string> Locations { get; set; } = new();
    public List<AvailableModel> AvailableModels { get; set; } = new();
}

internal class AvailableModel
{
    public Guid ModelId { get; set; }
    public string Make { get; set; } = "";
    public string ModelName { get; set; } = "";
    public int Year { get; set; }
    public string? Category { get; set; }
    public decimal DailyRate { get; set; }
    public string? ImageUrl { get; set; }
    public int AvailableCount { get; set; }
}

internal class AIResponse
{
    public string? Message { get; set; }
    public string? Action { get; set; }
    public ExtractedData? ExtractedData { get; set; }
}

internal class ExtractedData
{
    public string? PickupDate { get; set; }
    public string? ReturnDate { get; set; }
    public string? Location { get; set; }
    public Guid? SelectedModelId { get; set; }
}

internal class ClaudeApiResponse
{
    public List<ContentBlock>? Content { get; set; }
}

internal class ContentBlock
{
    public string? Type { get; set; }
    public string? Text { get; set; }
}

#endregion
