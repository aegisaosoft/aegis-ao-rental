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
    Task SyncAccountStatusAsync(string stripeAccountId, Guid? companyId = null);
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

    /// <summary>
    /// Gets or creates a StripeCompany record for the given company
    /// </summary>
    private async Task<StripeCompany> GetOrCreateStripeCompanyAsync(Company company)
    {
        if (company.StripeSettingsId == null)
        {
            throw new InvalidOperationException($"Company {company.Id} does not have a StripeSettingsId configured");
        }

        var stripeCompany = await _context.StripeCompanies
            .FirstOrDefaultAsync(sc => sc.CompanyId == company.Id && sc.SettingsId == company.StripeSettingsId.Value);

        if (stripeCompany == null)
        {
            stripeCompany = new StripeCompany
            {
                CompanyId = company.Id,
                SettingsId = company.StripeSettingsId.Value,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.StripeCompanies.Add(stripeCompany);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Created new StripeCompany record for company {CompanyId} with settings {SettingsId}", 
                company.Id, company.StripeSettingsId.Value);
        }

        return stripeCompany;
    }

    public async Task<(bool Success, string AccountId, string? Error)> CreateConnectedAccountAsync(Guid companyId)
    {
        try
        {
            var company = await _context.Companies.FindAsync(companyId);
            if (company == null)
                return (false, "", "Company not found");

            // Auto-resolve StripeSettingsId if not configured
            if (company.StripeSettingsId == null)
            {
                _logger.LogInformation("Company {CompanyId} does not have StripeSettingsId configured. Attempting to auto-resolve...", companyId);
                company.StripeSettingsId = await ResolveStripeSettingsIdAsync(company.IsTestCompany, company.Country);
                
                if (company.StripeSettingsId == null)
                {
                    _logger.LogError("Cannot create Stripe account for company {CompanyId}: Could not resolve StripeSettingsId (IsTestCompany: {IsTestCompany}, Country: {Country})", 
                        companyId, company.IsTestCompany, company.Country);
                    return (false, "", "Company does not have Stripe settings configured and could not auto-resolve. Please configure Stripe settings first.");
                }
                
                // Save the resolved StripeSettingsId
                _context.Companies.Update(company);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Auto-resolved StripeSettingsId: {StripeSettingsId} for company {CompanyId}", company.StripeSettingsId, companyId);
            }

            // Get or create StripeCompany record
            var stripeCompany = await GetOrCreateStripeCompanyAsync(company);

            if (!string.IsNullOrEmpty(stripeCompany.StripeAccountId))
            {
                try
                {
                    var existingAccountId = _encryptionService.Decrypt(stripeCompany.StripeAccountId);
                    return (true, existingAccountId, null);
                }
                catch
                {
                    // Existing ID might not be encrypted, proceed to create new
                }
            }

            // Validate subdomain is present (required for account identification)
            if (string.IsNullOrWhiteSpace(company.Subdomain))
            {
                _logger.LogError("Cannot create Stripe account for company {CompanyId}: subdomain is missing", companyId);
                return (false, "", "Company subdomain is required to create Stripe account");
            }

            // Construct full domain name from subdomain
            // Try to get DNS zone name from configuration, fallback to default
            string? fullDomainName = null;
            if (!string.IsNullOrWhiteSpace(company.Subdomain))
            {
                // Construct domain: {subdomain}.{dnsZoneName}
                // Default to aegis-rental.com if DNS zone not configured
                fullDomainName = $"{company.Subdomain.ToLower().Trim()}.aegis-rental.com";
                _logger.LogInformation("Constructed domain name for company {CompanyId}: {DomainName}", companyId, fullDomainName);
            }

            // Create Stripe Connected Account (pass companyId to use company-specific Stripe settings)
            // Use subdomain as identifier - email will be collected during onboarding
            var account = await _stripeService.CreateConnectedAccountAsync(
                company.Subdomain,
                company.StripeAccountType ?? "express",
                company.Country ?? "US",
                companyId,
                fullDomainName
            );

            // Encrypt and store account ID in StripeCompany
            var encryptedAccountId = _encryptionService.Encrypt(account.Id);
            
            stripeCompany.StripeAccountId = encryptedAccountId;
            stripeCompany.UpdatedAt = DateTime.UtcNow;
            
            // Update company Stripe status fields (these can stay on Company for quick access)
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

            // Check if company has StripeSettingsId configured
            if (company.StripeSettingsId == null)
            {
                _logger.LogError("Cannot start onboarding for company {CompanyId}: StripeSettingsId is not configured", companyId);
                return (false, "", "Company does not have Stripe settings configured. Please configure Stripe settings first.");
            }

            // Get or create StripeCompany record
            var stripeCompany = await GetOrCreateStripeCompanyAsync(company);

            // Ensure we have a connected account
            string accountId;
            if (string.IsNullOrEmpty(stripeCompany.StripeAccountId))
            {
                var (success, newAccountId, error) = await CreateConnectedAccountAsync(companyId);
                if (!success)
                    return (false, "", error);
                accountId = newAccountId;
            }
            else
            {
                accountId = _encryptionService.Decrypt(stripeCompany.StripeAccountId);
            }

            // Check account status before creating link
            try
            {
                var account = await _stripeService.GetAccountAsync(accountId, companyId);
                
                // Check if account is rejected or has issues
                if (account.Requirements?.DisabledReason != null)
                {
                    var disabledReason = account.Requirements.DisabledReason;
                    _logger.LogWarning("Stripe account {AccountId} for company {CompanyId} is disabled. Reason: {Reason}", 
                        accountId, companyId, disabledReason);
                    
                    // If account is rejected or has past due requirements, we should delete it and create a new one
                    // This allows the user to start fresh with onboarding
                    bool shouldRecreate = disabledReason.Contains("rejected", StringComparison.OrdinalIgnoreCase) || 
                                         disabledReason.Contains("reject", StringComparison.OrdinalIgnoreCase) ||
                                         disabledReason.Contains("past_due", StringComparison.OrdinalIgnoreCase) ||
                                         disabledReason.Contains("requirements.past_due", StringComparison.OrdinalIgnoreCase);
                    
                    if (shouldRecreate)
                    {
                        var reasonType = disabledReason.Contains("past_due", StringComparison.OrdinalIgnoreCase) 
                            ? "has past due requirements" 
                            : "was rejected";
                        
                        _logger.LogInformation("Account {AccountId} {ReasonType}. Deleting and recreating...", accountId, reasonType);
                        
                        try
                        {
                            // Delete the problematic account
                            var accountService = new Stripe.AccountService();
                            await accountService.DeleteAsync(accountId);
                            
                            // Clear the account ID from StripeCompany
                            stripeCompany.StripeAccountId = null;
                            stripeCompany.UpdatedAt = DateTime.UtcNow;
                            await _context.SaveChangesAsync();
                            
                            _logger.LogInformation("Deleted account {AccountId}. Creating new account...", accountId);
                            
                            // Create a new account
                            var (createSuccess, newAccountId, createError) = await CreateConnectedAccountAsync(companyId);
                            if (!createSuccess)
                                return (false, "", $"Failed to recreate account after deletion: {createError}");
                            
                            accountId = newAccountId;
                            account = await _stripeService.GetAccountAsync(accountId, companyId);
                            
                            _logger.LogInformation("Successfully created new account {NewAccountId} to replace {OldAccountId}", 
                                newAccountId, accountId);
                        }
                        catch (Exception deleteEx)
                        {
                            _logger.LogError(deleteEx, "Error deleting disabled account {AccountId}", accountId);
                            return (false, "", $"Account is disabled ({disabledReason}) and could not be deleted: {deleteEx.Message}");
                        }
                    }
                    else
                    {
                        // Account is disabled for other reasons - return error
                        return (false, "", $"Account is disabled: {disabledReason}. Please check the account in Stripe dashboard.");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not check account status for {AccountId}, proceeding with link creation", accountId);
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
        
        if (company == null)
        {
            _logger.LogWarning("Company {CompanyId} not found when getting Stripe account status", companyId);
            return new StripeAccountStatusDto
            {
                StripeAccountId = null,
                AccountStatus = "not_started"
            };
        }

        if (company.StripeSettingsId == null)
        {
            _logger.LogWarning("Company {CompanyId} does not have StripeSettingsId configured", companyId);
            return new StripeAccountStatusDto
            {
                StripeAccountId = null,
                AccountStatus = "not_started"
            };
        }

        // Try to find StripeCompany using the same join logic as the SQL query:
        // FROM companies c 
        // INNER JOIN stripe_company sc ON c.id = sc.company_id 
        // INNER JOIN stripe_settings ss ON c.stripe_settings_id = ss.id AND ss.id = sc.settings_id
        var stripeCompany = await _context.StripeCompanies
            .Where(sc => sc.CompanyId == companyId && sc.SettingsId == company.StripeSettingsId.Value)
            .FirstOrDefaultAsync();

        if (stripeCompany == null)
        {
            // Log all StripeCompany records for this company to help debug
            var allStripeCompanies = await _context.StripeCompanies
                .Where(sc => sc.CompanyId == companyId)
                .Select(sc => new { sc.SettingsId, HasAccountId = !string.IsNullOrEmpty(sc.StripeAccountId) })
                .ToListAsync();
            
            _logger.LogWarning(
                "No StripeCompany found for company {CompanyId} with SettingsId {SettingsId}. " +
                "Found {Count} StripeCompany records for this company: {Records}",
                companyId, 
                company.StripeSettingsId.Value,
                allStripeCompanies.Count,
                string.Join(", ", allStripeCompanies.Select(sc => $"SettingsId={sc.SettingsId}, HasAccountId={sc.HasAccountId}"))
            );
            
            // If no exact match, try to find any StripeCompany record for this company (fallback)
            // This handles cases where the settings_id might not match exactly
            if (allStripeCompanies.Count > 0)
            {
                var fallbackStripeCompany = await _context.StripeCompanies
                    .Where(sc => sc.CompanyId == companyId && !string.IsNullOrEmpty(sc.StripeAccountId))
                    .FirstOrDefaultAsync();
                
                if (fallbackStripeCompany != null)
                {
                    _logger.LogInformation(
                        "Using fallback StripeCompany record for company {CompanyId} with SettingsId {SettingsId} (company's SettingsId is {CompanySettingsId})",
                        companyId,
                        fallbackStripeCompany.SettingsId,
                        company.StripeSettingsId.Value
                    );
                    stripeCompany = fallbackStripeCompany;
                }
            }
            
            if (stripeCompany == null)
            {
                return new StripeAccountStatusDto
                {
                    StripeAccountId = null,
                    AccountStatus = "not_started"
                };
            }
        }

        if (string.IsNullOrEmpty(stripeCompany.StripeAccountId))
        {
            _logger.LogWarning("StripeCompany found for company {CompanyId} but StripeAccountId is empty", companyId);
            return new StripeAccountStatusDto
            {
                StripeAccountId = null,
                AccountStatus = "not_started"
            };
        }

        string stripeAccountId;
        try
        {
            stripeAccountId = _encryptionService.Decrypt(stripeCompany.StripeAccountId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt StripeAccountId for company {CompanyId}. The value might be stored in plain text.", companyId);
            // If decryption fails, it might be stored in plain text (for backward compatibility)
            stripeAccountId = stripeCompany.StripeAccountId;
        }
        
        // Set StripeAccountId on company for DetermineAccountStatus to work correctly
        company.StripeAccountId = stripeAccountId;

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

    public async Task SyncAccountStatusAsync(string stripeAccountId, Guid? companyId = null)
    {
        try
        {
            // Fetch StripeCompany records with non-null StripeAccountId
            // If companyId is provided, filter by it for better performance
            // We can't decrypt in LINQ queries, so we need to fetch and decrypt in memory
            var query = _context.StripeCompanies
                .Include(sc => sc.Company)
                .Where(sc => sc.StripeAccountId != null);
            
            if (companyId.HasValue)
            {
                query = query.Where(sc => sc.CompanyId == companyId.Value);
            }
            
            var stripeCompanies = await query.ToListAsync();

            // Find the matching StripeCompany by decrypting in memory
            StripeCompany? stripeCompany = null;
            foreach (var sc in stripeCompanies)
            {
                try
                {
                    var decryptedId = _encryptionService.Decrypt(sc.StripeAccountId!);
                    if (decryptedId == stripeAccountId)
                    {
                        stripeCompany = sc;
                        break;
                    }
                }
                catch
                {
                    // If decryption fails, try comparing as plain text (for backward compatibility)
                    if (sc.StripeAccountId == stripeAccountId)
                    {
                        stripeCompany = sc;
                        break;
                    }
                }
            }
            
            if (stripeCompany == null || stripeCompany.Company == null)
            {
                _logger.LogWarning("No StripeCompany record found for Stripe account {AccountId}", stripeAccountId);
                return;
            }

            var company = stripeCompany.Company;

            // Get account using company-specific Stripe settings
            var account = await _stripeService.GetAccountAsync(stripeAccountId, company.Id);

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

            if (booking.Company.StripeSettingsId == null)
                return (false, null, "Company does not have Stripe settings configured");

            var stripeCompany = await _context.StripeCompanies
                .FirstOrDefaultAsync(sc => sc.CompanyId == booking.Company.Id && 
                    sc.SettingsId == booking.Company.StripeSettingsId.Value);

            if (stripeCompany == null || string.IsNullOrEmpty(stripeCompany.StripeAccountId))
                return (false, null, "Company does not have a Stripe Connect account");

            if (!(booking.Company.StripeChargesEnabled ?? false) || !(booking.Company.StripePayoutsEnabled ?? false))
                return (false, null, "Company Stripe account is not fully enabled");

            if (!string.IsNullOrEmpty(booking.StripeTransferId))
                return (false, null, "Funds already transferred for this booking");

            // Decrypt account ID
            var destinationAccountId = _encryptionService.Decrypt(stripeCompany.StripeAccountId);

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
            var stripeCompany = await _context.StripeCompanies
                .Include(sc => sc.Company)
                .FirstOrDefaultAsync(sc => sc.StripeAccountId != null && 
                    _encryptionService.Decrypt(sc.StripeAccountId) == stripeAccountId);

            if (stripeCompany?.Company != null)
            {
                var company = stripeCompany.Company;
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
            var stripeCompany = await _context.StripeCompanies
                .Include(sc => sc.Company)
                .FirstOrDefaultAsync(sc => sc.StripeAccountId != null && 
                    _encryptionService.Decrypt(sc.StripeAccountId) == stripeAccountId);

            if (stripeCompany?.Company != null)
            {
                var company = stripeCompany.Company;
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

            // Find StripeCompany record
            var stripeCompany = await _context.StripeCompanies
                .Include(sc => sc.Company)
                .FirstOrDefaultAsync(sc => sc.StripeAccountId != null && 
                    _encryptionService.Decrypt(sc.StripeAccountId) == stripeAccountId);

            if (stripeCompany == null || stripeCompany.Company == null)
            {
                return (false, "Account not found in database");
            }

            var company = stripeCompany.Company;

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

            // Очистить ссылку в StripeCompany
            stripeCompany.StripeAccountId = null;
            stripeCompany.UpdatedAt = DateTime.UtcNow;
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
                _logger.LogInformation("Processing account: ID={AccountId}, Email={Email}, Type={Type}, Metadata={Metadata}", 
                    account.Id, account.Email, account.Type,
                    account.Metadata != null ? string.Join(", ", account.Metadata.Select(kvp => $"{kvp.Key}={kvp.Value}")) : "null");

                // Find company by subdomain from metadata (primary identifier)
                Company? company = null;
                
                if (account.Metadata != null && account.Metadata.ContainsKey("subdomain"))
                {
                    var subdomain = account.Metadata["subdomain"];
                    company = await _context.Companies
                        .FirstOrDefaultAsync(c => c.Subdomain != null && 
                            c.Subdomain.Equals(subdomain, StringComparison.OrdinalIgnoreCase));
                    
                    _logger.LogInformation("Looking for company with subdomain={Subdomain} from account metadata", subdomain);
                }

                // Fallback: Find company by email (for backward compatibility with existing accounts)
                if (company == null && !string.IsNullOrEmpty(account.Email))
                {
                    company = await _context.Companies
                        .FirstOrDefaultAsync(c => c.Email != null && 
                            c.Email.Equals(account.Email, StringComparison.OrdinalIgnoreCase));
                    
                    _logger.LogInformation("Fallback: Looking for company with email={Email}", account.Email);
                }

                if (company == null)
                {
                    var identifier = account.Metadata?.ContainsKey("subdomain") == true 
                        ? $"subdomain={account.Metadata["subdomain"]}" 
                        : $"email={account.Email}";
                    _logger.LogWarning("No company found for Stripe account {AccountId} with {Identifier}", 
                        account.Id, identifier);
                    continue;
                }

                // Get or create StripeCompany record
                if (company.StripeSettingsId == null)
                {
                    _logger.LogWarning("Company {CompanyId} does not have StripeSettingsId, skipping", company.Id);
                    continue;
                }

                var stripeCompany = await _context.StripeCompanies
                    .FirstOrDefaultAsync(sc => sc.CompanyId == company.Id && sc.SettingsId == company.StripeSettingsId.Value);

                if (stripeCompany == null)
                {
                    stripeCompany = new StripeCompany
                    {
                        CompanyId = company.Id,
                        SettingsId = company.StripeSettingsId.Value,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.StripeCompanies.Add(stripeCompany);
                }

                // Check if company already has this account linked
                string? existingAccountId = null;
                if (!string.IsNullOrEmpty(stripeCompany.StripeAccountId))
                {
                    try
                    {
                        existingAccountId = _encryptionService.Decrypt(stripeCompany.StripeAccountId);
                    }
                    catch
                    {
                        // Account ID might not be encrypted, use as-is
                        existingAccountId = stripeCompany.StripeAccountId;
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

                // Link the account to the StripeCompany
                var encryptedAccountId = _encryptionService.Encrypt(account.Id);
                stripeCompany.StripeAccountId = encryptedAccountId;
                stripeCompany.UpdatedAt = DateTime.UtcNow;
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

    /// <summary>
    /// Helper method to resolve StripeSettingsId based on company test status and country
    /// </summary>
    private async Task<Guid?> ResolveStripeSettingsIdAsync(bool isTestCompany, string? countryCode)
    {
        try
        {
            // If test company, look for "test" settings
            if (isTestCompany)
            {
                var testSettings = await _context.StripeSettings
                    .FirstOrDefaultAsync(s => s.Name.ToLower() == "test".ToLower());
                
                if (testSettings != null)
                {
                    _logger.LogInformation("Resolved StripeSettingsId: {Id} (Name: {Name}) for test company", 
                        testSettings.Id, testSettings.Name);
                    return testSettings.Id;
                }
                
                _logger.LogWarning("Test Stripe settings not found. Falling back to country-based settings.");
            }

            // Look for country-specific settings
            if (!string.IsNullOrWhiteSpace(countryCode))
            {
                var countrySettings = await _context.StripeSettings
                    .FirstOrDefaultAsync(s => s.Name.ToLower() == countryCode.ToLower());
                
                if (countrySettings != null)
                {
                    _logger.LogInformation("Resolved StripeSettingsId: {Id} (Name: {Name}) for country: {Country}", 
                        countrySettings.Id, countrySettings.Name, countryCode);
                    return countrySettings.Id;
                }
                
                _logger.LogWarning("Stripe settings for country '{Country}' not found. Falling back to US settings.", countryCode);
            }

            // Fallback to US settings
            var usSettings = await _context.StripeSettings
                .FirstOrDefaultAsync(s => s.Name.ToLower() == "us".ToLower());
            
            if (usSettings != null)
            {
                _logger.LogInformation("Resolved StripeSettingsId: {Id} (Name: {Name}) as fallback US settings", 
                    usSettings.Id, usSettings.Name);
                return usSettings.Id;
            }

            _logger.LogWarning("US Stripe settings not found. No StripeSettingsId will be assigned.");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving StripeSettingsId for IsTestCompany: {IsTestCompany}, Country: {Country}", 
                isTestCompany, countryCode);
            return null;
        }
    }
}

