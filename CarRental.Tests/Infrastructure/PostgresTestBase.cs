/*
 * CarRental.Tests - Integration Tests with Azure PostgreSQL
 * Copyright (c) 2025 Alexander Orlov
 * 
 * IMPORTANT: This connects to PRODUCTION database in test mode.
 * All test data is tracked and cleaned up after each test.
 */

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using CarRental.Api.Data;
using CarRental.Api.Models;
using Xunit;

namespace CarRental.Tests.Infrastructure;

/// <summary>
/// Base class for integration tests using Azure PostgreSQL.
/// Tracks all created entities and removes them after each test.
/// </summary>
public abstract class PostgresTestBase : IAsyncLifetime
{
    protected CarRentalDbContext Context { get; private set; } = null!;
    
    // Track all created entities for cleanup
    private readonly List<Guid> _createdBookingIds = new();
    private readonly List<Guid> _createdVehicleIds = new();
    private readonly List<Guid> _createdCustomerIds = new();
    private readonly List<Guid> _createdCompanyIds = new();
    private readonly List<Guid> _createdPaymentIds = new();
    private readonly List<Guid> _createdStripeTransferIds = new();
    private readonly List<Guid> _createdStripePayoutIds = new();
    private readonly List<Guid> _createdStripeCompanyIds = new();
    private readonly List<Guid> _createdStripeSettingsIds = new();
    private readonly List<Guid> _createdPaymentMethodIds = new();
    
    // Meta/Social integration entities
    private readonly List<int> _createdMetaCredentialIds = new();
    private readonly List<Guid> _createdAutoPostSettingsIds = new();
    private readonly List<Guid> _createdScheduledPostIds = new();
    private readonly List<Guid> _createdSocialPostTemplateIds = new();
    private readonly List<Guid> _createdSocialPostAnalyticsIds = new();
    private readonly List<Guid> _createdVehicleSocialPostIds = new();

    public async Task InitializeAsync()
    {
        var connectionString = GetConnectionString();
        
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException(
                "Test database connection string not found. " +
                "Set TEST_DATABASE_CONNECTION_STRING environment variable or configure appsettings.Test.json");
        }

        // Simple connection - no NpgsqlDataSource needed
        // Enums are stored as strings using HasConversion<string>()
        var options = new DbContextOptionsBuilder<CarRentalDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        Context = new CarRentalDbContext(options);
        
        // Verify connection
        await Context.Database.CanConnectAsync();
    }

    public async Task DisposeAsync()
    {
        // Clean up ALL tracked test data in correct order (foreign key constraints)
        await CleanupTrackedDataAsync();
        await Context.DisposeAsync();
    }

    private static string? GetConnectionString()
    {
        // First try environment variable
        var envConnectionString = Environment.GetEnvironmentVariable("TEST_DATABASE_CONNECTION_STRING");
        if (!string.IsNullOrEmpty(envConnectionString))
        {
            return envConnectionString;
        }

        // Then try appsettings.Test.json
        try
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.Test.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            return configuration.GetConnectionString("TestDatabase");
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Cleans up ALL tracked test data created during the test
    /// </summary>
    private async Task CleanupTrackedDataAsync()
    {
        try
        {
            // 0a. Delete Social Post Analytics (references VehicleSocialPosts)
            if (_createdSocialPostAnalyticsIds.Count > 0)
            {
                var analyticsToDelete = await Context.SocialPostAnalytics
                    .Where(a => _createdSocialPostAnalyticsIds.Contains(a.Id))
                    .ToListAsync();
                
                if (analyticsToDelete.Count > 0)
                {
                    Context.SocialPostAnalytics.RemoveRange(analyticsToDelete);
                    await Context.SaveChangesAsync();
                }
            }

            // 0b. Delete Vehicle Social Posts (references Vehicles, Companies)
            if (_createdVehicleSocialPostIds.Count > 0)
            {
                var socialPostsToDelete = await Context.VehicleSocialPosts
                    .Where(p => _createdVehicleSocialPostIds.Contains(p.Id))
                    .ToListAsync();
                
                if (socialPostsToDelete.Count > 0)
                {
                    Context.VehicleSocialPosts.RemoveRange(socialPostsToDelete);
                    await Context.SaveChangesAsync();
                }
            }

            // 0c. Delete Scheduled Posts (references Companies, Vehicles)
            if (_createdScheduledPostIds.Count > 0)
            {
                var scheduledPostsToDelete = await Context.ScheduledPosts
                    .Where(p => _createdScheduledPostIds.Contains(p.Id))
                    .ToListAsync();
                
                if (scheduledPostsToDelete.Count > 0)
                {
                    Context.ScheduledPosts.RemoveRange(scheduledPostsToDelete);
                    await Context.SaveChangesAsync();
                }
            }

            // 0d. Delete Social Post Templates (references Companies)
            if (_createdSocialPostTemplateIds.Count > 0)
            {
                var templatesToDelete = await Context.SocialPostTemplates
                    .Where(t => _createdSocialPostTemplateIds.Contains(t.Id))
                    .ToListAsync();
                
                if (templatesToDelete.Count > 0)
                {
                    Context.SocialPostTemplates.RemoveRange(templatesToDelete);
                    await Context.SaveChangesAsync();
                }
            }

            // 0e. Delete Auto Post Settings (references Companies)
            if (_createdAutoPostSettingsIds.Count > 0)
            {
                var autoPostSettingsToDelete = await Context.CompanyAutoPostSettings
                    .Where(s => _createdAutoPostSettingsIds.Contains(s.Id))
                    .ToListAsync();
                
                if (autoPostSettingsToDelete.Count > 0)
                {
                    Context.CompanyAutoPostSettings.RemoveRange(autoPostSettingsToDelete);
                    await Context.SaveChangesAsync();
                }
            }

            // 0f. Delete Meta Credentials (references Companies)
            if (_createdMetaCredentialIds.Count > 0)
            {
                var metaCredentialsToDelete = await Context.CompanyMetaCredentials
                    .Where(c => _createdMetaCredentialIds.Contains(c.Id))
                    .ToListAsync();
                
                if (metaCredentialsToDelete.Count > 0)
                {
                    Context.CompanyMetaCredentials.RemoveRange(metaCredentialsToDelete);
                    await Context.SaveChangesAsync();
                }
            }

            // 1. Delete Payments first (references Bookings, Customers, Companies)
            if (_createdPaymentIds.Count > 0)
            {
                var paymentsToDelete = await Context.Payments
                    .Where(p => _createdPaymentIds.Contains(p.Id))
                    .ToListAsync();
                
                if (paymentsToDelete.Count > 0)
                {
                    Context.Payments.RemoveRange(paymentsToDelete);
                    await Context.SaveChangesAsync();
                }
            }

            // 2. Delete Stripe Transfers (references Bookings, Companies)
            if (_createdStripeTransferIds.Count > 0)
            {
                var transfersToDelete = await Context.StripeTransfers
                    .Where(t => _createdStripeTransferIds.Contains(t.Id))
                    .ToListAsync();
                
                if (transfersToDelete.Count > 0)
                {
                    Context.StripeTransfers.RemoveRange(transfersToDelete);
                    await Context.SaveChangesAsync();
                }
            }

            // 3. Delete Stripe Payouts (references Companies)
            if (_createdStripePayoutIds.Count > 0)
            {
                var payoutsToDelete = await Context.StripePayoutRecords
                    .Where(p => _createdStripePayoutIds.Contains(p.Id))
                    .ToListAsync();
                
                if (payoutsToDelete.Count > 0)
                {
                    Context.StripePayoutRecords.RemoveRange(payoutsToDelete);
                    await Context.SaveChangesAsync();
                }
            }

            // 4. Delete Customer Payment Methods
            if (_createdPaymentMethodIds.Count > 0)
            {
                var methodsToDelete = await Context.CustomerPaymentMethods
                    .Where(pm => _createdPaymentMethodIds.Contains(pm.Id))
                    .ToListAsync();
                
                if (methodsToDelete.Count > 0)
                {
                    Context.CustomerPaymentMethods.RemoveRange(methodsToDelete);
                    await Context.SaveChangesAsync();
                }
            }

            // 5. Delete Bookings (references Customers, Vehicles, Companies)
            if (_createdBookingIds.Count > 0)
            {
                var bookingsToDelete = await Context.Bookings
                    .Where(b => _createdBookingIds.Contains(b.Id))
                    .ToListAsync();
                
                if (bookingsToDelete.Count > 0)
                {
                    Context.Bookings.RemoveRange(bookingsToDelete);
                    await Context.SaveChangesAsync();
                }
            }

            // 6. Delete Vehicles (references Companies)
            if (_createdVehicleIds.Count > 0)
            {
                var vehiclesToDelete = await Context.Vehicles
                    .Where(v => _createdVehicleIds.Contains(v.Id))
                    .ToListAsync();
                
                if (vehiclesToDelete.Count > 0)
                {
                    Context.Vehicles.RemoveRange(vehiclesToDelete);
                    await Context.SaveChangesAsync();
                }
            }

            // 7. Delete Customers (references Companies)
            if (_createdCustomerIds.Count > 0)
            {
                var customersToDelete = await Context.Customers
                    .Where(c => _createdCustomerIds.Contains(c.Id))
                    .ToListAsync();
                
                if (customersToDelete.Count > 0)
                {
                    Context.Customers.RemoveRange(customersToDelete);
                    await Context.SaveChangesAsync();
                }
            }

            // 8. Delete Stripe Companies (references Companies, StripeSettings)
            if (_createdStripeCompanyIds.Count > 0)
            {
                var stripeCompaniesToDelete = await Context.StripeCompanies
                    .Where(sc => _createdStripeCompanyIds.Contains(sc.Id))
                    .ToListAsync();
                
                if (stripeCompaniesToDelete.Count > 0)
                {
                    Context.StripeCompanies.RemoveRange(stripeCompaniesToDelete);
                    await Context.SaveChangesAsync();
                }
            }

            // 9. Delete Companies (no dependencies at this point)
            if (_createdCompanyIds.Count > 0)
            {
                var companiesToDelete = await Context.Companies
                    .Where(c => _createdCompanyIds.Contains(c.Id))
                    .ToListAsync();
                
                if (companiesToDelete.Count > 0)
                {
                    Context.Companies.RemoveRange(companiesToDelete);
                    await Context.SaveChangesAsync();
                }
            }

            // 10. Delete Stripe Settings last
            if (_createdStripeSettingsIds.Count > 0)
            {
                var settingsToDelete = await Context.StripeSettings
                    .Where(ss => _createdStripeSettingsIds.Contains(ss.Id))
                    .ToListAsync();
                
                if (settingsToDelete.Count > 0)
                {
                    Context.StripeSettings.RemoveRange(settingsToDelete);
                    await Context.SaveChangesAsync();
                }
            }
        }
        catch (Exception ex)
        {
            // Log but don't fail - we don't want cleanup errors to mask test failures
            Console.WriteLine($"Warning: Cleanup error - {ex.Message}");
        }
    }

    /// <summary>
    /// Seeds the database with a test company (tracked for cleanup)
    /// </summary>
    protected async Task<Company> SeedCompanyAsync(string name = "Test Rental Company", string subdomain = "testcompany")
    {
        var uniqueSuffix = Guid.NewGuid().ToString()[..6];
        var company = new Company
        {
            Id = Guid.NewGuid(),
            CompanyName = $"[TEST] {name}",
            Subdomain = $"test_{subdomain}_{uniqueSuffix}",
            Email = $"test_{subdomain}_{uniqueSuffix}@test.example.com",
            IsActive = true,
            SecurityDeposit = 500m,
            Currency = "USD",
            Country = "United States"
        };

        Context.Companies.Add(company);
        await Context.SaveChangesAsync();
        
        // Track for cleanup
        _createdCompanyIds.Add(company.Id);
        
        return company;
    }

    /// <summary>
    /// Seeds the database with a test customer (tracked for cleanup)
    /// </summary>
    protected async Task<Customer> SeedCustomerAsync(Guid? companyId = null, string firstName = "TestJohn", string lastName = "TestDoe")
    {
        var uniqueSuffix = Guid.NewGuid().ToString()[..6];
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            FirstName = $"[TEST]{firstName}",
            LastName = $"[TEST]{lastName}",
            Email = $"test_{firstName.ToLower()}_{uniqueSuffix}@test.example.com",
            Phone = "+1-555-000-0000",
            Role = "customer",
            IsActive = true,
            CompanyId = companyId
        };

        Context.Customers.Add(customer);
        await Context.SaveChangesAsync();
        
        // Track for cleanup
        _createdCustomerIds.Add(customer.Id);
        
        return customer;
    }

    /// <summary>
    /// Seeds the database with a test vehicle (tracked for cleanup)
    /// </summary>
    protected async Task<Vehicle> SeedVehicleAsync(Guid companyId, string licensePlate = "TEST")
    {
        var uniqueSuffix = Guid.NewGuid().ToString("N")[..8].ToUpper();
        var vehicle = new Vehicle
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            LicensePlate = $"T{uniqueSuffix}",
            Vin = $"TST{uniqueSuffix}000000", // Exactly 17 chars: TST(3) + suffix(8) + 000000(6) = 17
            Color = "TestBlack",
            Transmission = "Automatic",
            Seats = 5,
            Mileage = 0,
            Status = VehicleStatus.Available,
            State = "TS"
        };

        Context.Vehicles.Add(vehicle);
        await Context.SaveChangesAsync();
        
        // Track for cleanup
        _createdVehicleIds.Add(vehicle.Id);
        
        return vehicle;
    }

    /// <summary>
    /// Seeds the database with a test booking (tracked for cleanup)
    /// </summary>
    protected async Task<Booking> SeedBookingAsync(Guid customerId, Guid vehicleId, Guid companyId, int daysFromNow = 1, int duration = 3)
    {
        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            VehicleId = vehicleId,
            CompanyId = companyId,
            BookingNumber = $"TEST-{Guid.NewGuid():N}".Substring(0, 20),
            PickupDate = DateTime.UtcNow.AddDays(daysFromNow),
            PickupTime = "10:00",
            ReturnDate = DateTime.UtcNow.AddDays(daysFromNow + duration),
            ReturnTime = "18:00",
            DailyRate = 100m,
            TotalDays = duration,
            Subtotal = 100m * duration,
            TaxAmount = 7m * duration,
            TotalAmount = 107m * duration,
            Status = "Pending",
            PaymentStatus = "pending",
            SecurityDeposit = 500m,
            Currency = "USD"
        };

        Context.Bookings.Add(booking);
        await Context.SaveChangesAsync();
        
        // Track for cleanup
        _createdBookingIds.Add(booking.Id);
        
        return booking;
    }

    /// <summary>
    /// Seeds the database with a complete test scenario (all tracked for cleanup)
    /// </summary>
    protected async Task<(Company Company, Customer Customer, Vehicle Vehicle, Booking Booking)> SeedCompleteScenarioAsync()
    {
        var company = await SeedCompanyAsync();
        var customer = await SeedCustomerAsync(company.Id);
        var vehicle = await SeedVehicleAsync(company.Id);
        var booking = await SeedBookingAsync(customer.Id, vehicle.Id, company.Id);

        return (company, customer, vehicle, booking);
    }
    
    /// <summary>
    /// Manually track an entity for cleanup (use if creating entities directly)
    /// </summary>
    protected void TrackForCleanup(Booking booking) => _createdBookingIds.Add(booking.Id);
    protected void TrackForCleanup(Vehicle vehicle) => _createdVehicleIds.Add(vehicle.Id);
    protected void TrackForCleanup(Customer customer) => _createdCustomerIds.Add(customer.Id);
    protected void TrackForCleanup(Company company) => _createdCompanyIds.Add(company.Id);
    protected void TrackForCleanup(Payment payment) => _createdPaymentIds.Add(payment.Id);
    protected void TrackForCleanup(StripeTransfer transfer) => _createdStripeTransferIds.Add(transfer.Id);
    protected void TrackForCleanup(StripePayoutRecord payout) => _createdStripePayoutIds.Add(payout.Id);
    protected void TrackForCleanup(StripeCompany stripeCompany) => _createdStripeCompanyIds.Add(stripeCompany.Id);
    protected void TrackForCleanup(StripeSettings stripeSettings) => _createdStripeSettingsIds.Add(stripeSettings.Id);
    protected void TrackForCleanup(CustomerPaymentMethod paymentMethod) => _createdPaymentMethodIds.Add(paymentMethod.Id);
    
    // Meta/Social integration tracking
    protected void TrackForCleanup(CompanyMetaCredentials credentials) => _createdMetaCredentialIds.Add(credentials.Id);
    protected void TrackForCleanup(CompanyAutoPostSettings settings) => _createdAutoPostSettingsIds.Add(settings.Id);
    protected void TrackForCleanup(ScheduledPost scheduledPost) => _createdScheduledPostIds.Add(scheduledPost.Id);
    protected void TrackForCleanup(SocialPostTemplate template) => _createdSocialPostTemplateIds.Add(template.Id);
    protected void TrackForCleanup(SocialPostAnalytics analytics) => _createdSocialPostAnalyticsIds.Add(analytics.Id);
    protected void TrackForCleanup(VehicleSocialPost socialPost) => _createdVehicleSocialPostIds.Add(socialPost.Id);
}
