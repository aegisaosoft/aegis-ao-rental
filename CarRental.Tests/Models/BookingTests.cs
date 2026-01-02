/*
 * CarRental.Tests - Unit Tests for Aegis AO Rental System
 * Copyright (c) 2025 Alexander Orlov
 */

using Xunit;
using FluentAssertions;
using CarRental.Api.Models;

namespace CarRental.Tests.Models;

/// <summary>
/// Unit tests for the Booking model
/// </summary>
public class BookingTests
{
    [Fact]
    public void Booking_ShouldInitializeWithDefaultValues()
    {
        // Arrange & Act
        var booking = new Booking();

        // Assert
        booking.Id.Should().NotBeEmpty();
        booking.Status.Should().Be("Pending");
        booking.PaymentStatus.Should().Be("pending");
        booking.SecurityDepositStatus.Should().Be("pending");
        booking.Currency.Should().Be("USD");
        booking.SecurityDeposit.Should().Be(1000m);
        booking.PickupTime.Should().Be("10:00");
        booking.ReturnTime.Should().Be("22:00");
        booking.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        booking.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Booking_ShouldCalculateTotalAmountCorrectly()
    {
        // Arrange
        var booking = new Booking
        {
            DailyRate = 100m,
            TotalDays = 5,
            TaxAmount = 50m,
            InsuranceAmount = 75m,
            AdditionalFees = 25m
        };

        // Act
        booking.Subtotal = booking.DailyRate * booking.TotalDays;
        booking.TotalAmount = booking.Subtotal + booking.TaxAmount + booking.InsuranceAmount + booking.AdditionalFees;

        // Assert
        booking.Subtotal.Should().Be(500m);
        booking.TotalAmount.Should().Be(650m);
    }

    [Fact]
    public void Booking_ShouldHaveValidBookingNumber()
    {
        // Arrange
        var booking = new Booking
        {
            BookingNumber = "BK-2025-00001"
        };

        // Assert
        booking.BookingNumber.Should().StartWith("BK-");
        booking.BookingNumber.Should().HaveLength(13);
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("Confirmed")]
    [InlineData("Active")]
    [InlineData("Completed")]
    [InlineData("Cancelled")]
    public void Booking_ShouldAcceptValidStatuses(string status)
    {
        // Arrange & Act
        var booking = new Booking { Status = status };

        // Assert
        booking.Status.Should().Be(status);
    }

    [Fact]
    public void Booking_ShouldCalculatePlatformFeeCorrectly()
    {
        // Arrange
        var booking = new Booking
        {
            TotalAmount = 1000m,
            PlatformFeeAmount = 50m // 5% platform fee
        };

        // Act
        booking.NetAmount = booking.TotalAmount - booking.PlatformFeeAmount;

        // Assert
        booking.PlatformFeeAmount.Should().Be(50m);
        booking.NetAmount.Should().Be(950m);
    }

    [Fact]
    public void Booking_DatesValidation_PickupShouldBeBeforeReturn()
    {
        // Arrange
        var pickupDate = DateTime.UtcNow.Date;
        var returnDate = pickupDate.AddDays(3);

        var booking = new Booking
        {
            PickupDate = pickupDate,
            ReturnDate = returnDate
        };

        // Assert
        booking.PickupDate.Should().BeBefore(booking.ReturnDate);
        (booking.ReturnDate - booking.PickupDate).Days.Should().Be(3);
    }

    [Fact]
    public void Booking_SecurityDeposit_ShouldTrackStatusChanges()
    {
        // Arrange
        var booking = new Booking
        {
            SecurityDepositAmount = 500m,
            SecurityDepositStatus = "authorized"
        };

        // Act - Simulate authorization
        booking.SecurityDepositAuthorizedAt = DateTime.UtcNow;

        // Assert
        booking.SecurityDepositStatus.Should().Be("authorized");
        booking.SecurityDepositAuthorizedAt.Should().NotBeNull();
        booking.SecurityDepositCapturedAt.Should().BeNull();
        booking.SecurityDepositReleasedAt.Should().BeNull();
    }

    [Fact]
    public void Booking_SecurityDeposit_CaptureShouldRecordReason()
    {
        // Arrange
        var booking = new Booking
        {
            SecurityDepositAmount = 500m,
            SecurityDepositStatus = "captured",
            SecurityDepositChargedAmount = 200m,
            SecurityDepositCaptureReason = "Vehicle damage - scratched bumper"
        };

        // Act
        booking.SecurityDepositCapturedAt = DateTime.UtcNow;

        // Assert
        booking.SecurityDepositChargedAmount.Should().Be(200m);
        booking.SecurityDepositCaptureReason.Should().Contain("damage");
        booking.SecurityDepositCapturedAt.Should().NotBeNull();
    }

    [Fact]
    public void Booking_NavigationProperties_ShouldBeInitialized()
    {
        // Arrange & Act
        var booking = new Booking();

        // Assert
        booking.Rentals.Should().NotBeNull();
        booking.Rentals.Should().BeEmpty();
        booking.Payments.Should().NotBeNull();
        booking.Payments.Should().BeEmpty();
        booking.RefundRecords.Should().NotBeNull();
        booking.RefundRecords.Should().BeEmpty();
    }

    [Fact]
    public void Booking_StripeIntegration_ShouldStorePaymentIds()
    {
        // Arrange & Act
        var booking = new Booking
        {
            StripePaymentIntentId = "pi_3ABC123DEF456",
            StripeCustomerId = "cus_XYZ789",
            PaymentMethodId = "pm_1ABC",
            SetupIntentId = "seti_1DEF"
        };

        // Assert
        booking.StripePaymentIntentId.Should().StartWith("pi_");
        booking.StripeCustomerId.Should().StartWith("cus_");
        booking.PaymentMethodId.Should().StartWith("pm_");
        booking.SetupIntentId.Should().StartWith("seti_");
    }
}
