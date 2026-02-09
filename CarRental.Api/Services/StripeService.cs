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
using Stripe.Checkout;
using CarRental.Api.Models;
using CarRental.Api.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading;
using System.Text;
using System.Linq;
using CarRental.Api.Helpers;

namespace CarRental.Api.Services;

public interface IStripeService
{
    Task<Models.Customer> CreateCustomerAsync(Models.Customer customer);
    Task<Models.Customer> UpdateCustomerAsync(Models.Customer customer);
    Task<Models.Customer> GetCustomerAsync(string stripeCustomerId, Guid? companyId = null);
    Task<PaymentMethod> CreatePaymentMethodAsync(string customerId, string paymentMethodId, Guid? companyId = null);
    Task<PaymentMethod> GetPaymentMethodAsync(string paymentMethodId, Guid? companyId = null);
    Task<PaymentIntent> CreatePaymentIntentAsync(
        decimal amount,
        string currency,
        string customerId,
        string? paymentMethodId = null,
        IDictionary<string, string>? metadata = null,
        bool captureImmediately = true,
        bool requestExtendedAuthorization = false,
        Guid? companyId = null);
    Task<PaymentIntent> ConfirmPaymentIntentAsync(string paymentIntentId, Guid? companyId = null);
    Task<PaymentIntent> CancelPaymentIntentAsync(string paymentIntentId, Guid? companyId = null);
    Task<PaymentIntent> CapturePaymentIntentAsync(string paymentIntentId, decimal? amountToCapture = null, string? currency = null, Guid? companyId = null);
    Task<Refund> CreateRefundAsync(string paymentIntentId, decimal amount, Guid? companyId = null);
    
    // Stripe Connect methods
    Task<Account> CreateConnectedAccountAsync(string subdomain, string accountType, string country, Guid? companyId = null, string? domainName = null);
    Task<AccountLink> CreateAccountLinkAsync(string accountId, string returnUrl, string refreshUrl, Guid? companyId = null);
    Task<Account> GetAccountAsync(string accountId, Guid? companyId = null);
    Task<Account> UpdateAccountAsync(string accountId);
    
    // Legacy methods (kept for backward compatibility)
    [Obsolete("Use GetAccountAsync instead.")]
    Task<Account> GetConnectedAccountAsync(string accountId);
    
    [Obsolete("Use CreateConnectedAccountAsync(string email, string accountType, string country) instead.")]
    Task<Account> CreateConnectedAccountLegacyAsync(string email, string country = "US");
    
    // Transfer methods
    Task<Transfer> CreateTransferAsync(string destinationAccountId, long amount, string currency, string transferGroup);
    Task<Transfer> GetTransferAsync(string transferId);
    
    // Legacy transfer method (kept for backward compatibility)
    [Obsolete("Use CreateTransferAsync(string destinationAccountId, long amount, string currency, string transferGroup) instead.")]
    Task<Transfer> CreateTransferLegacyAsync(string accountId, long amount, string currency = "usd");
    
    // Security deposit with Connect
    Task<PaymentIntent> CreateSecurityDepositAsync(
        string customerId, 
        decimal amount, 
        string currency, 
        string paymentMethodId,
        string connectedAccountId,
        Dictionary<string, string> metadata,
        Guid? companyId = null);
    
    Task<WebhookEndpoint> CreateWebhookEndpointAsync(string url, string[] events);
    Task<Event> ConstructWebhookEventAsync(string payload, string signature, string webhookSecret);
    Task<Session> CreateCheckoutSessionAsync(SessionCreateOptions options, RequestOptions? requestOptions = null, Guid? companyId = null);
}

public class StripeService : IStripeService
{
    private readonly ISettingsService _settingsService;
    private readonly CarRentalDbContext _context;
    private readonly ILogger<StripeService> _logger;
    private readonly IEncryptionService _encryptionService;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private readonly Dictionary<Guid, (string secretKey, string? publishableKey, string? webhookSecret)> _companySettingsCache = new();
    private bool _globalInitialized;
    private string? _globalPublishableKey;
    private string? _globalWebhookSecret;

    private const string SecretKeySetting = "stripe.secretKey";
    private const string PublishableKeySetting = "stripe.publishableKey";
    private const string WebhookSecretSetting = "stripe.webhookSecret";

    public StripeService(ISettingsService settingsService, CarRentalDbContext context, ILogger<StripeService> logger, IEncryptionService encryptionService)
    {
        _settingsService = settingsService;
        _context = context;
        _logger = logger;
        _encryptionService = encryptionService;
    }

    /// <summary>
    /// Decrypt a key using the same decryption as SettingsService
    /// </summary>
    private string? DecryptKey(string? encryptedKey)
    {
        if (string.IsNullOrWhiteSpace(encryptedKey))
            return null;
        
        try
        {
            return _encryptionService.Decrypt(encryptedKey);
        }
        catch (Exception ex) when (ex is FormatException || ex is System.Security.Cryptography.CryptographicException)
        {
            // Key might be stored in plaintext (backward compatibility)
            _logger.LogWarning(ex, "Key appears to be stored in plaintext");
            return encryptedKey;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt key");
            return null;
        }
    }

    private async Task EnsureInitializedAsync(Guid? companyId = null)
    {
        // Check if we already have settings for this company cached
        if (companyId.HasValue && _companySettingsCache.ContainsKey(companyId.Value))
        {
            var settings = _companySettingsCache[companyId.Value];
            StripeConfiguration.ApiKey = settings.secretKey;
            _globalPublishableKey = settings.publishableKey;
            _globalWebhookSecret = settings.webhookSecret;
            return;
        }

        // Check if global initialization is done (for backward compatibility)
        if (!companyId.HasValue && _globalInitialized)
            return;

        await _initializationLock.WaitAsync();
        try
        {
            // Double-check after acquiring lock
            if (companyId.HasValue && _companySettingsCache.ContainsKey(companyId.Value))
            {
                var settings = _companySettingsCache[companyId.Value];
                StripeConfiguration.ApiKey = settings.secretKey;
                _globalPublishableKey = settings.publishableKey;
                _globalWebhookSecret = settings.webhookSecret;
                return;
            }

            if (!companyId.HasValue && _globalInitialized)
                return;

            string? secretKey = null;
            string? publishableKey = null;
            string? webhookSecret = null;

            // Try to get from new stripe_settings table (company-specific)
            if (companyId.HasValue)
            {
                try
                {
                    // First, try to get from companies.stripe_settings_id
                    var company = await _context.Companies
                        .AsNoTracking()
                        .FirstOrDefaultAsync(c => c.Id == companyId.Value);

                    Guid? settingsId = null;
                    if (company?.StripeSettingsId.HasValue == true)
                    {
                        settingsId = company.StripeSettingsId;
                        _logger.LogInformation("[Stripe] Found StripeSettingsId={SettingsId} directly on company {CompanyId}", 
                            settingsId, companyId);
                    }
                    else
                    {
                        _logger.LogInformation("[Stripe] Company {CompanyId} does not have StripeSettingsId, checking stripe_company table", companyId);
                        // Fallback: try stripe_company table
                        var stripeCompany = await _context.StripeCompanies
                            .AsNoTracking()
                            .Include(sc => sc.Settings)
                            .FirstOrDefaultAsync(sc => sc.CompanyId == companyId.Value);

                        if (stripeCompany?.Settings != null)
                        {
                            settingsId = stripeCompany.Settings.Id;
                            _logger.LogInformation("[Stripe] Found StripeSettingsId={SettingsId} via stripe_company table for company {CompanyId}", 
                                settingsId, companyId);
                        }
                        else
                        {
                            _logger.LogInformation("[Stripe] No StripeSettingsId found in stripe_company table for company {CompanyId}, will try default stripe_settings", companyId);
                        }
                    }

                    if (settingsId.HasValue)
                    {
                        var stripeSettings = await _context.StripeSettings
                            .AsNoTracking()
                            .FirstOrDefaultAsync(ss => ss.Id == settingsId.Value);

                    if (stripeSettings != null)
                    {
                        // Decrypt keys from database before using with Stripe API
                        // Keys are stored encrypted in DB, must be decrypted to plain text for Stripe
                        secretKey = DecryptKey(stripeSettings.SecretKey);
                        publishableKey = DecryptKey(stripeSettings.PublishableKey);
                        webhookSecret = DecryptKey(stripeSettings.WebhookSecret);
                        
                        if (string.IsNullOrWhiteSpace(secretKey))
                        {
                            _logger.LogWarning("[Stripe] Decrypted secret key is null or empty for stripe_settings Id={SettingsId}, Name={SettingsName} (company {CompanyId})", 
                                settingsId, stripeSettings.Name, companyId);
                        }
                        else
                        {
                            _logger.LogInformation("[Stripe] Using company-specific settings from stripe_settings table for company {CompanyId}. SettingsId={SettingsId}, SettingsName={SettingsName}", 
                                companyId, settingsId, stripeSettings.Name);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("[Stripe] StripeSettings not found for settingsId={SettingsId} (company {CompanyId})", settingsId, companyId);
                    }
                    }
                    else
                    {
                        // No settingsId found for company - try to use first available stripe_settings record
                        _logger.LogInformation("[Stripe] Company {CompanyId} has no StripeSettingsId, trying to use first available stripe_settings record", companyId);
                        var defaultStripeSettings = await _context.StripeSettings
                            .AsNoTracking()
                            .OrderBy(ss => ss.CreatedAt)
                            .FirstOrDefaultAsync();

                        if (defaultStripeSettings != null)
                        {
                            secretKey = DecryptKey(defaultStripeSettings.SecretKey);
                            publishableKey = DecryptKey(defaultStripeSettings.PublishableKey);
                            webhookSecret = DecryptKey(defaultStripeSettings.WebhookSecret);
                            
                            if (string.IsNullOrWhiteSpace(secretKey))
                            {
                                _logger.LogWarning("[Stripe] Decrypted secret key is null or empty for stripe_settings Id={SettingsId}, Name={SettingsName}", 
                                    defaultStripeSettings.Id, defaultStripeSettings.Name);
                            }
                            else
                            {
                                _logger.LogInformation("[Stripe] Using default stripe_settings record (Id={SettingsId}, Name={SettingsName}) for company {CompanyId}", 
                                    defaultStripeSettings.Id, defaultStripeSettings.Name, companyId);
                            }
                        }
                        else
                        {
                            _logger.LogWarning("[Stripe] No stripe_settings records found in database for company {CompanyId}", companyId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[Stripe] Error loading company-specific settings, falling back to global settings");
                }
            }

            // Fallback to settings table if not found in new tables
            // SettingsService.GetValueAsync already decrypts keys automatically
            if (string.IsNullOrWhiteSpace(secretKey))
            {
                secretKey = await _settingsService.GetValueAsync(SecretKeySetting) ?? string.Empty;
                publishableKey = await _settingsService.GetValueAsync(PublishableKeySetting);
                webhookSecret = await _settingsService.GetValueAsync(WebhookSecretSetting);
                
                if (!string.IsNullOrWhiteSpace(secretKey))
                {
                    _logger.LogInformation("[Stripe] Using settings from settings table (fallback) - keys are already decrypted by SettingsService");
                }
            }

            if (string.IsNullOrWhiteSpace(secretKey))
            {
                throw new InvalidOperationException($"Stripe secret key is not configured. Set the '{SecretKeySetting}' setting or configure stripe_settings table.");
            }

            // secretKey is guaranteed to be non-null at this point due to the check above
            var nonNullSecretKey = secretKey!;

            // All keys are now decrypted (plain text) and ready to use with Stripe API
            _logger.LogInformation("[Stripe] Initialized for company {CompanyId}. Using secret key prefix: {SecretKeyPrefix}", 
                companyId?.ToString() ?? "GLOBAL", 
                nonNullSecretKey.Substring(0, Math.Min(10, nonNullSecretKey.Length)));

            StripeConfiguration.ApiKey = nonNullSecretKey;
            
            if (companyId.HasValue)
            {
                // Cache company-specific settings (secretKey is guaranteed non-null here)
                _companySettingsCache[companyId.Value] = (nonNullSecretKey, publishableKey, webhookSecret);
                _globalPublishableKey = publishableKey;
                _globalWebhookSecret = webhookSecret;
            }
            else
            {
                // Global initialization
                _globalPublishableKey = publishableKey;
                _globalWebhookSecret = webhookSecret;
                _globalInitialized = true;
            }

        }
        finally
        {
            _initializationLock.Release();
        }
    }

    /// <summary>
    /// Helper method to get RequestOptions with connected account context for company operations
    /// SECURITY: Ensures all payment operations use connected accounts, never platform account
    /// </summary>
    private async Task<RequestOptions> GetStripeRequestOptionsAsync(Guid? companyId)
    {
        var requestOptions = new RequestOptions();

        if (!companyId.HasValue)
        {
            _logger.LogWarning("[Stripe] No companyId provided - operations without companyId may use platform account");
            return requestOptions;
        }

        // Get API key from cache or database
        string? apiKey = null;
        if (_companySettingsCache.ContainsKey(companyId.Value))
        {
            apiKey = _companySettingsCache[companyId.Value].secretKey;
        }
        else
        {
            // Look up from database
            var company = await _context.Companies
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == companyId.Value);

            if (company?.StripeSettingsId.HasValue == true)
            {
                var stripeSettings = await _context.StripeSettings
                    .AsNoTracking()
                    .FirstOrDefaultAsync(ss => ss.Id == company.StripeSettingsId.Value);

                if (stripeSettings != null)
                {
                    apiKey = DecryptKey(stripeSettings.SecretKey);
                }
            }
        }

        if (!string.IsNullOrEmpty(apiKey))
        {
            requestOptions.ApiKey = apiKey;

            // Get Stripe connected account ID from stripe_company table
            try
            {
                var company = await _context.Companies
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == companyId.Value);

                if (company?.StripeSettingsId.HasValue == true)
                {
                    var stripeCompany = await _context.StripeCompanies
                        .AsNoTracking()
                        .FirstOrDefaultAsync(sc => sc.CompanyId == companyId.Value && sc.SettingsId == company.StripeSettingsId.Value);

                    if (stripeCompany != null && !string.IsNullOrEmpty(stripeCompany.StripeAccountId))
                    {
                        try
                        {
                            // Decrypt the account ID
                            var accountId = _encryptionService.Decrypt(stripeCompany.StripeAccountId);
                            requestOptions.StripeAccount = accountId;
                            _logger.LogInformation("[Stripe] Using connected account {StripeAccountId} for company {CompanyId}", accountId, companyId);
                        }
                        catch
                        {
                            // If decryption fails, use as-is (might be plain text)
                            requestOptions.StripeAccount = stripeCompany.StripeAccountId;
                            _logger.LogInformation("[Stripe] Using connected account {StripeAccountId} for company {CompanyId} (plain text)", stripeCompany.StripeAccountId, companyId);
                        }
                    }
                    else
                    {
                        // PROHIBIT operations on platform account when companyId is provided
                        _logger.LogError("[Stripe] No stripe_company record found or StripeAccountId is missing for company {CompanyId}. Operation PROHIBITED - operations must use company account, not platform account.", companyId);
                        throw new InvalidOperationException($"Stripe account not configured for company {companyId}. Operations must be processed through the company's connected account.");
                    }
                }
            }
            catch (Exception ex) when (!(ex is InvalidOperationException))
            {
                _logger.LogError(ex, "[Stripe] Error getting connected account ID for company {CompanyId}. Operation PROHIBITED - operations must use company account, not platform account.", companyId);
                throw new InvalidOperationException($"Failed to get Stripe account for company {companyId}. Operations must be processed through the company's connected account.", ex);
            }
        }
        else
        {
            _logger.LogWarning("[Stripe] No API key found for company {CompanyId}", companyId);
        }

        return requestOptions;
    }

    public async Task<Models.Customer> CreateCustomerAsync(Models.Customer customer)
    {
        await EnsureInitializedAsync(customer.CompanyId);
        _logger.LogWarning("[Stripe] CreateCustomerAsync payload: {@Customer}", customer);
        try
        {
            var customerCreateOptions = new CustomerCreateOptions
            {
                Email = customer.Email,
                Name = $"{customer.FirstName} {customer.LastName}",
                Phone = customer.Phone,
                Address = new AddressOptions
                {
                    Line1 = customer.Address,
                    City = customer.City,
                    State = customer.State,
                    Country = customer.Country,
                    PostalCode = customer.PostalCode
                },
                Metadata = new Dictionary<string, string>
                {
                    { "customer_id", customer.Id.ToString() },
                    { "drivers_license", "" }
                }
            };

            // SECURITY: Use connected account - never create customers on platform account
            var requestOptions = await GetStripeRequestOptionsAsync(customer.CompanyId);
            var stripeCustomer = await new CustomerService().CreateAsync(customerCreateOptions, requestOptions);
            
            // Update the customer with Stripe ID
            customer.StripeCustomerId = stripeCustomer.Id;
            
            return customer;
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Error creating Stripe customer for {Email}", customer.Email);
            throw new InvalidOperationException($"Failed to create Stripe customer: {ex.Message}", ex);
        }
    }

    public async Task<Models.Customer> UpdateCustomerAsync(Models.Customer customer)
    {
        await EnsureInitializedAsync(customer.CompanyId);
        _logger.LogWarning("[Stripe] UpdateCustomerAsync payload: {@Customer}", customer);
        try
        {
            if (string.IsNullOrEmpty(customer.StripeCustomerId))
            {
                throw new InvalidOperationException("Customer does not have a Stripe ID");
            }

            var customerUpdateOptions = new CustomerUpdateOptions
            {
                Email = customer.Email,
                Name = $"{customer.FirstName} {customer.LastName}",
                Phone = customer.Phone,
                Address = new AddressOptions
                {
                    Line1 = customer.Address,
                    City = customer.City,
                    State = customer.State,
                    Country = customer.Country,
                    PostalCode = customer.PostalCode
                },
                Metadata = new Dictionary<string, string>
                {
                    { "customer_id", customer.Id.ToString() },
                    { "drivers_license", "" }
                }
            };

            // SECURITY: Use connected account - never update customers on platform account
            var requestOptions = await GetStripeRequestOptionsAsync(customer.CompanyId);
            var stripeCustomer = await new CustomerService().UpdateAsync(customer.StripeCustomerId, customerUpdateOptions, requestOptions);
            
            return customer;
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Error updating Stripe customer {StripeCustomerId}", customer.StripeCustomerId);
            throw new InvalidOperationException($"Failed to update Stripe customer: {ex.Message}", ex);
        }
    }

    public async Task<Models.Customer> GetCustomerAsync(string stripeCustomerId, Guid? companyId = null)
    {
        await EnsureInitializedAsync(companyId);
        _logger.LogWarning("[Stripe] GetCustomerAsync stripeCustomerId={StripeCustomerId} companyId={CompanyId}", stripeCustomerId, companyId);
        try
        {
            // SECURITY: Use connected account - never get customers from platform account
            var requestOptions = await GetStripeRequestOptionsAsync(companyId);
            var stripeCustomer = await new CustomerService().GetAsync(stripeCustomerId, null, requestOptions);
            
            // Convert Stripe customer to our customer model
            var customer = new Models.Customer
            {
                StripeCustomerId = stripeCustomer.Id,
                Email = stripeCustomer.Email,
                FirstName = stripeCustomer.Name?.Split(' ').FirstOrDefault() ?? "",
                LastName = stripeCustomer.Name?.Split(' ').Skip(1).FirstOrDefault() ?? "",
                Phone = stripeCustomer.Phone,
                Address = stripeCustomer.Address?.Line1,
                City = stripeCustomer.Address?.City,
                State = stripeCustomer.Address?.State,
                Country = stripeCustomer.Address?.Country,
                PostalCode = stripeCustomer.Address?.PostalCode
            };

            return customer;
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Error retrieving Stripe customer {StripeCustomerId}", stripeCustomerId);
            throw new InvalidOperationException($"Failed to retrieve Stripe customer: {ex.Message}", ex);
        }
    }

    public async Task<PaymentMethod> CreatePaymentMethodAsync(string customerId, string paymentMethodId, Guid? companyId = null)
    {
        await EnsureInitializedAsync(companyId);
        _logger.LogWarning("[Stripe] CreatePaymentMethodAsync customerId={CustomerId} paymentMethodId={PaymentMethodId} companyId={CompanyId}", customerId, paymentMethodId, companyId);
        try
        {
            var attachOptions = new PaymentMethodAttachOptions
            {
                Customer = customerId
            };

            // SECURITY: Use connected account - never attach payment methods on platform account
            var requestOptions = await GetStripeRequestOptionsAsync(companyId);
            var paymentMethod = await new PaymentMethodService().AttachAsync(paymentMethodId, attachOptions, requestOptions);

            return paymentMethod;
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Error attaching payment method {PaymentMethodId} to customer {CustomerId}", paymentMethodId, customerId);
            throw new InvalidOperationException($"Failed to attach payment method: {ex.Message}", ex);
        }
    }

    public async Task<PaymentMethod> GetPaymentMethodAsync(string paymentMethodId, Guid? companyId = null)
    {
        await EnsureInitializedAsync(companyId);
        _logger.LogWarning("[Stripe] GetPaymentMethodAsync paymentMethodId={PaymentMethodId} companyId={CompanyId}", paymentMethodId, companyId);
        try
        {
            // SECURITY: Use connected account - never get payment methods from platform account
            var requestOptions = await GetStripeRequestOptionsAsync(companyId);
            var paymentMethod = await new PaymentMethodService().GetAsync(paymentMethodId, null, requestOptions);
            return paymentMethod;
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Error retrieving payment method {PaymentMethodId}", paymentMethodId);
            throw new InvalidOperationException($"Failed to retrieve payment method: {ex.Message}", ex);
        }
    }

    public async Task<PaymentIntent> CreatePaymentIntentAsync(
        decimal amount,
        string currency,
        string customerId,
        string? paymentMethodId = null,
        IDictionary<string, string>? metadata = null,
        bool captureImmediately = true,
        bool requestExtendedAuthorization = false,
        Guid? companyId = null)
    {
        await EnsureInitializedAsync(companyId);
        _logger.LogWarning("[Stripe] CreatePaymentIntentAsync amount={Amount} currency={Currency} customerId={CustomerId} paymentMethodId={PaymentMethodId} captureImmediately={CaptureImmediately}",
            amount, currency, customerId, paymentMethodId, captureImmediately);
        try
        {
            // For connected accounts, customers are separate entities
            // The customerId from database might be from platform account
            // We'll use it if provided, but Stripe will handle errors if it doesn't exist on connected account
            var paymentIntentOptions = new PaymentIntentCreateOptions
            {
                Amount = (long)(amount * 100), // Convert to cents
                Currency = currency.ToLower(),
                Customer = !string.IsNullOrEmpty(customerId) ? customerId : null, // May be null for connected accounts
                PaymentMethod = paymentMethodId,
                ConfirmationMethod = "manual",
                Confirm = paymentMethodId != null,
                CaptureMethod = captureImmediately ? "automatic" : "manual",
                Metadata = new Dictionary<string, string>
                {
                    { "source", "car_rental_api" }
                }
            };

            if (!captureImmediately)
            {
                paymentIntentOptions.PaymentMethodTypes = new List<string> { "card" };

                if (requestExtendedAuthorization)
                {
                    paymentIntentOptions.PaymentMethodOptions = new PaymentIntentPaymentMethodOptionsOptions
                    {
                        Card = new PaymentIntentPaymentMethodOptionsCardOptions
                        {
                            RequestExtendedAuthorization = "if_available"
                        }
                    };
                }
            }

            if (metadata != null)
            {
                foreach (var kvp in metadata)
                {
                    if (!string.IsNullOrWhiteSpace(kvp.Key) && !paymentIntentOptions.Metadata.ContainsKey(kvp.Key))
                    {
                        paymentIntentOptions.Metadata[kvp.Key] = kvp.Value;
                    }
                }
            }

            // Use RequestOptions with company-specific API key and connected account ID if available
            RequestOptions? requestOptions = null;
            if (companyId.HasValue)
            {
                // Get Stripe API key from cache or look it up
                string? apiKey = null;
                if (_companySettingsCache.ContainsKey(companyId.Value))
                {
                    apiKey = _companySettingsCache[companyId.Value].secretKey;
                }
                else
                {
                    // Look up from database
                    var company = await _context.Companies
                        .AsNoTracking()
                        .FirstOrDefaultAsync(c => c.Id == companyId.Value);

                    if (company?.StripeSettingsId.HasValue == true)
                    {
                        var stripeSettings = await _context.StripeSettings
                            .AsNoTracking()
                            .FirstOrDefaultAsync(ss => ss.Id == company.StripeSettingsId.Value);

                        if (stripeSettings != null)
                        {
                            apiKey = DecryptKey(stripeSettings.SecretKey);
                        }
                    }
                }

                if (!string.IsNullOrEmpty(apiKey))
                {
                    requestOptions = new RequestOptions { ApiKey = apiKey };
                    
                    // Get Stripe connected account ID from stripe_company table
                    try
                    {
                        var company = await _context.Companies
                            .AsNoTracking()
                            .FirstOrDefaultAsync(c => c.Id == companyId.Value);

                        if (company?.StripeSettingsId.HasValue == true)
                        {
                            var stripeCompany = await _context.StripeCompanies
                                .AsNoTracking()
                                .FirstOrDefaultAsync(sc => sc.CompanyId == companyId.Value && sc.SettingsId == company.StripeSettingsId.Value);

                            if (stripeCompany != null && !string.IsNullOrEmpty(stripeCompany.StripeAccountId))
                            {
                                try
                                {
                                    // Decrypt the account ID
                                    var accountId = _encryptionService.Decrypt(stripeCompany.StripeAccountId);
                                    requestOptions.StripeAccount = accountId;
                                    _logger.LogInformation("[Stripe] Using connected account {StripeAccountId} for PaymentIntent creation", accountId);
                                }
                                catch
                                {
                                    // If decryption fails, use as-is (might be plain text)
                                    requestOptions.StripeAccount = stripeCompany.StripeAccountId;
                                    _logger.LogInformation("[Stripe] Using connected account {StripeAccountId} for PaymentIntent creation (plain text)", stripeCompany.StripeAccountId);
                                }
                            }
                            else
                            {
                                // PROHIBIT payment creation on platform account when companyId is provided
                                // Payments must go to the company's connected account, not Aegis platform account
                                _logger.LogError("[Stripe] No stripe_company record found or StripeAccountId is missing for company {CompanyId}. PaymentIntent creation PROHIBITED - payments must go to company account, not platform account.", companyId);
                                throw new InvalidOperationException($"Stripe account not configured for company {companyId}. Payments must be processed through the company's connected account.");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[Stripe] Error getting connected account ID for company {CompanyId}. PaymentIntent creation PROHIBITED - payments must go to company account, not platform account.", companyId);
                        throw new InvalidOperationException($"Failed to get Stripe account for company {companyId}. Payments must be processed through the company's connected account.", ex);
                    }
                }
                else
                {
                    _logger.LogWarning("[Stripe] No API key found for company {CompanyId}, using global settings", companyId);
                }
            }

            var service = new PaymentIntentService();
            var paymentIntent = await service.CreateAsync(paymentIntentOptions, requestOptions);
            
            return paymentIntent;
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Error creating payment intent for customer {CustomerId}", customerId);
            throw new InvalidOperationException($"Failed to create payment intent: {ex.Message}", ex);
        }
    }

    public async Task<PaymentIntent> ConfirmPaymentIntentAsync(string paymentIntentId, Guid? companyId = null)
    {
        await EnsureInitializedAsync(companyId);
        _logger.LogWarning("[Stripe] ConfirmPaymentIntentAsync paymentIntentId={PaymentIntentId} companyId={CompanyId}", paymentIntentId, companyId);
        try
        {
            // Use RequestOptions with company-specific API key and connected account ID if available
            RequestOptions? requestOptions = null;
            if (companyId.HasValue)
            {
                // Get Stripe API key from cache or look it up
                string? apiKey = null;
                if (_companySettingsCache.ContainsKey(companyId.Value))
                {
                    apiKey = _companySettingsCache[companyId.Value].secretKey;
                }
                else
                {
                    // Look up from database
                    var company = await _context.Companies
                        .AsNoTracking()
                        .FirstOrDefaultAsync(c => c.Id == companyId.Value);

                    if (company?.StripeSettingsId.HasValue == true)
                    {
                        var stripeSettings = await _context.StripeSettings
                            .AsNoTracking()
                            .FirstOrDefaultAsync(ss => ss.Id == company.StripeSettingsId.Value);

                        if (stripeSettings != null)
                        {
                            apiKey = DecryptKey(stripeSettings.SecretKey);
                        }
                    }
                }

                if (!string.IsNullOrEmpty(apiKey))
                {
                    requestOptions = new RequestOptions { ApiKey = apiKey };
                    
                    // Get Stripe connected account ID from stripe_company table
                    try
                    {
                        var company = await _context.Companies
                            .AsNoTracking()
                            .FirstOrDefaultAsync(c => c.Id == companyId.Value);

                        if (company?.StripeSettingsId.HasValue == true)
                        {
                            var stripeCompany = await _context.StripeCompanies
                                .AsNoTracking()
                                .FirstOrDefaultAsync(sc => sc.CompanyId == companyId.Value && sc.SettingsId == company.StripeSettingsId.Value);

                            if (stripeCompany != null && !string.IsNullOrEmpty(stripeCompany.StripeAccountId))
                            {
                                try
                                {
                                    // Decrypt the account ID
                                    var accountId = _encryptionService.Decrypt(stripeCompany.StripeAccountId);
                                    requestOptions.StripeAccount = accountId;
                                    _logger.LogInformation("[Stripe] Using connected account {StripeAccountId} for PaymentIntent confirmation", accountId);
                                }
                                catch
                                {
                                    // If decryption fails, use as-is (might be plain text)
                                    requestOptions.StripeAccount = stripeCompany.StripeAccountId;
                                    _logger.LogInformation("[Stripe] Using connected account {StripeAccountId} for PaymentIntent confirmation (plain text)", stripeCompany.StripeAccountId);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[Stripe] Error getting connected account ID for company {CompanyId}, proceeding without connected account", companyId);
                    }
                }
            }

            var paymentIntent = await new PaymentIntentService().ConfirmAsync(paymentIntentId, null, requestOptions);
            return paymentIntent;
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Error confirming payment intent {PaymentIntentId}", paymentIntentId);
            throw new InvalidOperationException($"Failed to confirm payment intent: {ex.Message}", ex);
        }
    }

    public async Task<PaymentIntent> CancelPaymentIntentAsync(string paymentIntentId, Guid? companyId = null)
    {
        await EnsureInitializedAsync();
        _logger.LogWarning("[Stripe] CancelPaymentIntentAsync paymentIntentId={PaymentIntentId}, companyId={CompanyId}", paymentIntentId, companyId);
        try
        {
            // Get RequestOptions with connected account context (same as other payment operations)
            var requestOptions = await GetStripeRequestOptionsAsync(companyId);
            var paymentIntent = await new PaymentIntentService().CancelAsync(paymentIntentId, null, requestOptions);
            return paymentIntent;
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Error canceling payment intent {PaymentIntentId} for company {CompanyId}", paymentIntentId, companyId);
            throw new InvalidOperationException($"Failed to cancel payment intent: {ex.Message}", ex);
        }
    }

    public async Task<PaymentIntent> CapturePaymentIntentAsync(string paymentIntentId, decimal? amountToCapture = null, string? currency = null, Guid? companyId = null)
    {
        await EnsureInitializedAsync();
        _logger.LogWarning("[Stripe] CapturePaymentIntentAsync paymentIntentId={PaymentIntentId} amount={Amount} currency={Currency} companyId={CompanyId}", paymentIntentId, amountToCapture, currency, companyId);
        try
        {
            // Get RequestOptions with connected account context (same as other payment operations)
            var requestOptions = await GetStripeRequestOptionsAsync(companyId);

            var options = new PaymentIntentCaptureOptions();
            if (amountToCapture.HasValue)
            {
                // Get the payment intent to determine currency if not provided
                if (string.IsNullOrEmpty(currency))
                {
                    var paymentIntentService = new PaymentIntentService();
                    var existingIntent = await paymentIntentService.GetAsync(paymentIntentId, null, requestOptions);
                    currency = existingIntent.Currency?.ToLower() ?? "usd";
                }
                
                // Convert amount to smallest currency unit based on currency
                // Most currencies use 2 decimal places (multiply by 100)
                // Some currencies like JPY use 0 decimal places (no multiplication)
                int decimalPlaces = GetCurrencyDecimalPlaces(currency);
                long amountInSmallestUnit = (long)(amountToCapture.Value * (decimal)Math.Pow(10, decimalPlaces));
                
                options.AmountToCapture = amountInSmallestUnit;
                _logger.LogInformation("[Stripe] Converting {Amount} {Currency} to {SmallestUnit} (decimal places: {DecimalPlaces})", 
                    amountToCapture.Value, currency, amountInSmallestUnit, decimalPlaces);
            }

            var paymentIntent = await new PaymentIntentService().CaptureAsync(paymentIntentId, options, requestOptions);
            return paymentIntent;
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Error capturing payment intent {PaymentIntentId}", paymentIntentId);
            throw new InvalidOperationException($"Failed to capture payment intent: {ex.Message}", ex);
        }
    }
    
    private int GetCurrencyDecimalPlaces(string currency)
    {
        // Stripe currencies with 0 decimal places
        var zeroDecimalCurrencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "bif", "clp", "djf", "gnf", "jpy", "kmf", "krw", "mga", "pyg", "rwf", "ugx", "vnd", "vuv", "xaf", "xof", "xpf"
        };
        
        if (zeroDecimalCurrencies.Contains(currency))
            return 0;
        
        // All other currencies use 2 decimal places
        return 2;
    }

    public async Task<Refund> CreateRefundAsync(string paymentIntentId, decimal amount, Guid? companyId = null)
    {
        await EnsureInitializedAsync(companyId);
        _logger.LogWarning("[Stripe] CreateRefundAsync paymentIntentId={PaymentIntentId} amount={Amount} companyId={CompanyId}", paymentIntentId, amount, companyId);
        try
        {
            var refundOptions = new RefundCreateOptions
            {
                PaymentIntent = paymentIntentId,
                Amount = (long)(amount * 100) // Convert to cents
            };

            // SECURITY: Use connected account - never create refunds on platform account
            var requestOptions = await GetStripeRequestOptionsAsync(companyId);
            var refund = await new RefundService().CreateAsync(refundOptions, requestOptions);

            return refund;
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Error creating refund for payment intent {PaymentIntentId}", paymentIntentId);
            throw new InvalidOperationException($"Failed to create refund: {ex.Message}", ex);
        }
    }

    // Stripe Connect methods - new implementations
    public async Task<Account> CreateConnectedAccountAsync(string subdomain, string accountType, string country, Guid? companyId = null, string? domainName = null)
    {
        await EnsureInitializedAsync(companyId);
        _logger.LogWarning("[Stripe] CreateConnectedAccountAsync subdomain={Subdomain} accountType={AccountType} country={Country}, companyId={CompanyId}, domainName={DomainName}", subdomain, accountType, country, companyId, domainName);
        try
        {
            // Convert country name to ISO 3166-1 alpha-2 code if needed
            var countryCode = CountryHelper.NormalizeToIsoCode(country);
            
            // Construct full domain name if not provided
            string fullDomainName;
            if (!string.IsNullOrWhiteSpace(domainName))
            {
                fullDomainName = domainName;
            }
            else
            {
                // Try to get DNS zone name from settings
                var dnsZoneName = await _settingsService.GetValueAsync("azure.dnsZoneName");
                if (!string.IsNullOrWhiteSpace(dnsZoneName))
                {
                    fullDomainName = $"{subdomain.ToLower()}.{dnsZoneName}";
                }
                else
                {
                    // Fallback to default domain
                    fullDomainName = $"{subdomain.ToLower()}.aegis-rental.com";
                }
            }
            
            // Create account without email (email will be collected during onboarding)
            // Use subdomain in metadata as identifier
            var options = new AccountCreateOptions
            {
                Type = accountType, // "express" or "standard"
                Country = countryCode,
                Metadata = new Dictionary<string, string>
                {
                    { "subdomain", subdomain },
                    { "identifier", subdomain } // Use subdomain as the primary identifier
                },
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

            // Add BusinessProfile only if domain name is valid
            if (!string.IsNullOrWhiteSpace(fullDomainName))
            {
                // Validate URL format before adding
                var url = $"https://{fullDomainName}";
                if (Uri.TryCreate(url, UriKind.Absolute, out var validUri) && 
                    (validUri.Scheme == Uri.UriSchemeHttp || validUri.Scheme == Uri.UriSchemeHttps))
                {
                    options.BusinessProfile = new AccountBusinessProfileOptions
                    {
                        Name = fullDomainName, // Use full domain as account name
                        Url = url // Full URL
                    };
                    _logger.LogInformation("[Stripe] Adding BusinessProfile: Name={Name}, Url={Url}", fullDomainName, url);
                }
                else
                {
                    _logger.LogWarning("[Stripe] Invalid domain name format, skipping BusinessProfile: {DomainName}", fullDomainName);
                }
            }

            var service = new AccountService();
            return await service.CreateAsync(options);
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Error creating connected account for subdomain {Subdomain}", subdomain);
            throw new InvalidOperationException($"Failed to create connected account: {ex.Message}", ex);
        }
    }


    public async Task<AccountLink> CreateAccountLinkAsync(string accountId, string returnUrl, string refreshUrl, Guid? companyId = null)
    {
        await EnsureInitializedAsync(companyId);
        try
        {
            // Check account status first (use companyId to get account with correct Stripe settings)
            try
            {
                var account = await GetAccountAsync(accountId, companyId);
                
                // Check if account is rejected (but allow past_due accounts to create links so they can complete requirements)
                if (account.Requirements?.DisabledReason != null)
                {
                    var disabledReason = account.Requirements.DisabledReason;
                    
                    // Allow creating links for past_due accounts - they can still complete onboarding
                    if (disabledReason.Contains("past_due", StringComparison.OrdinalIgnoreCase) ||
                        disabledReason.Contains("requirements.past_due", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation("Account {AccountId} has past due requirements, but allowing link creation to complete onboarding", accountId);
                        // Continue - allow link creation
                    }
                    else if (disabledReason.Contains("rejected", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogError("Cannot create account link for {AccountId}: Account is rejected. Reason: {Reason}", 
                            accountId, disabledReason);
                        throw new InvalidOperationException(
                            $"Account {accountId} has been rejected by Stripe. " +
                            $"The account must be deleted and recreated. " +
                            $"Reason: {disabledReason}");
                    }
                    else
                    {
                        _logger.LogWarning("Account {AccountId} is disabled. Reason: {Reason}. Attempting to create link anyway.", 
                            accountId, disabledReason);
                        // Continue - try to create link, let Stripe decide if it's allowed
                    }
                }
            }
            catch (InvalidOperationException)
            {
                // Re-throw our custom exceptions
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not check account status before creating link, proceeding anyway");
            }

            var options = new AccountLinkCreateOptions
            {
                Account = accountId,
                RefreshUrl = refreshUrl,
                ReturnUrl = returnUrl,
                Type = "account_onboarding"
            };

            var service = new AccountLinkService();
            return await service.CreateAsync(options);
        }
        catch (StripeException ex) when (ex.Message.Contains("rejected", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogError(ex, "Account {AccountId} was rejected by Stripe. Cannot create account link.", accountId);
            throw new InvalidOperationException(
                $"Account {accountId} has been rejected by Stripe and cannot create account links. " +
                $"Please delete the rejected account in Stripe dashboard and try again. " +
                $"Stripe error: {ex.Message}", ex);
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Error creating account link for {AccountId}. Stripe error: {StripeError}", accountId, ex.Message);
            throw new InvalidOperationException($"Failed to create account link: {ex.Message}", ex);
        }
    }

    public async Task<Account> GetAccountAsync(string accountId, Guid? companyId = null)
    {
        await EnsureInitializedAsync(companyId);
        try
        {
            var service = new AccountService();
            return await service.GetAsync(accountId);
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Error retrieving account {AccountId}", accountId);
            throw new InvalidOperationException($"Failed to retrieve account: {ex.Message}", ex);
        }
    }

    public async Task<Account> UpdateAccountAsync(string accountId)
    {
        await EnsureInitializedAsync();
        _logger.LogWarning("[Stripe] UpdateAccountAsync accountId={AccountId}", accountId);
        try
        {
            // For now, just return the account - update logic can be added later
            var service = new AccountService();
            return await service.GetAsync(accountId);
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Error updating account {AccountId}", accountId);
            throw new InvalidOperationException($"Failed to update account: {ex.Message}", ex);
        }
    }

    public async Task<Transfer> CreateTransferAsync(string destinationAccountId, long amount, string currency, string transferGroup)
    {
        await EnsureInitializedAsync();
        _logger.LogWarning("[Stripe] CreateTransferAsync destinationAccountId={DestinationAccountId} amount={Amount} currency={Currency} transferGroup={TransferGroup}", 
            destinationAccountId, amount, currency, transferGroup);
        try
        {
            var options = new TransferCreateOptions
            {
                Amount = amount,
                Currency = currency,
                Destination = destinationAccountId,
                TransferGroup = transferGroup
            };

            var service = new TransferService();
            return await service.CreateAsync(options);
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Error creating transfer to account {AccountId}", destinationAccountId);
            throw new InvalidOperationException($"Failed to create transfer: {ex.Message}", ex);
        }
    }

    public async Task<Transfer> GetTransferAsync(string transferId)
    {
        await EnsureInitializedAsync();
        _logger.LogWarning("[Stripe] GetTransferAsync transferId={TransferId}", transferId);
        try
        {
            var service = new TransferService();
            return await service.GetAsync(transferId);
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Error retrieving transfer {TransferId}", transferId);
            throw new InvalidOperationException($"Failed to retrieve transfer: {ex.Message}", ex);
        }
    }

    public async Task<PaymentIntent> CreateSecurityDepositAsync(
        string customerId,
        decimal amount,
        string currency,
        string paymentMethodId,
        string connectedAccountId,
        Dictionary<string, string> metadata,
        Guid? companyId = null)
    {
        await EnsureInitializedAsync(companyId);
        _logger.LogWarning("[Stripe] CreateSecurityDepositAsync customerId={CustomerId} amount={Amount} currency={Currency} connectedAccountId={ConnectedAccountId}",
            customerId, amount, currency, connectedAccountId);
        try
        {
            var options = new PaymentIntentCreateOptions
            {
                Amount = (long)(amount * 100),
                Currency = currency.ToLower(),
                Customer = customerId,
                PaymentMethod = paymentMethodId,
                CaptureMethod = "manual", // Authorization only
                Confirm = true,
                Metadata = metadata
                // REMOVED: TransferData - using direct charges instead of destination charges
            };

            // SECURITY: Use connected account - never create security deposits on platform account
            // Direct charge pattern - charge goes directly to connected account, not platform
            var requestOptions = await GetStripeRequestOptionsAsync(companyId);

            var service = new PaymentIntentService();
            return await service.CreateAsync(options, requestOptions);
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Error creating security deposit payment intent for customer {CustomerId}", customerId);
            throw new InvalidOperationException($"Failed to create security deposit: {ex.Message}", ex);
        }
    }

    // Legacy methods (kept for backward compatibility)
    [Obsolete("Use CreateConnectedAccountAsync(string subdomain, string accountType, string country) instead.")]
    public async Task<Account> CreateConnectedAccountLegacyAsync(string email, string country = "US")
    {
        // Legacy method: extract subdomain from email or use email as subdomain
        // This is for backward compatibility only
        var subdomain = email.Contains("@") ? email.Split("@")[0] : email;
        return await CreateConnectedAccountAsync(subdomain, "express", country);
    }

    [Obsolete("Use GetAccountAsync instead.")]
    public async Task<Account> GetConnectedAccountAsync(string accountId)
    {
        return await GetAccountAsync(accountId);
    }

    [Obsolete("Use CreateTransferAsync(string destinationAccountId, long amount, string currency, string transferGroup) instead.")]
    public async Task<Transfer> CreateTransferLegacyAsync(string accountId, long amount, string currency = "usd")
    {
        // Call the new method with empty transfer group for backward compatibility
        return await CreateTransferAsync(accountId, amount, currency, $"legacy_{DateTime.UtcNow.Ticks}");
    }

    public async Task<WebhookEndpoint> CreateWebhookEndpointAsync(string url, string[] events)
    {
        await EnsureInitializedAsync();
        _logger.LogWarning("[Stripe] CreateWebhookEndpointAsync url={Url} events={Events}", url, events);
        try
        {
            var webhookOptions = new WebhookEndpointCreateOptions
            {
                Url = url,
                EnabledEvents = events.ToList()
            };

            var webhook = await new WebhookEndpointService().CreateAsync(webhookOptions);
            return webhook;
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Error creating webhook endpoint for {Url}", url);
            throw new InvalidOperationException($"Failed to create webhook endpoint: {ex.Message}", ex);
        }
    }

    public async Task<Event> ConstructWebhookEventAsync(string payload, string signature, string webhookSecret)
    {
        await EnsureInitializedAsync();
        _logger.LogWarning("[Stripe] ConstructWebhookEventAsync signature={Signature} incomingSecret={IncomingSecret} storedSecret={StoredSecret}", signature, webhookSecret, _globalWebhookSecret);
        try
        {
            var webhookSecretKey = !string.IsNullOrWhiteSpace(_globalWebhookSecret)
                ? _globalWebhookSecret!
                : webhookSecret;

            if (string.IsNullOrWhiteSpace(webhookSecretKey))
            {
                throw new InvalidOperationException("Stripe webhook secret is not configured.");
            }
            var stripeEvent = EventUtility.ConstructEvent(payload, signature, webhookSecretKey);
            return await Task.FromResult(stripeEvent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error constructing webhook event");
            throw new InvalidOperationException($"Failed to construct webhook event: {ex.Message}", ex);
        }
    }

    public async Task<Session> CreateCheckoutSessionAsync(SessionCreateOptions options, RequestOptions? requestOptions = null, Guid? companyId = null)
    {
        await EnsureInitializedAsync(companyId);
        
        // If RequestOptions is provided with StripeAccount, preserve it (for connected accounts)
        // Otherwise, if companyId is provided, use company-specific API key in RequestOptions
        if (requestOptions != null && !string.IsNullOrEmpty(requestOptions.StripeAccount))
        {
            // RequestOptions already has StripeAccount set (from PaymentsController)
            // Just ensure API key is set if not already
            if (string.IsNullOrEmpty(requestOptions.ApiKey) && companyId.HasValue && _companySettingsCache.ContainsKey(companyId.Value))
            {
                var settings = _companySettingsCache[companyId.Value];
                requestOptions.ApiKey = settings.secretKey;
            }
            _logger.LogInformation("[Stripe] CreateCheckoutSessionAsync: Using provided RequestOptions with StripeAccount={StripeAccount}", requestOptions.StripeAccount);
        }
        else if (companyId.HasValue && _companySettingsCache.ContainsKey(companyId.Value))
        {
            // No RequestOptions provided, create new one with company-specific API key
            var settings = _companySettingsCache[companyId.Value];
            requestOptions ??= new RequestOptions();
            requestOptions.ApiKey = settings.secretKey;
            
            // Try to get connected account ID from stripe_company table
            try
            {
                var company = await _context.Companies
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == companyId.Value);

                if (company?.StripeSettingsId.HasValue == true)
                {
                    var stripeCompany = await _context.StripeCompanies
                        .AsNoTracking()
                        .FirstOrDefaultAsync(sc => sc.CompanyId == companyId.Value && sc.SettingsId == company.StripeSettingsId.Value);

                    if (stripeCompany != null && !string.IsNullOrEmpty(stripeCompany.StripeAccountId))
                    {
                        try
                        {
                            var accountId = _encryptionService.Decrypt(stripeCompany.StripeAccountId);
                            requestOptions.StripeAccount = accountId;
                            _logger.LogInformation("[Stripe] CreateCheckoutSessionAsync: Using connected account {StripeAccountId} for company {CompanyId}", accountId, companyId);
                        }
                        catch
                        {
                            requestOptions.StripeAccount = stripeCompany.StripeAccountId;
                            _logger.LogInformation("[Stripe] CreateCheckoutSessionAsync: Using connected account {StripeAccountId} for company {CompanyId} (plain text)", stripeCompany.StripeAccountId, companyId);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Stripe] Error getting connected account ID for company {CompanyId} in CreateCheckoutSessionAsync", companyId);
            }
        }
        
        _logger.LogWarning("[Stripe] CreateCheckoutSessionAsync options={Options} requestOptions={RequestOptions}", options, requestOptions);
        try
        {
            var service = new Stripe.Checkout.SessionService();
            return await service.CreateAsync(options, requestOptions);
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Error creating checkout session");
            throw new InvalidOperationException($"Failed to create checkout session: {ex.Message}", ex);
        }
    }
}

