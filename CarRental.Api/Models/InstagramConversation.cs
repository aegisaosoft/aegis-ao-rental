/*
 * CarRental.Api - Instagram DM Conversation Models
 * Copyright (c) 2025 Alexander Orlov
 * 
 * Models for storing Instagram DM conversation history for AI-powered booking assistant
 */

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRental.Api.Models;

/// <summary>
/// Stores Instagram DM conversation sessions for booking assistant
/// </summary>
public class InstagramConversation
{
    public int Id { get; set; }

    /// <summary>
    /// Reference to the rental company
    /// </summary>
    public Guid CompanyId { get; set; }

    /// <summary>
    /// Instagram user ID (IGSID - Instagram Scoped ID)
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string InstagramUserId { get; set; } = "";

    /// <summary>
    /// Instagram username (if available)
    /// </summary>
    [MaxLength(100)]
    public string? InstagramUsername { get; set; }

    /// <summary>
    /// Current conversation state
    /// </summary>
    public ConversationState State { get; set; } = ConversationState.Initial;

    /// <summary>
    /// Pickup date selected by user
    /// </summary>
    public DateTime? PickupDate { get; set; }

    /// <summary>
    /// Return date selected by user
    /// </summary>
    public DateTime? ReturnDate { get; set; }

    /// <summary>
    /// Pickup location selected by user
    /// </summary>
    [MaxLength(200)]
    public string? PickupLocation { get; set; }

    /// <summary>
    /// Selected vehicle model ID
    /// </summary>
    public Guid? SelectedModelId { get; set; }

    /// <summary>
    /// If booking was created, reference to it
    /// </summary>
    public Guid? BookingId { get; set; }

    /// <summary>
    /// User's preferred language (detected or stated)
    /// </summary>
    [MaxLength(10)]
    public string Language { get; set; } = "en";

    /// <summary>
    /// When the conversation started
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last activity in conversation
    /// </summary>
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the conversation expires (inactive conversations cleanup)
    /// </summary>
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddHours(24);

    // Navigation properties
    public Company? Company { get; set; }
    public Model? SelectedModel { get; set; }
    public Booking? Booking { get; set; }
    public ICollection<InstagramMessage> Messages { get; set; } = new List<InstagramMessage>();
}

/// <summary>
/// Stores individual messages in Instagram DM conversation
/// </summary>
public class InstagramMessage
{
    public long Id { get; set; }

    /// <summary>
    /// Reference to the conversation
    /// </summary>
    public int ConversationId { get; set; }

    /// <summary>
    /// Instagram message ID (for deduplication)
    /// </summary>
    [MaxLength(100)]
    public string? InstagramMessageId { get; set; }

    /// <summary>
    /// Who sent the message
    /// </summary>
    public MessageSender Sender { get; set; }

    /// <summary>
    /// Message content
    /// </summary>
    [Required]
    public string Content { get; set; } = "";

    /// <summary>
    /// Message type (text, image, quick_reply, etc.)
    /// </summary>
    [MaxLength(50)]
    public string MessageType { get; set; } = "text";

    /// <summary>
    /// Quick reply payload if applicable
    /// </summary>
    [MaxLength(500)]
    public string? QuickReplyPayload { get; set; }

    /// <summary>
    /// When the message was sent/received
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Was the message successfully delivered (for outgoing)
    /// </summary>
    public bool? Delivered { get; set; }

    /// <summary>
    /// Error message if delivery failed
    /// </summary>
    public string? DeliveryError { get; set; }

    // Navigation property
    public InstagramConversation? Conversation { get; set; }
}

/// <summary>
/// Conversation state machine
/// </summary>
public enum ConversationState
{
    /// <summary>
    /// Initial greeting, waiting for intent
    /// </summary>
    Initial = 0,

    /// <summary>
    /// Asked for dates
    /// </summary>
    AskingDates = 1,

    /// <summary>
    /// Asked for location
    /// </summary>
    AskingLocation = 2,

    /// <summary>
    /// Showing available vehicles
    /// </summary>
    ShowingVehicles = 3,

    /// <summary>
    /// User selected a vehicle, showing details
    /// </summary>
    VehicleSelected = 4,

    /// <summary>
    /// Sent booking link
    /// </summary>
    BookingLinkSent = 5,

    /// <summary>
    /// Booking completed
    /// </summary>
    BookingCompleted = 6,

    /// <summary>
    /// User cancelled or conversation ended
    /// </summary>
    Ended = 7,

    /// <summary>
    /// Waiting for human support
    /// </summary>
    HandoffToHuman = 8
}

/// <summary>
/// Who sent the message
/// </summary>
public enum MessageSender
{
    /// <summary>
    /// Message from Instagram user (customer)
    /// </summary>
    User = 0,

    /// <summary>
    /// Message from AI assistant
    /// </summary>
    Assistant = 1,

    /// <summary>
    /// System message (e.g., conversation started)
    /// </summary>
    System = 2
}
