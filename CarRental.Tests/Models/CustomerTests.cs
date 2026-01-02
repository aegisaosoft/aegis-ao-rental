/*
 * CarRental.Tests - Unit Tests for Aegis AO Rental System
 * Copyright (c) 2025 Alexander Orlov
 */

using Xunit;
using FluentAssertions;
using CarRental.Api.Models;

namespace CarRental.Tests.Models;

/// <summary>
/// Unit tests for the Customer model
/// </summary>
public class CustomerTests
{
    [Fact]
    public void Customer_ShouldInitializeWithDefaultValues()
    {
        // Arrange & Act
        var customer = new Customer();

        // Assert
        customer.Id.Should().NotBeEmpty();
        customer.Role.Should().Be("customer");
        customer.IsActive.Should().BeTrue();
        customer.IsVerified.Should().BeFalse();
        customer.CustomerType.Should().Be(CustomerType.Individual);
        customer.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Customer_ShouldStoreBasicInfo()
    {
        // Arrange & Act
        var customer = new Customer
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john.doe@example.com",
            Phone = "+1-555-123-4567"
        };

        // Assert
        customer.FirstName.Should().Be("John");
        customer.LastName.Should().Be("Doe");
        customer.Email.Should().Be("john.doe@example.com");
        customer.Phone.Should().Contain("555");
    }

    [Theory]
    [InlineData("customer")]
    [InlineData("worker")]
    [InlineData("admin")]
    [InlineData("mainadmin")]
    [InlineData("designer")]
    public void Customer_ShouldAcceptValidRoles(string role)
    {
        // Arrange & Act
        var customer = new Customer { Role = role };

        // Assert
        customer.Role.Should().Be(role);
    }

    [Fact]
    public void Customer_ShouldStoreAddressInfo()
    {
        // Arrange & Act
        var customer = new Customer
        {
            Address = "123 Main Street",
            City = "New York",
            State = "NY",
            Country = "USA",
            PostalCode = "10001"
        };

        // Assert
        customer.Address.Should().Be("123 Main Street");
        customer.City.Should().Be("New York");
        customer.State.Should().Be("NY");
        customer.Country.Should().Be("USA");
        customer.PostalCode.Should().Be("10001");
    }

    [Fact]
    public void Customer_ShouldStoreStripeCustomerId()
    {
        // Arrange & Act
        var customer = new Customer
        {
            StripeCustomerId = "cus_ABC123XYZ"
        };

        // Assert
        customer.StripeCustomerId.Should().StartWith("cus_");
    }

    [Theory]
    [InlineData(CustomerType.Individual)]
    [InlineData(CustomerType.Corporate)]
    public void Customer_ShouldAcceptCustomerTypes(CustomerType customerType)
    {
        // Arrange & Act
        var customer = new Customer { CustomerType = customerType };

        // Assert
        customer.CustomerType.Should().Be(customerType);
    }

    [Fact]
    public void Customer_LastLogin_ShouldBeNullableAndTrackable()
    {
        // Arrange
        var customer = new Customer();

        // Assert - initially null
        customer.LastLogin.Should().BeNull();

        // Act - simulate login
        customer.LastLogin = DateTime.UtcNow;

        // Assert - now has value
        customer.LastLogin.Should().NotBeNull();
        customer.LastLogin.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Customer_SecurityDepositMandatory_ShouldBeNullable()
    {
        // Arrange
        var customer1 = new Customer();
        var customer2 = new Customer { IsSecurityDepositMandatory = true };
        var customer3 = new Customer { IsSecurityDepositMandatory = false };

        // Assert
        customer1.IsSecurityDepositMandatory.Should().BeNull();
        customer2.IsSecurityDepositMandatory.Should().BeTrue();
        customer3.IsSecurityDepositMandatory.Should().BeFalse();
    }

    [Fact]
    public void Customer_NavigationProperties_ShouldBeInitialized()
    {
        // Arrange & Act
        var customer = new Customer();

        // Assert
        customer.Bookings.Should().NotBeNull();
        customer.Bookings.Should().BeEmpty();
        customer.Rentals.Should().NotBeNull();
        customer.Payments.Should().NotBeNull();
        customer.PaymentMethods.Should().NotBeNull();
        customer.Reviews.Should().NotBeNull();
    }

    [Fact]
    public void Customer_CompanyAssociation_ShouldWorkForWorkers()
    {
        // Arrange & Act
        var companyId = Guid.NewGuid();
        var worker = new Customer
        {
            FirstName = "Jane",
            LastName = "Smith",
            Email = "jane@company.com",
            Role = "worker",
            CompanyId = companyId
        };

        // Assert
        worker.Role.Should().Be("worker");
        worker.CompanyId.Should().Be(companyId);
    }

    [Fact]
    public void Customer_DateOfBirth_ShouldBeNullable()
    {
        // Arrange
        var customer = new Customer
        {
            DateOfBirth = new DateTime(1990, 5, 15)
        };

        // Assert
        customer.DateOfBirth.Should().NotBeNull();
        customer.DateOfBirth!.Value.Year.Should().Be(1990);
    }

    [Fact]
    public void Customer_Token_ShouldStoreResetToken()
    {
        // Arrange & Act
        var customer = new Customer
        {
            Token = "abc123xyz789resettoken"
        };

        // Assert
        customer.Token.Should().NotBeNullOrEmpty();
        customer.Token.Should().HaveLength(22);
    }

    [Fact]
    public void Customer_IsActive_CanBeDeactivated()
    {
        // Arrange
        var customer = new Customer();
        customer.IsActive.Should().BeTrue(); // Default

        // Act
        customer.IsActive = false;

        // Assert
        customer.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Customer_IsVerified_CanBeChanged()
    {
        // Arrange
        var customer = new Customer();
        customer.IsVerified.Should().BeFalse(); // Default

        // Act
        customer.IsVerified = true;

        // Assert
        customer.IsVerified.Should().BeTrue();
    }
}
