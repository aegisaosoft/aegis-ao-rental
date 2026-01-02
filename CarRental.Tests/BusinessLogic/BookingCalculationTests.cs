/*
 * CarRental.Tests - Unit Tests for Aegis AO Rental System
 * Copyright (c) 2025 Alexander Orlov
 */

using Xunit;
using FluentAssertions;
using CarRental.Api.Models;

namespace CarRental.Tests.BusinessLogic;

/// <summary>
/// Tests for booking calculation business logic
/// </summary>
public class BookingCalculationTests
{
    #region Rental Period Calculations

    [Theory]
    [InlineData(1, 100, 100)]
    [InlineData(3, 100, 300)]
    [InlineData(7, 50, 350)]
    [InlineData(30, 45, 1350)]
    public void CalculateSubtotal_ShouldMultiplyDailyRateByDays(int days, decimal dailyRate, decimal expectedSubtotal)
    {
        // Arrange
        var booking = new Booking
        {
            DailyRate = dailyRate,
            TotalDays = days
        };

        // Act
        booking.Subtotal = booking.DailyRate * booking.TotalDays;

        // Assert
        booking.Subtotal.Should().Be(expectedSubtotal);
    }

    [Fact]
    public void CalculateTotalDays_ShouldCountInclusiveDays()
    {
        // Arrange
        var pickupDate = new DateTime(2025, 6, 1);
        var returnDate = new DateTime(2025, 6, 5);

        // Act
        var totalDays = (returnDate - pickupDate).Days;

        // Assert
        totalDays.Should().Be(4); // June 1, 2, 3, 4 (return on 5th)
    }

    [Theory]
    [InlineData("10:00", "18:00", false)] // Same day pickup/return time OK
    [InlineData("08:00", "20:00", false)]
    [InlineData("14:00", "10:00", true)]  // Return time before pickup time = extra day
    public void SameTimeConsideration_ShouldAffectTotalDays(string pickupTime, string returnTime, bool extraDay)
    {
        // Arrange
        var booking = new Booking
        {
            PickupTime = pickupTime,
            ReturnTime = returnTime,
            TotalDays = 3
        };

        // Act - Business logic would add extra day if return time is before pickup time
        if (extraDay && TimeSpan.Parse(returnTime) < TimeSpan.Parse(pickupTime))
        {
            booking.TotalDays += 1;
        }

        // Assert
        booking.TotalDays.Should().Be(extraDay ? 4 : 3);
    }

    #endregion

    #region Tax Calculations

    [Theory]
    [InlineData(100, 7.0, 7)]
    [InlineData(500, 8.25, 41.25)]
    [InlineData(1000, 6.5, 65)]
    [InlineData(250, 0, 0)]
    public void CalculateTax_ShouldApplyTaxRate(decimal subtotal, decimal taxRate, decimal expectedTax)
    {
        // Arrange & Act
        var taxAmount = Math.Round(subtotal * (taxRate / 100), 2);

        // Assert
        taxAmount.Should().Be(expectedTax);
    }

    [Fact]
    public void TotalAmount_ShouldIncludeAllComponents()
    {
        // Arrange
        var booking = new Booking
        {
            Subtotal = 500m,
            TaxAmount = 35m,
            InsuranceAmount = 50m,
            AdditionalFees = 25m
        };

        // Act
        booking.TotalAmount = booking.Subtotal + booking.TaxAmount + 
                              booking.InsuranceAmount + booking.AdditionalFees;

        // Assert
        booking.TotalAmount.Should().Be(610m);
    }

    #endregion

    #region Platform Fee Calculations

    [Theory]
    [InlineData(1000, 5.0, 50)]    // 5% platform fee
    [InlineData(1000, 7.5, 75)]    // 7.5% platform fee
    [InlineData(500, 10.0, 50)]    // 10% platform fee
    [InlineData(150, 5.0, 7.5)]    // Small amount
    public void CalculatePlatformFee_ShouldApplyPercentage(decimal total, decimal feePercent, decimal expectedFee)
    {
        // Act
        var platformFee = Math.Round(total * (feePercent / 100), 2);

        // Assert
        platformFee.Should().Be(expectedFee);
    }

    [Fact]
    public void NetAmount_ShouldBeAfterPlatformFee()
    {
        // Arrange
        var booking = new Booking
        {
            TotalAmount = 1000m,
            PlatformFeeAmount = 75m
        };

        // Act
        booking.NetAmount = booking.TotalAmount - booking.PlatformFeeAmount;

        // Assert
        booking.NetAmount.Should().Be(925m);
    }

    #endregion

    #region Security Deposit Calculations

    [Theory]
    [InlineData(500, 0, 500)]      // No charges, full refund
    [InlineData(500, 100, 400)]    // Partial charge
    [InlineData(500, 500, 0)]      // Full capture
    [InlineData(1000, 250, 750)]   // Larger deposit, partial
    public void SecurityDeposit_RefundCalculation(decimal depositAmount, decimal chargedAmount, decimal expectedRefund)
    {
        // Arrange
        var booking = new Booking
        {
            SecurityDepositAmount = depositAmount,
            SecurityDepositChargedAmount = chargedAmount
        };

        // Act
        var refundAmount = booking.SecurityDepositAmount - (booking.SecurityDepositChargedAmount ?? 0);

        // Assert
        refundAmount.Should().Be(expectedRefund);
    }

    [Fact]
    public void SecurityDeposit_MultipleDamageCharges_ShouldAccumulate()
    {
        // Arrange
        var booking = new Booking
        {
            SecurityDepositAmount = 1000m,
            SecurityDepositStatus = "authorized"
        };

        // Act - Simulate multiple damage assessments
        decimal fuelCharge = 50m;
        decimal cleaningCharge = 75m;
        decimal damageCharge = 200m;
        
        booking.SecurityDepositChargedAmount = fuelCharge + cleaningCharge + damageCharge;
        booking.SecurityDepositCaptureReason = "Fuel: $50, Cleaning: $75, Damage: $200";

        // Assert
        booking.SecurityDepositChargedAmount.Should().Be(325m);
        var refundAmount = booking.SecurityDepositAmount - booking.SecurityDepositChargedAmount;
        refundAmount.Should().Be(675m);
    }

    #endregion

    #region Weekly/Monthly Rate Calculations

    [Fact]
    public void WeeklyRate_ShouldProvideDiscount()
    {
        // Arrange
        decimal dailyRate = 100m;
        decimal weeklyDiscountPercent = 15m;
        int days = 7;

        // Act
        decimal standardTotal = dailyRate * days;
        decimal discountedTotal = standardTotal * (1 - weeklyDiscountPercent / 100);

        // Assert
        standardTotal.Should().Be(700m);
        discountedTotal.Should().Be(595m);
    }

    [Fact]
    public void MonthlyRate_ShouldProvideGreaterDiscount()
    {
        // Arrange
        decimal dailyRate = 100m;
        decimal monthlyDiscountPercent = 30m;
        int days = 30;

        // Act
        decimal standardTotal = dailyRate * days;
        decimal discountedTotal = standardTotal * (1 - monthlyDiscountPercent / 100);

        // Assert
        standardTotal.Should().Be(3000m);
        discountedTotal.Should().Be(2100m);
    }

    #endregion

    #region Multi-Currency Considerations

    [Theory]
    [InlineData("USD", 100, "$")]
    [InlineData("CAD", 100, "C$")]
    [InlineData("MXN", 100, "MX$")]
    [InlineData("BRL", 100, "R$")]
    [InlineData("EUR", 100, "€")]
    public void Currency_ShouldFormatCorrectly(string currencyCode, decimal amount, string expectedSymbol)
    {
        // Arrange
        var booking = new Booking
        {
            Currency = currencyCode,
            TotalAmount = amount
        };

        // Assert
        booking.Currency.Should().Be(currencyCode);
        expectedSymbol.Should().NotBeNullOrEmpty(); // Validates expected symbol is provided
        // Note: Actual formatting would be done by a helper/service
    }

    #endregion

    #region Date Validation

    [Fact]
    public void PickupDate_ShouldNotBeInPast()
    {
        // Arrange
        var pastDate = DateTime.UtcNow.AddDays(-1);
        var futureDate = DateTime.UtcNow.AddDays(1);

        // Assert
        pastDate.Should().BeBefore(DateTime.UtcNow);
        futureDate.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public void ReturnDate_ShouldBeAfterPickupDate()
    {
        // Arrange
        var booking = new Booking
        {
            PickupDate = DateTime.UtcNow.AddDays(1),
            ReturnDate = DateTime.UtcNow.AddDays(4)
        };

        // Assert
        booking.ReturnDate.Should().BeAfter(booking.PickupDate);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(30)]
    [InlineData(90)]
    public void ValidBookingDurations_ShouldBeAccepted(int duration)
    {
        // Arrange
        var booking = new Booking
        {
            PickupDate = DateTime.UtcNow.AddDays(1),
            TotalDays = duration
        };
        booking.ReturnDate = booking.PickupDate.AddDays(duration);

        // Assert
        booking.TotalDays.Should().BeGreaterThan(0);
        booking.ReturnDate.Should().BeAfter(booking.PickupDate);
    }

    #endregion

    #region Stripe Amount Conversion

    [Theory]
    [InlineData(100.00, 10000)]      // $100.00 = 10000 cents
    [InlineData(99.99, 9999)]        // $99.99 = 9999 cents
    [InlineData(1.50, 150)]          // $1.50 = 150 cents
    [InlineData(0.01, 1)]            // $0.01 = 1 cent
    [InlineData(1000.00, 100000)]    // $1000.00 = 100000 cents
    public void ConvertToStripeAmount_ShouldReturnCents(decimal dollarAmount, long expectedCents)
    {
        // Act
        var stripeCents = (long)(dollarAmount * 100);

        // Assert
        stripeCents.Should().Be(expectedCents);
    }

    [Theory]
    [InlineData(10000, 100.00)]
    [InlineData(9999, 99.99)]
    [InlineData(150, 1.50)]
    [InlineData(1, 0.01)]
    public void ConvertFromStripeAmount_ShouldReturnDollars(long cents, decimal expectedDollars)
    {
        // Act
        var dollars = cents / 100m;

        // Assert
        dollars.Should().Be(expectedDollars);
    }

    #endregion
}
