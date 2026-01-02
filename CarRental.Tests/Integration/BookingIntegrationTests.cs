/*
 * CarRental.Tests - Integration Tests with PostgreSQL
 * Copyright (c) 2025 Alexander Orlov
 */

using Xunit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using CarRental.Api.Models;
using CarRental.Tests.Infrastructure;

namespace CarRental.Tests.Integration;

/// <summary>
/// Integration tests for Booking operations using real PostgreSQL database
/// Requires Docker to be running
/// </summary>
[Collection("PostgreSQL")]
public class BookingIntegrationTests : PostgresTestBase
{
    [Fact]
    public async Task CreateBooking_ShouldPersistToDatabase()
    {
        // Arrange
        var company = await SeedCompanyAsync();
        var customer = await SeedCustomerAsync(company.Id);
        var vehicle = await SeedVehicleAsync(company.Id);
        var bookingNumber = $"TEST-{Guid.NewGuid():N}".Substring(0, 20);

        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            VehicleId = vehicle.Id,
            CompanyId = company.Id,
            BookingNumber = bookingNumber,
            PickupDate = DateTime.UtcNow.AddDays(1),
            ReturnDate = DateTime.UtcNow.AddDays(4),
            DailyRate = 150m,
            TotalDays = 3,
            Subtotal = 450m,
            TotalAmount = 480m,
            Status = "Pending"
        };

        // Act
        Context.Bookings.Add(booking);
        await Context.SaveChangesAsync();
        
        // Track for cleanup
        TrackForCleanup(booking);

        // Assert
        var savedBooking = await Context.Bookings
            .FirstOrDefaultAsync(b => b.BookingNumber == bookingNumber);

        savedBooking.Should().NotBeNull();
        savedBooking!.DailyRate.Should().Be(150m);
        savedBooking.TotalDays.Should().Be(3);
        savedBooking.TotalAmount.Should().Be(480m);
    }

    [Fact]
    public async Task GetBooking_WithNavigationProperties_ShouldLoadRelatedData()
    {
        // Arrange
        var (company, customer, vehicle, booking) = await SeedCompleteScenarioAsync();

        // Act
        var loadedBooking = await Context.Bookings
            .Include(b => b.Customer)
            .Include(b => b.Vehicle)
            .Include(b => b.Company)
            .FirstOrDefaultAsync(b => b.Id == booking.Id);

        // Assert
        loadedBooking.Should().NotBeNull();
        loadedBooking!.Customer.Should().NotBeNull();
        loadedBooking.Customer.FirstName.Should().Contain("TestJohn"); // Contains test marker
        loadedBooking.Vehicle.Should().NotBeNull();
        loadedBooking.Vehicle.LicensePlate.Should().StartWith("T"); // Starts with T
        loadedBooking.Company.Should().NotBeNull();
        loadedBooking.Company.CompanyName.Should().Contain("Test Rental Company");
    }

    [Fact]
    public async Task UpdateBookingStatus_ShouldPersistChanges()
    {
        // Arrange
        var (_, _, _, booking) = await SeedCompleteScenarioAsync();

        // Act
        booking.Status = "Confirmed";
        booking.PaymentStatus = "paid";
        booking.UpdatedAt = DateTime.UtcNow;
        await Context.SaveChangesAsync();

        // Clear the context to force reload
        Context.ChangeTracker.Clear();

        // Assert
        var updatedBooking = await Context.Bookings.FindAsync(booking.Id);
        updatedBooking.Should().NotBeNull();
        updatedBooking!.Status.Should().Be("Confirmed");
        updatedBooking.PaymentStatus.Should().Be("paid");
    }

    [Fact]
    public async Task GetBookingsByCustomer_ShouldReturnAllCustomerBookings()
    {
        // Arrange
        var company = await SeedCompanyAsync();
        var customer = await SeedCustomerAsync(company.Id);
        var vehicle1 = await SeedVehicleAsync(company.Id, "CAR-001");
        var vehicle2 = await SeedVehicleAsync(company.Id, "CAR-002");

        await SeedBookingAsync(customer.Id, vehicle1.Id, company.Id, 1, 2);
        await SeedBookingAsync(customer.Id, vehicle2.Id, company.Id, 5, 3);
        await SeedBookingAsync(customer.Id, vehicle1.Id, company.Id, 10, 1);

        // Act
        var customerBookings = await Context.Bookings
            .Where(b => b.CustomerId == customer.Id)
            .ToListAsync();

        // Assert
        customerBookings.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetBookingsByCompany_ShouldOnlyReturnCompanyBookings()
    {
        // Arrange
        var company1 = await SeedCompanyAsync("Company One", "company1");
        var company2 = await SeedCompanyAsync("Company Two", "company2");

        var customer1 = await SeedCustomerAsync(company1.Id, "Alice", "Smith");
        var customer2 = await SeedCustomerAsync(company2.Id, "Bob", "Jones");

        var vehicle1 = await SeedVehicleAsync(company1.Id, "C1-001");
        var vehicle2 = await SeedVehicleAsync(company2.Id, "C2-001");

        await SeedBookingAsync(customer1.Id, vehicle1.Id, company1.Id);
        await SeedBookingAsync(customer1.Id, vehicle1.Id, company1.Id, 5, 2);
        await SeedBookingAsync(customer2.Id, vehicle2.Id, company2.Id);

        // Act
        var company1Bookings = await Context.Bookings
            .Where(b => b.CompanyId == company1.Id)
            .ToListAsync();

        var company2Bookings = await Context.Bookings
            .Where(b => b.CompanyId == company2.Id)
            .ToListAsync();

        // Assert
        company1Bookings.Should().HaveCount(2);
        company2Bookings.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetActiveBookings_ShouldFilterByStatus()
    {
        // Arrange
        var (company, customer, vehicle, _) = await SeedCompleteScenarioAsync();

        // Create bookings with different statuses
        var pendingBooking = await SeedBookingAsync(customer.Id, vehicle.Id, company.Id, 1, 2);
        var confirmedBooking = await SeedBookingAsync(customer.Id, vehicle.Id, company.Id, 5, 2);
        var cancelledBooking = await SeedBookingAsync(customer.Id, vehicle.Id, company.Id, 10, 2);

        confirmedBooking.Status = "Confirmed";
        cancelledBooking.Status = "Cancelled";
        await Context.SaveChangesAsync();

        // Act
        var activeBookings = await Context.Bookings
            .Where(b => b.CompanyId == company.Id && b.Status != "Cancelled")
            .ToListAsync();

        var cancelledBookings = await Context.Bookings
            .Where(b => b.CompanyId == company.Id && b.Status == "Cancelled")
            .ToListAsync();

        // Assert
        activeBookings.Should().HaveCount(3); // Including the one from SeedCompleteScenario
        cancelledBookings.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetUpcomingBookings_ShouldFilterByDate()
    {
        // Arrange
        var company = await SeedCompanyAsync();
        var customer = await SeedCustomerAsync(company.Id);
        var vehicle = await SeedVehicleAsync(company.Id);

        // Past booking
        var pastBooking = new Booking
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            VehicleId = vehicle.Id,
            CompanyId = company.Id,
            BookingNumber = $"TEST-{Guid.NewGuid():N}".Substring(0, 20),
            PickupDate = DateTime.UtcNow.AddDays(-10),
            ReturnDate = DateTime.UtcNow.AddDays(-7),
            DailyRate = 100m,
            TotalDays = 3,
            Subtotal = 300m,
            TotalAmount = 300m,
            Status = "Completed"
        };
        Context.Bookings.Add(pastBooking);
        await Context.SaveChangesAsync();
        
        // Track for cleanup
        TrackForCleanup(pastBooking);

        // Future bookings
        await SeedBookingAsync(customer.Id, vehicle.Id, company.Id, 5, 2);
        await SeedBookingAsync(customer.Id, vehicle.Id, company.Id, 10, 3);

        // Act
        var upcomingBookings = await Context.Bookings
            .Where(b => b.CompanyId == company.Id && b.PickupDate > DateTime.UtcNow)
            .OrderBy(b => b.PickupDate)
            .ToListAsync();

        // Assert
        upcomingBookings.Should().HaveCount(2);
        upcomingBookings.First().PickupDate.Should().BeBefore(upcomingBookings.Last().PickupDate);
    }

    [Fact]
    public async Task DeleteBooking_ShouldRemoveFromDatabase()
    {
        // Arrange
        var (_, _, _, booking) = await SeedCompleteScenarioAsync();
        var bookingId = booking.Id;

        // Act
        Context.Bookings.Remove(booking);
        await Context.SaveChangesAsync();

        // Assert
        var deletedBooking = await Context.Bookings.FindAsync(bookingId);
        deletedBooking.Should().BeNull();
    }

    [Fact]
    public async Task CalculateTotalRevenue_ForCompany_ShouldSumCompletedBookings()
    {
        // Arrange
        var company = await SeedCompanyAsync();
        var customer = await SeedCustomerAsync(company.Id);
        var vehicle = await SeedVehicleAsync(company.Id);

        var booking1 = await SeedBookingAsync(customer.Id, vehicle.Id, company.Id, -10, 2);
        booking1.Status = "Completed";
        booking1.TotalAmount = 200m;

        var booking2 = await SeedBookingAsync(customer.Id, vehicle.Id, company.Id, -5, 3);
        booking2.Status = "Completed";
        booking2.TotalAmount = 300m;

        var booking3 = await SeedBookingAsync(customer.Id, vehicle.Id, company.Id, 5, 2);
        booking3.Status = "Pending"; // Not completed
        booking3.TotalAmount = 200m;

        await Context.SaveChangesAsync();

        // Act
        var totalRevenue = await Context.Bookings
            .Where(b => b.CompanyId == company.Id && b.Status == "Completed")
            .SumAsync(b => b.TotalAmount);

        // Assert
        totalRevenue.Should().Be(500m);
    }

    [Fact]
    public async Task SecurityDepositTracking_ShouldUpdateCorrectly()
    {
        // Arrange
        var (_, _, _, booking) = await SeedCompleteScenarioAsync();

        // Act - Authorize deposit
        booking.SecurityDepositStatus = "authorized";
        booking.SecurityDepositAmount = 500m;
        booking.SecurityDepositAuthorizedAt = DateTime.UtcNow;
        booking.SecurityDepositPaymentIntentId = "pi_test_123";
        await Context.SaveChangesAsync();

        // Assert
        var updatedBooking = await Context.Bookings.FindAsync(booking.Id);
        updatedBooking!.SecurityDepositStatus.Should().Be("authorized");
        updatedBooking.SecurityDepositAuthorizedAt.Should().NotBeNull();

        // Act - Capture partial deposit
        updatedBooking.SecurityDepositStatus = "captured";
        updatedBooking.SecurityDepositChargedAmount = 150m;
        updatedBooking.SecurityDepositCapturedAt = DateTime.UtcNow;
        updatedBooking.SecurityDepositCaptureReason = "Fuel charge";
        await Context.SaveChangesAsync();

        // Assert
        Context.ChangeTracker.Clear();
        var capturedBooking = await Context.Bookings.FindAsync(booking.Id);
        capturedBooking!.SecurityDepositStatus.Should().Be("captured");
        capturedBooking.SecurityDepositChargedAmount.Should().Be(150m);
        capturedBooking.SecurityDepositCaptureReason.Should().Be("Fuel charge");
    }

    [Fact]
    public async Task JsonbFields_ShouldWorkCorrectly()
    {
        // Arrange
        var company = await SeedCompanyAsync();
        
        // Act - Update company with JSON data
        company.About = "{\"en\": \"About us in English\", \"es\": \"Sobre nosotros\"}";
        company.TermsOfUse = "{\"version\": \"1.0\", \"content\": \"Terms content here\"}";
        await Context.SaveChangesAsync();

        Context.ChangeTracker.Clear();

        // Assert
        var loadedCompany = await Context.Companies.FindAsync(company.Id);
        loadedCompany!.About.Should().Contain("About us in English");
        loadedCompany.TermsOfUse.Should().Contain("version");
    }
}
