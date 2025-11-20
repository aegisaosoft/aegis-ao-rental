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

[Table("companies")]
public class Company
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(255)]
    [Column("company_name")]
    public string CompanyName { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    [EmailAddress]
    [Column("email")]
    public string Email { get; set; } = string.Empty;

    [MaxLength(255)]
    [Column("website")]
    public string? Website { get; set; }

    [MaxLength(255)]
    [Column("stripe_account_id")]
    public string? StripeAccountId { get; set; }

    [MaxLength(100)]
    [Column("tax_id")]
    public string? TaxId { get; set; }

    [MaxLength(500)]
    [Column("video_link")]
    public string? VideoLink { get; set; }

    [MaxLength(500)]
    [Column("banner_link")]
    public string? BannerLink { get; set; }

    [MaxLength(500)]
    [Column("logo_link")]
    public string? LogoLink { get; set; }

    [MaxLength(255)]
    [Column("motto")]
    public string? Motto { get; set; } = "Meet our newest fleet yet";

    [MaxLength(500)]
    [Column("motto_description")]
    public string? MottoDescription { get; set; } = "New rental cars. No lines. Let's go!";

    [Column("invitation")]
    public string? Invitation { get; set; } = "Find & Book a Great Deal Today";

    [Column("texts", TypeName = "jsonb")]
    public string? Texts { get; set; } // Stored as JSON string

    [MaxLength(255)]
    [Column("background_link")]
    public string? BackgroundLink { get; set; }

    [Column("about", TypeName = "jsonb")]
    public string? About { get; set; }

    [Column("terms_of_use", TypeName = "jsonb")]
    public string? TermsOfUse { get; set; }

    [Column("booking_integrated")]
    public string? BookingIntegrated { get; set; }

    [Column("company_path")]
    public string? CompanyPath { get; set; }

    [MaxLength(100)]
    [Column("subdomain")]
    public string? Subdomain { get; set; }

    [MaxLength(7)]
    [Column("primary_color")]
    public string? PrimaryColor { get; set; }

    [MaxLength(7)]
    [Column("secondary_color")]
    public string? SecondaryColor { get; set; }

    [MaxLength(500)]
    [Column("logo_url")]
    public string? LogoUrl { get; set; }

    [MaxLength(500)]
    [Column("favicon_url")]
    public string? FaviconUrl { get; set; }

    [Column("custom_css")]
    public string? CustomCss { get; set; }

    [MaxLength(100)]
    [Column("country")]
    public string? Country { get; set; }

    [Required]
    [MaxLength(3)]
    [Column("currency")]
    public string Currency { get; set; } = "USD";

    [Column("security_deposit", TypeName = "decimal(10,2)")]
    public decimal SecurityDeposit { get; set; } = 1000m;

    [Column("is_security_deposit_mandatory")]
    public bool IsSecurityDepositMandatory { get; set; } = true;

    [MaxLength(10)]
    [Column("language")]
    public string? Language { get; set; } = "en"; // ISO 639-1 language code

    [Column("blink_key")]
    public string? BlinkKey { get; set; } // BlinkID license key for the company (domain-specific license)

    [MaxLength(20)]
    [Column("ai_integration")]
    public string AiIntegration { get; set; } = "claude";

    // Stripe Connect Onboarding Properties
    [Column("stripe_onboarding_completed")]
    public bool? StripeOnboardingCompleted { get; set; } = false;

    [Column("stripe_charges_enabled")]
    public bool? StripeChargesEnabled { get; set; } = false;

    [Column("stripe_payouts_enabled")]
    public bool? StripePayoutsEnabled { get; set; } = false;

    [Column("stripe_details_submitted")]
    public bool? StripeDetailsSubmitted { get; set; } = false;

    [MaxLength(20)]
    [Column("stripe_account_type")]
    public string? StripeAccountType { get; set; } = "express";

    [Column("platform_fee_percentage", TypeName = "decimal(5,2)")]
    public decimal PlatformFeePercentage { get; set; } = 10.00m;

    // Stripe Requirements
    [Column("stripe_requirements_currently_due", TypeName = "text[]")]
    public string[]? StripeRequirementsCurrentlyDue { get; set; }

    [Column("stripe_requirements_eventually_due", TypeName = "text[]")]
    public string[]? StripeRequirementsEventuallyDue { get; set; }

    [Column("stripe_requirements_past_due", TypeName = "text[]")]
    public string[]? StripeRequirementsPastDue { get; set; }

    [MaxLength(255)]
    [Column("stripe_requirements_disabled_reason")]
    public string? StripeRequirementsDisabledReason { get; set; }

    // Stripe Sync Tracking
    [Column("stripe_last_sync_at")]
    public DateTime? StripeLastSyncAt { get; set; }

    [Column("stripe_onboarding_link")]
    public string? StripeOnboardingLink { get; set; }

    [Column("stripe_onboarding_link_expires_at")]
    public DateTime? StripeOnboardingLinkExpiresAt { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("is_test_company")]
    public bool IsTestCompany { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
    public virtual ICollection<Location> Locations { get; set; } = new List<Location>();
    public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public virtual ICollection<Rental> Rentals { get; set; } = new List<Rental>();
    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();
}
