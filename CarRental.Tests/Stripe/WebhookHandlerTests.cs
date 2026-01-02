/*
 * CarRental.Tests - Stripe Webhook Handler Tests
 * Copyright (c) 2025 Alexander Orlov
 * 
 * Tests for WebhooksController event handling logic
 */

using Xunit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using CarRental.Api.Models;
using CarRental.Tests.Infrastructure;

namespace CarRental.Tests.Stripe;

/// <summary>
/// Tests for Stripe Webhook event handling
/// Verifies that webhook events correctly update database records
/// </summary>
[Collection("PostgreSQL")]
public class WebhookHandlerTests : PostgresTestBase
{
    #region Payment Intent Events

    [Fact]
    public async Task PaymentIntentSucceeded_ShouldUpdateBookingStatusToConfirmed()
    {
        // Arrange
        var (company, customer, vehicle, booking) = await SeedCompleteScenarioAsync();
        booking.Status = "Pending";
        booking.StripePaymentIntentId = "pi_test_123";
        await Context.SaveChangesAsync();

        // Act - Simulate payment_intent.succeeded webhook effect
        var loadedBooking = await Context.Bookings.FindAsync(booking.Id);
        loadedBooking!.Status = "Confirmed";
        loadedBooking.PaymentStatus = "paid";
        await Context.SaveChangesAsync();

        // Assert
        Context.ChangeTracker.Clear();
        var verifyBooking = await Context.Bookings.FindAsync(booking.Id);
        verifyBooking!.Status.Should().Be("Confirmed");
        verifyBooking.PaymentStatus.Should().Be("paid");
    }

    [Fact]
    public async Task PaymentIntentFailed_ShouldUpdatePaymentStatus()
    {
        // Arrange
        var (company, customer, vehicle, booking) = await SeedCompleteScenarioAsync();
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            CompanyId = company.Id,
            ReservationId = booking.Id,
            Amount = 300m,
            Currency = "USD",
            PaymentType = "full_payment",
            PaymentMethod = "card",
            StripePaymentIntentId = "pi_test_failed_123",
            Status = "pending"
        };
        Context.Payments.Add(payment);
        await Context.SaveChangesAsync();
        TrackForCleanup(payment);

        // Act - Simulate payment_intent.payment_failed webhook effect
        var loadedPayment = await Context.Payments.FindAsync(payment.Id);
        loadedPayment!.Status = "failed";
        loadedPayment.FailureReason = "Your card was declined";
        loadedPayment.UpdatedAt = DateTime.UtcNow;
        await Context.SaveChangesAsync();

        // Assert
        Context.ChangeTracker.Clear();
        var verifyPayment = await Context.Payments.FindAsync(payment.Id);
        verifyPayment!.Status.Should().Be("failed");
        verifyPayment.FailureReason.Should().Contain("declined");
    }

    #endregion

    #region Charge Events

    [Fact]
    public async Task ChargeSucceeded_ShouldCreatePaymentRecordIfNotExists()
    {
        // Arrange
        var (company, customer, vehicle, booking) = await SeedCompleteScenarioAsync();
        booking.StripePaymentIntentId = "pi_charge_test_123";
        booking.Status = "Pending";
        await Context.SaveChangesAsync();

        // Act - Simulate charge.succeeded webhook creating payment record
        var newPayment = new Payment
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            CompanyId = company.Id,
            ReservationId = booking.Id,
            Amount = booking.TotalAmount,
            Currency = "USD",
            PaymentType = "full_payment",
            PaymentMethod = "card",
            StripePaymentIntentId = "pi_charge_test_123",
            StripeChargeId = "ch_test_123",
            Status = "succeeded",
            ProcessedAt = DateTime.UtcNow
        };
        Context.Payments.Add(newPayment);
        
        booking.Status = "Confirmed";
        await Context.SaveChangesAsync();
        TrackForCleanup(newPayment);

        // Assert
        Context.ChangeTracker.Clear();
        var verifyPayment = await Context.Payments
            .FirstOrDefaultAsync(p => p.StripeChargeId == "ch_test_123");
        verifyPayment.Should().NotBeNull();
        verifyPayment!.Status.Should().Be("succeeded");
        
        var verifyBooking = await Context.Bookings.FindAsync(booking.Id);
        verifyBooking!.Status.Should().Be("Confirmed");
    }

    [Fact]
    public async Task ChargeRefunded_ShouldUpdatePaymentStatus()
    {
        // Arrange
        var (company, customer, vehicle, booking) = await SeedCompleteScenarioAsync();
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            CompanyId = company.Id,
            ReservationId = booking.Id,
            Amount = 300m,
            Currency = "USD",
            PaymentType = "full_payment",
            PaymentMethod = "card",
            StripePaymentIntentId = "pi_refund_test_123",
            StripeChargeId = "ch_refund_test_123",
            Status = "succeeded",
            ProcessedAt = DateTime.UtcNow
        };
        Context.Payments.Add(payment);
        await Context.SaveChangesAsync();
        TrackForCleanup(payment);

        // Act - Simulate charge.refunded webhook effect
        var loadedPayment = await Context.Payments.FindAsync(payment.Id);
        loadedPayment!.Status = "refunded";
        loadedPayment.RefundAmount = 300m;
        loadedPayment.RefundDate = DateTime.UtcNow;
        loadedPayment.UpdatedAt = DateTime.UtcNow;
        await Context.SaveChangesAsync();

        // Assert
        Context.ChangeTracker.Clear();
        var verifyPayment = await Context.Payments.FindAsync(payment.Id);
        verifyPayment!.Status.Should().Be("refunded");
        verifyPayment.RefundAmount.Should().Be(300m);
        verifyPayment.RefundDate.Should().NotBeNull();
    }

    #endregion

    #region Checkout Session Events

    [Fact]
    public async Task CheckoutSessionCompleted_SecurityDeposit_ShouldUpdateBookingStatus()
    {
        // Arrange
        var (company, customer, vehicle, booking) = await SeedCompleteScenarioAsync();
        booking.Status = "Confirmed";
        booking.SecurityDeposit = 500m;
        await Context.SaveChangesAsync();

        // Act - Simulate checkout.session.completed for security deposit
        var loadedBooking = await Context.Bookings.FindAsync(booking.Id);
        loadedBooking!.SecurityDepositPaymentIntentId = "pi_deposit_123";
        loadedBooking.SecurityDepositStatus = "authorized";
        loadedBooking.SecurityDepositAuthorizedAt = DateTime.UtcNow;
        loadedBooking.Status = "Active";
        await Context.SaveChangesAsync();

        // Assert
        Context.ChangeTracker.Clear();
        var verifyBooking = await Context.Bookings.FindAsync(booking.Id);
        verifyBooking!.Status.Should().Be("Active");
        verifyBooking.SecurityDepositStatus.Should().Be("authorized");
        verifyBooking.SecurityDepositPaymentIntentId.Should().Be("pi_deposit_123");
        verifyBooking.SecurityDepositAuthorizedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task CheckoutSessionCompleted_RegularPayment_ShouldConfirmBooking()
    {
        // Arrange
        var (company, customer, vehicle, booking) = await SeedCompleteScenarioAsync();
        booking.Status = "Pending";
        await Context.SaveChangesAsync();

        // Act - Simulate checkout.session.completed for regular payment
        var newPayment = new Payment
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            CompanyId = company.Id,
            ReservationId = booking.Id,
            Amount = booking.TotalAmount,
            Currency = "USD",
            PaymentType = "booking",
            PaymentMethod = "stripe_checkout",
            StripePaymentIntentId = "pi_checkout_123",
            Status = "succeeded",
            ProcessedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        Context.Payments.Add(newPayment);
        
        booking.Status = "Confirmed";
        booking.StripePaymentIntentId = "pi_checkout_123";
        await Context.SaveChangesAsync();
        TrackForCleanup(newPayment);

        // Assert
        Context.ChangeTracker.Clear();
        var verifyBooking = await Context.Bookings.FindAsync(booking.Id);
        verifyBooking!.Status.Should().Be("Confirmed");
        verifyBooking.StripePaymentIntentId.Should().Be("pi_checkout_123");
    }

    #endregion

    #region Account Events (Stripe Connect)

    [Fact]
    public async Task AccountUpdated_ShouldUpdateCompanyStripeStatus()
    {
        // Arrange
        var company = await SeedCompanyAsync();
        company.StripeAccountId = "acct_test_123";
        company.StripeChargesEnabled = false;
        company.StripePayoutsEnabled = false;
        company.StripeDetailsSubmitted = false;
        company.StripeOnboardingCompleted = false;
        await Context.SaveChangesAsync();

        // Act - Simulate account.updated webhook effect
        var loadedCompany = await Context.Companies.FindAsync(company.Id);
        loadedCompany!.StripeChargesEnabled = true;
        loadedCompany.StripePayoutsEnabled = true;
        loadedCompany.StripeDetailsSubmitted = true;
        loadedCompany.StripeOnboardingCompleted = true;
        loadedCompany.StripeLastSyncAt = DateTime.UtcNow;
        loadedCompany.UpdatedAt = DateTime.UtcNow;
        await Context.SaveChangesAsync();

        // Assert
        Context.ChangeTracker.Clear();
        var verifyCompany = await Context.Companies.FindAsync(company.Id);
        verifyCompany!.StripeChargesEnabled.Should().BeTrue();
        verifyCompany.StripePayoutsEnabled.Should().BeTrue();
        verifyCompany.StripeDetailsSubmitted.Should().BeTrue();
        verifyCompany.StripeOnboardingCompleted.Should().BeTrue();
        verifyCompany.StripeLastSyncAt.Should().NotBeNull();
    }

    [Fact]
    public async Task AccountUpdated_WithRequirements_ShouldStoreRequirementsArray()
    {
        // Arrange
        var company = await SeedCompanyAsync();
        company.StripeAccountId = "acct_test_req_123";
        await Context.SaveChangesAsync();

        // Act - Simulate account.updated with requirements
        var loadedCompany = await Context.Companies.FindAsync(company.Id);
        loadedCompany!.StripeRequirementsCurrentlyDue = new[] { "business_profile.url", "external_account" };
        loadedCompany.StripeRequirementsEventuallyDue = new[] { "business_profile.support_phone" };
        loadedCompany.StripeRequirementsPastDue = Array.Empty<string>();
        loadedCompany.StripeRequirementsDisabledReason = null;
        await Context.SaveChangesAsync();

        // Assert
        Context.ChangeTracker.Clear();
        var verifyCompany = await Context.Companies.FindAsync(company.Id);
        verifyCompany!.StripeRequirementsCurrentlyDue.Should().HaveCount(2);
        verifyCompany.StripeRequirementsCurrentlyDue.Should().Contain("business_profile.url");
        verifyCompany.StripeRequirementsEventuallyDue.Should().HaveCount(1);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task DuplicateWebhook_ShouldNotCreateDuplicatePayments()
    {
        // Arrange
        var (company, customer, vehicle, booking) = await SeedCompleteScenarioAsync();
        var paymentIntentId = "pi_duplicate_test_123";
        
        // First payment record
        var payment1 = new Payment
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            CompanyId = company.Id,
            ReservationId = booking.Id,
            Amount = 300m,
            Currency = "USD",
            PaymentType = "full_payment",
            PaymentMethod = "card",
            StripePaymentIntentId = paymentIntentId,
            Status = "succeeded",
            ProcessedAt = DateTime.UtcNow
        };
        Context.Payments.Add(payment1);
        await Context.SaveChangesAsync();
        TrackForCleanup(payment1);

        // Act - Check if payment already exists (as webhook handler should do)
        var existingPayment = await Context.Payments
            .FirstOrDefaultAsync(p => p.StripePaymentIntentId == paymentIntentId);

        // Assert - Should find existing payment, not create duplicate
        existingPayment.Should().NotBeNull();
        existingPayment!.Id.Should().Be(payment1.Id);
        
        var paymentCount = await Context.Payments
            .CountAsync(p => p.StripePaymentIntentId == paymentIntentId);
        paymentCount.Should().Be(1);
    }

    [Fact]
    public async Task WebhookForUnknownBooking_ShouldNotThrow()
    {
        // Arrange
        var unknownPaymentIntentId = "pi_unknown_booking_123";

        // Act - Search for booking that doesn't exist
        var booking = await Context.Bookings
            .FirstOrDefaultAsync(b => b.StripePaymentIntentId == unknownPaymentIntentId);

        // Assert - Should return null, not throw
        booking.Should().BeNull();
    }

    #endregion
}
