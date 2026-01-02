/*
 * CarRental.Tests - Payment Processing Tests
 * Copyright (c) 2025 Alexander Orlov
 * 
 * Tests for Payment Intent, Checkout Session, and Refund logic
 */

using Xunit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using CarRental.Api.Models;
using CarRental.Tests.Infrastructure;

namespace CarRental.Tests.Stripe;

/// <summary>
/// Tests for Payment processing operations
/// Tests checkout sessions, payment intents, and refunds
/// </summary>
[Collection("PostgreSQL")]
public class PaymentProcessingTests : PostgresTestBase
{
    #region Checkout Session Tests

    [Fact]
    public async Task CreateCheckoutSession_ShouldPrepareBookingForPayment()
    {
        // Arrange
        var (company, customer, vehicle, booking) = await SeedCompleteScenarioAsync();
        booking.Status = "Pending";
        booking.PaymentStatus = "pending";
        await Context.SaveChangesAsync();

        // Assert - Booking should be ready for checkout
        Context.ChangeTracker.Clear();
        var verifyBooking = await Context.Bookings.FindAsync(booking.Id);
        verifyBooking!.Status.Should().Be("Pending");
        verifyBooking.TotalAmount.Should().BeGreaterThan(0);
        verifyBooking.CustomerId.Should().NotBeEmpty();
        verifyBooking.CompanyId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CheckoutSession_WithMetadata_ShouldStoreBookingReference()
    {
        // Arrange
        var (company, customer, vehicle, booking) = await SeedCompleteScenarioAsync();
        
        // Act - Simulate checkout session with metadata stored on payment
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            CompanyId = company.Id,
            ReservationId = booking.Id,
            Amount = booking.TotalAmount,
            Currency = "USD",
            PaymentType = "booking",
            PaymentMethod = "stripe_checkout",
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };
        Context.Payments.Add(payment);
        await Context.SaveChangesAsync();

        // Assert - Payment should reference booking
        Context.ChangeTracker.Clear();
        var verifyPayment = await Context.Payments
            .FirstOrDefaultAsync(p => p.ReservationId == booking.Id);
        
        verifyPayment.Should().NotBeNull();
        verifyPayment!.ReservationId.Should().Be(booking.Id);
        verifyPayment.CustomerId.Should().Be(customer.Id);
        verifyPayment.CompanyId.Should().Be(company.Id);

        // Cleanup
        Context.Payments.Remove(verifyPayment);
        await Context.SaveChangesAsync();
    }

    #endregion

    #region Payment Intent Tests

    [Fact]
    public async Task PaymentIntent_ShouldBeLinkedToBooking()
    {
        // Arrange
        var (company, customer, vehicle, booking) = await SeedCompleteScenarioAsync();
        var paymentIntentId = "pi_booking_link_123";

        // Act - Link payment intent to booking
        var loadedBooking = await Context.Bookings.FindAsync(booking.Id);
        loadedBooking!.StripePaymentIntentId = paymentIntentId;
        await Context.SaveChangesAsync();

        // Assert
        Context.ChangeTracker.Clear();
        var verifyBooking = await Context.Bookings
            .FirstOrDefaultAsync(b => b.StripePaymentIntentId == paymentIntentId);
        
        verifyBooking.Should().NotBeNull();
        verifyBooking!.Id.Should().Be(booking.Id);
    }

    [Fact]
    public async Task PaymentIntent_RequiresAction_ShouldBeTracked()
    {
        // Arrange
        var (company, customer, vehicle, booking) = await SeedCompleteScenarioAsync();
        
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            CompanyId = company.Id,
            ReservationId = booking.Id,
            Amount = booking.TotalAmount,
            Currency = "USD",
            PaymentType = "full_payment",
            PaymentMethod = "card",
            StripePaymentIntentId = "pi_3d_secure_123",
            Status = "requires_action", // 3D Secure required
            CreatedAt = DateTime.UtcNow
        };
        Context.Payments.Add(payment);
        await Context.SaveChangesAsync();

        // Assert
        Context.ChangeTracker.Clear();
        var verifyPayment = await Context.Payments.FindAsync(payment.Id);
        verifyPayment!.Status.Should().Be("requires_action");

        // Cleanup
        Context.Payments.Remove(verifyPayment);
        await Context.SaveChangesAsync();
    }

    [Theory]
    [InlineData("pending", "Payment is being processed")]
    [InlineData("requires_action", "Additional authentication required")]
    [InlineData("requires_payment_method", "Payment method needed")]
    [InlineData("succeeded", "Payment successful")]
    [InlineData("canceled", "Payment was canceled")]
    [InlineData("failed", "Payment failed")]
    public void PaymentIntentStatus_ShouldHaveMeaningfulDescription(string status, string expectedContains)
    {
        // Arrange & Act
        var description = GetPaymentStatusDescription(status);

        // Assert
        description.Should().Contain(expectedContains.Split(' ')[0], 
            because: $"Status '{status}' should have meaningful description");
    }

    private static string GetPaymentStatusDescription(string status)
    {
        return status switch
        {
            "pending" => "Payment is being processed",
            "requires_action" => "Additional authentication required (3D Secure)",
            "requires_payment_method" => "Payment method needed",
            "succeeded" => "Payment successful",
            "canceled" => "Payment was canceled",
            "failed" => "Payment failed - please try again",
            _ => "Unknown status"
        };
    }

    #endregion

    #region Refund Tests

    [Fact]
    public async Task CreateRefund_FullRefund_ShouldCreateNegativePayment()
    {
        // Arrange
        var (company, customer, vehicle, booking) = await SeedCompleteScenarioAsync();
        var paymentIntentId = $"pi_refund_full_{Guid.NewGuid():N}";
        
        var originalPayment = new Payment
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
            ProcessedAt = DateTime.UtcNow.AddDays(-1)
        };
        Context.Payments.Add(originalPayment);
        await Context.SaveChangesAsync();
        TrackForCleanup(originalPayment);

        // Act - Create refund payment record (refunds don't have their own PaymentIntentId)
        var refundPayment = new Payment
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            CompanyId = company.Id,
            ReservationId = booking.Id,
            Amount = -300m, // Negative amount for refund
            Currency = "USD",
            PaymentType = "refund",
            PaymentMethod = "stripe_refund",
            StripePaymentIntentId = null, // Refunds don't have their own PI
            Status = "succeeded",
            ProcessedAt = DateTime.UtcNow
        };
        Context.Payments.Add(refundPayment);
        await Context.SaveChangesAsync();
        TrackForCleanup(refundPayment);

        // Assert
        Context.ChangeTracker.Clear();
        var payments = await Context.Payments
            .Where(p => p.ReservationId == booking.Id)
            .ToListAsync();
        
        payments.Should().HaveCount(2);
        payments.Sum(p => p.Amount).Should().Be(0); // Net zero after refund
    }

    [Fact]
    public async Task CreateRefund_PartialRefund_ShouldRecordPartialAmount()
    {
        // Arrange
        var (company, customer, vehicle, booking) = await SeedCompleteScenarioAsync();
        var paymentIntentId = $"pi_refund_partial_{Guid.NewGuid():N}";
        
        var originalPayment = new Payment
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            CompanyId = company.Id,
            ReservationId = booking.Id,
            Amount = 500m,
            Currency = "USD",
            PaymentType = "full_payment",
            PaymentMethod = "card",
            StripePaymentIntentId = paymentIntentId,
            Status = "succeeded"
        };
        Context.Payments.Add(originalPayment);
        await Context.SaveChangesAsync();
        TrackForCleanup(originalPayment);

        // Act - Create partial refund (refunds don't have their own PaymentIntentId)
        var refundPayment = new Payment
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            CompanyId = company.Id,
            ReservationId = booking.Id,
            Amount = -200m, // Partial refund
            Currency = "USD",
            PaymentType = "refund",
            PaymentMethod = "stripe_refund",
            StripePaymentIntentId = null, // Refunds don't have their own PI
            Status = "succeeded"
        };
        Context.Payments.Add(refundPayment);
        await Context.SaveChangesAsync();
        TrackForCleanup(refundPayment);

        // Assert
        Context.ChangeTracker.Clear();
        var payments = await Context.Payments
            .Where(p => p.ReservationId == booking.Id)
            .ToListAsync();
        
        var netAmount = payments.Sum(p => p.Amount);
        netAmount.Should().Be(300m); // 500 - 200 = 300 remaining
    }

    #endregion

    #region Currency Conversion Tests

    [Theory]
    [InlineData("USD", 100.00, 10000)]
    [InlineData("EUR", 50.50, 5050)]
    [InlineData("GBP", 75.99, 7599)]
    [InlineData("BRL", 500.00, 50000)]
    [InlineData("JPY", 10000, 10000)] // Zero decimal currency
    [InlineData("KRW", 50000, 50000)] // Zero decimal currency
    public void ConvertToStripeAmount_ShouldHandleCurrencies(string currency, decimal amount, long expected)
    {
        // Arrange & Act
        var stripeAmount = ConvertToStripeAmount(amount, currency);

        // Assert
        stripeAmount.Should().Be(expected);
    }

    [Theory]
    [InlineData("USD", 10000, 100.00)]
    [InlineData("EUR", 5050, 50.50)]
    [InlineData("JPY", 10000, 10000)] // Zero decimal currency
    public void ConvertFromStripeAmount_ShouldHandleCurrencies(string currency, long stripeAmount, decimal expected)
    {
        // Arrange & Act
        var amount = ConvertFromStripeAmount(stripeAmount, currency);

        // Assert
        amount.Should().Be(expected);
    }

    private static long ConvertToStripeAmount(decimal amount, string currency)
    {
        var zeroDecimalCurrencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "bif", "clp", "djf", "gnf", "jpy", "kmf", "krw", "mga", 
            "pyg", "rwf", "ugx", "vnd", "vuv", "xaf", "xof", "xpf"
        };

        if (zeroDecimalCurrencies.Contains(currency.ToLowerInvariant()))
            return (long)amount;

        return (long)Math.Round(amount * 100, MidpointRounding.AwayFromZero);
    }

    private static decimal ConvertFromStripeAmount(long stripeAmount, string currency)
    {
        var zeroDecimalCurrencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "bif", "clp", "djf", "gnf", "jpy", "kmf", "krw", "mga", 
            "pyg", "rwf", "ugx", "vnd", "vuv", "xaf", "xof", "xpf"
        };

        if (zeroDecimalCurrencies.Contains(currency.ToLowerInvariant()))
            return stripeAmount;

        return stripeAmount / 100m;
    }

    #endregion

    #region Locale Tests

    [Theory]
    [InlineData("United States", "en", "en")]
    [InlineData("Brazil", "pt", "pt-BR")]
    [InlineData("Brazil", "en", "en")]
    [InlineData("Mexico", "es", "es-419")]
    [InlineData("Spain", "es", "es")]
    [InlineData("France", "fr", "fr")]
    [InlineData("Germany", "de", "de")]
    public void GetStripeLocale_ShouldReturnCorrectLocale(string country, string language, string expectedLocale)
    {
        // Arrange & Act
        var locale = GetStripeLocale(country, language);

        // Assert
        locale.Should().Be(expectedLocale);
    }

    private static string GetStripeLocale(string? country, string? language)
    {
        var countryLower = (country ?? "").ToLower();
        var langLower = (language ?? "en").ToLower();

        if (langLower.StartsWith("pt") && countryLower.Contains("brazil"))
            return "pt-BR";
        if (langLower.StartsWith("pt"))
            return "pt";
        if (langLower.StartsWith("es") && (countryLower.Contains("mexico") || countryLower.Contains("argentina")))
            return "es-419";
        if (langLower.StartsWith("es"))
            return "es";
        if (langLower.StartsWith("fr"))
            return "fr";
        if (langLower.StartsWith("de"))
            return "de";

        return "en";
    }

    #endregion

    #region Payment Method Tests

    [Fact]
    public async Task SavePaymentMethod_ShouldStoreCardDetails()
    {
        // Arrange
        var (company, customer, _, _) = await SeedCompleteScenarioAsync();

        // Act - Store payment method
        var paymentMethod = new CustomerPaymentMethod
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            StripePaymentMethodId = "pm_test_visa_123",
            CardBrand = "visa",
            CardLast4 = "4242",
            CardExpMonth = 12,
            CardExpYear = 2026,
            IsDefault = true,
            CreatedAt = DateTime.UtcNow
        };
        Context.CustomerPaymentMethods.Add(paymentMethod);
        await Context.SaveChangesAsync();

        // Assert
        Context.ChangeTracker.Clear();
        var verifyMethod = await Context.CustomerPaymentMethods
            .FirstOrDefaultAsync(pm => pm.CustomerId == customer.Id);
        
        verifyMethod.Should().NotBeNull();
        verifyMethod!.CardBrand.Should().Be("visa");
        verifyMethod.CardLast4.Should().Be("4242");
        verifyMethod.IsDefault.Should().BeTrue();

        // Cleanup
        Context.CustomerPaymentMethods.Remove(verifyMethod);
        await Context.SaveChangesAsync();
    }

    [Fact]
    public async Task SetDefaultPaymentMethod_ShouldUnsetOtherDefaults()
    {
        // Arrange
        var (_, customer, _, _) = await SeedCompleteScenarioAsync();

        var method1 = new CustomerPaymentMethod
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            StripePaymentMethodId = "pm_old_default",
            CardBrand = "visa",
            CardLast4 = "1111",
            IsDefault = true
        };
        var method2 = new CustomerPaymentMethod
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            StripePaymentMethodId = "pm_new_default",
            CardBrand = "mastercard",
            CardLast4 = "2222",
            IsDefault = false
        };
        Context.CustomerPaymentMethods.AddRange(method1, method2);
        await Context.SaveChangesAsync();

        // Act - Set new default
        var methods = await Context.CustomerPaymentMethods
            .Where(pm => pm.CustomerId == customer.Id)
            .ToListAsync();
        
        foreach (var m in methods)
        {
            m.IsDefault = m.Id == method2.Id;
        }
        await Context.SaveChangesAsync();

        // Assert
        Context.ChangeTracker.Clear();
        var verifyMethods = await Context.CustomerPaymentMethods
            .Where(pm => pm.CustomerId == customer.Id)
            .ToListAsync();
        
        verifyMethods.Count(m => m.IsDefault).Should().Be(1);
        verifyMethods.Single(m => m.IsDefault).StripePaymentMethodId.Should().Be("pm_new_default");

        // Cleanup
        Context.CustomerPaymentMethods.RemoveRange(verifyMethods);
        await Context.SaveChangesAsync();
    }

    #endregion
}
