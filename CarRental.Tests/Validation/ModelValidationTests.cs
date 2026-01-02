/*
 * CarRental.Tests - Unit Tests for Aegis AO Rental System
 * Copyright (c) 2025 Alexander Orlov
 */

using Xunit;
using FluentAssertions;
using System.ComponentModel.DataAnnotations;
using CarRental.Api.Models;

namespace CarRental.Tests.Validation;

/// <summary>
/// Tests for model validation
/// </summary>
public class ModelValidationTests
{
    private static IList<ValidationResult> ValidateModel(object model)
    {
        var validationResults = new List<ValidationResult>();
        var context = new ValidationContext(model, null, null);
        Validator.TryValidateObject(model, context, validationResults, true);
        return validationResults;
    }

    #region Customer Validation

    [Fact]
    public void Customer_ValidModel_ShouldPassValidation()
    {
        // Arrange
        var customer = new Customer
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john.doe@example.com"
        };

        // Act
        var results = ValidateModel(customer);

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public void Customer_MissingFirstName_ShouldFailValidation()
    {
        // Arrange
        var customer = new Customer
        {
            FirstName = "",
            LastName = "Doe",
            Email = "john@example.com"
        };

        // Act
        var results = ValidateModel(customer);

        // Assert
        results.Should().Contain(r => r.MemberNames.Contains("FirstName"));
    }

    [Fact]
    public void Customer_MissingLastName_ShouldFailValidation()
    {
        // Arrange
        var customer = new Customer
        {
            FirstName = "John",
            LastName = "",
            Email = "john@example.com"
        };

        // Act
        var results = ValidateModel(customer);

        // Assert
        results.Should().Contain(r => r.MemberNames.Contains("LastName"));
    }

    [Theory]
    [InlineData("invalid-email")]
    [InlineData("@nodomain.com")]
    [InlineData("spaces in@email.com")]
    public void Customer_InvalidEmail_ShouldFailValidation(string invalidEmail)
    {
        // Arrange
        var customer = new Customer
        {
            FirstName = "John",
            LastName = "Doe",
            Email = invalidEmail
        };

        // Act
        var results = ValidateModel(customer);

        // Assert
        results.Should().Contain(r => r.MemberNames.Contains("Email"));
    }

    [Theory]
    [InlineData("test@example.com")]
    [InlineData("user.name@domain.org")]
    [InlineData("user+tag@gmail.com")]
    public void Customer_ValidEmail_ShouldPassValidation(string validEmail)
    {
        // Arrange
        var customer = new Customer
        {
            FirstName = "John",
            LastName = "Doe",
            Email = validEmail
        };

        // Act
        var results = ValidateModel(customer);

        // Assert
        results.Should().NotContain(r => r.MemberNames.Contains("Email"));
    }

    #endregion

    #region Vehicle Validation

    [Fact]
    public void Vehicle_ValidModel_ShouldPassValidation()
    {
        // Arrange
        var vehicle = new Vehicle
        {
            CompanyId = Guid.NewGuid(),
            LicensePlate = "ABC-1234"
        };

        // Act
        var results = ValidateModel(vehicle);

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public void Vehicle_MissingLicensePlate_ShouldFailValidation()
    {
        // Arrange
        var vehicle = new Vehicle
        {
            CompanyId = Guid.NewGuid(),
            LicensePlate = ""
        };

        // Act
        var results = ValidateModel(vehicle);

        // Assert
        results.Should().Contain(r => r.MemberNames.Contains("LicensePlate"));
    }

    [Theory]
    [InlineData("NY")]
    [InlineData("CA")]
    [InlineData("TX")]
    public void Vehicle_ValidState_ShouldHaveTwoCharacters(string state)
    {
        // Assert
        state.Should().HaveLength(2);
    }

    [Theory]
    [InlineData("1HGBH41JXMN109186")]  // Valid 17-char VIN
    public void Vehicle_ValidVIN_ShouldHave17Characters(string vin)
    {
        // Arrange
        var vehicle = new Vehicle
        {
            CompanyId = Guid.NewGuid(),
            LicensePlate = "TEST-001",
            Vin = vin
        };

        // Assert
        vehicle.Vin.Should().HaveLength(17);
    }

    #endregion

    #region Booking Validation

    [Fact]
    public void Booking_ValidModel_ShouldPassValidation()
    {
        // Arrange
        var booking = new Booking
        {
            CustomerId = Guid.NewGuid(),
            VehicleId = Guid.NewGuid(),
            CompanyId = Guid.NewGuid(),
            BookingNumber = "BK-2025-00001",
            PickupDate = DateTime.UtcNow.AddDays(1),
            ReturnDate = DateTime.UtcNow.AddDays(4),
            DailyRate = 100m,
            TotalDays = 3,
            TotalAmount = 300m
        };

        // Act
        var results = ValidateModel(booking);

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public void Booking_MissingBookingNumber_ShouldFailValidation()
    {
        // Arrange
        var booking = new Booking
        {
            CustomerId = Guid.NewGuid(),
            VehicleId = Guid.NewGuid(),
            CompanyId = Guid.NewGuid(),
            BookingNumber = "",
            PickupDate = DateTime.UtcNow.AddDays(1),
            ReturnDate = DateTime.UtcNow.AddDays(4),
            DailyRate = 100m,
            TotalDays = 3,
            TotalAmount = 300m
        };

        // Act
        var results = ValidateModel(booking);

        // Assert
        results.Should().Contain(r => r.MemberNames.Contains("BookingNumber"));
    }

    [Theory]
    [InlineData("USD")]
    [InlineData("CAD")]
    [InlineData("MXN")]
    public void Booking_ValidCurrency_ShouldHaveThreeCharacters(string currency)
    {
        // Arrange
        var booking = new Booking
        {
            Currency = currency
        };

        // Assert
        booking.Currency.Should().HaveLength(3);
    }

    [Theory]
    [InlineData("10:00")]
    [InlineData("08:30")]
    [InlineData("22:00")]
    [InlineData("00:00")]
    [InlineData("23:59")]
    public void Booking_ValidPickupTime_ShouldBeHHMMFormat(string time)
    {
        // Arrange
        var booking = new Booking
        {
            PickupTime = time
        };

        // Assert
        booking.PickupTime.Should().MatchRegex(@"^\d{2}:\d{2}$");
    }

    #endregion

    #region Status Validation

    [Theory]
    [InlineData("Pending")]
    [InlineData("Confirmed")]
    [InlineData("Active")]
    [InlineData("Completed")]
    [InlineData("Cancelled")]
    public void BookingStatus_ValidStatuses_ShouldBeRecognized(string status)
    {
        // Arrange
        var validStatuses = new[] { "Pending", "Confirmed", "Active", "Completed", "Cancelled" };

        // Assert
        validStatuses.Should().Contain(status);
    }

    [Theory]
    [InlineData("pending")]
    [InlineData("paid")]
    [InlineData("failed")]
    [InlineData("refunded")]
    public void PaymentStatus_ValidStatuses_ShouldBeRecognized(string status)
    {
        // Arrange
        var validStatuses = new[] { "pending", "paid", "failed", "refunded" };

        // Assert
        validStatuses.Should().Contain(status);
    }

    [Theory]
    [InlineData("pending")]
    [InlineData("authorized")]
    [InlineData("captured")]
    [InlineData("released")]
    [InlineData("refunded")]
    public void SecurityDepositStatus_ValidStatuses_ShouldBeRecognized(string status)
    {
        // Arrange
        var validStatuses = new[] { "pending", "authorized", "captured", "released", "refunded" };

        // Assert
        validStatuses.Should().Contain(status);
    }

    #endregion

    #region String Length Validation

    [Fact]
    public void BookingNumber_ShouldNotExceed50Characters()
    {
        // Arrange
        var booking = new Booking
        {
            BookingNumber = new string('A', 50)
        };

        // Assert
        booking.BookingNumber.Length.Should().BeLessOrEqualTo(50);
    }

    [Fact]
    public void CustomerEmail_ShouldNotExceed255Characters()
    {
        // Arrange
        var longEmail = new string('a', 240) + "@example.com";
        
        // Assert
        longEmail.Length.Should().BeLessOrEqualTo(255);
    }

    [Fact]
    public void VehicleLicensePlate_ShouldNotExceed50Characters()
    {
        // Arrange
        var vehicle = new Vehicle
        {
            LicensePlate = "ABC-123-XYZ-789"
        };

        // Assert
        vehicle.LicensePlate.Length.Should().BeLessOrEqualTo(50);
    }

    #endregion

    #region Decimal Precision Validation

    [Theory]
    [InlineData(99.99)]
    [InlineData(100.00)]
    [InlineData(1234.56)]
    [InlineData(0.01)]
    public void DailyRate_ShouldAcceptValidDecimals(decimal rate)
    {
        // Arrange
        var booking = new Booking { DailyRate = rate };

        // Assert
        booking.DailyRate.Should().BeGreaterThan(0);
    }

    [Fact]
    public void TotalAmount_ShouldMaintainPrecision()
    {
        // Arrange
        var booking = new Booking
        {
            Subtotal = 299.99m,
            TaxAmount = 21.00m,
            InsuranceAmount = 15.50m
        };

        // Act
        booking.TotalAmount = booking.Subtotal + booking.TaxAmount + booking.InsuranceAmount;

        // Assert
        booking.TotalAmount.Should().Be(336.49m);
    }

    #endregion
}
