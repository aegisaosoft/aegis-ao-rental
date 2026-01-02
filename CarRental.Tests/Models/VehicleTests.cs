/*
 * CarRental.Tests - Unit Tests for Aegis AO Rental System
 * Copyright (c) 2025 Alexander Orlov
 */

using Xunit;
using FluentAssertions;
using CarRental.Api.Models;

namespace CarRental.Tests.Models;

/// <summary>
/// Unit tests for the Vehicle model
/// </summary>
public class VehicleTests
{
    [Fact]
    public void Vehicle_ShouldInitializeWithDefaultValues()
    {
        // Arrange & Act
        var vehicle = new Vehicle();

        // Assert
        vehicle.Id.Should().NotBeEmpty();
        vehicle.Status.Should().Be(VehicleStatus.Available);
        vehicle.Mileage.Should().Be(0);
        vehicle.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        vehicle.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Vehicle_ShouldStoreBasicInfo()
    {
        // Arrange & Act
        var vehicle = new Vehicle
        {
            LicensePlate = "ABC-1234",
            Vin = "1HGBH41JXMN109186",
            Color = "Red",
            Transmission = "Automatic",
            Seats = 5
        };

        // Assert
        vehicle.LicensePlate.Should().Be("ABC-1234");
        vehicle.Vin.Should().HaveLength(17);
        vehicle.Color.Should().Be("Red");
        vehicle.Transmission.Should().Be("Automatic");
        vehicle.Seats.Should().Be(5);
    }

    [Theory]
    [InlineData(VehicleStatus.Available)]
    [InlineData(VehicleStatus.Rented)]
    [InlineData(VehicleStatus.Maintenance)]
    [InlineData(VehicleStatus.OutOfService)]
    [InlineData(VehicleStatus.Cleaning)]
    public void Vehicle_ShouldAcceptValidStatuses(VehicleStatus status)
    {
        // Arrange & Act
        var vehicle = new Vehicle { Status = status };

        // Assert
        vehicle.Status.Should().Be(status);
    }

    [Fact]
    public void Vehicle_VIN_ShouldBe17Characters()
    {
        // Arrange
        var validVin = "1HGBH41JXMN109186";
        var vehicle = new Vehicle { Vin = validVin };

        // Assert
        vehicle.Vin.Should().HaveLength(17);
    }

    [Theory]
    [InlineData("NY")]
    [InlineData("CA")]
    [InlineData("TX")]
    [InlineData("FL")]
    public void Vehicle_State_ShouldBeTwoCharacters(string state)
    {
        // Arrange & Act
        var vehicle = new Vehicle { State = state };

        // Assert
        vehicle.State.Should().HaveLength(2);
    }

    [Fact]
    public void Vehicle_Features_ShouldBeArray()
    {
        // Arrange & Act
        var vehicle = new Vehicle
        {
            Features = new[] { "GPS", "Bluetooth", "Backup Camera", "Heated Seats" }
        };

        // Assert
        vehicle.Features.Should().HaveCount(4);
        vehicle.Features.Should().Contain("GPS");
        vehicle.Features.Should().Contain("Bluetooth");
    }

    [Fact]
    public void Vehicle_Mileage_ShouldBeTrackable()
    {
        // Arrange
        var vehicle = new Vehicle { Mileage = 50000 };

        // Act
        vehicle.Mileage += 1500; // Add trip miles

        // Assert
        vehicle.Mileage.Should().Be(51500);
    }

    [Fact]
    public void Vehicle_ImageUrl_ShouldStoreUrl()
    {
        // Arrange & Act
        var vehicle = new Vehicle
        {
            ImageUrl = "https://storage.example.com/vehicles/car123.jpg"
        };

        // Assert
        vehicle.ImageUrl.Should().StartWith("https://");
        vehicle.ImageUrl.Should().EndWith(".jpg");
    }

    [Fact]
    public void Vehicle_NavigationProperties_ShouldBeInitialized()
    {
        // Arrange & Act
        var vehicle = new Vehicle();

        // Assert
        vehicle.Bookings.Should().NotBeNull();
        vehicle.Bookings.Should().BeEmpty();
        vehicle.Rentals.Should().NotBeNull();
        vehicle.Reviews.Should().NotBeNull();
    }

    [Fact]
    public void Vehicle_CompanyAssociation_ShouldBeRequired()
    {
        // Arrange & Act
        var companyId = Guid.NewGuid();
        var vehicle = new Vehicle
        {
            CompanyId = companyId,
            LicensePlate = "TEST-001"
        };

        // Assert
        vehicle.CompanyId.Should().NotBeEmpty();
        vehicle.CompanyId.Should().Be(companyId);
    }

    [Fact]
    public void Vehicle_LocationId_ShouldBeNullable()
    {
        // Arrange
        var vehicle1 = new Vehicle();
        var vehicle2 = new Vehicle { LocationId = Guid.NewGuid() };

        // Assert
        vehicle1.LocationId.Should().BeNull();
        vehicle2.LocationId.Should().NotBeNull();
    }

    [Fact]
    public void Vehicle_CurrentLocationId_ShouldTrackCurrentPosition()
    {
        // Arrange
        var pickupLocation = Guid.NewGuid();
        var returnLocation = Guid.NewGuid();

        var vehicle = new Vehicle
        {
            LocationId = pickupLocation,
            CurrentLocationId = pickupLocation
        };

        // Act - simulate vehicle moved to different location
        vehicle.CurrentLocationId = returnLocation;

        // Assert
        vehicle.LocationId.Should().Be(pickupLocation); // Home location
        vehicle.CurrentLocationId.Should().Be(returnLocation); // Current location
    }

    [Fact]
    public void Vehicle_VehicleModelId_ShouldLinkToCatalog()
    {
        // Arrange
        var modelId = Guid.NewGuid();
        var vehicle = new Vehicle
        {
            VehicleModelId = modelId
        };

        // Assert
        vehicle.VehicleModelId.Should().Be(modelId);
    }

    [Theory]
    [InlineData("Automatic")]
    [InlineData("Manual")]
    [InlineData("CVT")]
    public void Vehicle_Transmission_ShouldAcceptValidTypes(string transmission)
    {
        // Arrange & Act
        var vehicle = new Vehicle { Transmission = transmission };

        // Assert
        vehicle.Transmission.Should().Be(transmission);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(7)]
    [InlineData(8)]
    public void Vehicle_Seats_ShouldAcceptValidCounts(int seats)
    {
        // Arrange & Act
        var vehicle = new Vehicle { Seats = seats };

        // Assert
        vehicle.Seats.Should().Be(seats);
    }
}
