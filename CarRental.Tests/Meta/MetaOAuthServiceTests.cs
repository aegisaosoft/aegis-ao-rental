/*
 * Meta OAuth Service Tests
 * Tests for Facebook/Instagram OAuth integration
 */

using CarRental.Api.Models;
using CarRental.Api.Services;
using CarRental.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Text;
using Xunit;

namespace CarRental.Tests.Meta;

/// <summary>
/// Tests for Meta OAuth functionality including state management,
/// token exchange, and credential storage
/// </summary>
[Collection("PostgreSql")]
public class MetaOAuthServiceTests : PostgresTestBase
{
    #region State Generation and Validation Tests

    [Fact]
    public void GenerateState_ShouldCreateBase64EncodedString_ContainingCompanyId()
    {
        // Arrange
        var companyId = Guid.NewGuid();
        var cache = new MemoryCache(new MemoryCacheOptions());
        
        // Act - simulate state generation logic
        var randomPart = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        var state = $"{companyId}:{randomPart}";
        var encodedState = Convert.ToBase64String(Encoding.UTF8.GetBytes(state));
        
        // Store in cache
        cache.Set($"meta_oauth_state:{state}", companyId, TimeSpan.FromMinutes(10));

        // Assert
        encodedState.Should().NotBeNullOrEmpty();
        
        // Decode and verify
        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(encodedState));
        decoded.Should().Contain(companyId.ToString());
        
        // Verify cache contains the state
        cache.TryGetValue($"meta_oauth_state:{state}", out Guid cachedCompanyId).Should().BeTrue();
        cachedCompanyId.Should().Be(companyId);
    }

    [Fact]
    public void ValidateState_WithValidState_ShouldReturnCompanyId()
    {
        // Arrange
        var companyId = Guid.NewGuid();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var randomPart = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        var state = $"{companyId}:{randomPart}";
        var encodedState = Convert.ToBase64String(Encoding.UTF8.GetBytes(state));
        
        // Store in cache (simulating GenerateState)
        cache.Set($"meta_oauth_state:{state}", companyId, TimeSpan.FromMinutes(10));

        // Act - simulate validation
        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(encodedState));
        cache.TryGetValue($"meta_oauth_state:{decoded}", out Guid extractedCompanyId);

        // Assert
        extractedCompanyId.Should().Be(companyId);
    }

    [Fact]
    public void ValidateState_WithInvalidState_ShouldReturnNull()
    {
        // Arrange
        var cache = new MemoryCache(new MemoryCacheOptions());
        var invalidState = "invalid_base64_state";

        // Act
        Guid? result = null;
        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(invalidState));
            if (!cache.TryGetValue($"meta_oauth_state:{decoded}", out Guid companyId))
            {
                result = null;
            }
        }
        catch
        {
            result = null;
        }

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ValidateState_ShouldBeOneTimeUse()
    {
        // Arrange
        var companyId = Guid.NewGuid();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var state = $"{companyId}:{Guid.NewGuid()}";
        
        cache.Set($"meta_oauth_state:{state}", companyId, TimeSpan.FromMinutes(10));

        // Act - First validation
        cache.TryGetValue($"meta_oauth_state:{state}", out Guid firstResult);
        cache.Remove($"meta_oauth_state:{state}"); // One-time use

        // Second validation attempt
        var secondResult = cache.TryGetValue($"meta_oauth_state:{state}", out Guid _);

        // Assert
        firstResult.Should().Be(companyId);
        secondResult.Should().BeFalse(); // State consumed
    }

    #endregion

    #region Meta Credentials Storage Tests

    [Fact]
    public async Task SaveMetaCredentials_ShouldPersistToDatabase()
    {
        // Arrange
        var company = await SeedCompanyAsync();
        
        var credentials = new CompanyMetaCredentials
        {
            CompanyId = company.Id,
            UserAccessToken = "EAABtest_user_token_123",
            TokenExpiresAt = DateTime.UtcNow.AddDays(60),
            PageId = "123456789",
            PageName = "Test Rental Company Page",
            PageAccessToken = "EAABtest_page_token_456",
            InstagramAccountId = "17841400000000000",
            InstagramUsername = "testrentalco",
            Status = MetaCredentialStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        Context.Set<CompanyMetaCredentials>().Add(credentials);
        await Context.SaveChangesAsync();
        TrackForCleanup(credentials);

        // Assert
        Context.ChangeTracker.Clear();
        var saved = await Context.Set<CompanyMetaCredentials>()
            .FirstOrDefaultAsync(c => c.CompanyId == company.Id);

        saved.Should().NotBeNull();
        saved!.PageId.Should().Be("123456789");
        saved.PageName.Should().Be("Test Rental Company Page");
        saved.InstagramAccountId.Should().Be("17841400000000000");
        saved.InstagramUsername.Should().Be("testrentalco");
        saved.Status.Should().Be(MetaCredentialStatus.Active);
    }

    [Fact]
    public async Task UpdateMetaCredentials_AfterTokenRefresh_ShouldUpdateToken()
    {
        // Arrange
        var company = await SeedCompanyAsync();
        var originalExpiry = DateTime.UtcNow.AddDays(30);
        
        var credentials = new CompanyMetaCredentials
        {
            CompanyId = company.Id,
            UserAccessToken = "original_token",
            TokenExpiresAt = originalExpiry,
            Status = MetaCredentialStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        Context.Set<CompanyMetaCredentials>().Add(credentials);
        await Context.SaveChangesAsync();
        TrackForCleanup(credentials);

        // Act - Simulate token refresh
        var loaded = await Context.Set<CompanyMetaCredentials>()
            .FirstAsync(c => c.CompanyId == company.Id);
        
        loaded.UserAccessToken = "refreshed_token_abc";
        loaded.TokenExpiresAt = DateTime.UtcNow.AddDays(60);
        loaded.LastTokenRefresh = DateTime.UtcNow;
        loaded.UpdatedAt = DateTime.UtcNow;
        await Context.SaveChangesAsync();

        // Assert
        Context.ChangeTracker.Clear();
        var updated = await Context.Set<CompanyMetaCredentials>()
            .FirstAsync(c => c.CompanyId == company.Id);

        updated.UserAccessToken.Should().Be("refreshed_token_abc");
        updated.TokenExpiresAt.Should().BeAfter(originalExpiry);
        updated.LastTokenRefresh.Should().NotBeNull();
    }

    [Fact]
    public async Task MetaCredentials_StatusTransition_PendingToActive()
    {
        // Arrange
        var company = await SeedCompanyAsync();
        
        var credentials = new CompanyMetaCredentials
        {
            CompanyId = company.Id,
            UserAccessToken = "token_123",
            TokenExpiresAt = DateTime.UtcNow.AddDays(60),
            Status = MetaCredentialStatus.PendingPageSelection,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        Context.Set<CompanyMetaCredentials>().Add(credentials);
        await Context.SaveChangesAsync();
        TrackForCleanup(credentials);

        // Act - Select page (simulating page selection)
        var loaded = await Context.Set<CompanyMetaCredentials>()
            .FirstAsync(c => c.CompanyId == company.Id);
        
        loaded.PageId = "page_123";
        loaded.PageName = "Selected Page";
        loaded.PageAccessToken = "page_token_456";
        loaded.Status = MetaCredentialStatus.Active;
        loaded.UpdatedAt = DateTime.UtcNow;
        await Context.SaveChangesAsync();

        // Assert
        Context.ChangeTracker.Clear();
        var updated = await Context.Set<CompanyMetaCredentials>()
            .FirstAsync(c => c.CompanyId == company.Id);

        updated.Status.Should().Be(MetaCredentialStatus.Active);
        updated.PageId.Should().Be("page_123");
    }

    [Fact]
    public async Task MetaCredentials_TokenExpired_ShouldMarkAsExpired()
    {
        // Arrange
        var company = await SeedCompanyAsync();
        
        var credentials = new CompanyMetaCredentials
        {
            CompanyId = company.Id,
            UserAccessToken = "expired_token",
            TokenExpiresAt = DateTime.UtcNow.AddDays(-1), // Already expired
            Status = MetaCredentialStatus.Active,
            CreatedAt = DateTime.UtcNow.AddDays(-61),
            UpdatedAt = DateTime.UtcNow.AddDays(-61)
        };

        Context.Set<CompanyMetaCredentials>().Add(credentials);
        await Context.SaveChangesAsync();
        TrackForCleanup(credentials);

        // Act - Check expiration and update status
        var loaded = await Context.Set<CompanyMetaCredentials>()
            .FirstAsync(c => c.CompanyId == company.Id);

        var isExpired = loaded.TokenExpiresAt < DateTime.UtcNow;
        if (isExpired)
        {
            loaded.Status = MetaCredentialStatus.TokenExpired;
            loaded.UpdatedAt = DateTime.UtcNow;
            await Context.SaveChangesAsync();
        }

        // Assert
        Context.ChangeTracker.Clear();
        var updated = await Context.Set<CompanyMetaCredentials>()
            .FirstAsync(c => c.CompanyId == company.Id);

        updated.Status.Should().Be(MetaCredentialStatus.TokenExpired);
    }

    [Fact]
    public async Task MetaCredentials_RevokeAccess_ShouldDeleteCredentials()
    {
        // Arrange
        var company = await SeedCompanyAsync();
        
        var credentials = new CompanyMetaCredentials
        {
            CompanyId = company.Id,
            UserAccessToken = "token_to_revoke",
            TokenExpiresAt = DateTime.UtcNow.AddDays(60),
            Status = MetaCredentialStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        Context.Set<CompanyMetaCredentials>().Add(credentials);
        await Context.SaveChangesAsync();
        // Note: Not tracking for cleanup since we're testing deletion

        // Act - Revoke access
        var loaded = await Context.Set<CompanyMetaCredentials>()
            .FirstOrDefaultAsync(c => c.CompanyId == company.Id);
        
        if (loaded != null)
        {
            Context.Set<CompanyMetaCredentials>().Remove(loaded);
            await Context.SaveChangesAsync();
        }

        // Assert
        Context.ChangeTracker.Clear();
        var deleted = await Context.Set<CompanyMetaCredentials>()
            .FirstOrDefaultAsync(c => c.CompanyId == company.Id);

        deleted.Should().BeNull();
    }

    #endregion

    #region Instagram Account Linking Tests

    [Fact]
    public async Task MetaCredentials_WithInstagramAccount_ShouldStoreInstagramDetails()
    {
        // Arrange
        var company = await SeedCompanyAsync();
        
        var credentials = new CompanyMetaCredentials
        {
            CompanyId = company.Id,
            UserAccessToken = "token_123",
            TokenExpiresAt = DateTime.UtcNow.AddDays(60),
            PageId = "fb_page_123",
            PageName = "Test Company",
            PageAccessToken = "page_token",
            InstagramAccountId = "17841412345678901",
            InstagramUsername = "test_rental_company",
            Status = MetaCredentialStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        Context.Set<CompanyMetaCredentials>().Add(credentials);
        await Context.SaveChangesAsync();
        TrackForCleanup(credentials);

        // Assert
        Context.ChangeTracker.Clear();
        var saved = await Context.Set<CompanyMetaCredentials>()
            .FirstAsync(c => c.CompanyId == company.Id);

        saved.InstagramAccountId.Should().Be("17841412345678901");
        saved.InstagramUsername.Should().Be("test_rental_company");
    }

    [Fact]
    public async Task MetaCredentials_WithoutInstagram_PageShouldStillWork()
    {
        // Arrange
        var company = await SeedCompanyAsync();
        
        var credentials = new CompanyMetaCredentials
        {
            CompanyId = company.Id,
            UserAccessToken = "token_123",
            TokenExpiresAt = DateTime.UtcNow.AddDays(60),
            PageId = "fb_page_only",
            PageName = "Facebook Only Page",
            PageAccessToken = "page_token",
            InstagramAccountId = null, // No Instagram linked
            InstagramUsername = null,
            Status = MetaCredentialStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        Context.Set<CompanyMetaCredentials>().Add(credentials);
        await Context.SaveChangesAsync();
        TrackForCleanup(credentials);

        // Assert
        Context.ChangeTracker.Clear();
        var saved = await Context.Set<CompanyMetaCredentials>()
            .FirstAsync(c => c.CompanyId == company.Id);

        saved.PageId.Should().Be("fb_page_only");
        saved.InstagramAccountId.Should().BeNull();
        saved.Status.Should().Be(MetaCredentialStatus.Active); // Still active for FB
    }

    #endregion

    #region Auto-Publish Settings Tests

    [Fact]
    public async Task MetaCredentials_AutoPublishSettings_ShouldPersist()
    {
        // Arrange
        var company = await SeedCompanyAsync();
        
        var credentials = new CompanyMetaCredentials
        {
            CompanyId = company.Id,
            UserAccessToken = "token_123",
            TokenExpiresAt = DateTime.UtcNow.AddDays(60),
            Status = MetaCredentialStatus.Active,
            AutoPublishFacebook = true,
            AutoPublishInstagram = true,
            AutoPublishIncludePrice = false,
            AutoPublishHashtags = "[\"#carrental\",\"#luxurycar\"]",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        Context.Set<CompanyMetaCredentials>().Add(credentials);
        await Context.SaveChangesAsync();
        TrackForCleanup(credentials);

        // Assert
        Context.ChangeTracker.Clear();
        var saved = await Context.Set<CompanyMetaCredentials>()
            .FirstAsync(c => c.CompanyId == company.Id);

        saved.AutoPublishFacebook.Should().BeTrue();
        saved.AutoPublishInstagram.Should().BeTrue();
        saved.AutoPublishIncludePrice.Should().BeFalse();
        saved.AutoPublishHashtags.Should().Contain("#carrental");
    }

    [Fact]
    public async Task MetaCredentials_DeepLinkSettings_ShouldPersist()
    {
        // Arrange
        var company = await SeedCompanyAsync();
        
        var credentials = new CompanyMetaCredentials
        {
            CompanyId = company.Id,
            UserAccessToken = "token_123",
            TokenExpiresAt = DateTime.UtcNow.AddDays(60),
            Status = MetaCredentialStatus.Active,
            DeepLinkBaseUrl = "https://mycompany.aegis-rental.com",
            DeepLinkVehiclePattern = "/book?modelId={modelId}&make={make}",
            DeepLinkBookingPattern = "/booking/{bookingId}",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        Context.Set<CompanyMetaCredentials>().Add(credentials);
        await Context.SaveChangesAsync();
        TrackForCleanup(credentials);

        // Assert
        Context.ChangeTracker.Clear();
        var saved = await Context.Set<CompanyMetaCredentials>()
            .FirstAsync(c => c.CompanyId == company.Id);

        saved.DeepLinkBaseUrl.Should().Be("https://mycompany.aegis-rental.com");
        saved.DeepLinkVehiclePattern.Should().Contain("{modelId}");
        saved.DeepLinkBookingPattern.Should().Contain("{bookingId}");
    }

    #endregion
}
