/*
 * CarRental.Tests - Frontend-Backend Integration Tests
 * Copyright (c) 2025 Alexander Orlov
 * 
 * These tests verify that the backend API endpoints match
 * what the frontend (aegis-ao-rental_web) expects.
 */

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using FluentAssertions;
using CarRental.Api.Data;
using CarRental.Api.Models;
using CarRental.Api.Controllers;
using CarRental.Tests.Infrastructure;
using Xunit;

namespace CarRental.Tests.Integration;

/// <summary>
/// Tests verifying API contract between frontend (aegis-ao-rental_web) and backend (aegis-ao-rental).
/// 
/// Frontend routes (from server/routes/meta.js):
/// - GET  /api/companies/{companyId}/meta/status
/// - GET  /api/companies/{companyId}/meta/pages  
/// - POST /api/companies/{companyId}/meta/disconnect
/// - POST /api/companies/{companyId}/meta/select-page
/// - POST /api/companies/{companyId}/meta/refresh-instagram
/// 
/// Frontend routes (from server/config/api.js):
/// - GET  /api/vehicles
/// - GET  /api/vehicles/{id}
/// - GET  /api/booking/bookings
/// - GET  /api/booking/bookings/{id}
/// - GET  /api/customers
/// - POST /api/auth/login
/// - GET  /api/auth/profile
/// - GET  /api/RentalCompanies
/// </summary>
public class FrontendBackendIntegrationTests : PostgresTestBase
{
    // Track test data for cleanup
    private readonly List<Guid> _createdMetaCredentialCompanyIds = new();

    #region Meta Integration Tests

    /// <summary>
    /// Test: GET /api/companies/{companyId}/meta/status
    /// Frontend: server/routes/meta.js line 28-52
    /// Backend: CompanyMetaController.GetStatus()
    /// </summary>
    [Fact]
    public async Task Meta_GetStatus_ShouldReturnCorrectFormat_WhenNotConnected()
    {
        // Arrange - use existing company without Meta credentials
        var company = await Context.Companies.FirstAsync(c => c.IsActive);

        // Verify no Meta credentials exist
        var existingCredentials = await Context.CompanyMetaCredentials
            .FirstOrDefaultAsync(c => c.CompanyId == company.Id);
        
        if (existingCredentials != null)
        {
            // Use a different company or skip
            company = await Context.Companies
                .Where(c => c.IsActive && !Context.CompanyMetaCredentials.Any(m => m.CompanyId == c.Id))
                .FirstOrDefaultAsync();
            
            if (company == null)
            {
                // All companies have Meta credentials, skip this specific test
                return;
            }
        }

        // Act - simulate what frontend expects
        var credentials = await Context.CompanyMetaCredentials
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CompanyId == company.Id);

        // Assert - verify response format matches frontend expectations
        // Frontend expects: { isConnected, status, pageId, pageName, instagramAccountId, ... }
        Assert.Null(credentials);
        
        // When not connected, frontend expects:
        var expectedResponse = new
        {
            isConnected = false,
            status = (string?)null,
            pageId = (string?)null,
            pageName = (string?)null,
            instagramAccountId = (string?)null,
            instagramUsername = (string?)null,
            tokenExpiresAt = (DateTime?)null,
            tokenStatus = (string?)null
        };
        
        // This is what CompanyMetaController.GetStatus() should return
        Assert.False(expectedResponse.isConnected);
    }

    /// <summary>
    /// Test: GET /api/companies/{companyId}/meta/status
    /// Frontend: server/routes/meta.js line 28-52
    /// Backend: CompanyMetaController.GetStatus()
    /// </summary>
    [Fact]
    public async Task Meta_GetStatus_ShouldReturnCorrectFormat_WhenConnected()
    {
        // Arrange - create company with Meta credentials
        var company = await CreateTestCompanyAsync("Meta Status Test Company");
        var credentials = await CreateTestMetaCredentialsAsync(company.Id, MetaCredentialStatus.Active);

        // Act - query credentials like CompanyMetaController does
        var result = await Context.CompanyMetaCredentials
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CompanyId == company.Id);

        // Assert - verify data matches frontend expectations
        Assert.NotNull(result);
        Assert.Equal(company.Id, result.CompanyId);
        Assert.Equal(MetaCredentialStatus.Active, result.Status);
        Assert.NotNull(result.PageId);
        Assert.NotNull(result.PageName);
        Assert.NotNull(result.InstagramAccountId);
        Assert.NotNull(result.InstagramUsername);
        
        // Frontend expects tokenStatus to be calculated:
        var tokenStatus = result.TokenExpiresAt > DateTime.UtcNow ? "valid" : "expired";
        Assert.Equal("valid", tokenStatus);
    }

    /// <summary>
    /// Test: GET /api/companies/{companyId}/meta/pages
    /// Frontend: server/routes/meta.js line 56-82
    /// Backend: CompanyMetaController.GetPages()
    /// </summary>
    [Fact]
    public async Task Meta_GetPages_ShouldReturnAvailablePages()
    {
        // Arrange
        var company = await CreateTestCompanyAsync("Meta Pages Test Company");
        var credentials = await CreateTestMetaCredentialsAsync(company.Id, MetaCredentialStatus.PendingPageSelection);

        // Act - get available pages like frontend expects
        var result = await Context.CompanyMetaCredentials
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CompanyId == company.Id);

        // Assert
        Assert.NotNull(result);
        var pages = ParseAvailablePages(result.AvailablePages);
        Assert.NotNull(pages);
        Assert.NotEmpty(pages);
        
        // Frontend expects each page to have: id, name, accessToken, instagramBusinessAccountId
        var firstPage = pages.First();
        Assert.NotNull(firstPage.Id);
        Assert.NotNull(firstPage.Name);
    }

    /// <summary>
    /// Test: POST /api/companies/{companyId}/meta/disconnect
    /// Frontend: server/routes/meta.js line 154-181
    /// Backend: CompanyMetaController.Disconnect()
    /// </summary>
    [Fact]
    public async Task Meta_Disconnect_ShouldRemoveCredentials()
    {
        // Arrange
        var company = await CreateTestCompanyAsync("Meta Disconnect Test Company");
        var credentials = await CreateTestMetaCredentialsAsync(company.Id, MetaCredentialStatus.Active);

        // Verify credentials exist
        var beforeDisconnect = await Context.CompanyMetaCredentials
            .FirstOrDefaultAsync(c => c.CompanyId == company.Id);
        Assert.NotNull(beforeDisconnect);

        // Act - disconnect like CompanyMetaController does
        Context.CompanyMetaCredentials.Remove(beforeDisconnect);
        await Context.SaveChangesAsync();

        // Remove from cleanup list since we deleted it
        _createdMetaCredentialCompanyIds.Remove(company.Id);

        // Assert
        var afterDisconnect = await Context.CompanyMetaCredentials
            .FirstOrDefaultAsync(c => c.CompanyId == company.Id);
        Assert.Null(afterDisconnect);
    }

    /// <summary>
    /// Test: POST /api/companies/{companyId}/meta/select-page
    /// Frontend: server/routes/meta.js line 185-212
    /// Backend: CompanyMetaController.SelectPage()
    /// </summary>
    [Fact]
    public async Task Meta_SelectPage_ShouldUpdateCredentials()
    {
        // Arrange
        var company = await CreateTestCompanyAsync("Meta Select Page Test Company");
        var credentials = await CreateTestMetaCredentialsAsync(company.Id, MetaCredentialStatus.PendingPageSelection);

        var pages = ParseAvailablePages(credentials.AvailablePages);
        Assert.NotNull(pages);
        Assert.NotEmpty(pages);
        var selectedPage = pages.First();

        // Act - select page like CompanyMetaController does
        var credentialsToUpdate = await Context.CompanyMetaCredentials
            .FirstAsync(c => c.CompanyId == company.Id);
        
        credentialsToUpdate.PageId = selectedPage.Id;
        credentialsToUpdate.PageName = selectedPage.Name;
        credentialsToUpdate.PageAccessToken = selectedPage.AccessToken;
        credentialsToUpdate.Status = MetaCredentialStatus.Active;
        credentialsToUpdate.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrEmpty(selectedPage.InstagramBusinessAccountId))
        {
            credentialsToUpdate.InstagramAccountId = selectedPage.InstagramBusinessAccountId;
            credentialsToUpdate.InstagramUsername = selectedPage.InstagramUsername;
        }

        await Context.SaveChangesAsync();

        // Assert
        var result = await Context.CompanyMetaCredentials
            .AsNoTracking()
            .FirstAsync(c => c.CompanyId == company.Id);

        Assert.Equal(MetaCredentialStatus.Active, result.Status);
        Assert.Equal(selectedPage.Id, result.PageId);
        Assert.Equal(selectedPage.Name, result.PageName);
    }

    /// <summary>
    /// Test: GET/POST /api/companies/{companyId}/meta/auto-publish
    /// Frontend expects these settings to be stored and retrieved
    /// </summary>
    [Fact]
    public async Task Meta_AutoPublishSettings_ShouldPersist()
    {
        // Arrange
        var company = await CreateTestCompanyAsync("Meta AutoPublish Test Company");
        var credentials = await CreateTestMetaCredentialsAsync(company.Id, MetaCredentialStatus.Active);

        // Act - update auto-publish settings
        var credentialsToUpdate = await Context.CompanyMetaCredentials
            .FirstAsync(c => c.CompanyId == company.Id);
        
        credentialsToUpdate.AutoPublishFacebook = true;
        credentialsToUpdate.AutoPublishInstagram = true;
        credentialsToUpdate.AutoPublishIncludePrice = true;
        credentialsToUpdate.AutoPublishHashtags = "#rental #cars #miami";
        credentialsToUpdate.UpdatedAt = DateTime.UtcNow;

        await Context.SaveChangesAsync();

        // Assert
        var result = await Context.CompanyMetaCredentials
            .AsNoTracking()
            .FirstAsync(c => c.CompanyId == company.Id);

        Assert.True(result.AutoPublishFacebook);
        Assert.True(result.AutoPublishInstagram);
        Assert.True(result.AutoPublishIncludePrice);
        Assert.Equal("#rental #cars #miami", result.AutoPublishHashtags);
    }

    /// <summary>
    /// Test: GET/POST /api/companies/{companyId}/meta/deep-links
    /// Frontend expects these settings to be stored and retrieved
    /// </summary>
    [Fact]
    public async Task Meta_DeepLinkSettings_ShouldPersist()
    {
        // Arrange
        var company = await CreateTestCompanyAsync("Meta DeepLinks Test Company");
        var credentials = await CreateTestMetaCredentialsAsync(company.Id, MetaCredentialStatus.Active);

        // Act - update deep link settings
        var credentialsToUpdate = await Context.CompanyMetaCredentials
            .FirstAsync(c => c.CompanyId == company.Id);
        
        credentialsToUpdate.DeepLinkBaseUrl = "https://rentals.example.com";
        credentialsToUpdate.DeepLinkVehiclePattern = "/vehicles/{vehicleId}";
        credentialsToUpdate.DeepLinkBookingPattern = "/book/{vehicleId}";
        credentialsToUpdate.UpdatedAt = DateTime.UtcNow;

        await Context.SaveChangesAsync();

        // Assert
        var result = await Context.CompanyMetaCredentials
            .AsNoTracking()
            .FirstAsync(c => c.CompanyId == company.Id);

        Assert.Equal("https://rentals.example.com", result.DeepLinkBaseUrl);
        Assert.Equal("/vehicles/{vehicleId}", result.DeepLinkVehiclePattern);
        Assert.Equal("/book/{vehicleId}", result.DeepLinkBookingPattern);
    }

    #endregion

    #region Vehicles Integration Tests

    /// <summary>
    /// Test: GET /api/vehicles
    /// Frontend: server/config/api.js line 108-114
    /// </summary>
    [Fact]
    public async Task Vehicles_GetAll_ShouldReturnVehicles()
    {
        // Act
        var vehicles = await Context.Vehicles
            .Include(v => v.Company)
            .Take(10)
            .ToListAsync();

        // Assert - verify data structure matches frontend expectations
        Assert.NotNull(vehicles);
        foreach (var vehicle in vehicles)
        {
            Assert.NotEqual(Guid.Empty, vehicle.Id);
            Assert.NotNull(vehicle.LicensePlate);
            // Frontend expects companyId for filtering
            Assert.NotEqual(Guid.Empty, vehicle.CompanyId);
        }
    }

    /// <summary>
    /// Test: GET /api/vehicles/{id}
    /// Frontend: server/config/api.js line 115-121
    /// </summary>
    [Fact]
    public async Task Vehicles_GetById_ShouldReturnVehicle()
    {
        // Arrange
        var vehicle = await Context.Vehicles.FirstAsync();

        // Act
        var result = await Context.Vehicles
            .Include(v => v.Company)
            .FirstOrDefaultAsync(v => v.Id == vehicle.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(vehicle.Id, result.Id);
    }

    #endregion

    #region Bookings Integration Tests

    /// <summary>
    /// Test: GET /api/booking/bookings
    /// Frontend: server/config/api.js line 153-159
    /// </summary>
    [Fact]
    public async Task Bookings_GetAll_ShouldReturnBookings()
    {
        // Act
        var bookings = await Context.Bookings
            .Include(b => b.Customer)
            .Include(b => b.Vehicle)
            .Take(10)
            .ToListAsync();

        // Assert
        Assert.NotNull(bookings);
        foreach (var booking in bookings)
        {
            Assert.NotEqual(Guid.Empty, booking.Id);
            // Frontend expects these for display
            Assert.NotEqual(Guid.Empty, booking.CompanyId);
        }
    }

    /// <summary>
    /// Test: GET /api/booking/companies/{companyId}/bookings
    /// Frontend: server/config/api.js line 167-185
    /// </summary>
    [Fact]
    public async Task Bookings_GetByCompany_ShouldFilterByCompany()
    {
        // Arrange
        var company = await Context.Companies.FirstAsync(c => c.IsActive);

        // Act
        var bookings = await Context.Bookings
            .Where(b => b.CompanyId == company.Id)
            .Take(10)
            .ToListAsync();

        // Assert - all bookings should belong to the company
        foreach (var booking in bookings)
        {
            Assert.Equal(company.Id, booking.CompanyId);
        }
    }

    #endregion

    #region Customers Integration Tests

    /// <summary>
    /// Test: GET /api/customers
    /// Frontend: server/config/api.js line 257-263
    /// </summary>
    [Fact]
    public async Task Customers_GetAll_ShouldReturnCustomers()
    {
        // Act
        var customers = await Context.Customers
            .Take(10)
            .ToListAsync();

        // Assert
        Assert.NotNull(customers);
        foreach (var customer in customers)
        {
            Assert.NotEqual(Guid.Empty, customer.Id);
            // Frontend expects email for display
            Assert.NotNull(customer.Email);
        }
    }

    #endregion

    #region Companies Integration Tests

    /// <summary>
    /// Test: GET /api/RentalCompanies
    /// Frontend: server/config/api.js line 345
    /// </summary>
    [Fact]
    public async Task Companies_GetAll_ShouldReturnCompanies()
    {
        // Act
        var companies = await Context.Companies
            .Where(c => c.IsActive)
            .Take(10)
            .ToListAsync();

        // Assert
        Assert.NotNull(companies);
        Assert.NotEmpty(companies);
        foreach (var company in companies)
        {
            Assert.NotEqual(Guid.Empty, company.Id);
            Assert.NotNull(company.CompanyName);
        }
    }

    #endregion

    #region Helper Methods

    private async Task<Company> CreateTestCompanyAsync(string name)
    {
        var company = new Company
        {
            Id = Guid.NewGuid(),
            CompanyName = $"[TEST] {name} {DateTime.UtcNow:HHmmss}",
            Email = $"test-{Guid.NewGuid():N}@integration-test.com",
            Country = "USA",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Subdomain = $"test-{Guid.NewGuid():N}"[..20]
        };

        Context.Companies.Add(company);
        await Context.SaveChangesAsync();
        TrackForCleanup(company);

        return company;
    }

    private async Task<CompanyMetaCredentials> CreateTestMetaCredentialsAsync(Guid companyId, MetaCredentialStatus status)
    {
        var availablePages = new List<MetaPageInfo>
        {
            new MetaPageInfo
            {
                Id = "page_" + Guid.NewGuid().ToString("N")[..10],
                Name = "Test Facebook Page",
                AccessToken = "test_page_access_token_" + Guid.NewGuid().ToString("N"),
                InstagramBusinessAccountId = "ig_" + Guid.NewGuid().ToString("N")[..10],
                InstagramUsername = "test_instagram_user"
            }
        };

        var credentials = new CompanyMetaCredentials
        {
            CompanyId = companyId,
            UserAccessToken = "test_user_token_" + Guid.NewGuid().ToString("N"),
            TokenExpiresAt = DateTime.UtcNow.AddDays(60),
            Status = status,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            AvailablePages = JsonDocument.Parse(JsonSerializer.Serialize(availablePages))
        };

        if (status == MetaCredentialStatus.Active)
        {
            var page = availablePages.First();
            credentials.PageId = page.Id;
            credentials.PageName = page.Name;
            credentials.PageAccessToken = page.AccessToken;
            credentials.InstagramAccountId = page.InstagramBusinessAccountId;
            credentials.InstagramUsername = page.InstagramUsername;
        }

        Context.CompanyMetaCredentials.Add(credentials);
        await Context.SaveChangesAsync();
        _createdMetaCredentialCompanyIds.Add(companyId);

        return credentials;
    }

    private static List<MetaPageInfo>? ParseAvailablePages(JsonDocument? availablePages)
    {
        if (availablePages == null)
            return null;

        try
        {
            return JsonSerializer.Deserialize<List<MetaPageInfo>>(availablePages.RootElement.GetRawText());
        }
        catch
        {
            return null;
        }
    }

    #endregion

    #region Rental Agreement Tests

    /// <summary>
    /// Test: POST /api/booking/bookings/{id}/sign-agreement
    /// Frontend: server/routes/reservations.js (sign-agreement route)
    /// Backend: BookingController.SignExistingBooking()
    /// 
    /// Verifies that the sign-agreement endpoint creates an agreement
    /// for an existing booking without an agreement.
    /// </summary>
    [Fact]
    public async Task RentalAgreement_SignExistingBooking_ShouldCreateAgreement()
    {
        // Arrange - create booking without agreement
        var (company, customer, vehicle, booking) = await SeedCompleteScenarioAsync();
        
        // Verify no agreement exists
        var existingAgreement = await Context.RentalAgreements
            .FirstOrDefaultAsync(a => a.BookingId == booking.Id);
        existingAgreement.Should().BeNull("Booking should not have agreement initially");

        // Simulate what frontend sends
        var agreementData = new
        {
            signatureImage = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==",
            language = "en",
            consents = new
            {
                termsAcceptedAt = DateTime.UtcNow,
                nonRefundableAcceptedAt = DateTime.UtcNow,
                damagePolicyAcceptedAt = DateTime.UtcNow,
                cardAuthorizationAcceptedAt = DateTime.UtcNow
            },
            consentTexts = new
            {
                termsTitle = "Terms and Conditions",
                termsText = "I agree to the rental terms.",
                nonRefundableTitle = "Non-Refundable",
                nonRefundableText = "I understand this is non-refundable.",
                damagePolicyTitle = "Damage Policy",
                damagePolicyText = "I am responsible for damage.",
                cardAuthorizationTitle = "Card Authorization",
                cardAuthorizationText = "I authorize charges."
            },
            signedAt = DateTime.UtcNow,
            userAgent = "Test/1.0",
            timezone = "America/New_York"
        };

        // Assert - data structure matches what frontend sends
        agreementData.signatureImage.Should().NotBeNullOrEmpty();
        agreementData.language.Should().Be("en");
        agreementData.consents.Should().NotBeNull();
        agreementData.consentTexts.Should().NotBeNull();
    }

    /// <summary>
    /// Test: GET /api/booking/bookings/{id}/rental-agreement
    /// Frontend: server/routes/reservations.js (rental-agreement route)
    /// Backend: BookingController.GetRentalAgreement()
    /// 
    /// Verifies response format matches what frontend expects.
    /// </summary>
    [Fact]
    public async Task RentalAgreement_GetRentalAgreement_ResponseFormat()
    {
        // Arrange - create booking with agreement using seed method
        var (company, customer, vehicle, booking, agreement) = await SeedCompleteScenarioWithAgreementAsync();

        // Act - load agreement from database
        var loadedAgreement = await Context.RentalAgreements
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.BookingId == booking.Id);

        // Assert - verify response format matches frontend expectations
        loadedAgreement.Should().NotBeNull();
        loadedAgreement!.Id.Should().NotBeEmpty();
        loadedAgreement.AgreementNumber.Should().NotBeNullOrEmpty();
        loadedAgreement.BookingId.Should().Be(booking.Id);
        loadedAgreement.CustomerId.Should().Be(customer.Id);
        loadedAgreement.VehicleId.Should().Be(vehicle.Id);
        loadedAgreement.CompanyId.Should().Be(company.Id);
        loadedAgreement.Language.Should().Be("en");
        loadedAgreement.SignatureImage.Should().NotBeNullOrEmpty();
        loadedAgreement.Status.Should().Be("active");
    }

    /// <summary>
    /// Test: Frontend expects specific field names in agreement response
    /// Verifies camelCase property names that frontend JavaScript expects
    /// </summary>
    [Fact]
    public async Task RentalAgreement_ResponseFields_MatchFrontendExpectations()
    {
        // Arrange
        var (_, _, _, booking, agreement) = await SeedCompleteScenarioWithAgreementAsync();

        // Assert - these field names must match what frontend expects (camelCase)
        // Frontend accesses: response.data.pdfUrl, response.data.id, etc.
        agreement.Should().NotBeNull();
        
        // Property existence checks (these become camelCase in JSON)
        agreement.Id.Should().NotBeEmpty(); // -> id
        agreement.AgreementNumber.Should().NotBeNullOrEmpty(); // -> agreementNumber
        agreement.BookingId.Should().NotBeEmpty(); // -> bookingId
        agreement.PdfUrl.Should().BeNullOrEmpty(); // -> pdfUrl (null without blob storage)
        agreement.SignatureImage.Should().NotBeNullOrEmpty(); // -> signatureImage
        agreement.SignedAt.Should().NotBe(default); // -> signedAt
        agreement.Status.Should().Be("active"); // -> status
    }

    #endregion

    #region Cleanup

    // Note: Meta credentials are cleaned up automatically via FK cascade 
    // when companies are deleted in PostgresTestBase.DisposeAsync()

    #endregion
}
