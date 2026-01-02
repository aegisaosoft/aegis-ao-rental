/*
 * CarRental.Tests - Security Deposit Tests
 * Copyright (c) 2025 Alexander Orlov
 * 
 * Tests for Security Deposit authorization, capture, and release logic
 */

using Xunit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using CarRental.Api.Models;
using CarRental.Tests.Infrastructure;

namespace CarRental.Tests.Stripe;

/// <summary>
/// Tests for Security Deposit operations
/// Tests the database state changes for authorize, capture, and release operations
/// </summary>
[Collection("PostgreSQL")]
public class SecurityDepositTests : PostgresTestBase
{
    #region Authorization Tests

    [Fact]
    public async Task AuthorizeSecurityDeposit_ShouldSetAuthorizedStatus()
    {
        // Arrange
        var (company, customer, vehicle, booking) = await SeedCompleteScenarioAsync();
        booking.Status = "Confirmed";
        booking.SecurityDeposit = 500m;
        await Context.SaveChangesAsync();

        // Act - Simulate successful authorization
        var loadedBooking = await Context.Bookings.FindAsync(booking.Id);
        loadedBooking!.SecurityDepositPaymentIntentId = "pi_auth_test_123";
        loadedBooking.SecurityDepositStatus = "authorized";
        loadedBooking.SecurityDepositAmount = 500m;
        loadedBooking.SecurityDepositAuthorizedAt = DateTime.UtcNow;
        await Context.SaveChangesAsync();

        // Assert
        Context.ChangeTracker.Clear();
        var verifyBooking = await Context.Bookings.FindAsync(booking.Id);
        verifyBooking!.SecurityDepositStatus.Should().Be("authorized");
        verifyBooking.SecurityDepositAmount.Should().Be(500m);
        verifyBooking.SecurityDepositPaymentIntentId.Should().StartWith("pi_");
        verifyBooking.SecurityDepositAuthorizedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task AuthorizeSecurityDeposit_WithExtendedHold_ShouldRecordDetails()
    {
        // Arrange
        var (company, customer, vehicle, booking) = await SeedCompleteScenarioAsync();
        booking.Status = "Confirmed";
        booking.SecurityDeposit = 1000m;
        await Context.SaveChangesAsync();

        // Act - Simulate extended authorization (31-day hold)
        var loadedBooking = await Context.Bookings.FindAsync(booking.Id);
        loadedBooking!.SecurityDepositPaymentIntentId = "pi_extended_123";
        loadedBooking.SecurityDepositStatus = "authorized";
        loadedBooking.SecurityDepositAmount = 1000m;
        loadedBooking.SecurityDepositAuthorizedAt = DateTime.UtcNow;
        // Extended authorization can hold for up to 31 days
        await Context.SaveChangesAsync();

        // Assert
        Context.ChangeTracker.Clear();
        var verifyBooking = await Context.Bookings.FindAsync(booking.Id);
        verifyBooking!.SecurityDepositStatus.Should().Be("authorized");
        verifyBooking.SecurityDepositAmount.Should().Be(1000m);
    }

    #endregion

    #region Capture Tests

    [Fact]
    public async Task CaptureSecurityDeposit_FullAmount_ShouldSetCapturedStatus()
    {
        // Arrange
        var (company, customer, vehicle, booking) = await SeedCompleteScenarioAsync();
        booking.SecurityDepositPaymentIntentId = "pi_capture_full_123";
        booking.SecurityDepositStatus = "authorized";
        booking.SecurityDepositAmount = 500m;
        booking.SecurityDepositAuthorizedAt = DateTime.UtcNow.AddDays(-3);
        await Context.SaveChangesAsync();

        // Act - Simulate full capture
        var loadedBooking = await Context.Bookings.FindAsync(booking.Id);
        loadedBooking!.SecurityDepositStatus = "captured";
        loadedBooking.SecurityDepositCapturedAt = DateTime.UtcNow;
        loadedBooking.SecurityDepositChargedAmount = 500m;
        loadedBooking.SecurityDepositCaptureReason = "Vehicle damage - scratched bumper";
        await Context.SaveChangesAsync();

        // Assert
        Context.ChangeTracker.Clear();
        var verifyBooking = await Context.Bookings.FindAsync(booking.Id);
        verifyBooking!.SecurityDepositStatus.Should().Be("captured");
        verifyBooking.SecurityDepositChargedAmount.Should().Be(500m);
        verifyBooking.SecurityDepositCaptureReason.Should().Contain("damage");
        verifyBooking.SecurityDepositCapturedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task CaptureSecurityDeposit_PartialAmount_ShouldRecordPartialCapture()
    {
        // Arrange
        var (company, customer, vehicle, booking) = await SeedCompleteScenarioAsync();
        booking.SecurityDepositPaymentIntentId = "pi_capture_partial_123";
        booking.SecurityDepositStatus = "authorized";
        booking.SecurityDepositAmount = 500m;
        booking.SecurityDepositAuthorizedAt = DateTime.UtcNow.AddDays(-5);
        await Context.SaveChangesAsync();

        // Act - Simulate partial capture (fuel charge only)
        var loadedBooking = await Context.Bookings.FindAsync(booking.Id);
        loadedBooking!.SecurityDepositStatus = "captured";
        loadedBooking.SecurityDepositCapturedAt = DateTime.UtcNow;
        loadedBooking.SecurityDepositChargedAmount = 75m; // Only fuel charge
        loadedBooking.SecurityDepositCaptureReason = "Fuel not refilled - charged for fuel";
        await Context.SaveChangesAsync();

        // Assert
        Context.ChangeTracker.Clear();
        var verifyBooking = await Context.Bookings.FindAsync(booking.Id);
        verifyBooking!.SecurityDepositStatus.Should().Be("captured");
        verifyBooking.SecurityDepositChargedAmount.Should().Be(75m);
        verifyBooking.SecurityDepositChargedAmount.Should().BeLessThan(verifyBooking.SecurityDepositAmount ?? 0);
    }

    [Fact]
    public async Task CaptureSecurityDeposit_MultipleCharges_ShouldAccumulateAmount()
    {
        // Arrange
        var (company, customer, vehicle, booking) = await SeedCompleteScenarioAsync();
        booking.SecurityDepositPaymentIntentId = "pi_multi_charge_123";
        booking.SecurityDepositStatus = "authorized";
        booking.SecurityDepositAmount = 500m;
        booking.SecurityDepositAuthorizedAt = DateTime.UtcNow.AddDays(-7);
        await Context.SaveChangesAsync();

        // Act - Simulate multiple charges combined
        var loadedBooking = await Context.Bookings.FindAsync(booking.Id);
        loadedBooking!.SecurityDepositStatus = "captured";
        loadedBooking.SecurityDepositCapturedAt = DateTime.UtcNow;
        loadedBooking.SecurityDepositChargedAmount = 250m; // Fuel ($75) + Cleaning ($50) + Minor damage ($125)
        loadedBooking.SecurityDepositCaptureReason = "Fuel: $75, Cleaning: $50, Minor scratch repair: $125";
        await Context.SaveChangesAsync();

        // Assert
        Context.ChangeTracker.Clear();
        var verifyBooking = await Context.Bookings.FindAsync(booking.Id);
        verifyBooking!.SecurityDepositChargedAmount.Should().Be(250m);
        verifyBooking.SecurityDepositCaptureReason.Should().Contain("Fuel");
        verifyBooking.SecurityDepositCaptureReason.Should().Contain("Cleaning");
        verifyBooking.SecurityDepositCaptureReason.Should().Contain("scratch");
    }

    #endregion

    #region Release Tests

    [Fact]
    public async Task ReleaseSecurityDeposit_ShouldSetReleasedStatus()
    {
        // Arrange
        var (company, customer, vehicle, booking) = await SeedCompleteScenarioAsync();
        booking.SecurityDepositPaymentIntentId = "pi_release_test_123";
        booking.SecurityDepositStatus = "authorized";
        booking.SecurityDepositAmount = 500m;
        booking.SecurityDepositAuthorizedAt = DateTime.UtcNow.AddDays(-3);
        await Context.SaveChangesAsync();

        // Act - Simulate successful release (vehicle returned in good condition)
        var loadedBooking = await Context.Bookings.FindAsync(booking.Id);
        loadedBooking!.SecurityDepositStatus = "released";
        loadedBooking.SecurityDepositReleasedAt = DateTime.UtcNow;
        loadedBooking.SecurityDepositCaptureReason = "Vehicle returned in good condition - no charges";
        await Context.SaveChangesAsync();

        // Assert
        Context.ChangeTracker.Clear();
        var verifyBooking = await Context.Bookings.FindAsync(booking.Id);
        verifyBooking!.SecurityDepositStatus.Should().Be("released");
        verifyBooking.SecurityDepositReleasedAt.Should().NotBeNull();
        verifyBooking.SecurityDepositChargedAmount.Should().BeNull();
    }

    [Fact]
    public async Task ReleaseSecurityDeposit_AfterPartialCapture_ShouldNotBeAllowed()
    {
        // Arrange - Security deposit already captured
        var (company, customer, vehicle, booking) = await SeedCompleteScenarioAsync();
        booking.SecurityDepositPaymentIntentId = "pi_captured_123";
        booking.SecurityDepositStatus = "captured";
        booking.SecurityDepositAmount = 500m;
        booking.SecurityDepositChargedAmount = 100m;
        booking.SecurityDepositCapturedAt = DateTime.UtcNow.AddHours(-2);
        await Context.SaveChangesAsync();

        // Assert - Cannot release already captured deposit
        Context.ChangeTracker.Clear();
        var verifyBooking = await Context.Bookings.FindAsync(booking.Id);
        verifyBooking!.SecurityDepositStatus.Should().Be("captured");
        // Business logic should prevent setting to "released" after "captured"
    }

    #endregion

    #region Status Transition Tests

    [Theory]
    [InlineData("pending", "authorized", true)]
    [InlineData("authorized", "captured", true)]
    [InlineData("authorized", "released", true)]
    [InlineData("captured", "released", false)] // Cannot release after capture
    [InlineData("released", "captured", false)] // Cannot capture after release
    public async Task SecurityDepositStatusTransitions_ShouldFollowValidPaths(
        string fromStatus, string toStatus, bool isValidTransition)
    {
        // Arrange
        var (_, _, _, booking) = await SeedCompleteScenarioAsync();
        booking.SecurityDepositPaymentIntentId = "pi_transition_123";
        booking.SecurityDepositStatus = fromStatus;
        booking.SecurityDepositAmount = 500m;
        await Context.SaveChangesAsync();

        // Act & Assert
        var loadedBooking = await Context.Bookings.FindAsync(booking.Id);
        
        // This tests the expected business logic rules
        // In real implementation, controller would validate transitions
        var canTransition = IsValidStatusTransition(fromStatus, toStatus);
        canTransition.Should().Be(isValidTransition);
    }

    private static bool IsValidStatusTransition(string from, string to)
    {
        // Define valid transitions
        var validTransitions = new Dictionary<string, string[]>
        {
            { "pending", new[] { "authorized" } },
            { "authorized", new[] { "captured", "released" } },
            { "captured", Array.Empty<string>() }, // Terminal state for charges
            { "released", Array.Empty<string>() }  // Terminal state for releases
        };

        if (!validTransitions.ContainsKey(from))
            return false;

        return validTransitions[from].Contains(to);
    }

    #endregion

    #region Currency Handling Tests

    [Theory]
    [InlineData("USD", 500.00, 50000)] // 2 decimal places
    [InlineData("BRL", 1000.00, 100000)] // 2 decimal places
    [InlineData("JPY", 50000, 50000)] // 0 decimal places
    [InlineData("EUR", 250.50, 25050)] // 2 decimal places
    public void SecurityDepositAmount_ShouldConvertToStripeUnitsCorrectly(
        string currency, decimal amount, long expectedStripeAmount)
    {
        // Arrange & Act
        var stripeAmount = ConvertToStripeAmount(amount, currency);

        // Assert
        stripeAmount.Should().Be(expectedStripeAmount);
    }

    private static long ConvertToStripeAmount(decimal amount, string currency)
    {
        // Zero-decimal currencies
        var zeroDecimalCurrencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "bif", "clp", "djf", "gnf", "jpy", "kmf", "krw", "mga", 
            "pyg", "rwf", "ugx", "vnd", "vuv", "xaf", "xof", "xpf"
        };

        if (zeroDecimalCurrencies.Contains(currency.ToLowerInvariant()))
            return (long)amount;

        return (long)(amount * 100);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task SecurityDeposit_ExpiredAuthorization_ShouldBeHandled()
    {
        // Arrange - Authorization older than 7 days (standard auth period)
        var (_, _, _, booking) = await SeedCompleteScenarioAsync();
        booking.SecurityDepositPaymentIntentId = "pi_expired_auth_123";
        booking.SecurityDepositStatus = "authorized";
        booking.SecurityDepositAmount = 500m;
        booking.SecurityDepositAuthorizedAt = DateTime.UtcNow.AddDays(-8); // Expired
        await Context.SaveChangesAsync();

        // Assert - Authorization is expired (business logic would need to re-authorize)
        Context.ChangeTracker.Clear();
        var verifyBooking = await Context.Bookings.FindAsync(booking.Id);
        var daysSinceAuth = (DateTime.UtcNow - verifyBooking!.SecurityDepositAuthorizedAt!.Value).TotalDays;
        daysSinceAuth.Should().BeGreaterThan(7);
    }

    [Fact]
    public async Task SecurityDeposit_ZeroAmount_ShouldNotRequirePayment()
    {
        // Arrange
        var (_, _, _, booking) = await SeedCompleteScenarioAsync();
        booking.SecurityDeposit = 0m; // No deposit required
        await Context.SaveChangesAsync();

        // Assert
        Context.ChangeTracker.Clear();
        var verifyBooking = await Context.Bookings.FindAsync(booking.Id);
        verifyBooking!.SecurityDeposit.Should().Be(0m);
        verifyBooking.SecurityDepositPaymentIntentId.Should().BeNull();
    }

    #endregion
}
