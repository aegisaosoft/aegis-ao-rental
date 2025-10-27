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
using System.Text.Json;

namespace CarRental.Api.Models;

[Table("booking_confirmations")]
public class BookingConfirmation
{
    [Key]
    [Column("confirmation_id")]
    public Guid ConfirmationId { get; set; } = Guid.NewGuid();

    [Required]
    [Column("booking_token_id")]
    public Guid BookingTokenId { get; set; }

    [Column("reservation_id")]
    public Guid? ReservationId { get; set; }

    [Required]
    [EmailAddress]
    [MaxLength(255)]
    [Column("customer_email")]
    public string CustomerEmail { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    [Column("confirmation_number")]
    public string ConfirmationNumber { get; set; } = string.Empty;

    [Required]
    [Column("booking_details", TypeName = "jsonb")]
    public string BookingDetailsJson { get; set; } = string.Empty;

    [NotMapped]
    public BookingData BookingDetails
    {
        get => JsonSerializer.Deserialize<BookingData>(BookingDetailsJson) ?? new BookingData();
        set => BookingDetailsJson = JsonSerializer.Serialize(value);
    }

    [Required]
    [MaxLength(50)]
    [Column("payment_status")]
    public string PaymentStatus { get; set; } = "pending";

    [MaxLength(255)]
    [Column("stripe_payment_intent_id")]
    public string? StripePaymentIntentId { get; set; }

    [Column("confirmation_sent")]
    public bool ConfirmationSent { get; set; } = false;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey("BookingTokenId")]
    public virtual BookingToken BookingToken { get; set; } = null!;

    [ForeignKey("ReservationId")]
    public virtual Reservation? Reservation { get; set; }
}
