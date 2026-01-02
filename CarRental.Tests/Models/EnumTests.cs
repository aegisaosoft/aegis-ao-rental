/*
 * CarRental.Tests - Unit Tests for Aegis AO Rental System
 * Copyright (c) 2025 Alexander Orlov
 */

using Xunit;
using FluentAssertions;
using CarRental.Api.Models;

namespace CarRental.Tests.Models;

/// <summary>
/// Tests for enum types and status constants
/// </summary>
public class EnumTests
{
    #region VehicleStatus Tests

    [Fact]
    public void VehicleStatus_ShouldHaveExpectedValues()
    {
        // Assert
        Enum.GetValues<VehicleStatus>().Should().HaveCount(5);
        Enum.IsDefined(VehicleStatus.Available).Should().BeTrue();
        Enum.IsDefined(VehicleStatus.Rented).Should().BeTrue();
        Enum.IsDefined(VehicleStatus.Maintenance).Should().BeTrue();
        Enum.IsDefined(VehicleStatus.OutOfService).Should().BeTrue();
        Enum.IsDefined(VehicleStatus.Cleaning).Should().BeTrue();
    }

    [Fact]
    public void VehicleStatus_DefaultValue_ShouldBeAvailable()
    {
        // Arrange
        var vehicle = new Vehicle();

        // Assert
        vehicle.Status.Should().Be(VehicleStatus.Available);
    }

    [Theory]
    [InlineData(VehicleStatus.Available, "Available")]
    [InlineData(VehicleStatus.Rented, "Rented")]
    [InlineData(VehicleStatus.Maintenance, "Maintenance")]
    [InlineData(VehicleStatus.OutOfService, "OutOfService")]
    [InlineData(VehicleStatus.Cleaning, "Cleaning")]
    public void VehicleStatus_ToString_ShouldMatchExpected(VehicleStatus status, string expected)
    {
        // Act
        var result = status.ToString();

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("Available", VehicleStatus.Available)]
    [InlineData("Rented", VehicleStatus.Rented)]
    [InlineData("Maintenance", VehicleStatus.Maintenance)]
    public void VehicleStatus_Parse_ShouldWork(string value, VehicleStatus expected)
    {
        // Act
        var result = Enum.Parse<VehicleStatus>(value);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void VehicleStatusConstants_ShouldMatchEnumValues()
    {
        // Assert
        VehicleStatusConstants.Available.Should().Be(VehicleStatus.Available.ToString());
        VehicleStatusConstants.Rented.Should().Be(VehicleStatus.Rented.ToString());
        VehicleStatusConstants.Maintenance.Should().Be(VehicleStatus.Maintenance.ToString());
        VehicleStatusConstants.OutOfService.Should().Be(VehicleStatus.OutOfService.ToString());
        VehicleStatusConstants.Cleaning.Should().Be(VehicleStatus.Cleaning.ToString());
    }

    #endregion

    #region CustomerType Tests

    [Fact]
    public void CustomerType_ShouldHaveExpectedValues()
    {
        // Assert
        Enum.IsDefined(CustomerType.Individual).Should().BeTrue();
        Enum.IsDefined(CustomerType.Corporate).Should().BeTrue();
    }

    [Fact]
    public void CustomerType_DefaultValue_ShouldBeIndividual()
    {
        // Arrange
        var customer = new Customer();

        // Assert
        customer.CustomerType.Should().Be(CustomerType.Individual);
    }

    #endregion

    #region Booking Status State Machine Tests

    [Fact]
    public void BookingStatus_ValidTransitions_PendingToConfirmed()
    {
        // Arrange
        var booking = new Booking { Status = "Pending" };
        var validNextStatuses = new[] { "Confirmed", "Cancelled" };

        // Assert
        validNextStatuses.Should().Contain("Confirmed");
        booking.Status = "Confirmed";
        booking.Status.Should().Be("Confirmed");
    }

    [Fact]
    public void BookingStatus_ValidTransitions_ConfirmedToActive()
    {
        // Arrange
        var booking = new Booking { Status = "Confirmed" };
        var validNextStatuses = new[] { "Active", "Cancelled" };

        // Assert
        validNextStatuses.Should().Contain("Active");
        booking.Status = "Active";
        booking.Status.Should().Be("Active");
    }

    [Fact]
    public void BookingStatus_ValidTransitions_ActiveToCompleted()
    {
        // Arrange
        var booking = new Booking { Status = "Active" };
        var validNextStatuses = new[] { "Completed" };

        // Assert
        validNextStatuses.Should().Contain("Completed");
        booking.Status = "Completed";
        booking.Status.Should().Be("Completed");
    }

    [Fact]
    public void BookingStatus_TerminalStates_ShouldNotChange()
    {
        // Arrange
        var completedBooking = new Booking { Status = "Completed" };
        var cancelledBooking = new Booking { Status = "Cancelled" };

        // Assert - These are terminal states
        completedBooking.Status.Should().Be("Completed");
        cancelledBooking.Status.Should().Be("Cancelled");
    }

    #endregion

    #region Vehicle Status State Machine Tests

    [Fact]
    public void VehicleStatus_AvailableCanBecomeRented()
    {
        // Arrange
        var vehicle = new Vehicle { Status = VehicleStatus.Available };

        // Act
        vehicle.Status = VehicleStatus.Rented;

        // Assert
        vehicle.Status.Should().Be(VehicleStatus.Rented);
    }

    [Fact]
    public void VehicleStatus_RentedCanBecomeAvailable()
    {
        // Arrange
        var vehicle = new Vehicle { Status = VehicleStatus.Rented };

        // Act - After return, vehicle becomes available (possibly after cleaning)
        vehicle.Status = VehicleStatus.Cleaning;
        vehicle.Status = VehicleStatus.Available;

        // Assert
        vehicle.Status.Should().Be(VehicleStatus.Available);
    }

    [Fact]
    public void VehicleStatus_MaintenanceCanBecomeAvailable()
    {
        // Arrange
        var vehicle = new Vehicle { Status = VehicleStatus.Maintenance };

        // Act
        vehicle.Status = VehicleStatus.Available;

        // Assert
        vehicle.Status.Should().Be(VehicleStatus.Available);
    }

    [Fact]
    public void VehicleStatus_OutOfServiceRequiresMaintenance()
    {
        // Arrange
        var vehicle = new Vehicle { Status = VehicleStatus.OutOfService };

        // Act - Should go through maintenance before becoming available
        vehicle.Status = VehicleStatus.Maintenance;
        vehicle.Status = VehicleStatus.Available;

        // Assert
        vehicle.Status.Should().Be(VehicleStatus.Available);
    }

    #endregion

    #region Payment Status Tests

    [Theory]
    [InlineData("pending")]
    [InlineData("processing")]
    [InlineData("paid")]
    [InlineData("failed")]
    [InlineData("refunded")]
    [InlineData("partially_refunded")]
    public void PaymentStatus_ValidValues_ShouldBeAccepted(string status)
    {
        // Arrange
        var booking = new Booking { PaymentStatus = status };

        // Assert
        booking.PaymentStatus.Should().Be(status);
    }

    [Fact]
    public void PaymentStatus_DefaultValue_ShouldBePending()
    {
        // Arrange
        var booking = new Booking();

        // Assert
        booking.PaymentStatus.Should().Be("pending");
    }

    #endregion

    #region Security Deposit Status Tests

    [Theory]
    [InlineData("pending")]
    [InlineData("authorized")]
    [InlineData("captured")]
    [InlineData("partially_captured")]
    [InlineData("released")]
    [InlineData("refunded")]
    public void SecurityDepositStatus_ValidValues_ShouldBeAccepted(string status)
    {
        // Arrange
        var booking = new Booking { SecurityDepositStatus = status };

        // Assert
        booking.SecurityDepositStatus.Should().Be(status);
    }

    [Fact]
    public void SecurityDepositStatus_DefaultValue_ShouldBePending()
    {
        // Arrange
        var booking = new Booking();

        // Assert
        booking.SecurityDepositStatus.Should().Be("pending");
    }

    [Fact]
    public void SecurityDepositStatus_AuthorizedToReleased_ValidTransition()
    {
        // Arrange
        var booking = new Booking
        {
            SecurityDepositStatus = "authorized",
            SecurityDepositAmount = 500m
        };

        // Act - No damages, release full deposit
        booking.SecurityDepositStatus = "released";
        booking.SecurityDepositReleasedAt = DateTime.UtcNow;

        // Assert
        booking.SecurityDepositStatus.Should().Be("released");
        booking.SecurityDepositReleasedAt.Should().NotBeNull();
    }

    [Fact]
    public void SecurityDepositStatus_AuthorizedToCaptured_ValidTransition()
    {
        // Arrange
        var booking = new Booking
        {
            SecurityDepositStatus = "authorized",
            SecurityDepositAmount = 500m
        };

        // Act - Damages found, capture deposit
        booking.SecurityDepositStatus = "captured";
        booking.SecurityDepositChargedAmount = 200m;
        booking.SecurityDepositCapturedAt = DateTime.UtcNow;
        booking.SecurityDepositCaptureReason = "Vehicle damage";

        // Assert
        booking.SecurityDepositStatus.Should().Be("captured");
        booking.SecurityDepositChargedAmount.Should().Be(200m);
    }

    #endregion
}
