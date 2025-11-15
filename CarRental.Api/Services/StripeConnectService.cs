/*
 *
 * Copyright (c) 2025 Alexander Orlov.
 * 34 Middletown Ave Atlantic Highlands NJ 07716
 *
 * THIS SOFTWARE IS THE CONFIDENTIAL AND PROPRIETARY INFORMATION OF
 * Alexander Orlov. ("CONFIDENTIAL INFORMATION"). YOU SHALL NOT DISCLOSE
 * SUCH CONFIDENTIAL INFORMATION AND SHALL USE IT ONLY IN ACCORDANCE
 * WITH THE TERMS OF THE LICENSE AGREEMENT YOU ENTERED INTO WITH
 * Alexander Orlov.
 *
 * Author: Alexander Orlov
 *
 */

using Stripe;
using Microsoft.EntityFrameworkCore;
using CarRental.Api.Data;
using CarRental.Api.Models;
using CarRental.Api.DTOs.Stripe;

namespace CarRental.Api.Services;

public interface IStripeConnectService
{
    Task<(bool Success, string AccountId, string? Error)> CreateConnectedAccountAsync(Guid companyId);
    Task<(bool Success, string OnboardingUrl, string? Error)> StartOnboardingAsync(Guid companyId, string returnUrl, string refreshUrl);
    Task<StripeAccountStatusDto> GetAccountStatusAsync(Guid companyId);
    Task SyncAccountStatusAsync(string stripeAccountId);
    Task<(bool Success, Guid? TransferId, string? Error)> TransferBookingFundsAsync(Guid bookingId);
    Task<(bool Success, string? PaymentIntentId, string? Error)> AuthorizeSecurityDepositAsync(
        Guid bookingId, 
        string paymentMethodId, 
        decimal amount);
}

public class StripeConnectService : IStripeConnectService
{
    private readonly CarRentalDbContext _context;
    private readonly IStripeService _stripeService;
    private readonly IEncryptionService _encryptionService;
    private readonly ILogger<StripeConnectService> _logger;
    private readonly string _platformAccountId;

    public StripeConnectService(
        CarRentalDbContext context,
        IStripeService stripeService,
        IEncryptionService encryptionService,
        ILogger<StripeConnectService> logger,
        IConfiguration configuration)
    {
        _context = context;
        _stripeService = stripeService;
        _encryptionService = encryptionService;
        _logger = logger;
        
        // You might want to store this in config or database
        _platformAccountId = configuration["Stripe:PlatformAccountId"] ?? "";
    }

    public async Task<(bool Success, string AccountId, string? Error)> CreateConnectedAccountAsync(Guid companyId)
    {
        try
        {
            var company = await _context.Companies.FindAsync(companyId);
            if (company == null)
                return (false, "", "Company not found");

            if (!string.IsNullOrEmpty(company.StripeAccountId))
            {
                try
                {
                    var existingAccountId = _encryptionService.Decrypt(company.StripeAccountId);
                    return (true, existingAccountId, null);
                }
                catch
                {
                    // Existing ID might not be encrypted, proceed to create new
                }
            }

            // Create Stripe Connected Account
            var account = await _stripeService.CreateConnectedAccountAsync(
                company.Email,
                company.StripeAccountType ?? "express",
                company.Country ?? "US"
            );

            // Encrypt and store account ID
            var encryptedAccountId = _encryptionService.Encrypt(account.Id);
            
            company.StripeAccountId = encryptedAccountId;
            company.StripeAccountType = account.Type;
            company.StripeChargesEnabled = account.ChargesEnabled;
            company.StripePayoutsEnabled = account.PayoutsEnabled;
            company.StripeDetailsSubmitted = account.DetailsSubmitted;
            company.StripeOnboardingCompleted = account.ChargesEnabled && 
                                               account.PayoutsEnabled && 
                                               account.DetailsSubmitted;
            company.StripeLastSyncAt = DateTime.UtcNow;
            company.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Created Stripe Connect account {AccountId} for company {CompanyId}", 
                account.Id, companyId);

            return (true, account.Id, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating connected account for company {CompanyId}", companyId);
            return (false, "", ex.Message);
        }
    }

    public async Task<(bool Success, string OnboardingUrl, string? Error)> StartOnboardingAsync(
        Guid companyId, 
        string returnUrl, 
        string refreshUrl)
    {
        try
        {
            var company = await _context.Companies.FindAsync(companyId);
            if (company == null)
                return (false, "", "Company not found");

            // Ensure we have a connected account
            string accountId;
            if (string.IsNullOrEmpty(company.StripeAccountId))
            {
                var (success, newAccountId, error) = await CreateConnectedAccountAsync(companyId);
                if (!success)
                    return (false, "", error);
                accountId = newAccountId;
            }
            else
            {
                accountId = _encryptionService.Decrypt(company.StripeAccountId);
            }

            // Create account link for onboarding
            var accountLink = await _stripeService.CreateAccountLinkAsync(
                accountId,
                returnUrl,
                refreshUrl
            );

            // Save onboarding session
            var session = new StripeOnboardingSession
            {
                CompanyId = companyId,
                AccountLinkUrl = accountLink.Url,
                ReturnUrl = returnUrl,
                RefreshUrl = refreshUrl,
                ExpiresAt = DateTime.UtcNow.AddHours(1), // Stripe links expire in 1 hour
                Completed = false
            };

            _context.StripeOnboardingSessions.Add(session);

            // Update company
            company.StripeOnboardingLink = accountLink.Url;
            company.StripeOnboardingLinkExpiresAt = session.ExpiresAt;
            company.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Created onboarding link for company {CompanyId}, account {AccountId}", 
                companyId, accountId);

            return (true, accountLink.Url, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating onboarding link for company {CompanyId}", companyId);
            return (false, "", ex.Message);
        }
    }

    public async Task<StripeAccountStatusDto> GetAccountStatusAsync(Guid companyId)
    {
        var company = await _context.Companies.FindAsync(companyId);
        
        if (company == null || string.IsNullOrEmpty(company.StripeAccountId))
        {
            return new StripeAccountStatusDto
            {
                AccountStatus = "not_started"
            };
        }

        return new StripeAccountStatusDto
        {
            ChargesEnabled = company.StripeChargesEnabled ?? false,
            PayoutsEnabled = company.StripePayoutsEnabled ?? false,
            DetailsSubmitted = company.StripeDetailsSubmitted ?? false,
            OnboardingCompleted = company.StripeOnboardingCompleted ?? false,
            AccountStatus = DetermineAccountStatus(company),
            RequirementsCurrentlyDue = company.StripeRequirementsCurrentlyDue?.ToList() ?? new(),
            RequirementsPastDue = company.StripeRequirementsPastDue?.ToList() ?? new(),
            DisabledReason = company.StripeRequirementsDisabledReason,
            LastSyncAt = company.StripeLastSyncAt
        };
    }

    public async Task SyncAccountStatusAsync(string stripeAccountId)
    {
        try
        {
            var account = await _stripeService.GetAccountAsync(stripeAccountId);
            
            var company = await _context.Companies
                .FirstOrDefaultAsync(c => c.StripeAccountId != null && 
                    _encryptionService.Decrypt(c.StripeAccountId) == stripeAccountId);

            if (company == null)
            {
                _logger.LogWarning("No company found for Stripe account {AccountId}", stripeAccountId);
                return;
            }

            company.StripeChargesEnabled = account.ChargesEnabled;
            company.StripePayoutsEnabled = account.PayoutsEnabled;
            company.StripeDetailsSubmitted = account.DetailsSubmitted;
            company.StripeOnboardingCompleted = account.ChargesEnabled && 
                                               account.PayoutsEnabled && 
                                               account.DetailsSubmitted;
            
            company.StripeRequirementsCurrentlyDue = account.Requirements?.CurrentlyDue?.ToArray();
            company.StripeRequirementsEventuallyDue = account.Requirements?.EventuallyDue?.ToArray();
            company.StripeRequirementsPastDue = account.Requirements?.PastDue?.ToArray();
            company.StripeRequirementsDisabledReason = account.Requirements?.DisabledReason;
            
            company.StripeLastSyncAt = DateTime.UtcNow;
            company.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Synced account status for {AccountId}, company {CompanyId}", 
                stripeAccountId, company.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing account status for {AccountId}", stripeAccountId);
            throw;
        }
    }

    public async Task<(bool Success, Guid? TransferId, string? Error)> TransferBookingFundsAsync(Guid bookingId)
    {
        try
        {
            var booking = await _context.Bookings
                .Include(b => b.Company)
                .FirstOrDefaultAsync(b => b.Id == bookingId);

            if (booking == null)
                return (false, null, "Booking not found");

            if (booking.Company == null)
                return (false, null, "Company not found for booking");

            if (string.IsNullOrEmpty(booking.Company.StripeAccountId))
                return (false, null, "Company does not have a Stripe Connect account");

            if (!(booking.Company.StripeChargesEnabled ?? false) || !(booking.Company.StripePayoutsEnabled ?? false))
                return (false, null, "Company Stripe account is not fully enabled");

            if (!string.IsNullOrEmpty(booking.StripeTransferId))
                return (false, null, "Funds already transferred for this booking");

            // Decrypt account ID
            var destinationAccountId = _encryptionService.Decrypt(booking.Company.StripeAccountId);

            // Calculate amounts
            var platformFee = booking.PlatformFeeAmount;
            var netAmount = booking.NetAmount ?? (booking.TotalAmount - platformFee);
            var transferAmount = (long)(netAmount * 100); // Convert to cents

            // Create transfer
            var transfer = await _stripeService.CreateTransferAsync(
                destinationAccountId,
                transferAmount,
                booking.Currency ?? "USD",
                $"booking_{booking.BookingNumber}"
            );

            // Log transfer in database
            var transferRecord = new StripeTransfer
            {
                BookingId = booking.Id,
                CompanyId = booking.CompanyId,
                StripeTransferId = transfer.Id,
                StripePaymentIntentId = booking.StripePaymentIntentId,
                Amount = booking.TotalAmount,
                Currency = booking.Currency ?? "USD",
                PlatformFee = platformFee,
                NetAmount = netAmount,
                DestinationAccountId = destinationAccountId,
                Status = "paid", // Transfer is created successfully
                TransferredAt = DateTime.UtcNow
            };

            _context.StripeTransfers.Add(transferRecord);

            // Update booking
            booking.StripeTransferId = transfer.Id;
            booking.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Transferred {Amount} {Currency} to account {AccountId} for booking {BookingId}, transfer {TransferId}",
                netAmount, booking.Currency, destinationAccountId, bookingId, transfer.Id);

            return (true, transferRecord.Id, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error transferring funds for booking {BookingId}", bookingId);
            return (false, null, ex.Message);
        }
    }

    public async Task<(bool Success, string? PaymentIntentId, string? Error)> AuthorizeSecurityDepositAsync(
        Guid bookingId,
        string paymentMethodId,
        decimal amount)
    {
        try
        {
            var booking = await _context.Bookings
                .Include(b => b.Customer)
                .Include(b => b.Company)
                .FirstOrDefaultAsync(b => b.Id == bookingId);

            if (booking == null)
                return (false, null, "Booking not found");

            if (booking.Customer == null)
                return (false, null, "Customer not found");

            if (booking.Company == null || string.IsNullOrEmpty(booking.Company.StripeAccountId))
                return (false, null, "Company Stripe account not configured");

            var destinationAccountId = _encryptionService.Decrypt(booking.Company.StripeAccountId);

            // Create payment intent for security deposit (authorization only)
            var paymentIntent = await _stripeService.CreateSecurityDepositAsync(
                booking.Customer.StripeCustomerId!,
                amount,
                booking.Currency ?? "USD",
                paymentMethodId,
                destinationAccountId,
                new Dictionary<string, string>
                {
                    { "booking_id", bookingId.ToString() },
                    { "booking_number", booking.BookingNumber },
                    { "payment_type", "security_deposit" }
                }
            );

            // Update booking
            booking.SecurityDepositPaymentIntentId = paymentIntent.Id;
            booking.SecurityDepositAuthorizedAt = DateTime.UtcNow;
            booking.SecurityDepositStatus = "authorized";
            booking.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Authorized security deposit {Amount} {Currency} for booking {BookingId}, PaymentIntent {PaymentIntentId}",
                amount, booking.Currency, bookingId, paymentIntent.Id);

            return (true, paymentIntent.Id, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error authorizing security deposit for booking {BookingId}", bookingId);
            return (false, null, ex.Message);
        }
    }

    private string DetermineAccountStatus(Company company)
    {
        if (string.IsNullOrEmpty(company.StripeAccountId))
            return "not_started";

        if (!(company.StripeOnboardingCompleted ?? false))
            return "onboarding";

        if (company.StripeRequirementsPastDue?.Length > 0)
            return "past_due";

        if (!(company.StripeChargesEnabled ?? false) || !(company.StripePayoutsEnabled ?? false))
            return "restricted";

        return "active";
    }
}

