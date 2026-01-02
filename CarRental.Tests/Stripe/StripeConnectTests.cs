/*
 * CarRental.Tests - Stripe Connect Tests
 * Copyright (c) 2025 Alexander Orlov
 * 
 * Tests for Stripe Connect account management and transfers
 */

using Xunit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using CarRental.Api.Models;
using CarRental.Tests.Infrastructure;

namespace CarRental.Tests.Stripe;

/// <summary>
/// Tests for Stripe Connect operations
/// Tests account linking, onboarding status, and fund transfers
/// </summary>
[Collection("PostgreSQL")]
public class StripeConnectTests : PostgresTestBase
{
    #region Connected Account Tests

    [Fact]
    public async Task CreateConnectedAccount_ShouldLinkToCompany()
    {
        // Arrange
        var company = await SeedCompanyAsync();
        
        // Create StripeSettings first (required for StripeCompany)
        var stripeSettings = new StripeSettings
        {
            Id = Guid.NewGuid(),
            Name = $"test-{Guid.NewGuid().ToString()[..6]}",
            SecretKey = "sk_test_123",
            PublishableKey = "pk_test_123",
            CreatedAt = DateTime.UtcNow
        };
        Context.StripeSettings.Add(stripeSettings);
        await Context.SaveChangesAsync();
        TrackForCleanup(stripeSettings);

        // Act - Create StripeCompany record (this is where StripeAccountId is stored)
        var stripeCompany = new StripeCompany
        {
            Id = Guid.NewGuid(),
            SettingsId = stripeSettings.Id,
            CompanyId = company.Id,
            StripeAccountId = "acct_test_new_123",
            CreatedAt = DateTime.UtcNow
        };
        Context.StripeCompanies.Add(stripeCompany);
        await Context.SaveChangesAsync();
        TrackForCleanup(stripeCompany);

        // Assert - verify StripeCompany was created and linked
        Context.ChangeTracker.Clear();
        var verifyStripeCompany = await Context.StripeCompanies
            .FirstOrDefaultAsync(sc => sc.CompanyId == company.Id);
        
        verifyStripeCompany.Should().NotBeNull();
        verifyStripeCompany!.StripeAccountId.Should().Be("acct_test_new_123");
        verifyStripeCompany.SettingsId.Should().Be(stripeSettings.Id);
    }

    [Fact]
    public async Task OnboardingComplete_ShouldEnablePayments()
    {
        // Arrange
        var company = await SeedCompanyAsync();
        company.StripeAccountId = "acct_onboarding_123";
        company.StripeChargesEnabled = false;
        company.StripePayoutsEnabled = false;
        company.StripeDetailsSubmitted = false;
        company.StripeOnboardingCompleted = false;
        await Context.SaveChangesAsync();

        // Act - Simulate onboarding completion
        var loadedCompany = await Context.Companies.FindAsync(company.Id);
        loadedCompany!.StripeChargesEnabled = true;
        loadedCompany.StripePayoutsEnabled = true;
        loadedCompany.StripeDetailsSubmitted = true;
        loadedCompany.StripeOnboardingCompleted = true;
        loadedCompany.StripeLastSyncAt = DateTime.UtcNow;
        await Context.SaveChangesAsync();

        // Assert
        Context.ChangeTracker.Clear();
        var verifyCompany = await Context.Companies.FindAsync(company.Id);
        verifyCompany!.StripeOnboardingCompleted.Should().BeTrue();
        verifyCompany.StripeChargesEnabled.Should().BeTrue();
        verifyCompany.StripePayoutsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task AccountWithPendingRequirements_ShouldTrackRequirements()
    {
        // Arrange
        var company = await SeedCompanyAsync();
        company.StripeAccountId = "acct_requirements_123";
        company.StripeOnboardingCompleted = false;
        await Context.SaveChangesAsync();

        // Act - Simulate account with pending requirements
        var loadedCompany = await Context.Companies.FindAsync(company.Id);
        loadedCompany!.StripeChargesEnabled = false;
        loadedCompany.StripePayoutsEnabled = false;
        loadedCompany.StripeDetailsSubmitted = true;
        loadedCompany.StripeRequirementsCurrentlyDue = new[] 
        { 
            "individual.id_number",
            "individual.verification.document"
        };
        loadedCompany.StripeRequirementsEventuallyDue = new[]
        {
            "business_profile.annual_revenue"
        };
        loadedCompany.StripeRequirementsPastDue = Array.Empty<string>();
        await Context.SaveChangesAsync();

        // Assert
        Context.ChangeTracker.Clear();
        var verifyCompany = await Context.Companies.FindAsync(company.Id);
        verifyCompany!.StripeRequirementsCurrentlyDue.Should().HaveCount(2);
        verifyCompany.StripeRequirementsCurrentlyDue.Should().Contain("individual.id_number");
        verifyCompany.StripeChargesEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task AccountWithPastDueRequirements_ShouldBeRestricted()
    {
        // Arrange
        var company = await SeedCompanyAsync();
        company.StripeAccountId = "acct_pastdue_123";
        company.StripeChargesEnabled = true;
        company.StripeOnboardingCompleted = true;
        await Context.SaveChangesAsync();

        // Act - Simulate requirements becoming past due
        var loadedCompany = await Context.Companies.FindAsync(company.Id);
        loadedCompany!.StripeRequirementsPastDue = new[]
        {
            "individual.verification.document"
        };
        loadedCompany.StripeRequirementsDisabledReason = "requirements.past_due";
        loadedCompany.StripeChargesEnabled = false; // Disabled due to past due
        await Context.SaveChangesAsync();

        // Assert
        Context.ChangeTracker.Clear();
        var verifyCompany = await Context.Companies.FindAsync(company.Id);
        verifyCompany!.StripeRequirementsPastDue.Should().NotBeEmpty();
        verifyCompany.StripeRequirementsDisabledReason.Should().Contain("past_due");
        verifyCompany.StripeChargesEnabled.Should().BeFalse();
    }

    #endregion

    #region StripeCompany Record Tests

    [Fact]
    public async Task StripeCompany_ShouldLinkCompanyToSettings()
    {
        // Arrange
        var company = await SeedCompanyAsync();
        
        // Create StripeSettings record
        var stripeSettings = new StripeSettings
        {
            Id = Guid.NewGuid(),
            Name = $"test-settings-{Guid.NewGuid().ToString()[..6]}",
            SecretKey = "sk_test_encrypted",
            PublishableKey = "pk_test_123",
            CreatedAt = DateTime.UtcNow
        };
        Context.StripeSettings.Add(stripeSettings);
        await Context.SaveChangesAsync();
        TrackForCleanup(stripeSettings);

        // Link company to stripe settings
        company.StripeSettingsId = stripeSettings.Id;
        await Context.SaveChangesAsync();

        // Create StripeCompany record
        var stripeCompany = new StripeCompany
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            SettingsId = stripeSettings.Id,
            StripeAccountId = "acct_encrypted_123",
            CreatedAt = DateTime.UtcNow
        };
        Context.StripeCompanies.Add(stripeCompany);
        await Context.SaveChangesAsync();

        // Assert
        Context.ChangeTracker.Clear();
        var verifyStripeCompany = await Context.StripeCompanies
            .FirstOrDefaultAsync(sc => sc.CompanyId == company.Id);
        
        verifyStripeCompany.Should().NotBeNull();
        verifyStripeCompany!.SettingsId.Should().Be(stripeSettings.Id);
        verifyStripeCompany.StripeAccountId.Should().Be("acct_encrypted_123");
        
        // Cleanup tracked via TrackForCleanup
        TrackForCleanup(stripeCompany);
    }

    #endregion

    #region Transfer Tests

    [Fact]
    public async Task CreateTransfer_ShouldRecordTransferDetails()
    {
        // Arrange
        var (company, customer, vehicle, booking) = await SeedCompleteScenarioAsync();
        booking.Status = "Confirmed";
        booking.TotalAmount = 500m;
        await Context.SaveChangesAsync();

        // Act - Simulate transfer creation
        var transfer = new StripeTransfer
        {
            Id = Guid.NewGuid(),
            BookingId = booking.Id,
            CompanyId = company.Id,
            StripeTransferId = "tr_test_123",
            Amount = 500m,
            PlatformFee = 50m, // 10% platform fee
            NetAmount = 450m,
            Currency = "USD",
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };
        Context.StripeTransfers.Add(transfer);
        await Context.SaveChangesAsync();

        // Assert
        Context.ChangeTracker.Clear();
        var verifyTransfer = await Context.StripeTransfers
            .FirstOrDefaultAsync(t => t.BookingId == booking.Id);
        
        verifyTransfer.Should().NotBeNull();
        verifyTransfer!.Amount.Should().Be(500m);
        verifyTransfer.PlatformFee.Should().Be(50m);
        verifyTransfer.NetAmount.Should().Be(450m);
        verifyTransfer.Status.Should().Be("pending");

        // Cleanup
        Context.StripeTransfers.Remove(verifyTransfer);
        await Context.SaveChangesAsync();
    }

    [Fact]
    public async Task TransferPaid_ShouldUpdateStatus()
    {
        // Arrange
        var (company, _, _, booking) = await SeedCompleteScenarioAsync();
        var transfer = new StripeTransfer
        {
            Id = Guid.NewGuid(),
            BookingId = booking.Id,
            CompanyId = company.Id,
            StripeTransferId = "tr_paid_test_123",
            Amount = 300m,
            PlatformFee = 30m,
            NetAmount = 270m,
            Currency = "USD",
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };
        Context.StripeTransfers.Add(transfer);
        await Context.SaveChangesAsync();

        // Act - Simulate transfer.paid webhook
        var loadedTransfer = await Context.StripeTransfers.FindAsync(transfer.Id);
        loadedTransfer!.Status = "paid";
        loadedTransfer.TransferredAt = DateTime.UtcNow;
        loadedTransfer.UpdatedAt = DateTime.UtcNow;
        await Context.SaveChangesAsync();

        // Assert
        Context.ChangeTracker.Clear();
        var verifyTransfer = await Context.StripeTransfers.FindAsync(transfer.Id);
        verifyTransfer!.Status.Should().Be("paid");
        verifyTransfer.TransferredAt.Should().NotBeNull();

        // Cleanup
        Context.StripeTransfers.Remove(verifyTransfer);
        await Context.SaveChangesAsync();
    }

    [Fact]
    public async Task TransferFailed_ShouldRecordFailureReason()
    {
        // Arrange
        var (company, _, _, booking) = await SeedCompleteScenarioAsync();
        var transfer = new StripeTransfer
        {
            Id = Guid.NewGuid(),
            BookingId = booking.Id,
            CompanyId = company.Id,
            StripeTransferId = "tr_failed_test_123",
            Amount = 300m,
            PlatformFee = 30m,
            NetAmount = 270m,
            Currency = "USD",
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };
        Context.StripeTransfers.Add(transfer);
        await Context.SaveChangesAsync();

        // Act - Simulate transfer.failed webhook
        var loadedTransfer = await Context.StripeTransfers.FindAsync(transfer.Id);
        loadedTransfer!.Status = "failed";
        loadedTransfer.FailureCode = "insufficient_funds";
        loadedTransfer.FailureMessage = "Insufficient funds in platform account";
        loadedTransfer.UpdatedAt = DateTime.UtcNow;
        await Context.SaveChangesAsync();

        // Assert
        Context.ChangeTracker.Clear();
        var verifyTransfer = await Context.StripeTransfers.FindAsync(transfer.Id);
        verifyTransfer!.Status.Should().Be("failed");
        verifyTransfer.FailureCode.Should().Be("insufficient_funds");
        verifyTransfer.FailureMessage.Should().NotBeNullOrEmpty();

        // Cleanup
        Context.StripeTransfers.Remove(verifyTransfer);
        await Context.SaveChangesAsync();
    }

    #endregion

    #region Platform Fee Calculation Tests

    [Theory]
    [InlineData(100, 10, 10, 90)]   // 10% fee
    [InlineData(500, 10, 50, 450)]  // 10% fee
    [InlineData(1000, 5, 50, 950)]  // 5% fee
    [InlineData(250, 15, 37.50, 212.50)] // 15% fee
    public void PlatformFeeCalculation_ShouldBeCorrect(
        decimal totalAmount, decimal feePercentage, decimal expectedFee, decimal expectedNet)
    {
        // Arrange & Act
        var platformFee = Math.Round(totalAmount * (feePercentage / 100), 2);
        var netAmount = totalAmount - platformFee;

        // Assert
        platformFee.Should().Be(expectedFee);
        netAmount.Should().Be(expectedNet);
    }

    #endregion

    #region Payout Tests

    [Fact]
    public async Task PayoutPaid_ShouldRecordPayoutDetails()
    {
        // Arrange
        var company = await SeedCompanyAsync();
        company.StripeAccountId = "acct_payout_test_123";
        await Context.SaveChangesAsync();

        // Act - Simulate payout.paid webhook
        var payout = new StripePayoutRecord
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            StripePayoutId = "po_test_123",
            Amount = 1000m,
            Currency = "USD",
            Status = "paid",
            PayoutType = "bank_account",
            ArrivalDate = DateTime.UtcNow.AddDays(2),
            Description = "Weekly payout",
            Method = "standard",
            CreatedAt = DateTime.UtcNow
        };
        Context.StripePayoutRecords.Add(payout);
        await Context.SaveChangesAsync();

        // Assert
        Context.ChangeTracker.Clear();
        var verifyPayout = await Context.StripePayoutRecords
            .FirstOrDefaultAsync(p => p.CompanyId == company.Id);
        
        verifyPayout.Should().NotBeNull();
        verifyPayout!.Amount.Should().Be(1000m);
        verifyPayout.Status.Should().Be("paid");
        verifyPayout.ArrivalDate.Should().NotBeNull();

        // Cleanup
        Context.StripePayoutRecords.Remove(verifyPayout);
        await Context.SaveChangesAsync();
    }

    [Fact]
    public async Task PayoutFailed_ShouldRecordFailureDetails()
    {
        // Arrange
        var company = await SeedCompanyAsync();
        company.StripeAccountId = "acct_payout_fail_123";
        await Context.SaveChangesAsync();

        // Act - Create payout and mark as failed
        var payout = new StripePayoutRecord
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            StripePayoutId = "po_failed_123",
            Amount = 500m,
            Currency = "USD",
            Status = "failed",
            PayoutType = "bank_account",
            FailureCode = "account_closed",
            FailureMessage = "The bank account has been closed",
            CreatedAt = DateTime.UtcNow
        };
        Context.StripePayoutRecords.Add(payout);
        await Context.SaveChangesAsync();

        // Assert
        Context.ChangeTracker.Clear();
        var verifyPayout = await Context.StripePayoutRecords.FindAsync(payout.Id);
        verifyPayout!.Status.Should().Be("failed");
        verifyPayout.FailureCode.Should().Be("account_closed");
        verifyPayout.FailureMessage.Should().Contain("closed");

        // Cleanup
        Context.StripePayoutRecords.Remove(verifyPayout);
        await Context.SaveChangesAsync();
    }

    #endregion

    #region Account Status Determination Tests

    [Theory]
    [InlineData(null, false, false, false, "not_started")]
    [InlineData("acct_123", false, false, false, "onboarding")]
    [InlineData("acct_123", true, true, true, "active")]
    [InlineData("acct_123", false, true, true, "restricted")]
    public void DetermineAccountStatus_ShouldReturnCorrectStatus(
        string? stripeAccountId, 
        bool chargesEnabled, 
        bool payoutsEnabled, 
        bool onboardingCompleted,
        string expectedStatus)
    {
        // Arrange & Act
        var status = DetermineAccountStatus(stripeAccountId, chargesEnabled, payoutsEnabled, onboardingCompleted, false);

        // Assert
        status.Should().Be(expectedStatus);
    }

    private static string DetermineAccountStatus(
        string? stripeAccountId,
        bool chargesEnabled,
        bool payoutsEnabled,
        bool onboardingCompleted,
        bool hasPastDueRequirements)
    {
        if (string.IsNullOrEmpty(stripeAccountId))
            return "not_started";

        if (!onboardingCompleted)
            return "onboarding";

        if (hasPastDueRequirements)
            return "past_due";

        if (!chargesEnabled || !payoutsEnabled)
            return "restricted";

        return "active";
    }

    #endregion
}
