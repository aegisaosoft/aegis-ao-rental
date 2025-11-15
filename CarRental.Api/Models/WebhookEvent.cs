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

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CarRental.Api.Models;

[Table("webhook_events")]
public class WebhookEvent
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(255)]
    [Column("stripe_event_id")]
    public string StripeEventId { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [Column("event_type")]
    public string EventType { get; set; } = string.Empty;

    [MaxLength(255)]
    [Column("connected_account_id")]
    public string? ConnectedAccountId { get; set; }

    [Column("booking_id")]
    public Guid? BookingId { get; set; }

    [Column("company_id")]
    public Guid? CompanyId { get; set; }

    [Column("payload", TypeName = "jsonb")]
    public string? Payload { get; set; }

    [Column("processed")]
    public bool Processed { get; set; } = false;

    [Column("processed_at")]
    public DateTime? ProcessedAt { get; set; }

    [MaxLength(100)]
    [Column("processed_by")]
    public string? ProcessedBy { get; set; }

    [Column("error_message")]
    public string? ErrorMessage { get; set; }

    [Column("retry_count")]
    public int RetryCount { get; set; } = 0;

    [Column("next_retry_at")]
    public DateTime? NextRetryAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey("CompanyId")]
    public virtual Company? Company { get; set; }

    [ForeignKey("BookingId")]
    public virtual Booking? Booking { get; set; }
}

