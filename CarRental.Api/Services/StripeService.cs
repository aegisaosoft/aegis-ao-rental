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
using System.Collections.Generic;
using System.Threading;

namespace CarRental.Api.Services;

public interface IStripeService
{
    Task<Models.Customer> CreateCustomerAsync(Models.Customer customer);
    Task<Models.Customer> UpdateCustomerAsync(Models.Customer customer);
    Task<Models.Customer> GetCustomerAsync(string stripeCustomerId);
    Task<PaymentMethod> CreatePaymentMethodAsync(string customerId, string paymentMethodId);
    Task<PaymentMethod> GetPaymentMethodAsync(string paymentMethodId);
    Task<PaymentIntent> CreatePaymentIntentAsync(
        decimal amount,
        string currency,
        string customerId,
        string? paymentMethodId = null,
        IDictionary<string, string>? metadata = null,
        bool captureImmediately = true,
        bool requestExtendedAuthorization = false);
    Task<PaymentIntent> ConfirmPaymentIntentAsync(string paymentIntentId);
    Task<PaymentIntent> CancelPaymentIntentAsync(string paymentIntentId);
    Task<PaymentIntent> CapturePaymentIntentAsync(string paymentIntentId, decimal? amountToCapture = null);
    Task<Refund> CreateRefundAsync(string paymentIntentId, decimal amount);
    Task<Account> CreateConnectedAccountAsync(string email, string country = "US");
    Task<Account> GetConnectedAccountAsync(string accountId);
    Task<Transfer> CreateTransferAsync(string accountId, long amount, string currency = "usd");
    Task<WebhookEndpoint> CreateWebhookEndpointAsync(string url, string[] events);
    Task<Event> ConstructWebhookEventAsync(string payload, string signature, string webhookSecret);
    Task<Session> CreateCheckoutSessionAsync(SessionCreateOptions options, RequestOptions? requestOptions = null);
}

public class StripeService : IStripeService
{
    private readonly ISettingsService _settingsService;
    private readonly ILogger<StripeService> _logger;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private bool _initialized;
    private string? _publishableKey;
    private string? _webhookSecret;

    private const string SecretKeySetting = "stripe.secretKey";
    private const string PublishableKeySetting = "stripe.publishableKey";
    private const string WebhookSecretSetting = "stripe.webhookSecret";

    public StripeService(ISettingsService settingsService, ILogger<StripeService> logger)
    {
        _settingsService = settingsService;
        _logger = logger;
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized)
            return;

        await _initializationLock.WaitAsync();
        try
        {
            if (_initialized)
                return;

            var secretKey = await _settingsService.GetValueAsync(SecretKeySetting) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(secretKey))
            {
                throw new InvalidOperationException($"Stripe secret key is not configured. Set the '{SecretKeySetting}' setting.");
            }

            _logger.LogWarning("[Stripe] Using secret key: {SecretKey}", secretKey);

            StripeConfiguration.ApiKey = secretKey;
            _publishableKey = await _settingsService.GetValueAsync(PublishableKeySetting);
            _webhookSecret = await _settingsService.GetValueAsync(WebhookSecretSetting);

            _logger.LogWarning("[Stripe] Publishable key: {Publishable}", _publishableKey);
            _logger.LogWarning("[Stripe] Webhook secret: {WebhookSecret}", _webhookSecret);
            _initialized = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    public async Task<Models.Customer> CreateCustomerAsync(Models.Customer customer)
    {
        await EnsureInitializedAsync();
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

            var stripeCustomer = await new CustomerService().CreateAsync(customerCreateOptions);
            
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
        await EnsureInitializedAsync();
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

            var stripeCustomer = await new CustomerService().UpdateAsync(customer.StripeCustomerId, customerUpdateOptions);
            
            return customer;
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Error updating Stripe customer {StripeCustomerId}", customer.StripeCustomerId);
            throw new InvalidOperationException($"Failed to update Stripe customer: {ex.Message}", ex);
        }
    }

    public async Task<Models.Customer> GetCustomerAsync(string stripeCustomerId)
    {
        await EnsureInitializedAsync();
        _logger.LogWarning("[Stripe] GetCustomerAsync stripeCustomerId={StripeCustomerId}", stripeCustomerId);
        try
        {
            var stripeCustomer = await new CustomerService().GetAsync(stripeCustomerId);
            
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

    public async Task<PaymentMethod> CreatePaymentMethodAsync(string customerId, string paymentMethodId)
    {
        await EnsureInitializedAsync();
        _logger.LogWarning("[Stripe] CreatePaymentMethodAsync customerId={CustomerId} paymentMethodId={PaymentMethodId}", customerId, paymentMethodId);
        try
        {
            var attachOptions = new PaymentMethodAttachOptions
            {
                Customer = customerId
            };

            var paymentMethod = await new PaymentMethodService().AttachAsync(paymentMethodId, attachOptions);
            
            return paymentMethod;
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Error attaching payment method {PaymentMethodId} to customer {CustomerId}", paymentMethodId, customerId);
            throw new InvalidOperationException($"Failed to attach payment method: {ex.Message}", ex);
        }
    }

    public async Task<PaymentMethod> GetPaymentMethodAsync(string paymentMethodId)
    {
        await EnsureInitializedAsync();
        _logger.LogWarning("[Stripe] GetPaymentMethodAsync paymentMethodId={PaymentMethodId}", paymentMethodId);
        try
        {
            var paymentMethod = await new PaymentMethodService().GetAsync(paymentMethodId);
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
        bool requestExtendedAuthorization = false)
    {
        await EnsureInitializedAsync();
        _logger.LogWarning("[Stripe] CreatePaymentIntentAsync amount={Amount} currency={Currency} customerId={CustomerId} paymentMethodId={PaymentMethodId} captureImmediately={CaptureImmediately}",
            amount, currency, customerId, paymentMethodId, captureImmediately);
        try
        {
            var paymentIntentOptions = new PaymentIntentCreateOptions
            {
                Amount = (long)(amount * 100), // Convert to cents
                Currency = currency.ToLower(),
                Customer = customerId,
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

            var paymentIntent = await new PaymentIntentService().CreateAsync(paymentIntentOptions);
            
            return paymentIntent;
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Error creating payment intent for customer {CustomerId}", customerId);
            throw new InvalidOperationException($"Failed to create payment intent: {ex.Message}", ex);
        }
    }

    public async Task<PaymentIntent> ConfirmPaymentIntentAsync(string paymentIntentId)
    {
        await EnsureInitializedAsync();
        _logger.LogWarning("[Stripe] ConfirmPaymentIntentAsync paymentIntentId={PaymentIntentId}", paymentIntentId);
        try
        {
            var paymentIntent = await new PaymentIntentService().ConfirmAsync(paymentIntentId);
            return paymentIntent;
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Error confirming payment intent {PaymentIntentId}", paymentIntentId);
            throw new InvalidOperationException($"Failed to confirm payment intent: {ex.Message}", ex);
        }
    }

    public async Task<PaymentIntent> CancelPaymentIntentAsync(string paymentIntentId)
    {
        await EnsureInitializedAsync();
        _logger.LogWarning("[Stripe] CancelPaymentIntentAsync paymentIntentId={PaymentIntentId}", paymentIntentId);
        try
        {
            var paymentIntent = await new PaymentIntentService().CancelAsync(paymentIntentId);
            return paymentIntent;
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Error canceling payment intent {PaymentIntentId}", paymentIntentId);
            throw new InvalidOperationException($"Failed to cancel payment intent: {ex.Message}", ex);
        }
    }

    public async Task<PaymentIntent> CapturePaymentIntentAsync(string paymentIntentId, decimal? amountToCapture = null)
    {
        await EnsureInitializedAsync();
        _logger.LogWarning("[Stripe] CapturePaymentIntentAsync paymentIntentId={PaymentIntentId} amount={Amount}", paymentIntentId, amountToCapture);
        try
        {
            var options = new PaymentIntentCaptureOptions();
            if (amountToCapture.HasValue)
            {
                options.AmountToCapture = (long)(amountToCapture.Value * 100);
            }

            var paymentIntent = await new PaymentIntentService().CaptureAsync(paymentIntentId, options);
            return paymentIntent;
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Error capturing payment intent {PaymentIntentId}", paymentIntentId);
            throw new InvalidOperationException($"Failed to capture payment intent: {ex.Message}", ex);
        }
    }

    public async Task<Refund> CreateRefundAsync(string paymentIntentId, decimal amount)
    {
        await EnsureInitializedAsync();
        _logger.LogWarning("[Stripe] CreateRefundAsync paymentIntentId={PaymentIntentId} amount={Amount}", paymentIntentId, amount);
        try
        {
            var refundOptions = new RefundCreateOptions
            {
                PaymentIntent = paymentIntentId,
                Amount = (long)(amount * 100) // Convert to cents
            };

            var refund = await new RefundService().CreateAsync(refundOptions);
            return refund;
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Error creating refund for payment intent {PaymentIntentId}", paymentIntentId);
            throw new InvalidOperationException($"Failed to create refund: {ex.Message}", ex);
        }
    }

    public async Task<Account> CreateConnectedAccountAsync(string email, string country = "US")
    {
        await EnsureInitializedAsync();
        _logger.LogWarning("[Stripe] CreateConnectedAccountAsync email={Email} country={Country}", email, country);
        try
        {
            var accountOptions = new AccountCreateOptions
            {
                Type = "express",
                Country = country,
                Email = email,
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

            var account = await new AccountService().CreateAsync(accountOptions);
            return account;
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Error creating connected account for {Email}", email);
            throw new InvalidOperationException($"Failed to create connected account: {ex.Message}", ex);
        }
    }

    public async Task<Account> GetConnectedAccountAsync(string accountId)
    {
        await EnsureInitializedAsync();
        _logger.LogWarning("[Stripe] GetConnectedAccountAsync accountId={AccountId}", accountId);
        try
        {
            var account = await new AccountService().GetAsync(accountId);
            return account;
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Error retrieving connected account {AccountId}", accountId);
            throw new InvalidOperationException($"Failed to retrieve connected account: {ex.Message}", ex);
        }
    }

    public async Task<Transfer> CreateTransferAsync(string accountId, long amount, string currency = "usd")
    {
        await EnsureInitializedAsync();
        _logger.LogWarning("[Stripe] CreateTransferAsync accountId={AccountId} amount={Amount} currency={Currency}", accountId, amount, currency);
        try
        {
            var transferOptions = new TransferCreateOptions
            {
                Amount = amount,
                Currency = currency,
                Destination = accountId
            };

            var transfer = await new TransferService().CreateAsync(transferOptions);
            return transfer;
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Error creating transfer to account {AccountId}", accountId);
            throw new InvalidOperationException($"Failed to create transfer: {ex.Message}", ex);
        }
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
        _logger.LogWarning("[Stripe] ConstructWebhookEventAsync signature={Signature} incomingSecret={IncomingSecret} storedSecret={StoredSecret}", signature, webhookSecret, _webhookSecret);
        try
        {
            var webhookSecretKey = !string.IsNullOrWhiteSpace(_webhookSecret)
                ? _webhookSecret!
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

    public async Task<Session> CreateCheckoutSessionAsync(SessionCreateOptions options, RequestOptions? requestOptions = null)
    {
        await EnsureInitializedAsync();
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
