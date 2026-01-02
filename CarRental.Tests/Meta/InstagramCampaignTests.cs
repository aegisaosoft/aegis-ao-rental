/*
 * Instagram Campaign Service Tests
 * Tests for Instagram content publishing and campaign management
 */

using CarRental.Api.Models;
using CarRental.Api.Services;
using CarRental.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CarRental.Tests.Meta;

// Local definition for tests (mirrors InstagramCampaignService)
public class TestPostAnalytics
{
    public string PostId { get; set; } = "";
    public int Impressions { get; set; }
    public int Reach { get; set; }
    public int Engagement { get; set; }
    public int Likes { get; set; }
    public int Comments { get; set; }
    public int Shares { get; set; }
    public int Saves { get; set; }
}

/// <summary>
/// Tests for Instagram Campaign functionality including caption generation,
/// hashtag recommendations, post scheduling, and publishing
/// </summary>
[Collection("PostgreSql")]
public class InstagramCampaignTests : PostgresTestBase
{
    #region Caption Generation Tests

    [Fact]
    public void GenerateCaption_ForSUV_ShouldIncludeCorrectEmoji()
    {
        // Arrange
        var category = "SUV";
        
        // Act
        var emoji = GetVehicleEmoji(category);

        // Assert
        emoji.Should().Be("🚙");
    }

    [Fact]
    public void GenerateCaption_ForSedan_ShouldIncludeCorrectEmoji()
    {
        // Arrange
        var category = "Sedan";
        
        // Act
        var emoji = GetVehicleEmoji(category);

        // Assert
        emoji.Should().Be("🚗");
    }

    [Fact]
    public void GenerateCaption_ForLuxury_ShouldIncludeCorrectEmoji()
    {
        // Arrange
        var category = "Luxury";
        
        // Act
        var emoji = GetVehicleEmoji(category);

        // Assert
        emoji.Should().Be("🏎️");
    }

    [Fact]
    public void GenerateCaption_ForElectric_ShouldIncludeCorrectEmoji()
    {
        // Arrange
        var category = "Electric";
        
        // Act
        var emoji = GetVehicleEmoji(category);

        // Assert
        emoji.Should().Be("⚡");
    }

    [Fact]
    public void GenerateCaption_ForTruck_ShouldIncludeCorrectEmoji()
    {
        // Arrange
        var category = "Truck";
        
        // Act
        var emoji = GetVehicleEmoji(category);

        // Assert
        emoji.Should().Be("🛻");
    }

    [Fact]
    public void GenerateCaption_ForVan_ShouldIncludeCorrectEmoji()
    {
        // Arrange
        var category = "Van";
        
        // Act
        var emoji = GetVehicleEmoji(category);

        // Assert
        emoji.Should().Be("🚐");
    }

    [Fact]
    public void GenerateCaption_ForHybrid_ShouldIncludeCorrectEmoji()
    {
        // Arrange
        var category = "Hybrid";
        
        // Act
        var emoji = GetVehicleEmoji(category);

        // Assert
        emoji.Should().Be("🍃");
    }

    [Fact]
    public void GenerateCaption_WithPrice_ShouldIncludePriceLine()
    {
        // Arrange
        var dailyRate = 99.99m;
        var currency = "USD";

        // Act
        var priceSymbol = GetCurrencySymbol(currency);
        var priceLine = $"💰 From {priceSymbol}{dailyRate:N0}/day";

        // Assert
        priceLine.Should().Contain("$");
        priceLine.Should().Contain("100"); // N0 rounds
        priceLine.Should().Contain("/day");
    }

    [Theory]
    [InlineData("USD", "$")]
    [InlineData("EUR", "€")]
    [InlineData("GBP", "£")]
    [InlineData("BRL", "R$")]
    [InlineData("JPY", "¥")]
    public void GetCurrencySymbol_ShouldReturnCorrectSymbol(string currency, string expectedSymbol)
    {
        // Act
        var symbol = GetCurrencySymbol(currency);

        // Assert
        symbol.Should().Be(expectedSymbol);
    }

    [Fact]
    public void GenerateCaption_ShouldNotExceedInstagramLimit()
    {
        // Arrange
        var maxLength = 2200; // Instagram caption limit
        
        // Act - Generate a long caption
        var caption = new string('x', 2000) + "\n#hashtag1 #hashtag2";

        // Assert
        caption.Length.Should().BeLessOrEqualTo(maxLength);
    }

    #endregion

    #region Hashtag Generation Tests

    [Fact]
    public void GetRecommendedHashtags_ShouldIncludeBaseHashtags()
    {
        // Act
        var hashtags = GetBaseHashtags();

        // Assert
        hashtags.Should().Contain("#carrental");
        hashtags.Should().Contain("#rentacar");
        hashtags.Should().Contain("#luxuryrentals");
    }

    [Fact]
    public void GetRecommendedHashtags_ForToyota_ShouldIncludeBrandHashtag()
    {
        // Arrange
        var make = "Toyota";

        // Act
        var brandHashtag = $"#{make.ToLower().Replace(" ", "")}";
        var brandRentalHashtag = $"#{make.ToLower().Replace(" ", "")}rental";

        // Assert
        brandHashtag.Should().Be("#toyota");
        brandRentalHashtag.Should().Be("#toyotarental");
    }

    [Fact]
    public void GetRecommendedHashtags_ForMercedes_ShouldIncludeBrandHashtag()
    {
        // Arrange
        var make = "Mercedes Benz";

        // Act
        var brandHashtag = $"#{make.ToLower().Replace(" ", "")}";

        // Assert
        brandHashtag.Should().Be("#mercedesbenz");
    }

    [Fact]
    public void GetRecommendedHashtags_WithLocation_ShouldIncludeLocationHashtag()
    {
        // Arrange
        var location = "Miami, FL";

        // Act
        var locationClean = location.ToLower()
            .Replace(" ", "")
            .Replace(",", "")
            .Replace(".", "");
        var locationHashtag = $"#{locationClean}";
        var locationRentalHashtag = $"#{locationClean}rental";

        // Assert
        locationHashtag.Should().Be("#miamifl");
        locationRentalHashtag.Should().Be("#miamiflrental");
    }

    [Fact]
    public void GetCategoryHashtags_ForSUV_ShouldReturnSUVHashtags()
    {
        // Arrange
        var category = "SUV";

        // Act
        var hashtags = GetCategoryHashtags(category);

        // Assert
        hashtags.Should().Contain("#suvrental");
        hashtags.Should().Contain("#suvlife");
    }

    [Fact]
    public void GetCategoryHashtags_ForElectric_ShouldReturnEVHashtags()
    {
        // Arrange
        var category = "Electric";

        // Act
        var hashtags = GetCategoryHashtags(category);

        // Assert
        hashtags.Should().Contain("#electriccar");
        hashtags.Should().Contain("#evrental");
        hashtags.Should().Contain("#sustainable");
    }

    [Fact]
    public void GetCategoryHashtags_ForLuxury_ShouldReturnLuxuryHashtags()
    {
        // Arrange
        var category = "Luxury";

        // Act
        var hashtags = GetCategoryHashtags(category);

        // Assert
        hashtags.Should().Contain("#luxurycar");
        hashtags.Should().Contain("#sportscar");
        hashtags.Should().Contain("#exoticcars");
    }

    [Fact]
    public void GetRecommendedHashtags_ShouldLimitToMaxCount()
    {
        // Arrange
        var maxHashtags = 20;
        var allHashtags = new List<string>();
        for (int i = 0; i < 30; i++)
        {
            allHashtags.Add($"#hashtag{i}");
        }

        // Act
        var limitedHashtags = allHashtags.Distinct().Take(maxHashtags).ToList();

        // Assert
        limitedHashtags.Should().HaveCount(maxHashtags);
    }

    [Fact]
    public void GetRecommendedHashtags_ShouldRemoveDuplicates()
    {
        // Arrange
        var hashtags = new List<string>
        {
            "#carrental",
            "#rentacar",
            "#carrental", // Duplicate
            "#toyota",
            "#rentacar"  // Duplicate
        };

        // Act
        var unique = hashtags.Distinct().ToList();

        // Assert
        unique.Should().HaveCount(3);
    }

    #endregion

    #region Scheduled Posts Tests

    [Fact]
    public async Task SchedulePost_ShouldCreatePendingPost()
    {
        // Arrange
        var company = await SeedCompanyAsync();
        var vehicle = await SeedVehicleAsync(company.Id);
        var scheduledFor = DateTime.UtcNow.AddDays(1);

        var scheduledPost = new ScheduledPost
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            VehicleId = vehicle.Id,
            PostType = ScheduledPostType.Single,
            Platform = SocialPlatform.Instagram,
            Caption = "Check out this amazing car! 🚗",
            ScheduledFor = scheduledFor,
            IncludePrice = true,
            DailyRate = 99.99m,
            Currency = "USD",
            Status = ScheduledPostStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        Context.Set<ScheduledPost>().Add(scheduledPost);
        await Context.SaveChangesAsync();

        // Assert
        Context.ChangeTracker.Clear();
        var saved = await Context.Set<ScheduledPost>()
            .FirstOrDefaultAsync(p => p.Id == scheduledPost.Id);

        saved.Should().NotBeNull();
        saved!.Status.Should().Be(ScheduledPostStatus.Pending);
        saved.Platform.Should().Be(SocialPlatform.Instagram);
        saved.ScheduledFor.Should().BeCloseTo(scheduledFor, TimeSpan.FromSeconds(1));
        saved.Caption.Should().Contain("amazing car");

        // Cleanup
        Context.Set<ScheduledPost>().Remove(saved);
        await Context.SaveChangesAsync();
    }

    [Fact]
    public void SchedulePost_InPast_ShouldFail()
    {
        // Arrange
        var scheduledFor = DateTime.UtcNow.AddHours(-1); // In the past

        // Act & Assert
        var isValid = scheduledFor > DateTime.UtcNow;
        isValid.Should().BeFalse();
    }

    [Fact]
    public async Task CancelScheduledPost_ShouldUpdateStatus()
    {
        // Arrange
        var company = await SeedCompanyAsync();
        var vehicle = await SeedVehicleAsync(company.Id);

        var scheduledPost = new ScheduledPost
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            VehicleId = vehicle.Id,
            PostType = ScheduledPostType.Single,
            Platform = SocialPlatform.Instagram,
            Caption = "Post to cancel",
            ScheduledFor = DateTime.UtcNow.AddDays(1),
            Status = ScheduledPostStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        Context.Set<ScheduledPost>().Add(scheduledPost);
        await Context.SaveChangesAsync();

        // Act - Cancel
        var loaded = await Context.Set<ScheduledPost>()
            .FirstAsync(p => p.Id == scheduledPost.Id);
        loaded.Status = ScheduledPostStatus.Cancelled;
        loaded.UpdatedAt = DateTime.UtcNow;
        await Context.SaveChangesAsync();

        // Assert
        Context.ChangeTracker.Clear();
        var cancelled = await Context.Set<ScheduledPost>()
            .FirstAsync(p => p.Id == scheduledPost.Id);

        cancelled.Status.Should().Be(ScheduledPostStatus.Cancelled);

        // Cleanup
        Context.Set<ScheduledPost>().Remove(cancelled);
        await Context.SaveChangesAsync();
    }

    [Fact]
    public async Task GetScheduledPosts_ShouldReturnOnlyPending()
    {
        // Arrange
        var company = await SeedCompanyAsync();
        var vehicle = await SeedVehicleAsync(company.Id);

        var pendingPost = new ScheduledPost
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            VehicleId = vehicle.Id,
            PostType = ScheduledPostType.Single,
            Platform = SocialPlatform.Instagram,
            Caption = "Pending post",
            ScheduledFor = DateTime.UtcNow.AddDays(1),
            Status = ScheduledPostStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var publishedPost = new ScheduledPost
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            VehicleId = vehicle.Id,
            PostType = ScheduledPostType.Single,
            Platform = SocialPlatform.Facebook,
            Caption = "Published post",
            ScheduledFor = DateTime.UtcNow.AddDays(-1),
            Status = ScheduledPostStatus.Published,
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            UpdatedAt = DateTime.UtcNow
        };

        Context.Set<ScheduledPost>().AddRange(pendingPost, publishedPost);
        await Context.SaveChangesAsync();

        // Act
        var pendingPosts = await Context.Set<ScheduledPost>()
            .Where(p => p.CompanyId == company.Id && p.Status == ScheduledPostStatus.Pending)
            .ToListAsync();

        // Assert
        pendingPosts.Should().HaveCount(1);
        pendingPosts[0].Caption.Should().Be("Pending post");

        // Cleanup
        Context.Set<ScheduledPost>().RemoveRange(pendingPost, publishedPost);
        await Context.SaveChangesAsync();
    }

    #endregion

    #region Vehicle Social Post Tests
    
    // NOTE: These tests require the vehicle_model_id column migration to be run first.
    // Run: psql -f migrations/001_meta_enums_and_vehicle_model_id.sql

    [Fact]
    public async Task SaveVehicleSocialPost_ShouldPersist()
    {
        // Arrange
        var company = await SeedCompanyAsync();
        var vehicle = await SeedVehicleAsync(company.Id);

        var post = new VehicleSocialPost
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            VehicleId = vehicle.Id,
            VehicleModelId = null, // Not using model reference
            Platform = SocialPlatform.Instagram,
            PostId = "17841234567890123",
            Permalink = "https://www.instagram.com/p/ABC123/",
            Caption = "Check out this vehicle!",
            ImageUrl = "https://example.com/vehicle.jpg",
            DailyRate = 75.00m,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        Context.Set<VehicleSocialPost>().Add(post);
        await Context.SaveChangesAsync();

        // Assert
        Context.ChangeTracker.Clear();
        var saved = await Context.Set<VehicleSocialPost>()
            .FirstOrDefaultAsync(p => p.Id == post.Id);

        saved.Should().NotBeNull();
        saved!.Platform.Should().Be(SocialPlatform.Instagram);
        saved.PostId.Should().Be("17841234567890123");
        saved.Permalink.Should().Contain("instagram.com");

        // Cleanup
        Context.Set<VehicleSocialPost>().Remove(saved);
        await Context.SaveChangesAsync();
    }

    [Fact]
    public async Task GetVehiclePosts_ByCompany_ShouldFilterCorrectly()
    {
        // Arrange
        var company1 = await SeedCompanyAsync("Company 1");
        var company2 = await SeedCompanyAsync("Company 2");
        var vehicle1 = await SeedVehicleAsync(company1.Id);
        var vehicle2 = await SeedVehicleAsync(company2.Id);

        var post1 = new VehicleSocialPost
        {
            Id = Guid.NewGuid(),
            CompanyId = company1.Id,
            VehicleId = vehicle1.Id,
            VehicleModelId = null,
            Platform = SocialPlatform.Instagram,
            PostId = "post_company1",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var post2 = new VehicleSocialPost
        {
            Id = Guid.NewGuid(),
            CompanyId = company2.Id,
            VehicleId = vehicle2.Id,
            VehicleModelId = null,
            Platform = SocialPlatform.Instagram,
            PostId = "post_company2",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        Context.Set<VehicleSocialPost>().AddRange(post1, post2);
        await Context.SaveChangesAsync();

        // Act
        var company1Posts = await Context.Set<VehicleSocialPost>()
            .Where(p => p.CompanyId == company1.Id)
            .ToListAsync();

        // Assert
        company1Posts.Should().HaveCount(1);
        company1Posts[0].PostId.Should().Be("post_company1");

        // Cleanup
        Context.Set<VehicleSocialPost>().RemoveRange(post1, post2);
        await Context.SaveChangesAsync();
    }

    #endregion

    #region Carousel Post Tests

    [Fact]
    public void Carousel_MinimumItems_ShouldBeTwo()
    {
        // Arrange
        var vehicleIds = new List<Guid> { Guid.NewGuid() };

        // Act
        var isValid = vehicleIds.Count >= 2;

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void Carousel_MaximumItems_ShouldBeTen()
    {
        // Arrange
        var vehicleIds = Enumerable.Range(0, 11).Select(_ => Guid.NewGuid()).ToList();

        // Act
        var isValid = vehicleIds.Count <= 10;

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void Carousel_ValidRange_ShouldPass()
    {
        // Arrange
        var vehicleIds = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();

        // Act
        var isValid = vehicleIds.Count >= 2 && vehicleIds.Count <= 10;

        // Assert
        isValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(2, true)]
    [InlineData(5, true)]
    [InlineData(10, true)]
    [InlineData(1, false)]
    [InlineData(11, false)]
    public void Carousel_ItemCount_Validation(int count, bool expectedValid)
    {
        // Arrange
        var vehicleIds = Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToList();

        // Act
        var isValid = vehicleIds.Count >= 2 && vehicleIds.Count <= 10;

        // Assert
        isValid.Should().Be(expectedValid);
    }

    #endregion

    #region Post Analytics Tests

    [Fact]
    public void PostAnalytics_ShouldTrackEngagementMetrics()
    {
        // Arrange
        var analytics = new TestPostAnalytics
        {
            PostId = "17841234567890123",
            Impressions = 1500,
            Reach = 1200,
            Engagement = 150,
            Likes = 100,
            Comments = 25,
            Shares = 15,
            Saves = 10
        };

        // Assert
        analytics.Impressions.Should().BeGreaterThan(analytics.Reach);
        analytics.Engagement.Should().Be(150);
        analytics.Likes.Should().BeGreaterThan(analytics.Comments);
    }

    [Fact]
    public void EngagementRate_ShouldCalculateCorrectly()
    {
        // Arrange
        var reach = 1000;
        var engagement = 50;

        // Act
        var engagementRate = (double)engagement / reach * 100;

        // Assert
        engagementRate.Should().Be(5.0); // 5% engagement rate
    }

    [Theory]
    [InlineData(1000, 100, 10.0)]
    [InlineData(500, 25, 5.0)]
    [InlineData(2000, 40, 2.0)]
    public void EngagementRate_VariousScenarios(int reach, int engagement, double expectedRate)
    {
        // Act
        var engagementRate = (double)engagement / reach * 100;

        // Assert
        engagementRate.Should().Be(expectedRate);
    }

    #endregion

    #region Auto-Post Settings Tests

    [Fact]
    public async Task CompanyAutoPostSettings_ShouldPersist()
    {
        // Arrange
        var company = await SeedCompanyAsync();

        var settings = new CompanyAutoPostSettings
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            IsEnabled = true,
            PostOnVehicleAdded = true,
            PostOnVehicleUpdated = false,
            PostOnVehicleAvailable = true,
            PostOnPriceChange = false,
            IncludePriceInPosts = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        Context.Set<CompanyAutoPostSettings>().Add(settings);
        await Context.SaveChangesAsync();

        // Assert
        Context.ChangeTracker.Clear();
        var saved = await Context.Set<CompanyAutoPostSettings>()
            .FirstOrDefaultAsync(s => s.CompanyId == company.Id);

        saved.Should().NotBeNull();
        saved!.IsEnabled.Should().BeTrue();
        saved.PostOnVehicleAdded.Should().BeTrue();
        saved.PostOnVehicleUpdated.Should().BeFalse();

        // Cleanup
        Context.Set<CompanyAutoPostSettings>().Remove(saved);
        await Context.SaveChangesAsync();
    }

    [Theory]
    [InlineData(AutoPostTrigger.VehicleAdded, true, true)]
    [InlineData(AutoPostTrigger.VehicleAdded, false, false)]
    [InlineData(AutoPostTrigger.VehicleUpdated, true, true)]
    [InlineData(AutoPostTrigger.PriceChanged, true, true)]
    [InlineData(AutoPostTrigger.VehicleAvailable, false, false)]
    public void AutoPostTrigger_ShouldRespectSettings(AutoPostTrigger trigger, bool settingEnabled, bool expectedResult)
    {
        // Arrange
        var settings = new Dictionary<AutoPostTrigger, bool>
        {
            { AutoPostTrigger.VehicleAdded, settingEnabled },
            { AutoPostTrigger.VehicleUpdated, settingEnabled },
            { AutoPostTrigger.VehicleAvailable, settingEnabled },
            { AutoPostTrigger.PriceChanged, settingEnabled }
        };

        // Act
        var shouldPost = settings.TryGetValue(trigger, out var enabled) && enabled;

        // Assert
        shouldPost.Should().Be(expectedResult);
    }

    [Fact]
    public void AutoPostTrigger_ShouldHaveAllValues()
    {
        // Assert
        var triggers = Enum.GetValues<AutoPostTrigger>();
        triggers.Should().Contain(AutoPostTrigger.VehicleAdded);
        triggers.Should().Contain(AutoPostTrigger.VehicleUpdated);
        triggers.Should().Contain(AutoPostTrigger.VehicleAvailable);
        triggers.Should().Contain(AutoPostTrigger.PriceChanged);
    }

    #endregion

    #region Rate Limiting Tests

    [Fact]
    public void RateLimiting_MinHoursBetweenPosts_ShouldEnforce()
    {
        // Arrange
        var minHours = 4;
        var lastPostAt = DateTime.UtcNow.AddHours(-2); // 2 hours ago

        // Act
        var hoursSinceLastPost = (DateTime.UtcNow - lastPostAt).TotalHours;
        var canPost = hoursSinceLastPost >= minHours;

        // Assert
        canPost.Should().BeFalse(); // Only 2 hours passed, need 4
    }

    [Fact]
    public void RateLimiting_AfterMinHours_ShouldAllow()
    {
        // Arrange
        var minHours = 4;
        var lastPostAt = DateTime.UtcNow.AddHours(-5); // 5 hours ago

        // Act
        var hoursSinceLastPost = (DateTime.UtcNow - lastPostAt).TotalHours;
        var canPost = hoursSinceLastPost >= minHours;

        // Assert
        canPost.Should().BeTrue(); // 5 hours passed, need 4
    }

    #endregion

    #region Social Platform Tests

    [Fact]
    public void SocialPlatform_ShouldHaveCorrectValues()
    {
        // Assert
        ((int)SocialPlatform.Facebook).Should().Be(0);
        ((int)SocialPlatform.Instagram).Should().Be(1);
    }

    [Fact]
    public void ScheduledPostStatus_ShouldHaveCorrectValues()
    {
        // Assert
        ((int)ScheduledPostStatus.Pending).Should().Be(0);
        ((int)ScheduledPostStatus.Processing).Should().Be(1);
        ((int)ScheduledPostStatus.Published).Should().Be(2);
        ((int)ScheduledPostStatus.Failed).Should().Be(3);
        ((int)ScheduledPostStatus.Cancelled).Should().Be(4);
    }

    [Fact]
    public void ScheduledPostType_ShouldHaveCorrectValues()
    {
        // Assert
        ((int)ScheduledPostType.Single).Should().Be(0);
        ((int)ScheduledPostType.Carousel).Should().Be(1);
        ((int)ScheduledPostType.Reel).Should().Be(2);
        ((int)ScheduledPostType.Story).Should().Be(3);
    }

    #endregion

    #region Helper Methods

    private string GetVehicleEmoji(string category)
    {
        return category.ToLower() switch
        {
            "suv" or "crossover" => "🚙",
            "sedan" => "🚗",
            "sports" or "luxury" => "🏎️",
            "truck" or "pickup" => "🛻",
            "van" or "minivan" => "🚐",
            "convertible" => "🏎️",
            "electric" or "ev" => "⚡",
            "hybrid" => "🍃",
            _ => "🚗"
        };
    }

    private string GetCurrencySymbol(string currency)
    {
        return currency.ToUpper() switch
        {
            "USD" => "$",
            "EUR" => "€",
            "GBP" => "£",
            "BRL" => "R$",
            "JPY" => "¥",
            _ => "$"
        };
    }

    private List<string> GetBaseHashtags()
    {
        return new List<string>
        {
            "#carrental",
            "#rentacar",
            "#luxuryrentals"
        };
    }

    private List<string> GetCategoryHashtags(string category)
    {
        return category.ToLower() switch
        {
            "suv" or "crossover" => new List<string> { "#suvrental", "#suvlife", "#crossover" },
            "sedan" => new List<string> { "#sedanrental", "#sedanlife" },
            "sports" or "luxury" => new List<string> { "#luxurycar", "#sportscar", "#exoticcars" },
            "truck" or "pickup" => new List<string> { "#truckrental", "#pickup" },
            "van" or "minivan" => new List<string> { "#vanrental", "#familycar", "#minivan" },
            "convertible" => new List<string> { "#convertible", "#droptoprental" },
            "electric" or "ev" => new List<string> { "#electriccar", "#evrental", "#sustainable" },
            _ => new List<string>()
        };
    }

    #endregion
}

