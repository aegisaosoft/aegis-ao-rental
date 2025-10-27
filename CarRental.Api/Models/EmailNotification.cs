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

[Table("email_notifications")]
public class EmailNotification
{
    [Key]
    [Column("notification_id")]
    public Guid NotificationId { get; set; } = Guid.NewGuid();

    [Column("booking_token_id")]
    public Guid? BookingTokenId { get; set; }

    [Required]
    [EmailAddress]
    [MaxLength(255)]
    [Column("customer_email")]
    public string CustomerEmail { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    [Column("notification_type")]
    public string NotificationType { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string Subject { get; set; } = string.Empty;

    [Required]
    public string Body { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Status { get; set; } = "pending";

    [Column("sent_at")]
    public DateTime? SentAt { get; set; }

    [Column("error_message")]
    public string? ErrorMessage { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey("BookingTokenId")]
    public virtual BookingToken? BookingToken { get; set; }
}
