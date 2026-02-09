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

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System;
using CarRental.Api.Data;
using CarRental.Api.DTOs;
using CarRental.Api.Models;
using CarRental.Api.Services;
using Stripe;
using CarRental.Api.DTOs.Payments;
using System.Security.Cryptography;
using System.Text.Json;
using BCrypt.Net;

namespace CarRental.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly CarRentalDbContext _context;
    private readonly IStripeService _stripeService;
    private readonly ILogger<PaymentsController> _logger;
    private readonly IEncryptionService _encryptionService;
    private readonly IConfiguration _configuration;
    private readonly IStripeConnectService _stripeConnectService;
    private readonly IWebHostEnvironment _environment;
    private readonly ISettingsService _settingsService;

    public PaymentsController(
        CarRentalDbContext context, 
        IStripeService stripeService, 
        ILogger<PaymentsController> logger,
        IEncryptionService encryptionService,
        IConfiguration configuration,
        IStripeConnectService stripeConnectService,
        IWebHostEnvironment environment,
        ISettingsService settingsService)
    {
        _context = context;
        _stripeService = stripeService;
        _logger = logger;
        _encryptionService = encryptionService;
        _configuration = configuration;
        _stripeConnectService = stripeConnectService;
        _environment = environment;
        _settingsService = settingsService;
    }

    /// <summary>
    /// Get all payments with optional filtering
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<PaymentDto>>> GetPayments(
        Guid? customerId = null,
        Guid? companyId = null,
        Guid? reservationId = null,
        string? status = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int page = 1,
        int pageSize = 20)
    {
        var query = _context.Payments
            .Include(p => p.Customer)
            .Include(p => p.Company)
            .Include(p => p.Reservation)
            .AsQueryable();

        if (customerId.HasValue)
            query = query.Where(p => p.CustomerId == customerId.Value);

        if (companyId.HasValue)
            query = query.Where(p => p.CompanyId == companyId.Value);

        if (reservationId.HasValue)
            query = query.Where(p => p.ReservationId == reservationId.Value);

        if (!string.IsNullOrEmpty(status))
            query = query.Where(p => p.Status == status);

        if (fromDate.HasValue)
            query = query.Where(p => p.CreatedAt >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(p => p.CreatedAt <= toDate.Value);

        var payments = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new PaymentDto
            {
                PaymentId = p.Id,
                ReservationId = p.ReservationId,
                RentalId = p.RentalId,
                CustomerId = p.CustomerId,
                CompanyId = p.CompanyId,
                Amount = p.Amount,
                Currency = p.Currency,
                PaymentType = p.PaymentType,
                PaymentMethod = p.PaymentMethod,
                StripePaymentIntentId = p.StripePaymentIntentId,
                StripeChargeId = p.StripeChargeId,
                StripePaymentMethodId = p.StripePaymentMethodId,
                Status = p.Status,
                SecurityDepositAmount = p.SecurityDepositAmount,
                SecurityDepositStatus = p.SecurityDepositStatus,
                SecurityDepositPaymentIntentId = p.SecurityDepositPaymentIntentId,
                SecurityDepositChargeId = p.SecurityDepositChargeId,
                SecurityDepositAuthorizedAt = p.SecurityDepositAuthorizedAt,
                SecurityDepositCapturedAt = p.SecurityDepositCapturedAt,
                SecurityDepositReleasedAt = p.SecurityDepositReleasedAt,
                SecurityDepositCapturedAmount = p.SecurityDepositCapturedAmount,
                SecurityDepositCaptureReason = p.SecurityDepositCaptureReason,
                FailureReason = p.FailureReason,
                ProcessedAt = p.ProcessedAt,
                CreatedAt = p.CreatedAt,
                CustomerName = p.Customer.FirstName + " " + p.Customer.LastName,
                CompanyName = p.Company.CompanyName
            })
            .ToListAsync();

        return Ok(payments);
    }

    /// <summary>
    /// Get a specific payment by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<PaymentDto>> GetPayment(Guid id)
    {
        var payment = await _context.Payments
            .Include(p => p.Customer)
            .Include(p => p.Company)
            .Include(p => p.Reservation)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (payment == null)
            return NotFound();

        var paymentDto = new PaymentDto
        {
            PaymentId = payment.Id,
            ReservationId = payment.ReservationId,
            RentalId = payment.RentalId,
            CustomerId = payment.CustomerId,
            CompanyId = payment.CompanyId,
            Amount = payment.Amount,
            Currency = payment.Currency,
            PaymentType = payment.PaymentType,
            PaymentMethod = payment.PaymentMethod,
            StripePaymentIntentId = payment.StripePaymentIntentId,
            StripeChargeId = payment.StripeChargeId,
            StripePaymentMethodId = payment.StripePaymentMethodId,
            Status = payment.Status,
            SecurityDepositAmount = payment.SecurityDepositAmount,
            SecurityDepositStatus = payment.SecurityDepositStatus,
            SecurityDepositPaymentIntentId = payment.SecurityDepositPaymentIntentId,
            SecurityDepositChargeId = payment.SecurityDepositChargeId,
            SecurityDepositAuthorizedAt = payment.SecurityDepositAuthorizedAt,
            SecurityDepositCapturedAt = payment.SecurityDepositCapturedAt,
            SecurityDepositReleasedAt = payment.SecurityDepositReleasedAt,
            SecurityDepositCapturedAmount = payment.SecurityDepositCapturedAmount,
            SecurityDepositCaptureReason = payment.SecurityDepositCaptureReason,
            FailureReason = payment.FailureReason,
            ProcessedAt = payment.ProcessedAt,
            CreatedAt = payment.CreatedAt,
            CustomerName = payment.Customer.FirstName + " " + payment.Customer.LastName,
            CompanyName = payment.Company.CompanyName
        };

        return Ok(paymentDto);
    }

    /// <summary>
    /// Create a new payment
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<PaymentDto>> CreatePayment(CreatePaymentDto createPaymentDto)
    {
        var customer = await _context.Customers.FindAsync(createPaymentDto.CustomerId);
        if (customer == null)
            return BadRequest("Customer not found");

        var company = await _context.Companies.FindAsync(createPaymentDto.CompanyId);
        if (company == null)
            return BadRequest("Company not found");

        var payment = new Payment
        {
            CustomerId = createPaymentDto.CustomerId,
            CompanyId = createPaymentDto.CompanyId,
            ReservationId = createPaymentDto.ReservationId,
            RentalId = createPaymentDto.RentalId,
            Amount = createPaymentDto.Amount,
            Currency = createPaymentDto.Currency,
            PaymentType = createPaymentDto.PaymentType,
            PaymentMethod = createPaymentDto.PaymentMethod,
            StripePaymentMethodId = createPaymentDto.StripePaymentMethodId,
            Status = "pending",
            SecurityDepositAmount = createPaymentDto.SecurityDepositAmount,
            SecurityDepositStatus = createPaymentDto.SecurityDepositAmount > 0 ? "pending" : null
        };

        _context.Payments.Add(payment);
        await _context.SaveChangesAsync();

        var paymentDto = new PaymentDto
        {
            PaymentId = payment.Id,
            ReservationId = payment.ReservationId,
            RentalId = payment.RentalId,
            CustomerId = payment.CustomerId,
            CompanyId = payment.CompanyId,
            Amount = payment.Amount,
            Currency = payment.Currency,
            PaymentType = payment.PaymentType,
            PaymentMethod = payment.PaymentMethod,
            StripePaymentIntentId = payment.StripePaymentIntentId,
            StripeChargeId = payment.StripeChargeId,
            StripePaymentMethodId = payment.StripePaymentMethodId,
            Status = payment.Status,
            SecurityDepositAmount = payment.SecurityDepositAmount,
            SecurityDepositStatus = payment.SecurityDepositStatus,
            SecurityDepositPaymentIntentId = payment.SecurityDepositPaymentIntentId,
            SecurityDepositChargeId = payment.SecurityDepositChargeId,
            SecurityDepositAuthorizedAt = payment.SecurityDepositAuthorizedAt,
            SecurityDepositCapturedAt = payment.SecurityDepositCapturedAt,
            SecurityDepositReleasedAt = payment.SecurityDepositReleasedAt,
            SecurityDepositCapturedAmount = payment.SecurityDepositCapturedAmount,
            SecurityDepositCaptureReason = payment.SecurityDepositCaptureReason,
            FailureReason = payment.FailureReason,
            ProcessedAt = payment.ProcessedAt,
            CreatedAt = payment.CreatedAt,
            CustomerName = customer.FirstName + " " + customer.LastName,
            CompanyName = company.CompanyName
        };

        return CreatedAtAction(nameof(GetPayment), new { id = payment.Id }, paymentDto);
    }

    /// <summary>
    /// Create a Stripe Checkout session for a reservation/payment
    /// </summary>
    [HttpPost("checkout-session")]
    public async Task<ActionResult<object>> CreateCheckoutSession([FromBody] CreateCheckoutSessionDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (dto.Amount <= 0)
                return BadRequest("Amount must be greater than zero.");

            _logger.LogInformation("[Stripe] Checkout session request received: {@Request}", dto);

            var customer = await _context.Customers.FindAsync(dto.CustomerId);
            if (customer == null)
                return BadRequest("Customer not found");

            if (string.IsNullOrEmpty(customer.Email))
            {
                _logger.LogError("Customer email is missing for customer {CustomerId}", dto.CustomerId);
                return StatusCode(500, new { error = "Customer email is missing" });
            }

            var company = await _context.Companies.FirstOrDefaultAsync(c => c.Id == dto.CompanyId);
            if (company == null)
                return BadRequest("Company not found");

            // Check if this specific booking already has a completed payment
            if (dto.BookingId.HasValue)
            {
                var existingBooking = await _context.Bookings
                    .FirstOrDefaultAsync(b => b.Id == dto.BookingId.Value);

                if (existingBooking != null && existingBooking.PaymentStatus == "completed")
                {
                    _logger.LogInformation(
                        "[Stripe] Booking {BookingId} already has completed payment. Skipping checkout session creation.",
                        dto.BookingId.Value);

                    return Ok(new {
                        alreadyPaid = true,
                        bookingId = existingBooking.Id,
                        bookingNumber = existingBooking.BookingNumber,
                        paymentStatus = existingBooking.PaymentStatus,
                        message = "Payment already completed for this booking",
                        redirectUrl = dto.SuccessUrl?.Replace("{booking_id}", existingBooking.Id.ToString())
                    });
                }
            }
            else
            {
                // If no specific BookingId, check if customer has any pending bookings
                var existingPaidBooking = await _context.Bookings
                    .FirstOrDefaultAsync(b =>
                        b.CustomerId == dto.CustomerId &&
                        b.CompanyId == dto.CompanyId &&
                        b.PaymentStatus == "completed" &&
                        b.Status != BookingStatus.Cancelled);

                if (existingPaidBooking != null)
                {
                    _logger.LogInformation(
                        "[Stripe] Customer {CustomerId} already has paid booking {BookingId} for company {CompanyId}. Skipping checkout session creation.",
                        dto.CustomerId, existingPaidBooking.Id, dto.CompanyId);

                    return Ok(new {
                        alreadyPaid = true,
                        bookingId = existingPaidBooking.Id,
                        bookingNumber = existingPaidBooking.BookingNumber,
                        paymentStatus = existingPaidBooking.PaymentStatus,
                        message = "Payment already completed for this booking",
                        redirectUrl = dto.SuccessUrl?.Replace("{booking_id}", existingPaidBooking.Id.ToString())
                    });
                }
            }

            // Get company's StripeSettings to determine environment (test/US)
            var companyStripeSettings = await GetCompanyStripeSettingsAsync(company.Id);
            if (companyStripeSettings == null)
            {
                _logger.LogError("CreateCheckoutSession: StripeSettings not found for company {CompanyId}", company.Id);
                return StatusCode(500, new { error = "Stripe not configured for this company" });
            }

            // Get platform Stripe settings for application fee collection
            var (platformAccountId, platformSecretKey) = await GetPlatformStripeSettingsAsync(company.Id);
            if (string.IsNullOrEmpty(platformSecretKey))
            {
                _logger.LogError("CreateCheckoutSession: Platform Stripe settings not configured for environment {Environment}", companyStripeSettings.Name);
                return StatusCode(500, new { error = "Platform Stripe not configured for this environment" });
            }


            // Get company Stripe API key for connected account operations
            var companySecretKey = await GetStripeSecretKeyAsync(company.Id);
            if (string.IsNullOrEmpty(companySecretKey))
            {
                _logger.LogError("CreateCheckoutSession: Company Stripe secret key not configured for company {CompanyId}", company.Id);
                return StatusCode(500, new { error = "Stripe not configured for this company" });
            }

            // Get Stripe connected account ID from stripe_company table (REQUIRED)
            // Same logic as security deposit - payments must go to company account, not platform account
            var stripeAccountId = await GetStripeAccountIdAsync(company.Id);
            
            // Determine valid customer ID (same logic as security deposit)
            string? validCustomerId = null;
            var countryCode = GetCountryCode(company.Country);
            
            if (!string.IsNullOrEmpty(customer.StripeCustomerId) && string.IsNullOrEmpty(stripeAccountId))
            {
                // Only try to use customer ID on platform account (not connected account)
                // On connected accounts, customers are separate entities, so use email instead
                try
                {
                    var customerService = new Stripe.CustomerService();
                    var customerRequestOptions = new Stripe.RequestOptions { ApiKey = companySecretKey };
                    
                    try
                    {
                        // Verify customer exists on platform account
                        var stripeCustomer = await customerService.GetAsync(customer.StripeCustomerId, null, customerRequestOptions);
                        validCustomerId = customer.StripeCustomerId;
                        
                        // Only update if customer doesn't have an address set
                        if (!string.IsNullOrEmpty(countryCode) && string.IsNullOrEmpty(stripeCustomer.Address?.Country))
                        {
                            await customerService.UpdateAsync(customer.StripeCustomerId, new Stripe.CustomerUpdateOptions
                            {
                                Address = new Stripe.AddressOptions
                                {
                                    Country = countryCode
                                }
                            }, customerRequestOptions);
                            
                            _logger.LogInformation(
                                "[Stripe] Updated customer {CustomerId} address with country {CountryCode}",
                                customer.StripeCustomerId,
                                countryCode
                            );
                        }
                    }
                    catch (Stripe.StripeException stripeEx) when (stripeEx.StripeError?.Code == "resource_missing")
                    {
                        // Customer doesn't exist on platform account - use email instead
                        _logger.LogWarning(
                            "[Stripe] Customer {CustomerId} not found on platform account. Will use email instead.",
                            customer.StripeCustomerId
                        );
                        validCustomerId = null;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[Stripe] Failed to verify customer {CustomerId}. Will use email instead.", customer.StripeCustomerId);
                    validCustomerId = null;
                }
            }
            else if (!string.IsNullOrEmpty(stripeAccountId))
            {
                // Using connected account - always use email to let Stripe handle customer creation/lookup
                _logger.LogInformation(
                    "[Stripe] Using connected account {StripeAccountId}. Will use customer email {Email} instead of customer ID to let Stripe handle customer on connected account.",
                    stripeAccountId,
                    customer.Email
                );
                validCustomerId = null;
            }

            var amountInCents = (long)Math.Round(dto.Amount * 100M, MidpointRounding.AwayFromZero);

            var bookingId = dto.BookingId;
#pragma warning disable CS0618
            bookingId ??= dto.ReservationId;
#pragma warning restore CS0618

            // Normalize currency code (some Stripe operations are case-sensitive)
            var normalizedCurrency = dto.Currency.ToLower().Trim();
            
            // Log currency being used
            _logger.LogInformation(
                "[Stripe] Using currency: {Currency} (from request: {RequestCurrency}) for company {CompanyId} ({CompanyName})",
                normalizedCurrency,
                dto.Currency,
                company.Id,
                company.CompanyName
            );
            
            var lineItem = new Stripe.Checkout.SessionLineItemOptions
            {
                PriceData = new Stripe.Checkout.SessionLineItemPriceDataOptions
                {
                    Currency = normalizedCurrency,
                    UnitAmountDecimal = dto.Amount * 100,
                    ProductData = new Stripe.Checkout.SessionLineItemPriceDataProductDataOptions
                    {
                        Name = dto.Description ?? $"Booking {dto.BookingNumber ?? bookingId?.ToString() ?? ""}".Trim()
                    }
                },
                Quantity = 1
            };

            _logger.LogInformation("[Stripe] Checkout session line item: {@LineItem}", new
            {
                lineItem.PriceData?.Currency,
                lineItem.PriceData?.UnitAmountDecimal,
                ProductName = lineItem.PriceData?.ProductData?.Name
            });

            // Set locale based on user's current language (from request) or fall back to company's language
            var userLanguage = !string.IsNullOrEmpty(dto.Language) ? dto.Language : company.Language;
            var locale = GetStripeLocaleFromCountry(company.Country, userLanguage);
            
            _logger.LogInformation(
                "[Stripe] Using language: {Language} (User: {UserLanguage}, Company: {CompanyLanguage})",
                locale,
                dto.Language ?? "null",
                company.Language ?? "null"
            );
            
            var sessionOptions = new Stripe.Checkout.SessionCreateOptions
            {
                Mode = "payment",
                SuccessUrl = dto.SuccessUrl,
                CancelUrl = dto.CancelUrl,
                Locale = locale,
                BillingAddressCollection = "required",
                LineItems = new List<Stripe.Checkout.SessionLineItemOptions> { lineItem },
                PaymentIntentData = new Stripe.Checkout.SessionPaymentIntentDataOptions
                {
                    Metadata = new Dictionary<string, string>
                    {
                        { "customer_id", customer.Id.ToString() },
                        { "company_id", dto.CompanyId.ToString() }
                    },
                    ApplicationFeeAmount = (long)(dto.Amount * 100m * (company.PlatformFeePercentage / 100m)) // Dynamic platform commission per company
                },
                Metadata = new Dictionary<string, string>
                {
                    { "customer_id", customer.Id.ToString() },
                    { "company_id", dto.CompanyId.ToString() }
                }
            };

            // Log platform fee calculation
            var totalAmount = (long)(dto.Amount * 100);
            var platformFee = (long)(totalAmount * (company.PlatformFeePercentage / 100m));
            _logger.LogInformation("[Stripe] Checkout session with platform fee: Amount={Amount}, PlatformFee={PlatformFee} ({Percentage}%), Environment={Environment}, ConnectedAccount={AccountId}",
                totalAmount, platformFee, company.PlatformFeePercentage, companyStripeSettings.Name, stripeAccountId);

            // Update options to use valid customer ID or email (same as security deposit)
            if (!string.IsNullOrEmpty(validCustomerId))
            {
                // Platform account with valid customer ID
                sessionOptions.Customer = validCustomerId;
                sessionOptions.CustomerEmail = null;
                sessionOptions.CustomerUpdate = new Stripe.Checkout.SessionCustomerUpdateOptions
                {
                    Address = "auto",
                    Name = "auto"
                };
                _logger.LogInformation("[Stripe] Using customer ID {CustomerId} for checkout session on platform account", validCustomerId);
            }
            else
            {
                // Use email (for connected accounts or when customer ID doesn't exist)
                sessionOptions.Customer = null;
                sessionOptions.CustomerEmail = customer.Email;
                sessionOptions.CustomerUpdate = null; // CustomerUpdate can only be used with Customer, not CustomerEmail
                _logger.LogInformation("[Stripe] Using customer email {Email} for checkout session", customer.Email);
            }
            
            // Note: CustomerUpdate is already set above based on whether we have validCustomerId
            // When using CustomerEmail (connected accounts), CustomerUpdate must be null
            // When using Customer (platform account), CustomerUpdate is already set to "auto"
            // Do NOT set CustomerUpdate again here - it will override the null we set for connected accounts
            if (!string.IsNullOrEmpty(countryCode))
            {
                // Pre-fill customer's billing address if we have customer data
                sessionOptions.BillingAddressCollection = "required";
                
                _logger.LogInformation(
                    "[Stripe] Pre-filling billing address with Country={CountryCode}",
                    countryCode
                );
            }
            
            _logger.LogInformation(
                "[Stripe] Checkout session configured with Locale={Locale}, Country={Country}, CountryCode={CountryCode}, Currency={Currency}",
                locale,
                company.Country,
                countryCode,
                normalizedCurrency
            );

            if (bookingId.HasValue)
            {
                sessionOptions.PaymentIntentData.Metadata!["booking_id"] = bookingId.Value.ToString();
                sessionOptions.Metadata!["booking_id"] = bookingId.Value.ToString();
            }

            // Add company_id to metadata for webhook processing
            sessionOptions.PaymentIntentData.Metadata!["company_id"] = company.Id.ToString();
            sessionOptions.Metadata!["company_id"] = company.Id.ToString();

            if (!string.IsNullOrWhiteSpace(dto.BookingNumber))
            {
                sessionOptions.PaymentIntentData.Metadata!["booking_number"] = dto.BookingNumber;
                sessionOptions.Metadata!["booking_number"] = dto.BookingNumber;
            }

            if (string.IsNullOrEmpty(stripeAccountId))
            {
                _logger.LogError(
                    "CreateCheckoutSession: stripe_company record not found or StripeAccountId is missing for company {CompanyId}. " +
                    "Stripe operations are PROHIBITED. Please ensure stripe_company record exists with matching CompanyId and SettingsId.", 
                    company.Id
                );
                return StatusCode(500, new { 
                    error = "Stripe account not configured for this company",
                    message = "Stripe account ID must exist in stripe_company table. Please configure Stripe account first."
                });
            }

            _logger.LogInformation(
                "CreateCheckoutSession: Creating checkout session for connected account {StripeAccountId} using Stripe keys from stripe_settings (CompanyId: {CompanyId})",
                stripeAccountId,
                company.Id
            );



            // Create RequestOptions for direct charge to connected account with application fee
            // Application fee automatically goes to platform account
            var requestOptions = new Stripe.RequestOptions
            {
                ApiKey = platformSecretKey, // Platform key based on company's environment (test/US)
                StripeAccount = stripeAccountId // Direct charge to connected account
            };

            _logger.LogInformation("[Stripe] Checkout session options: {@Options}", new
            {
                sessionOptions.Mode,
                sessionOptions.Customer,
                sessionOptions.SuccessUrl,
                sessionOptions.CancelUrl,
                LineItems = sessionOptions.LineItems?.Select(li => new
                {
                    Currency = li.PriceData?.Currency,
                    Amount = li.PriceData?.UnitAmountDecimal,
                    ProductName = li.PriceData?.ProductData?.Name
                }).ToList(),
                Metadata = sessionOptions.PaymentIntentData?.Metadata,
                ApplicationFeeAmount = sessionOptions.PaymentIntentData?.ApplicationFeeAmount,
                ConnectedAccount = requestOptions.StripeAccount
            });

            // Direct charge to connected account with automatic application fee to platform
            var session = await _stripeService.CreateCheckoutSessionAsync(sessionOptions, requestOptions, companyId: company.Id);

            _logger.LogInformation("[Stripe] Checkout session created. SessionId={SessionId} Url={Url}", session.Id, session.Url);

            return Ok(new
            {
                url = session.Url,
                sessionId = session.Id
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Stripe checkout session for customer {CustomerId}", dto.CustomerId);
            return BadRequest($"Error creating checkout session: {ex.Message}");
        }
    }

    /// <summary>
    /// Process a payment with Stripe
    /// </summary>
    [HttpPost("process")]
    public async Task<ActionResult<object>> ProcessPayment(ProcessPaymentDto processPaymentDto)
    {
        try
        {
            _logger.LogInformation("[Stripe] Process payment request received: {@Request}", processPaymentDto);

            var customer = await _context.Customers.FindAsync(processPaymentDto.CustomerId);
            if (customer == null)
                return BadRequest("Customer not found");

            // Ensure customer has Stripe ID
            if (string.IsNullOrEmpty(customer.StripeCustomerId))
            {
                customer = await _stripeService.CreateCustomerAsync(customer);
                _context.Customers.Update(customer);
                await _context.SaveChangesAsync();
            }

            // Create payment intent with metadata for webhook processing
            var metadata = new Dictionary<string, string>
            {
                { "customer_id", processPaymentDto.CustomerId.ToString() },
                { "payment_type", "full_payment" }
            };

            // Add company_id and booking metadata if available
            if (processPaymentDto.CompanyId.HasValue)
            {
                metadata["company_id"] = processPaymentDto.CompanyId.Value.ToString();
            }
            if (processPaymentDto.BookingId.HasValue)
            {
                metadata["booking_id"] = processPaymentDto.BookingId.Value.ToString();
            }

            var paymentIntent = await _stripeService.CreatePaymentIntentAsync(
                processPaymentDto.Amount,
                processPaymentDto.Currency,
                customer.StripeCustomerId!,
                processPaymentDto.PaymentMethodId,
                metadata);

            _logger.LogInformation("[Stripe] Payment intent created: {@Info}", new
            {
                paymentIntent.Id,
                paymentIntent.Amount,
                paymentIntent.Currency,
                paymentIntent.Status,
                CustomerId = processPaymentDto.CustomerId
            });

            // Create payment record
            var payment = new Payment
            {
                CustomerId = processPaymentDto.CustomerId,
                CompanyId = processPaymentDto.CompanyId ?? Guid.Empty,
                ReservationId = processPaymentDto.BookingId,
                Amount = processPaymentDto.Amount,
                Currency = processPaymentDto.Currency,
                PaymentType = "full_payment",
                PaymentMethod = "card",
                StripePaymentIntentId = paymentIntent.Id,
                StripePaymentMethodId = processPaymentDto.PaymentMethodId,
                Status = paymentIntent.Status
            };

            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                PaymentId = payment.Id,
                PaymentIntentId = paymentIntent.Id,
                ClientSecret = paymentIntent.ClientSecret,
                Status = paymentIntent.Status,
                Amount = paymentIntent.Amount,
                Currency = paymentIntent.Currency
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing payment for customer {CustomerId}", processPaymentDto.CustomerId);
            return BadRequest($"Error processing payment: {ex.Message}");
        }
    }

    /// <summary>
    /// Confirm a payment intent
    /// </summary>
    [HttpPost("confirm/{paymentIntentId}")]
    public async Task<ActionResult<object>> ConfirmPayment(string paymentIntentId)
    {
        try
        {
            // Get payment record first to retrieve companyId
            var payment = await _context.Payments
                .FirstOrDefaultAsync(p => p.StripePaymentIntentId == paymentIntentId);
            
            // Confirm payment intent (pass companyId to use connected account if available)
            var paymentIntent = await _stripeService.ConfirmPaymentIntentAsync(paymentIntentId, payment?.CompanyId);
            
            if (payment != null)
            {
                payment.Status = paymentIntent.Status;
                payment.ProcessedAt = DateTime.UtcNow;
                payment.StripeChargeId = paymentIntent.LatestCharge?.Id;
                
                _context.Payments.Update(payment);
                await _context.SaveChangesAsync();
            }

            return Ok(new
            {
                PaymentIntentId = paymentIntent.Id,
                Status = paymentIntent.Status,
                Amount = paymentIntent.Amount,
                Currency = paymentIntent.Currency
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error confirming payment intent {PaymentIntentId}", paymentIntentId);
            return BadRequest($"Error confirming payment: {ex.Message}");
        }
    }

    /// <summary>
    /// Cancel a payment intent
    /// </summary>
    [HttpPost("cancel/{paymentIntentId}")]
    public async Task<ActionResult<object>> CancelPayment(string paymentIntentId)
    {
        try
        {
            var paymentIntent = await _stripeService.CancelPaymentIntentAsync(paymentIntentId);
            
            // Update payment record
            var payment = await _context.Payments
                .FirstOrDefaultAsync(p => p.StripePaymentIntentId == paymentIntentId);
            
            if (payment != null)
            {
                payment.Status = paymentIntent.Status;
                _context.Payments.Update(payment);
                await _context.SaveChangesAsync();
            }

            return Ok(new
            {
                PaymentIntentId = paymentIntent.Id,
                Status = paymentIntent.Status
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error canceling payment intent {PaymentIntentId}", paymentIntentId);
            return BadRequest($"Error canceling payment: {ex.Message}");
        }
    }

    /// <summary>
    /// Decrypt Stripe key (handles both encrypted and plaintext keys for backward compatibility)
    /// </summary>
    private string? DecryptStripeKey(string? encryptedKey)
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
            _logger.LogDebug("Stripe key appears to be stored in plaintext, using as-is");
            return encryptedKey;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error decrypting Stripe key, using as-is");
            return encryptedKey;
        }
    }

    /// <summary>
    /// Get platform Stripe settings based on company's StripeSettings environment
    /// </summary>
    private async Task<(string? platformAccountId, string? platformSecretKey)> GetPlatformStripeSettingsAsync(Guid companyId)
    {
        try
        {
            // Get company and its StripeSettings
            var company = await _context.Companies
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == companyId);

            if (company?.StripeSettingsId == null)
            {
                _logger.LogWarning("GetPlatformStripeSettings: Company {CompanyId} has no StripeSettingsId", companyId);
                return (null, null);
            }

            var companyStripeSettings = await _context.StripeSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == company.StripeSettingsId.Value);

            if (companyStripeSettings == null)
            {
                _logger.LogWarning("GetPlatformStripeSettings: StripeSettings not found for company {CompanyId}", companyId);
                return (null, null);
            }

            // Use the same settings as company settings - they already contain platform account info
            var platformSettings = companyStripeSettings;

            if (platformSettings == null)
            {
                _logger.LogError("GetPlatformStripeSettings: Stripe settings not found for company {CompanyId}", companyId);
                return (null, null);
            }

            var platformSecretKey = DecryptStripeKey(platformSettings.SecretKey);

            _logger.LogInformation("GetPlatformStripeSettings: Using stripe settings for company environment '{CompanyEnvironment}'",
                companyStripeSettings.Name);

            // Get platform account ID from database
            var platformAccountId = platformSettings.PlatformAccountId;

            if (string.IsNullOrEmpty(platformAccountId))
            {
                _logger.LogWarning("GetPlatformStripeSettings: Platform account ID not configured for environment '{Environment}'",
                    companyStripeSettings.Name);
            }
            else
            {
                _logger.LogInformation("GetPlatformStripeSettings: Platform account ID: {AccountId} for environment '{Environment}'",
                    platformAccountId, companyStripeSettings.Name);
            }

            return (platformAccountId, platformSecretKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting platform Stripe settings for company {CompanyId}", companyId);
            return (null, null);
        }
    }

    /// <summary>
    /// Get company's StripeSettings information
    /// </summary>
    private async Task<StripeSettings?> GetCompanyStripeSettingsAsync(Guid companyId)
    {
        try
        {
            var company = await _context.Companies
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == companyId);

            if (company?.StripeSettingsId == null)
            {
                _logger.LogWarning("GetCompanyStripeSettings: Company {CompanyId} has no StripeSettingsId", companyId);
                return null;
            }

            var stripeSettings = await _context.StripeSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == company.StripeSettingsId.Value);

            return stripeSettings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting company StripeSettings for company {CompanyId}", companyId);
            return null;
        }
    }

    /// <summary>
    /// Get Stripe secret key for a company from stripe_settings table using company.StripeSettingsId
    /// This ensures we use the correct Stripe keys from the stripe_settings table
    /// </summary>
    private async Task<string?> GetStripeSecretKeyAsync(Guid? companyId)
    {
        if (!companyId.HasValue)
        {
            _logger.LogWarning("GetStripeSecretKeyAsync: No companyId provided, falling back to global settings");
            return await _settingsService.GetValueAsync("stripe.secretKey");
        }

        try
        {
            // Get company and its StripeSettingsId
            var company = await _context.Companies
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == companyId.Value);

            if (company == null)
            {
                _logger.LogWarning("GetStripeSecretKeyAsync: Company {CompanyId} not found, falling back to global settings", companyId);
                return await _settingsService.GetValueAsync("stripe.secretKey");
            }

            // Must use stripe_settings table via company.StripeSettingsId
            if (!company.StripeSettingsId.HasValue)
            {
                _logger.LogWarning(
                    "GetStripeSecretKeyAsync: Company {CompanyId} does not have StripeSettingsId configured. Cannot use stripe_settings table.", 
                    companyId
                );
                return await _settingsService.GetValueAsync("stripe.secretKey");
            }

            var settingsId = company.StripeSettingsId.Value;

            // Get secret key from stripe_settings table
            var stripeSettings = await _context.StripeSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(ss => ss.Id == settingsId);

            if (stripeSettings == null)
            {
                _logger.LogError(
                    "GetStripeSecretKeyAsync: stripe_settings record not found for StripeSettingsId {StripeSettingsId} (Company: {CompanyId})", 
                    settingsId, 
                    companyId
                );
                return await _settingsService.GetValueAsync("stripe.secretKey");
            }

            if (string.IsNullOrWhiteSpace(stripeSettings.SecretKey))
            {
                _logger.LogError(
                    "GetStripeSecretKeyAsync: stripe_settings.SecretKey is empty for StripeSettingsId {StripeSettingsId} (Company: {CompanyId})", 
                    settingsId, 
                    companyId
                );
                return await _settingsService.GetValueAsync("stripe.secretKey");
            }

            _logger.LogInformation(
                "[Stripe] Using secret key from stripe_settings table (Id: {SettingsId}, Name: {SettingsName}) for company {CompanyId}", 
                settingsId, 
                stripeSettings.Name ?? "unnamed",
                companyId
            );
            
            // Decrypt the key using the same pattern as StripeService (handles both encrypted and plaintext)
            return DecryptStripeKey(stripeSettings.SecretKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Stripe] Error loading company-specific settings from stripe_settings table for company {CompanyId}, falling back to global settings", companyId);
            return await _settingsService.GetValueAsync("stripe.secretKey");
        }
    }

    /// <summary>
    /// Get Stripe connected account ID for a company from stripe_company table
    /// REQUIRES: stripe_company record must exist with matching company_id and settings_id
    /// Returns null only if record doesn't exist - this will prohibit Stripe operations
    /// </summary>
    private async Task<string?> GetStripeAccountIdAsync(Guid companyId)
    {
        try
        {
            // Get company and its StripeSettingsId
            var company = await _context.Companies
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == companyId);

            if (company == null)
            {
                _logger.LogError("GetStripeAccountIdAsync: Company {CompanyId} not found", companyId);
                return null;
            }

            if (!company.StripeSettingsId.HasValue)
            {
                _logger.LogError("GetStripeAccountIdAsync: Company {CompanyId} does not have StripeSettingsId configured. Stripe operations are prohibited.", companyId);
                return null;
            }

            var companyStripeSettingsId = company.StripeSettingsId.Value;

            // Verify stripe_settings exists with matching ID
            var stripeSettings = await _context.StripeSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(ss => ss.Id == companyStripeSettingsId);

            if (stripeSettings == null)
            {
                _logger.LogError("GetStripeAccountIdAsync: stripe_settings record not found for StripeSettingsId {StripeSettingsId} (from company {CompanyId}). Stripe operations are prohibited.", 
                    companyStripeSettingsId, companyId);
                return null;
            }

            // STRICT REQUIREMENT: stripe_company record MUST exist with matching company_id and settings_id
            var stripeCompany = await _context.StripeCompanies
                .AsNoTracking()
                .FirstOrDefaultAsync(sc => sc.CompanyId == companyId && sc.SettingsId == companyStripeSettingsId);

            if (stripeCompany == null)
            {
                _logger.LogError("GetStripeAccountIdAsync: stripe_company record not found for CompanyId {CompanyId} and SettingsId {SettingsId}. Stripe operations are PROHIBITED until stripe_company record is created.", 
                    companyId, companyStripeSettingsId);
                return null;
            }

            if (string.IsNullOrEmpty(stripeCompany.StripeAccountId))
            {
                _logger.LogError("GetStripeAccountIdAsync: stripe_company.StripeAccountId is empty for CompanyId {CompanyId}. Stripe operations are PROHIBITED until Stripe account is created.", companyId);
                return null;
            }

            // Decrypt the account ID
            try
            {
                var accountId = _encryptionService.Decrypt(stripeCompany.StripeAccountId);
                _logger.LogInformation("GetStripeAccountIdAsync: Found Stripe account {AccountId} for company {CompanyId} from stripe_company table", accountId, companyId);
                return accountId;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "GetStripeAccountIdAsync: Failed to decrypt StripeAccountId for company {CompanyId}. The value might be stored in plain text.", companyId);
                // If decryption fails, it might be stored in plain text (for backward compatibility)
                return stripeCompany.StripeAccountId;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetStripeAccountIdAsync: Error getting Stripe account ID for company {CompanyId}", companyId);
            return null;
        }
    }

    /// <summary>
    /// Create a refund
    /// </summary>
    [HttpPost("refund")]
    public async Task<ActionResult<object>> CreateRefund(RefundPaymentDto refundDto)
    {
        try
        {
            var payment = await _context.Payments
                .Include(p => p.Company)
                .FirstOrDefaultAsync(p => p.Id == refundDto.PaymentId);
            
            if (payment == null)
                return NotFound("Payment not found");

            if (string.IsNullOrEmpty(payment.StripePaymentIntentId))
                return BadRequest("Payment does not have a Stripe payment intent");

            if (payment.CompanyId == Guid.Empty)
            {
                _logger.LogError("CreateRefund: Payment {PaymentId} does not have a valid CompanyId", refundDto.PaymentId);
                return BadRequest("Payment does not have a company associated");
            }

            // Get Stripe API key from stripe_settings table using company.StripeSettingsId
            var stripeSecretKey = await GetStripeSecretKeyAsync(payment.CompanyId);
            if (string.IsNullOrEmpty(stripeSecretKey))
            {
                _logger.LogError("CreateRefund: Stripe secret key not configured for company {CompanyId}", payment.CompanyId);
                return StatusCode(500, new { error = "Stripe not configured for this company" });
            }

            // Get Stripe connected account ID from stripe_company table (REQUIRED)
            var stripeAccountId = await GetStripeAccountIdAsync(payment.CompanyId);
            if (string.IsNullOrEmpty(stripeAccountId))
            {
                _logger.LogError(
                    "CreateRefund: stripe_company record not found or StripeAccountId is missing for company {CompanyId}. " +
                    "Stripe operations are PROHIBITED.", 
                    payment.CompanyId
                );
                return StatusCode(500, new { 
                    error = "Stripe account not configured for this company",
                    message = "Stripe account ID must exist in stripe_company table. Please configure Stripe account first."
                });
            }

            _logger.LogInformation(
                "[Refund] Processing refund for payment {PaymentId}: Amount={Amount}, ConnectedAccount={StripeAccountId}",
                refundDto.PaymentId,
                refundDto.Amount,
                stripeAccountId
            );

            var refund = await _stripeService.CreateRefundAsync(
                payment.StripePaymentIntentId, 
                refundDto.Amount,
                payment.CompanyId);

            // Create refund payment record
            var refundPayment = new Payment
            {
                CustomerId = payment.CustomerId,
                CompanyId = payment.CompanyId,
                Amount = -refundDto.Amount, // Negative amount for refund
                Currency = payment.Currency,
                PaymentType = "refund",
                PaymentMethod = "stripe_refund",
                StripePaymentIntentId = payment.StripePaymentIntentId,
                Status = refund.Status,
                ProcessedAt = DateTime.UtcNow
            };

            _context.Payments.Add(refundPayment);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                RefundId = refund.Id,
                Amount = refund.Amount,
                Status = refund.Status,
                PaymentId = refundPayment.Id
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating refund for payment {PaymentId}", refundDto.PaymentId);
            return BadRequest($"Error creating refund: {ex.Message}");
        }
    }

    /// <summary>
    /// Capture a previously authorized security deposit
    /// </summary>
    [HttpPost("security-deposit/capture")]
    public async Task<ActionResult<object>> CaptureSecurityDeposit([FromBody] CaptureSecurityDepositDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var payment = await _context.Payments
            .Include(p => p.Customer)
            .FirstOrDefaultAsync(p => p.ReservationId == dto.ReservationId);

        if (payment == null)
            return NotFound("Payment record for reservation not found.");

        if (string.IsNullOrWhiteSpace(payment.SecurityDepositPaymentIntentId))
            return BadRequest("No security deposit authorization exists for this reservation.");

        if (payment.SecurityDepositStatus == "captured")
            return BadRequest("Security deposit has already been captured.");

        if (payment.SecurityDepositStatus == "released")
            return BadRequest("Security deposit has already been released.");

        if (payment.SecurityDepositAmount.HasValue && dto.Amount > payment.SecurityDepositAmount.Value)
            return BadRequest("Capture amount cannot exceed the authorized security deposit.");

        try
        {
            // Get currency from payment or company
            var currency = payment.Currency ?? payment.Company?.Currency ?? "USD";
            
            var captureIntent = await _stripeService.CapturePaymentIntentAsync(
                payment.SecurityDepositPaymentIntentId,
                dto.Amount,
                currency,
                payment.CompanyId);

            // Convert from smallest currency unit to decimal amount
            // Get currency decimal places (0 for JPY, etc., 2 for most currencies)
            // Convert from smallest currency unit to decimal amount
            // Get currency decimal places (0 for JPY, etc., 2 for most currencies)
            int decimalPlaces = GetCurrencyDecimalPlaces(currency);
            decimal divisor = (decimal)Math.Pow(10, decimalPlaces);
            var capturedAmount = captureIntent.AmountReceived > 0
                ? captureIntent.AmountReceived / divisor
                : dto.Amount;

            payment.SecurityDepositStatus = "captured";
            payment.SecurityDepositCapturedAt = DateTime.UtcNow;
            payment.SecurityDepositCapturedAmount = capturedAmount;
            payment.SecurityDepositCaptureReason = dto.Reason;
            payment.SecurityDepositChargeId = captureIntent.LatestChargeId;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                payment.SecurityDepositStatus,
                payment.SecurityDepositCapturedAt,
                payment.SecurityDepositCapturedAmount,
                payment.SecurityDepositChargeId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error capturing security deposit for reservation {ReservationId}",
                dto.ReservationId);
            return BadRequest($"Failed to capture security deposit: {ex.Message}");
        }
    }

    /// <summary>
    /// Release a previously authorized security deposit hold
    /// </summary>
    [HttpPost("security-deposit/release")]
    public async Task<ActionResult<object>> ReleaseSecurityDeposit([FromBody] ReleaseSecurityDepositDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var payment = await _context.Payments
            .Include(p => p.Customer)
            .FirstOrDefaultAsync(p => p.ReservationId == dto.ReservationId);

        if (payment == null)
            return NotFound("Payment record for reservation not found.");

        if (string.IsNullOrWhiteSpace(payment.SecurityDepositPaymentIntentId))
            return BadRequest("No security deposit authorization exists for this reservation.");

        if (payment.SecurityDepositStatus == "captured")
            return BadRequest("Security deposit has already been captured and cannot be released.");

        if (payment.SecurityDepositStatus == "released")
            return BadRequest("Security deposit has already been released.");

        try
        {
            var cancelIntent = await _stripeService.CancelPaymentIntentAsync(payment.SecurityDepositPaymentIntentId, payment.CompanyId);

            payment.SecurityDepositStatus = "released";
            payment.SecurityDepositReleasedAt = DateTime.UtcNow;
            payment.SecurityDepositCaptureReason = dto.Reason;
            payment.SecurityDepositChargeId = cancelIntent.LatestChargeId;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                payment.SecurityDepositStatus,
                payment.SecurityDepositReleasedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error releasing security deposit for reservation {ReservationId}",
                dto.ReservationId);
            return BadRequest($"Failed to release security deposit: {ex.Message}");
        }
    }

    /// <summary>
    /// Get customer payment methods
    /// </summary>
    [HttpGet("customer/{customerId}/payment-methods")]
    public async Task<ActionResult<IEnumerable<PaymentMethodDto>>> GetCustomerPaymentMethods(Guid customerId)
    {
        var paymentMethods = await _context.CustomerPaymentMethods
            .Where(pm => pm.CustomerId == customerId)
            .Select(pm => new PaymentMethodDto
            {
                PaymentMethodId = pm.Id,
                CustomerId = pm.CustomerId,
                StripePaymentMethodId = pm.StripePaymentMethodId,
                CardBrand = pm.CardBrand,
                CardLast4 = pm.CardLast4,
                CardExpMonth = pm.CardExpMonth,
                CardExpYear = pm.CardExpYear,
                IsDefault = pm.IsDefault,
                CreatedAt = pm.CreatedAt
            })
            .ToListAsync();

        return Ok(paymentMethods);
    }

    /// <summary>
    /// Add a payment method for a customer
    /// </summary>
    [HttpPost("customer/{customerId}/payment-methods")]
    public async Task<ActionResult<PaymentMethodDto>> AddPaymentMethod(Guid customerId, CreatePaymentMethodDto createDto)
    {
        try
        {
            var customer = await _context.Customers.FindAsync(customerId);
            if (customer == null)
                return NotFound("Customer not found");

            // Ensure customer has Stripe ID
            if (string.IsNullOrEmpty(customer.StripeCustomerId))
            {
                customer = await _stripeService.CreateCustomerAsync(customer);
                _context.Customers.Update(customer);
                await _context.SaveChangesAsync();
            }

            // Attach payment method to customer
            var paymentMethod = await _stripeService.CreatePaymentMethodAsync(
                customer.StripeCustomerId!,
                createDto.StripePaymentMethodId,
                customer.CompanyId);

            // If this is the first payment method or marked as default, set as default
            if (createDto.IsDefault)
            {
                var existingMethods = await _context.CustomerPaymentMethods
                    .Where(pm => pm.CustomerId == customerId)
                    .ToListAsync();
                
                if (existingMethods.Any())
                {
                    foreach (var method in existingMethods)
                    {
                        method.IsDefault = false;
                    }
                }
            }

            var customerPaymentMethod = new CustomerPaymentMethod
            {
                CustomerId = customerId,
                StripePaymentMethodId = createDto.StripePaymentMethodId,
                CardBrand = paymentMethod.Card?.Brand,
                CardLast4 = paymentMethod.Card?.Last4,
                CardExpMonth = (int?)paymentMethod.Card?.ExpMonth,
                CardExpYear = (int?)paymentMethod.Card?.ExpYear,
                IsDefault = createDto.IsDefault
            };

            _context.CustomerPaymentMethods.Add(customerPaymentMethod);
            await _context.SaveChangesAsync();

            var paymentMethodDto = new PaymentMethodDto
            {
                PaymentMethodId = customerPaymentMethod.Id,
                CustomerId = customerPaymentMethod.CustomerId,
                StripePaymentMethodId = customerPaymentMethod.StripePaymentMethodId,
                CardBrand = customerPaymentMethod.CardBrand,
                CardLast4 = customerPaymentMethod.CardLast4,
                CardExpMonth = customerPaymentMethod.CardExpMonth,
                CardExpYear = customerPaymentMethod.CardExpYear,
                IsDefault = customerPaymentMethod.IsDefault,
                CreatedAt = customerPaymentMethod.CreatedAt
            };

            return CreatedAtAction(nameof(GetCustomerPaymentMethods), new { customerId }, paymentMethodDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding payment method for customer {CustomerId}", customerId);
            return BadRequest($"Error adding payment method: {ex.Message}");
        }
    }

    /// <summary>
    /// Handle Stripe webhooks
    /// </summary>
    [HttpPost("webhook")]
    public async Task<IActionResult> HandleWebhook()
    {
        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
        var signature = Request.Headers["Stripe-Signature"].FirstOrDefault();

        if (string.IsNullOrEmpty(signature))
            return BadRequest("Missing Stripe signature");

        try
        {
            var webhookSecret = _configuration["Stripe:WebhookSecret"];
            if (string.IsNullOrWhiteSpace(webhookSecret))
            {
                _logger.LogError("[Stripe] Webhook secret is not configured (Stripe:WebhookSecret)");
                return BadRequest("Stripe webhook secret is not configured.");
            }

            var stripeEvent = await _stripeService.ConstructWebhookEventAsync(json, signature, webhookSecret);
            
            switch (stripeEvent.Type)
            {
                case "payment_intent.succeeded":
                    await HandlePaymentIntentSucceeded(stripeEvent);
                    break;
                case "payment_intent.payment_failed":
                    await HandlePaymentIntentFailed(stripeEvent);
                    break;
                case "payment_intent.amount_capturable_updated":
                    await HandlePaymentIntentAmountCapturableUpdated(stripeEvent);
                    break;
                case "payment_intent.canceled":
                    await HandlePaymentIntentCanceled(stripeEvent);
                    break;
                case "payment_method.attached":
                    await HandlePaymentMethodAttached(stripeEvent);
                    break;
                case "account.updated":
                    await HandleAccountUpdated(stripeEvent);
                    break;
                case "transfer.created":
                    await HandleTransferCreated(stripeEvent);
                    break;
                case "transfer.paid":
                    await HandleTransferPaid(stripeEvent);
                    break;
                case "transfer.failed":
                    await HandleTransferFailed(stripeEvent);
                    break;
                case "payout.paid":
                    await HandlePayoutPaid(stripeEvent);
                    break;
                case "payout.failed":
                    await HandlePayoutFailed(stripeEvent);
                    break;
                default:
                    _logger.LogInformation("Unhandled event type: {EventType}", stripeEvent.Type);
                    break;
            }

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing webhook");
            return BadRequest($"Webhook error: {ex.Message}");
        }
    }

    private async Task HandlePaymentIntentSucceeded(Event stripeEvent)
    {
        var paymentIntent = stripeEvent.Data.Object as PaymentIntent;
        if (paymentIntent == null) return;

        var paymentType = paymentIntent.Metadata?.GetValueOrDefault("payment_type");

        if (string.Equals(paymentType, "security_deposit", StringComparison.OrdinalIgnoreCase))
        {
            var depositPayment = await FindPaymentForDepositIntent(paymentIntent);
            if (depositPayment != null)
            {
                var amountReceived = paymentIntent.AmountReceived;
                var capturedAmount = amountReceived > 0
                    ? amountReceived / 100m
                    : depositPayment.SecurityDepositAmount;

                depositPayment.SecurityDepositStatus = "captured";
                depositPayment.SecurityDepositCapturedAt = DateTime.UtcNow;
                depositPayment.SecurityDepositCapturedAmount = capturedAmount;
                depositPayment.SecurityDepositChargeId = paymentIntent.LatestChargeId;

                await _context.SaveChangesAsync();
            }

            return;
        }

        var payment = await _context.Payments
            .FirstOrDefaultAsync(p => p.StripePaymentIntentId == paymentIntent.Id);

        Guid? bookingId = null;
        string? bookingNumber = null;
        if (paymentIntent.Metadata != null)
        {
            if (paymentIntent.Metadata.TryGetValue("booking_id", out var bookingIdValue) &&
                Guid.TryParse(bookingIdValue, out var parsedBookingId))
            {
                bookingId = parsedBookingId;
            }
            else if (paymentIntent.Metadata.TryGetValue("reservation_id", out var reservationIdValue) &&
                     Guid.TryParse(reservationIdValue, out var parsedReservationId))
            {
                bookingId = parsedReservationId;
            }

            paymentIntent.Metadata.TryGetValue("booking_number", out bookingNumber);
        }

        if (payment != null)
        {
            payment.Status = "succeeded";
            payment.ProcessedAt = DateTime.UtcNow;
            payment.StripeChargeId = paymentIntent.LatestCharge?.Id;

            if (bookingId.HasValue && payment.ReservationId == null)
            {
                payment.ReservationId = bookingId.Value;
            }

            _context.Payments.Update(payment);
        }
        else if (paymentIntent.Metadata != null &&
                 paymentIntent.Metadata.TryGetValue("customer_id", out var customerIdValue) &&
                 Guid.TryParse(customerIdValue, out var customerId) &&
                 paymentIntent.Metadata.TryGetValue("company_id", out var companyIdValue) &&
                 Guid.TryParse(companyIdValue, out var companyId))
        {
            long amountRaw = paymentIntent.AmountReceived;
            if (amountRaw == 0)
            {
                amountRaw = paymentIntent.Amount;
            }
            var amount = amountRaw / 100m;
            var currency = string.IsNullOrWhiteSpace(paymentIntent.Currency)
                ? "USD"
                : paymentIntent.Currency.ToUpperInvariant();

            payment = new Payment
            {
                CustomerId = customerId,
                CompanyId = companyId,
                ReservationId = bookingId,
                Amount = amount,
                Currency = currency,
                PaymentType = "checkout",
                PaymentMethod = paymentIntent.PaymentMethodTypes?.FirstOrDefault(),
                StripePaymentIntentId = paymentIntent.Id,
                StripeChargeId = paymentIntent.LatestCharge?.Id,
                Status = "succeeded",
                ProcessedAt = DateTime.UtcNow
            };

            _context.Payments.Add(payment);
        }

        if (bookingId.HasValue || !string.IsNullOrWhiteSpace(bookingNumber))
        {
            Reservation? reservation = null;

            if (bookingId.HasValue)
            {
                reservation = await _context.Bookings
                    .FirstOrDefaultAsync(r => r.Id == bookingId.Value);
            }

            if (reservation == null && !string.IsNullOrWhiteSpace(bookingNumber))
            {
                reservation = await _context.Bookings
                    .FirstOrDefaultAsync(r => r.BookingNumber == bookingNumber);
            }

            if (reservation != null)
            {
                reservation.Status = BookingStatus.Confirmed;
                reservation.UpdatedAt = DateTime.UtcNow;
                _context.Bookings.Update(reservation);

                var vehicle = await _context.Vehicles
                    .FirstOrDefaultAsync(v => v.Id == reservation.VehicleId);

                if (vehicle != null && vehicle.Status != VehicleStatus.Rented)
                {
                    vehicle.Status = VehicleStatus.Rented;
                    _context.Vehicles.Update(vehicle);
                }

                // Send invitation email with booking details and password if customer was created without password
                await SendInvitationEmailAfterPayment(reservation);
            }
            else
            {
                _logger.LogWarning("Booking {BookingId} referenced in payment intent {PaymentIntentId} was not found", bookingId, paymentIntent.Id);
            }
        }

        await _context.SaveChangesAsync();
    }

    private async Task SendInvitationEmailAfterPayment(Reservation reservation)
    {
        try
        {
            _logger.LogInformation(
                "SendInvitationEmailAfterPayment: Starting for booking {BookingId}, CustomerId: {CustomerId}",
                reservation.Id,
                reservation.CustomerId);

            // Load customer
            var customer = await _context.Customers.FindAsync(reservation.CustomerId);
            if (customer == null)
            {
                _logger.LogWarning(
                    "SendInvitationEmailAfterPayment: Customer {CustomerId} not found for booking {BookingId}",
                    reservation.CustomerId,
                    reservation.Id);
                return;
            }

            // Only send invitation if customer has a password hash (meaning password was generated)
            // and hasn't received an invitation yet (we'll track this by checking if they've logged in)
            if (string.IsNullOrEmpty(customer.PasswordHash))
            {
                _logger.LogInformation(
                    "SendInvitationEmailAfterPayment: Skipping - Customer {CustomerId} has no password hash (may have been created with existing password)",
                    customer.Id);
                return; // Customer already has access or no password was set
            }

            if (customer.LastLogin.HasValue)
            {
                _logger.LogInformation(
                    "SendInvitationEmailAfterPayment: Skipping - Customer {CustomerId} has already logged in (LastLogin: {LastLogin})",
                    customer.Id,
                    customer.LastLogin);
                return; // Customer already has access
            }

            // Generate a temporary password for the invitation email
            // (We can't retrieve the original password from the hash)
            var random = new Random();
            const string chars = "ABCDEFGHJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var temporaryPassword = new string(Enumerable.Repeat(chars, 12)
                .Select(s => s[random.Next(s.Length)]).ToArray());

            // Update customer's password with the new temporary password
            customer.PasswordHash = BCrypt.Net.BCrypt.HashPassword(temporaryPassword);
            _context.Customers.Update(customer);
            await _context.SaveChangesAsync(); // Save password update before sending email

            // Load company and vehicle for email details
            var company = await _context.Companies.FindAsync(reservation.CompanyId);
            var vehicle = await _context.Vehicles
                .Include(v => v.VehicleModel)
                .ThenInclude(vm => vm!.Model)
                .FirstOrDefaultAsync(v => v.Id == reservation.VehicleId);

            if (company == null)
            {
                _logger.LogWarning(
                    "SendInvitationEmailAfterPayment: Company {CompanyId} not found for booking {BookingId}",
                    reservation.CompanyId,
                    reservation.Id);
                return;
            }

            // Determine language from company
            var languageCode = company.Language?.ToLower() ?? "en";
            var language = LanguageCodes.FromCode(languageCode);

            // Get email service
            var multiTenantEmailService = HttpContext.RequestServices.GetRequiredService<MultiTenantEmailService>();
            
            _logger.LogInformation(
                "SendInvitationEmailAfterPayment: Preparing to send email to {Email} for booking {BookingNumber}",
                customer.Email,
                reservation.BookingNumber);

            // Prepare booking details
            var vehicleName = vehicle?.VehicleModel?.Model != null
                ? $"{vehicle.VehicleModel.Model.Make} {vehicle.VehicleModel.Model.ModelName} ({vehicle.VehicleModel.Model.Year})"
                : "Vehicle";

            var invitationUrl = $"{Request.Scheme}://{Request.Host}/login?email={Uri.EscapeDataString(customer.Email)}";

            // Send invitation email with booking details and password
            var customerName = (!string.IsNullOrWhiteSpace(customer.FirstName) || !string.IsNullOrWhiteSpace(customer.LastName))
                ? $"{customer.FirstName} {customer.LastName}".Trim()
                : customer.Email;
            
            var emailSent = await multiTenantEmailService.SendInvitationEmailWithBookingDetailsAsync(
                reservation.CompanyId,
                customer.Email,
                customerName,
                invitationUrl,
                temporaryPassword,
                reservation.BookingNumber ?? reservation.Id.ToString(),
                reservation.PickupDate,
                reservation.ReturnDate,
                vehicleName,
                reservation.PickupLocation ?? "",
                reservation.TotalAmount,
                company.Currency ?? "USD",
                language);

            if (emailSent)
            {
                _logger.LogInformation(
                    "SendInvitationEmailAfterPayment: Invitation email with booking details sent successfully to {Email} for booking {BookingNumber}",
                    customer.Email,
                    reservation.BookingNumber);
            }
            else
            {
                _logger.LogWarning(
                    "SendInvitationEmailAfterPayment: Failed to send invitation email to {Email} for booking {BookingNumber}",
                    customer.Email,
                    reservation.BookingNumber);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error sending invitation email after payment for booking {BookingId}",
                reservation.Id);
            // Don't throw - payment was successful, email failure shouldn't break the flow
        }
    }

    private async Task HandlePaymentIntentFailed(Event stripeEvent)
    {
        var paymentIntent = stripeEvent.Data.Object as PaymentIntent;
        if (paymentIntent == null) return;

        var paymentType = paymentIntent.Metadata?.GetValueOrDefault("payment_type");

        if (string.Equals(paymentType, "security_deposit", StringComparison.OrdinalIgnoreCase))
        {
            var depositPayment = await FindPaymentForDepositIntent(paymentIntent);
            if (depositPayment != null)
            {
                depositPayment.SecurityDepositStatus = "failed";
                depositPayment.SecurityDepositCaptureReason = paymentIntent.LastPaymentError?.Message;
                await _context.SaveChangesAsync();
            }

            return;
        }

        var payment = await _context.Payments
            .FirstOrDefaultAsync(p => p.StripePaymentIntentId == paymentIntent.Id);

        if (payment != null)
        {
            payment.Status = "failed";
            payment.FailureReason = paymentIntent.LastPaymentError?.Message;
            
            _context.Payments.Update(payment);
            await _context.SaveChangesAsync();
        }
    }

    private async Task HandlePaymentIntentAmountCapturableUpdated(Event stripeEvent)
    {
        var paymentIntent = stripeEvent.Data.Object as PaymentIntent;
        if (paymentIntent == null) return;

        var paymentType = paymentIntent.Metadata?.GetValueOrDefault("payment_type");
        if (!string.Equals(paymentType, "security_deposit", StringComparison.OrdinalIgnoreCase))
            return;

        var depositPayment = await FindPaymentForDepositIntent(paymentIntent);
        if (depositPayment == null)
            return;

        depositPayment.SecurityDepositStatus = "authorized";
        depositPayment.SecurityDepositAuthorizedAt = DateTime.UtcNow;
        depositPayment.SecurityDepositPaymentIntentId ??= paymentIntent.Id;

        var amount = paymentIntent.Amount;

        if (!depositPayment.SecurityDepositAmount.HasValue && amount > 0)
        {
            depositPayment.SecurityDepositAmount = amount / 100m;
        }

        await _context.SaveChangesAsync();
    }

    private async Task HandlePaymentIntentCanceled(Event stripeEvent)
    {
        var paymentIntent = stripeEvent.Data.Object as PaymentIntent;
        if (paymentIntent == null) return;

        var paymentType = paymentIntent.Metadata?.GetValueOrDefault("payment_type");
        if (string.Equals(paymentType, "security_deposit", StringComparison.OrdinalIgnoreCase))
        {
            var depositPayment = await FindPaymentForDepositIntent(paymentIntent);
            if (depositPayment != null)
            {
                depositPayment.SecurityDepositStatus = "released";
                depositPayment.SecurityDepositReleasedAt = DateTime.UtcNow;
                depositPayment.SecurityDepositCaptureReason = paymentIntent.CancellationReason;
                depositPayment.SecurityDepositChargeId = paymentIntent.LatestChargeId;
                await _context.SaveChangesAsync();
            }

            return;
        }

        var payment = await _context.Payments
            .FirstOrDefaultAsync(p => p.StripePaymentIntentId == paymentIntent.Id);

        if (payment != null)
        {
            payment.Status = "canceled";
            payment.FailureReason = paymentIntent.CancellationReason;
            _context.Payments.Update(payment);
            await _context.SaveChangesAsync();
        }
    }

    private async Task HandlePaymentMethodAttached(Event stripeEvent)
    {
        var paymentMethod = stripeEvent.Data.Object as PaymentMethod;
        if (paymentMethod == null) return;

        // Update payment method details if it exists in our database
        var customerPaymentMethod = await _context.CustomerPaymentMethods
            .FirstOrDefaultAsync(pm => pm.StripePaymentMethodId == paymentMethod.Id);

        if (customerPaymentMethod != null)
        {
            customerPaymentMethod.CardBrand = paymentMethod.Card?.Brand;
            customerPaymentMethod.CardLast4 = paymentMethod.Card?.Last4;
            customerPaymentMethod.CardExpMonth = (int?)paymentMethod.Card?.ExpMonth;
            customerPaymentMethod.CardExpYear = (int?)paymentMethod.Card?.ExpYear;
            
            _context.CustomerPaymentMethods.Update(customerPaymentMethod);
            await _context.SaveChangesAsync();
        }
    }

    private async Task<Payment?> FindPaymentForDepositIntent(PaymentIntent paymentIntent)
    {
        var payment = await _context.Payments
            .FirstOrDefaultAsync(p => p.SecurityDepositPaymentIntentId == paymentIntent.Id);

        if (payment != null)
            return payment;

        if (paymentIntent.Metadata != null &&
            paymentIntent.Metadata.TryGetValue("booking_id", out var bookingIdValue) &&
            Guid.TryParse(bookingIdValue, out var bookingId))
        {
            payment = await _context.Payments
                .FirstOrDefaultAsync(p => p.ReservationId == bookingId);

            if (payment != null && string.IsNullOrEmpty(payment.SecurityDepositPaymentIntentId))
            {
                payment.SecurityDepositPaymentIntentId = paymentIntent.Id;
            }

            return payment;
        }

        return null;
    }

    private async Task HandleAccountUpdated(Event stripeEvent)
    {
        var account = stripeEvent.Data.Object as Stripe.Account;
        if (account == null)
        {
            _logger.LogWarning("[Webhook] account.updated event received but account object is null");
            return;
        }

        _logger.LogInformation("[Webhook] Processing account.updated for Stripe account {AccountId}", account.Id);

        try
        {
            // Find company by Stripe account ID
            // First, try to find companies with StripeAccountId set
            var companies = await _context.Companies
                .Where(c => !string.IsNullOrEmpty(c.StripeAccountId))
                .ToListAsync();

            Company? targetCompany = null;

            foreach (var company in companies)
            {
                if (company.StripeSettingsId == null)
                    continue;

                var stripeCompany = await _context.StripeCompanies
                    .FirstOrDefaultAsync(sc => sc.CompanyId == company.Id && sc.SettingsId == company.StripeSettingsId.Value);

                if (stripeCompany != null && !string.IsNullOrEmpty(stripeCompany.StripeAccountId))
                {
                    try
                    {
                        var decryptedId = _encryptionService.Decrypt(stripeCompany.StripeAccountId);
                        if (decryptedId == account.Id)
                        {
                            targetCompany = company;
                            _logger.LogInformation("[Webhook] Found company {CompanyId} ({CompanyName}) for Stripe account {AccountId}", 
                                company.Id, company.CompanyName, account.Id);
                            break;
                        }
                    }
                    catch (Exception decryptEx)
                    {
                        // Log decryption failures for debugging
                        _logger.LogWarning(decryptEx, "[Webhook] Failed to decrypt StripeAccountId for company {CompanyId}", company.Id);
                        continue;
                    }
                }
            }

            if (targetCompany == null)
            {
                _logger.LogWarning("[Webhook] No company found for Stripe account {AccountId}. Searched {CompanyCount} companies with StripeAccountId set.", 
                    account.Id, companies.Count);
                return;
            }

            // Update company with account status
            var oldChargesEnabled = targetCompany.StripeChargesEnabled;
            var oldPayoutsEnabled = targetCompany.StripePayoutsEnabled;
            var oldDetailsSubmitted = targetCompany.StripeDetailsSubmitted;
            var oldOnboardingCompleted = targetCompany.StripeOnboardingCompleted;

            targetCompany.StripeChargesEnabled = account.ChargesEnabled;
            targetCompany.StripePayoutsEnabled = account.PayoutsEnabled;
            targetCompany.StripeDetailsSubmitted = account.DetailsSubmitted;
            targetCompany.StripeOnboardingCompleted = account.ChargesEnabled && 
                                                     account.PayoutsEnabled && 
                                                     account.DetailsSubmitted;
            targetCompany.StripeRequirementsCurrentlyDue = account.Requirements?.CurrentlyDue?.ToArray();
            targetCompany.StripeRequirementsEventuallyDue = account.Requirements?.EventuallyDue?.ToArray();
            targetCompany.StripeRequirementsPastDue = account.Requirements?.PastDue?.ToArray();
            targetCompany.StripeRequirementsDisabledReason = account.Requirements?.DisabledReason;
            targetCompany.StripeLastSyncAt = DateTime.UtcNow;
            targetCompany.UpdatedAt = DateTime.UtcNow;

            // Explicitly mark as modified to ensure EF tracks changes
            _context.Companies.Update(targetCompany);
            
            var changesSaved = await _context.SaveChangesAsync();

            _logger.LogInformation(
                "[Webhook] Updated Stripe account status for company {CompanyId} ({CompanyName}). " +
                "Changes: ChargesEnabled {OldCharges}->{NewCharges}, PayoutsEnabled {OldPayouts}->{NewPayouts}, " +
                "DetailsSubmitted {OldDetails}->{NewDetails}, OnboardingCompleted {OldOnboarding}->{NewOnboarding}. " +
                "SaveChanges returned {ChangesSaved}",
                targetCompany.Id, 
                targetCompany.CompanyName,
                oldChargesEnabled, account.ChargesEnabled,
                oldPayoutsEnabled, account.PayoutsEnabled,
                oldDetailsSubmitted, account.DetailsSubmitted,
                oldOnboardingCompleted, targetCompany.StripeOnboardingCompleted,
                changesSaved);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Webhook] Error handling account.updated webhook for account {AccountId}", account.Id);
        }
    }

    private async Task HandleTransferCreated(Event stripeEvent)
    {
        var transfer = stripeEvent.Data.Object as Stripe.Transfer;
        if (transfer == null) return;

        var transferRecord = await _context.StripeTransfers
            .FirstOrDefaultAsync(t => t.StripeTransferId == transfer.Id);

        if (transferRecord != null)
        {
            // Note: Stripe.Transfer doesn't have Status property - this requires the StripeTransfer model
            // transferRecord.Status = transfer.Status ?? "pending";
            transferRecord.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    private async Task HandleTransferPaid(Event stripeEvent)
    {
        var transfer = stripeEvent.Data.Object as Stripe.Transfer;
        if (transfer == null) return;

        var transferRecord = await _context.StripeTransfers
            .FirstOrDefaultAsync(t => t.StripeTransferId == transfer.Id);

        if (transferRecord != null)
        {
            transferRecord.Status = "paid";
            transferRecord.TransferredAt = DateTime.UtcNow;
            transferRecord.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Transfer {TransferId} marked as paid", transfer.Id);
        }
    }

    private async Task HandleTransferFailed(Event stripeEvent)
    {
        var transfer = stripeEvent.Data.Object as Stripe.Transfer;
        if (transfer == null) return;

        var transferRecord = await _context.StripeTransfers
            .FirstOrDefaultAsync(t => t.StripeTransferId == transfer.Id);

        if (transferRecord != null)
        {
            transferRecord.Status = "failed";
            // Note: Stripe.Transfer doesn't have FailureCode/FailureMessage properties - this requires the StripeTransfer model
            // transferRecord.FailureCode = transfer.FailureCode;
            // transferRecord.FailureMessage = transfer.FailureMessage;
            transferRecord.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogError("Transfer {TransferId} failed", transfer.Id);
        }
    }

    private async Task HandlePayoutPaid(Event stripeEvent)
    {
        var payout = stripeEvent.Data.Object as Stripe.Payout;
        if (payout == null) return;

        // Find company by destination account
        var companies = await _context.Companies.ToListAsync();
        Guid? companyId = null;

        foreach (var company in companies)
        {
            if (company.StripeSettingsId == null)
                continue;

            var stripeCompany = await _context.StripeCompanies
                .FirstOrDefaultAsync(sc => sc.CompanyId == company.Id && sc.SettingsId == company.StripeSettingsId.Value);

            if (stripeCompany != null && !string.IsNullOrEmpty(stripeCompany.StripeAccountId))
            {
                try
                {
                    var decryptedId = _encryptionService.Decrypt(stripeCompany.StripeAccountId);
                    // Payout event comes from connected account context
                    companyId = company.Id;
                    break;
                }
                catch
                {
                    continue;
                }
            }
        }

        if (companyId.HasValue)
        {
            var existingPayout = await _context.StripePayoutRecords
                .FirstOrDefaultAsync(p => p.StripePayoutId == payout.Id);

            if (existingPayout == null)
            {
                var payoutRecord = new Models.StripePayoutRecord
                {
                    CompanyId = companyId.Value,
                    StripePayoutId = payout.Id,
                    Amount = payout.Amount / 100m,
                    Currency = payout.Currency.ToUpperInvariant(),
                    Status = payout.Status,
                    PayoutType = payout.Type,
                    ArrivalDate = payout.ArrivalDate,
                    Description = payout.Description,
                    Method = payout.Method,
                    StatementDescriptor = payout.StatementDescriptor
                };

                _context.StripePayoutRecords.Add(payoutRecord);
            }
            else
            {
                existingPayout.Status = payout.Status;
                existingPayout.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
        }
    }

    private async Task HandlePayoutFailed(Event stripeEvent)
    {
        var payout = stripeEvent.Data.Object as Stripe.Payout;
        if (payout == null) return;

        var payoutRecord = await _context.StripePayoutRecords
            .FirstOrDefaultAsync(p => p.StripePayoutId == payout.Id);

        if (payoutRecord != null)
        {
            payoutRecord.Status = "failed";
            payoutRecord.FailureCode = payout.FailureCode;
            payoutRecord.FailureMessage = payout.FailureMessage;
            payoutRecord.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogError("Payout {PayoutId} failed for company {CompanyId}: {Reason}", 
                payout.Id, payoutRecord.CompanyId, payout.FailureMessage);
        }
    }

    /// <summary>
    /// Get Stripe locale based on user's current language and company country
    /// Priority: USER'S LANGUAGE > Country default
    /// </summary>
    private string GetStripeLocaleFromCountry(string? country, string? language)
    {
        // Normalize inputs
        var countryLower = (country ?? "").ToLower();
        var langLower = (language ?? "en").ToLower();
        
        // PRIORITY 1: Use the user's selected language with country code (if applicable)
        // For Portuguese speakers in Brazil
        if (langLower.StartsWith("pt") && countryLower.Contains("brazil"))
            return "pt-BR";
        
        // For Portuguese (general)
        if (langLower.StartsWith("pt"))
            return "pt";
            
        // For Spanish speakers in Latin America
        if (langLower.StartsWith("es") && (countryLower.Contains("mexico") || countryLower.Contains("argentina") || countryLower.Contains("colombia")))
            return "es-419";
            
        // For Spanish (general)
        if (langLower.StartsWith("es"))
            return "es";
        
        // Other languages
        if (langLower.StartsWith("fr")) return "fr";
        if (langLower.StartsWith("de")) return "de";
        if (langLower.StartsWith("it")) return "it";
        if (langLower.StartsWith("ja")) return "ja";
        if (langLower.StartsWith("zh")) return "zh";
        
        // Default to English
        return "en";
    }

    /// <summary>
    /// Get ISO country code from country name
    /// </summary>
    private string? GetCountryCode(string? country)
    {
        if (string.IsNullOrEmpty(country))
            return null;
            
        var countryLower = country.ToLower();
        
        // Map country names to ISO 3166-1 alpha-2 codes
        if (countryLower.Contains("brazil") || countryLower.Contains("brasil")) return "BR";
        if (countryLower.Contains("united states") || countryLower.Contains("usa") || countryLower == "us") return "US";
        if (countryLower.Contains("canada")) return "CA";
        if (countryLower.Contains("mexico")) return "MX";
        if (countryLower.Contains("argentina")) return "AR";
        if (countryLower.Contains("chile")) return "CL";
        if (countryLower.Contains("colombia")) return "CO";
        if (countryLower.Contains("peru")) return "PE";
        if (countryLower.Contains("portugal")) return "PT";
        if (countryLower.Contains("spain") || countryLower.Contains("españa")) return "ES";
        if (countryLower.Contains("france")) return "FR";
        if (countryLower.Contains("germany") || countryLower.Contains("deutschland")) return "DE";
        if (countryLower.Contains("italy") || countryLower.Contains("italia")) return "IT";
        if (countryLower.Contains("united kingdom") || countryLower.Contains("uk") || countryLower.Contains("england")) return "GB";
        if (countryLower.Contains("japan")) return "JP";
        if (countryLower.Contains("china")) return "CN";
        if (countryLower.Contains("india")) return "IN";
        if (countryLower.Contains("australia")) return "AU";
        
        // If it's already a 2-letter code, return as-is
        if (country.Length == 2)
            return country.ToUpper();
            
        return null;
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
}
