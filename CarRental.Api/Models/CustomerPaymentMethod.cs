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

[Table("customer_payment_methods")]
public class CustomerPaymentMethod
{
    [Key]
    [Column("payment_method_id")]
    public Guid PaymentMethodId { get; set; } = Guid.NewGuid();

    [Required]
    [Column("customer_id")]
    public Guid CustomerId { get; set; }

    [Required]
    [MaxLength(255)]
    [Column("stripe_payment_method_id")]
    public string StripePaymentMethodId { get; set; } = string.Empty;

    [MaxLength(50)]
    [Column("card_brand")]
    public string? CardBrand { get; set; }

    [MaxLength(4)]
    [Column("card_last4")]
    public string? CardLast4 { get; set; }

    [Column("card_exp_month")]
    public int? CardExpMonth { get; set; }

    [Column("card_exp_year")]
    public int? CardExpYear { get; set; }

    [Column("is_default")]
    public bool IsDefault { get; set; } = false;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey("CustomerId")]
    public virtual Customer Customer { get; set; } = null!;
}
