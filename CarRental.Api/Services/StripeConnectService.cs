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
using System.Linq;

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
    Task<(bool success, string? error)> SuspendAccountAsync(string stripeAccountId, string reason);
    Task<(bool success, string? error)> ReactivateAccountAsync(string stripeAccountId);
    Task<(bool success, string? error)> DeleteAccountAsync(string stripeAccountId);
    Task<List<ConnectedAccountDetailsDto>> GetAllAccountsAsync(int limit);
    Task<ConnectedAccountDetailsDto?> GetAccountDetailsAsync(string stripeAccountId);
    Task<int> FindAndSyncAccountsForCompaniesAsync(int limit = 100);
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

            // Create Stripe Connected Account (pass companyId to use company-specific Stripe settings)
            var account = await _stripeService.CreateConnectedAccountAsync(
                company.Email,
                company.StripeAccountType ?? "express",
                company.Country ?? "US",
                companyId
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

            // Create account link for onboarding (pass companyId to use company-specific Stripe settings)
            var accountLink = await _stripeService.CreateAccountLinkAsync(
                accountId,
                returnUrl,
                refreshUrl,
                companyId
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
                StripeAccountId = null,
                AccountStatus = "not_started"
            };
        }

        var stripeAccountId = _encryptionService.Decrypt(company.StripeAccountId);

        return new StripeAccountStatusDto
        {
            StripeAccountId = stripeAccountId,
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

            // Determine currency: use booking currency, fallback to company currency, then USD
            var currency = booking.Currency ?? booking.Company.Currency ?? "USD";
            
            _logger.LogInformation(
                "Authorizing security deposit for booking {BookingId}: Amount={Amount}, Currency={Currency} (Booking.Currency={BookingCurrency}, Company.Currency={CompanyCurrency})",
                bookingId,
                amount,
                currency,
                booking.Currency ?? "null",
                booking.Company.Currency ?? "null"
            );

            // Create payment intent for security deposit (authorization only)
            var paymentIntent = await _stripeService.CreateSecurityDepositAsync(
                booking.Customer.StripeCustomerId!,
                amount,
                currency,
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

    public async Task<(bool success, string? error)> SuspendAccountAsync(string stripeAccountId, string reason)
    {
        try
        {
            _logger.LogInformation("Suspending Stripe account {AccountId} for reason: {Reason}", 
                stripeAccountId, reason);

            var service = new AccountService();
            
            // Update account to reject future charges
            var options = new AccountUpdateOptions
            {
                // Stripe не позволяет напрямую "suspend", но можно отключить capabilities
                Capabilities = new AccountCapabilitiesOptions
                {
                    CardPayments = new AccountCapabilitiesCardPaymentsOptions
                    {
                        Requested = false // Отключить приём платежей
                    },
                    Transfers = new AccountCapabilitiesTransfersOptions
                    {
                        Requested = false // Отключить переводы
                    }
                }
            };

            await service.UpdateAsync(stripeAccountId, options);

            // Обновить статус в базе данных
            var company = await _context.Companies
                .FirstOrDefaultAsync(c => c.StripeAccountId != null && 
                    _encryptionService.Decrypt(c.StripeAccountId) == stripeAccountId);

            if (company != null)
            {
                company.StripeChargesEnabled = false;
                company.StripePayoutsEnabled = false;
                company.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            _logger.LogInformation("Successfully suspended account {AccountId}", stripeAccountId);
            return (true, null);
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe error suspending account {AccountId}", stripeAccountId);
            return (false, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error suspending account {AccountId}", stripeAccountId);
            return (false, "Failed to suspend account");
        }
    }

    public async Task<(bool success, string? error)> ReactivateAccountAsync(string stripeAccountId)
    {
        try
        {
            _logger.LogInformation("Reactivating Stripe account {AccountId}", stripeAccountId);

            var service = new AccountService();
            
            // Включить обратно capabilities
            var options = new AccountUpdateOptions
            {
                Capabilities = new AccountCapabilitiesOptions
                {
                    CardPayments = new AccountCapabilitiesCardPaymentsOptions
                    {
                        Requested = true
                    },
                    Transfers = new AccountCapabilitiesTransfersOptions
                    {
                        Requested = true
                    }
                }
            };

            var account = await service.UpdateAsync(stripeAccountId, options);

            // Обновить статус в базе данных
            var company = await _context.Companies
                .FirstOrDefaultAsync(c => c.StripeAccountId != null && 
                    _encryptionService.Decrypt(c.StripeAccountId) == stripeAccountId);

            if (company != null)
            {
                company.StripeChargesEnabled = account.ChargesEnabled;
                company.StripePayoutsEnabled = account.PayoutsEnabled;
                company.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            _logger.LogInformation("Successfully reactivated account {AccountId}", stripeAccountId);
            return (true, null);
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe error reactivating account {AccountId}", stripeAccountId);
            return (false, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reactivating account {AccountId}", stripeAccountId);
            return (false, "Failed to reactivate account");
        }
    }

    public async Task<(bool success, string? error)> DeleteAccountAsync(string stripeAccountId)
    {
        try
        {
            _logger.LogInformation("Deleting Stripe account {AccountId}", stripeAccountId);

            // Проверить, что нет активных платежей
            var company = await _context.Companies
                .FirstOrDefaultAsync(c => c.StripeAccountId != null && 
                    _encryptionService.Decrypt(c.StripeAccountId) == stripeAccountId);

            if (company == null)
            {
                return (false, "Account not found in database");
            }

            // Проверить, есть ли bookings с платежами
            var hasPayments = await _context.Bookings
                .AnyAsync(b => b.CompanyId == company.Id && 
                          !string.IsNullOrEmpty(b.StripePaymentIntentId));

            if (hasPayments)
            {
                return (false, "Cannot delete account with existing payments. Suspend it instead.");
            }

            var service = new AccountService();
            
            // Удалить account в Stripe
            await service.DeleteAsync(stripeAccountId);

            // Очистить ссылку в Company
            company.StripeAccountId = null;
            company.StripeAccountType = null;
            company.StripeChargesEnabled = false;
            company.StripePayoutsEnabled = false;
            company.StripeDetailsSubmitted = false;
            company.StripeOnboardingCompleted = false;
            company.UpdatedAt = DateTime.UtcNow;
            
            await _context.SaveChangesAsync();

            _logger.LogInformation("Successfully deleted account {AccountId}", stripeAccountId);
            return (true, null);
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe error deleting account {AccountId}", stripeAccountId);
            return (false, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting account {AccountId}", stripeAccountId);
            return (false, "Failed to delete account");
        }
    }

    public async Task<List<ConnectedAccountDetailsDto>> GetAllAccountsAsync(int limit)
    {
        try
        {
            var service = new AccountService();
            var options = new AccountListOptions
            {
                Limit = limit
            };

            var accounts = await service.ListAsync(options);

            return accounts.Data.Select(a => MapToDetailsDto(a)).ToList();
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe error getting all accounts");
            return new List<ConnectedAccountDetailsDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all accounts");
            return new List<ConnectedAccountDetailsDto>();
        }
    }

    /// <summary>
    /// Find Stripe accounts and sync them with companies by matching email addresses
    /// </summary>
    public async Task<int> FindAndSyncAccountsForCompaniesAsync(int limit = 100)
    {
        try
        {
            _logger.LogInformation("Finding and syncing Stripe accounts for companies (limit: {Limit})", limit);

            var service = new AccountService();
            var options = new AccountListOptions
            {
                Limit = limit
            };

            var accounts = await service.ListAsync(options);

            int syncedCount = 0;

            foreach (var account in accounts.Data)
            {
                _logger.LogInformation("Processing account: ID={AccountId}, Email={Email}, Type={Type}", 
                    account.Id, account.Email, account.Type);

                // Find company by email
                var company = await _context.Companies
                    .FirstOrDefaultAsync(c => c.Email != null && 
                        c.Email.Equals(account.Email, StringComparison.OrdinalIgnoreCase));

                if (company == null)
                {
                    _logger.LogWarning("No company found for Stripe account {AccountId} with email {Email}", 
                        account.Id, account.Email);
                    continue;
                }

                // Check if company already has this account linked
                string? existingAccountId = null;
                if (!string.IsNullOrEmpty(company.StripeAccountId))
                {
                    try
                    {
                        existingAccountId = _encryptionService.Decrypt(company.StripeAccountId);
                    }
                    catch
                    {
                        // Account ID might not be encrypted, use as-is
                        existingAccountId = company.StripeAccountId;
                    }
                }

                // If account is already linked, just sync the status
                if (existingAccountId == account.Id)
                {
                    _logger.LogInformation("Account {AccountId} already linked to company {CompanyId}, syncing status", 
                        account.Id, company.Id);
                    await SyncAccountStatusAsync(account.Id);
                    syncedCount++;
                    continue;
                }

                // Link the account to the company
                var encryptedAccountId = _encryptionService.Encrypt(account.Id);
                company.StripeAccountId = encryptedAccountId;
                company.StripeAccountType = account.Type;
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

                _logger.LogInformation("Linked Stripe account {AccountId} to company {CompanyId} ({CompanyName})", 
                    account.Id, company.Id, company.CompanyName);
                syncedCount++;
            }

            _logger.LogInformation("Completed syncing accounts. Total synced: {SyncedCount}", syncedCount);
            return syncedCount;
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe error finding and syncing accounts");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding and syncing accounts");
            throw;
        }
    }

    public async Task<ConnectedAccountDetailsDto?> GetAccountDetailsAsync(string stripeAccountId)
    {
        try
        {
            var service = new AccountService();
            var account = await service.GetAsync(stripeAccountId);

            return MapToDetailsDto(account);
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe error getting account details {AccountId}", stripeAccountId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting account details {AccountId}", stripeAccountId);
            return null;
        }
    }

    private ConnectedAccountDetailsDto MapToDetailsDto(Account account)
    {
        ExternalAccountDto? externalAccount = null;
        
        if (account.ExternalAccounts?.Data?.Any() == true)
        {
            var bankAccount = account.ExternalAccounts.Data.FirstOrDefault() as BankAccount;
            if (bankAccount != null)
            {
                externalAccount = new ExternalAccountDto
                {
                    BankName = bankAccount.BankName,
                    Last4 = bankAccount.Last4,
                    Country = bankAccount.Country,
                    Currency = bankAccount.Currency
                };
            }
        }

        var capabilities = new Dictionary<string, string>();
        if (account.Capabilities != null)
        {
            // In this Stripe.NET version, capabilities are stored as strings directly
            if (!string.IsNullOrEmpty(account.Capabilities.CardPayments))
                capabilities["card_payments"] = account.Capabilities.CardPayments;
            if (!string.IsNullOrEmpty(account.Capabilities.Transfers))
                capabilities["transfers"] = account.Capabilities.Transfers;
            if (!string.IsNullOrEmpty(account.Capabilities.LegacyPayments))
                capabilities["legacy_payments"] = account.Capabilities.LegacyPayments;
        }

        // account.Created is already a DateTime in this Stripe.NET version
        DateTime created = account.Created;

        // account.Requirements.CurrentDeadline is already a DateTime? in this Stripe.NET version
        DateTime? currentDeadline = account.Requirements?.CurrentDeadline;

        return new ConnectedAccountDetailsDto
        {
            Id = account.Id,
            Type = account.Type ?? "unknown",
            Email = account.Email,
            Country = account.Country,
            ChargesEnabled = account.ChargesEnabled,
            PayoutsEnabled = account.PayoutsEnabled,
            DetailsSubmitted = account.DetailsSubmitted,
            Created = created,
            BusinessName = account.BusinessProfile?.Name,
            BusinessType = account.BusinessType,
            CurrentlyDue = account.Requirements?.CurrentlyDue?.ToList(),
            EventuallyDue = account.Requirements?.EventuallyDue?.ToList(),
            CurrentDeadline = currentDeadline,
            DisabledReason = account.Requirements?.DisabledReason,
            Capabilities = capabilities,
            ExternalAccount = externalAccount
        };
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

