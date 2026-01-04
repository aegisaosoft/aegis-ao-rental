/*
 * CarRental.Tests - RentalAgreementService Integration Tests
 * Tests for SignExistingBookingAsync and related functionality
 * Copyright (c) 2025 Alexander Orlov
 */

using Xunit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using CarRental.Api.Models;
using CarRental.Api.Services;
using CarRental.Api.DTOs;
using CarRental.Tests.Infrastructure;

namespace CarRental.Tests.Services;

/// <summary>
/// Integration tests for RentalAgreementService
/// Tests the SignExistingBookingAsync method and agreement creation
/// </summary>
[Collection("PostgreSQL")]
public class RentalAgreementServiceTests : PostgresTestBase
{
    private RentalAgreementService CreateService()
    {
        var logger = new Mock<ILogger<RentalAgreementService>>();
        var blobStorageService = new Mock<IAzureBlobStorageService>();
        
        // Mock blob storage - IsConfiguredAsync returns false so PDF generation is skipped
        blobStorageService.Setup(x => x.IsConfiguredAsync()).ReturnsAsync(false);
        
        return new RentalAgreementService(Context, logger.Object, blobStorageService.Object);
    }

    private static AgreementDataDto CreateValidAgreementData(string language = "en")
    {
        return new AgreementDataDto
        {
            SignatureImage = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==",
            Language = language,
            Consents = new AgreementConsentsDto
            {
                TermsAcceptedAt = DateTime.UtcNow,
                NonRefundableAcceptedAt = DateTime.UtcNow,
                DamagePolicyAcceptedAt = DateTime.UtcNow,
                CardAuthorizationAcceptedAt = DateTime.UtcNow,
            },
            ConsentTexts = new ConsentTextsDto
            {
                TermsTitle = "Terms and Conditions",
                TermsText = "I agree to the rental terms.",
                NonRefundableTitle = "Non-Refundable",
                NonRefundableText = "I understand this is non-refundable.",
                DamagePolicyTitle = "Damage Policy",
                DamagePolicyText = "I am responsible for damage.",
                CardAuthorizationTitle = "Card Authorization",
                CardAuthorizationText = "I authorize charges.",
            },
            SignedAt = DateTime.UtcNow,
            UserAgent = "Test/1.0",
            Timezone = "America/New_York"
        };
    }

    [Fact]
    public async Task SignExistingBookingAsync_WithValidBooking_ShouldCreateAgreement()
    {
        // Arrange
        var (company, customer, vehicle, booking) = await SeedCompleteScenarioAsync();
        var service = CreateService();
        var agreementData = CreateValidAgreementData();

        // Act
        var agreement = await service.SignExistingBookingAsync(booking.Id, agreementData, "127.0.0.1");

        // Track for cleanup
        TrackForCleanup(agreement);

        // Assert
        agreement.Should().NotBeNull();
        agreement.BookingId.Should().Be(booking.Id);
        agreement.CustomerId.Should().Be(customer.Id);
        agreement.CompanyId.Should().Be(company.Id);
        agreement.VehicleId.Should().Be(vehicle.Id);
        agreement.SignatureImage.Should().NotBeNullOrEmpty();
        agreement.Language.Should().Be("en");
        agreement.Status.Should().Be("active");
        agreement.AgreementNumber.Should().NotBeNullOrEmpty();
        agreement.CustomerName.Should().Contain(customer.FirstName);
        agreement.CustomerEmail.Should().Be(customer.Email);
    }

    [Fact]
    public async Task SignExistingBookingAsync_WithoutSignature_ShouldThrowArgumentException()
    {
        // Arrange
        var (_, _, _, booking) = await SeedCompleteScenarioAsync();
        var service = CreateService();
        
        var agreementData = new AgreementDataDto
        {
            SignatureImage = "", // Empty signature
            Language = "en"
        };

        // Act & Assert
        var act = async () => await service.SignExistingBookingAsync(booking.Id, agreementData);
        
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*SignatureImage*");
    }

    [Fact]
    public async Task SignExistingBookingAsync_WithNonExistentBooking_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var service = CreateService();
        var nonExistentBookingId = Guid.NewGuid();
        var agreementData = CreateValidAgreementData();

        // Act & Assert
        var act = async () => await service.SignExistingBookingAsync(nonExistentBookingId, agreementData);
        
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*Booking {nonExistentBookingId} not found*");
    }

    [Fact]
    public async Task SignExistingBookingAsync_WithExistingAgreement_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var (_, _, _, booking) = await SeedCompleteScenarioAsync();
        var service = CreateService();
        var agreementData = CreateValidAgreementData();

        // Create first agreement
        var firstAgreement = await service.SignExistingBookingAsync(booking.Id, agreementData);
        TrackForCleanup(firstAgreement);

        // Act & Assert - Try to create second agreement
        var act = async () => await service.SignExistingBookingAsync(booking.Id, agreementData);
        
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*Agreement already exists*{booking.Id}*");
    }

    [Fact]
    public async Task SignExistingBookingAsync_ShouldCreateAuditLog()
    {
        // Arrange
        var (_, _, _, booking) = await SeedCompleteScenarioAsync();
        var service = CreateService();
        var agreementData = CreateValidAgreementData();

        // Act
        var agreement = await service.SignExistingBookingAsync(booking.Id, agreementData, "192.168.1.1");
        TrackForCleanup(agreement);

        // Assert - Check audit log was created
        var auditLogs = await Context.RentalAgreementAuditLogs
            .Where(l => l.AgreementId == agreement.Id)
            .ToListAsync();
        
        auditLogs.Should().NotBeEmpty();
        auditLogs.Should().Contain(l => l.Action == "created");
    }

    [Fact]
    public async Task GetByBookingIdAsync_WithExistingAgreement_ShouldReturnAgreement()
    {
        // Arrange
        var (_, _, _, booking) = await SeedCompleteScenarioAsync();
        var service = CreateService();
        var agreementData = CreateValidAgreementData("es");

        var createdAgreement = await service.SignExistingBookingAsync(booking.Id, agreementData);
        TrackForCleanup(createdAgreement);

        // Act
        var retrievedAgreement = await service.GetByBookingIdAsync(booking.Id);

        // Assert
        retrievedAgreement.Should().NotBeNull();
        retrievedAgreement!.Id.Should().Be(createdAgreement.Id);
        retrievedAgreement.Language.Should().Be("es");
    }

    [Fact]
    public async Task GetByBookingIdAsync_WithNoAgreement_ShouldReturnNull()
    {
        // Arrange
        var (_, _, _, booking) = await SeedCompleteScenarioAsync();
        var service = CreateService();

        // Act
        var agreement = await service.GetByBookingIdAsync(booking.Id);

        // Assert
        agreement.Should().BeNull();
    }

    [Fact]
    public async Task SignExistingBookingAsync_ShouldStoreConsentTimestamps()
    {
        // Arrange
        var (_, _, _, booking) = await SeedCompleteScenarioAsync();
        var service = CreateService();
        
        var termsTime = DateTime.UtcNow.AddMinutes(-5);
        var nonRefundableTime = DateTime.UtcNow.AddMinutes(-4);
        var damagePolicyTime = DateTime.UtcNow.AddMinutes(-3);
        var cardAuthTime = DateTime.UtcNow.AddMinutes(-2);
        
        var agreementData = new AgreementDataDto
        {
            SignatureImage = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==",
            Language = "en",
            Consents = new AgreementConsentsDto
            {
                TermsAcceptedAt = termsTime,
                NonRefundableAcceptedAt = nonRefundableTime,
                DamagePolicyAcceptedAt = damagePolicyTime,
                CardAuthorizationAcceptedAt = cardAuthTime,
            },
            SignedAt = DateTime.UtcNow
        };

        // Act
        var agreement = await service.SignExistingBookingAsync(booking.Id, agreementData);
        TrackForCleanup(agreement);

        // Assert
        agreement.TermsAcceptedAt.Should().BeCloseTo(termsTime, TimeSpan.FromSeconds(1));
        agreement.NonRefundableAcceptedAt.Should().BeCloseTo(nonRefundableTime, TimeSpan.FromSeconds(1));
        agreement.DamagePolicyAcceptedAt.Should().BeCloseTo(damagePolicyTime, TimeSpan.FromSeconds(1));
        agreement.CardAuthorizationAcceptedAt.Should().BeCloseTo(cardAuthTime, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task SignExistingBookingAsync_ShouldStoreSignatureHash()
    {
        // Arrange
        var (_, _, _, booking) = await SeedCompleteScenarioAsync();
        var service = CreateService();
        var agreementData = CreateValidAgreementData();

        // Act
        var agreement = await service.SignExistingBookingAsync(booking.Id, agreementData);
        TrackForCleanup(agreement);

        // Assert
        agreement.SignatureHash.Should().NotBeNullOrEmpty();
        agreement.SignatureHash.Should().HaveLength(64); // SHA256 produces 64 hex characters
    }

    [Fact]
    public async Task SignExistingBookingAsync_WithDifferentLanguages_ShouldStoreCorrectLanguage()
    {
        // Arrange
        var service = CreateService();
        var languages = new[] { "en", "es", "pt", "de", "fr" };

        foreach (var language in languages)
        {
            var (_, _, _, booking) = await SeedCompleteScenarioAsync();
            var agreementData = CreateValidAgreementData(language);

            // Act
            var agreement = await service.SignExistingBookingAsync(booking.Id, agreementData);
            TrackForCleanup(agreement);

            // Assert
            agreement.Language.Should().Be(language);
        }
    }

    [Fact]
    public async Task GetByIdAsync_WithExistingAgreement_ShouldReturnAgreement()
    {
        // Arrange
        var (_, _, _, booking) = await SeedCompleteScenarioAsync();
        var service = CreateService();
        var agreementData = CreateValidAgreementData();

        var createdAgreement = await service.SignExistingBookingAsync(booking.Id, agreementData);
        TrackForCleanup(createdAgreement);

        // Act
        var retrievedAgreement = await service.GetByIdAsync(createdAgreement.Id);

        // Assert
        retrievedAgreement.Should().NotBeNull();
        retrievedAgreement!.Id.Should().Be(createdAgreement.Id);
        retrievedAgreement.AgreementNumber.Should().Be(createdAgreement.AgreementNumber);
    }

    [Fact]
    public async Task VoidAgreementAsync_ShouldChangeStatusToVoided()
    {
        // Arrange
        var (_, _, _, booking) = await SeedCompleteScenarioAsync();
        var service = CreateService();
        var agreementData = CreateValidAgreementData();

        var agreement = await service.SignExistingBookingAsync(booking.Id, agreementData);
        TrackForCleanup(agreement);

        // Act
        await service.VoidAgreementAsync(agreement.Id, "Test voiding", "TestUser");

        // Assert
        var voidedAgreement = await service.GetByIdAsync(agreement.Id);
        voidedAgreement.Should().NotBeNull();
        voidedAgreement!.Status.Should().Be("voided");
    }
}

