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
using CarRental.Api.Models;

namespace CarRental.Api.Services;

public interface IStripeService
{
    Task<Models.Customer> CreateCustomerAsync(Models.Customer customer);
    Task<Models.Customer> UpdateCustomerAsync(Models.Customer customer);
    Task<Models.Customer> GetCustomerAsync(string stripeCustomerId);
    Task<PaymentMethod> CreatePaymentMethodAsync(string customerId, string paymentMethodId);
    Task<PaymentMethod> GetPaymentMethodAsync(string paymentMethodId);
    Task<PaymentIntent> CreatePaymentIntentAsync(decimal amount, string currency, string customerId, string? paymentMethodId = null);
    Task<PaymentIntent> ConfirmPaymentIntentAsync(string paymentIntentId);
    Task<PaymentIntent> CancelPaymentIntentAsync(string paymentIntentId);
    Task<Refund> CreateRefundAsync(string paymentIntentId, decimal amount);
    Task<Account> CreateConnectedAccountAsync(string email, string country = "US");
    Task<Account> GetConnectedAccountAsync(string accountId);
    Task<Transfer> CreateTransferAsync(string accountId, long amount, string currency = "usd");
    Task<WebhookEndpoint> CreateWebhookEndpointAsync(string url, string[] events);
    Task<Event> ConstructWebhookEventAsync(string payload, string signature, string webhookSecret);
}

public class StripeService : IStripeService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<StripeService> _logger;

    public StripeService(IConfiguration configuration, ILogger<StripeService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        
        // Set Stripe API key
        var apiKey = _configuration["Stripe:SecretKey"];
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException("Stripe SecretKey is not configured");
        }
        StripeConfiguration.ApiKey = apiKey;
    }

    public async Task<Models.Customer> CreateCustomerAsync(Models.Customer customer)
    {
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

    public async Task<PaymentIntent> CreatePaymentIntentAsync(decimal amount, string currency, string customerId, string? paymentMethodId = null)
    {
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
                Metadata = new Dictionary<string, string>
                {
                    { "source", "car_rental_api" }
                }
            };

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

    public async Task<Refund> CreateRefundAsync(string paymentIntentId, decimal amount)
    {
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

    public Task<Event> ConstructWebhookEventAsync(string payload, string signature, string webhookSecret)
    {
        try
        {
            var webhookSecretKey = _configuration["Stripe:WebhookSecret"] ?? webhookSecret;
            var stripeEvent = EventUtility.ConstructEvent(payload, signature, webhookSecretKey);
            return Task.FromResult(stripeEvent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error constructing webhook event");
            throw new InvalidOperationException($"Failed to construct webhook event: {ex.Message}", ex);
        }
    }
}
