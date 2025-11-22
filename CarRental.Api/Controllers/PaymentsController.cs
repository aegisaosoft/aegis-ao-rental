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

    public PaymentsController(
        CarRentalDbContext context, 
        IStripeService stripeService, 
        ILogger<PaymentsController> logger,
        IEncryptionService encryptionService,
        IConfiguration configuration,
        IStripeConnectService stripeConnectService,
        IWebHostEnvironment environment)
    {
        _context = context;
        _stripeService = stripeService;
        _logger = logger;
        _encryptionService = encryptionService;
        _configuration = configuration;
        _stripeConnectService = stripeConnectService;
        _environment = environment;
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

            if (string.IsNullOrEmpty(customer.StripeCustomerId))
            {
                customer = await _stripeService.CreateCustomerAsync(customer);
                _context.Customers.Update(customer);
                await _context.SaveChangesAsync();
            }

            var company = await _context.Companies.FirstOrDefaultAsync(c => c.Id == dto.CompanyId);
            if (company == null)
                return BadRequest("Company not found");

            // Update customer's default country if not set, to help Stripe pre-fill the correct country
            var countryCode = GetCountryCode(company.Country);
            if (!string.IsNullOrEmpty(countryCode) && !string.IsNullOrEmpty(customer.StripeCustomerId))
            {
                try
                {
                    var customerService = new Stripe.CustomerService();
                    var stripeCustomer = await customerService.GetAsync(customer.StripeCustomerId);
                    
                    // Only update if customer doesn't have an address set
                    if (string.IsNullOrEmpty(stripeCustomer.Address?.Country))
                    {
                        await customerService.UpdateAsync(customer.StripeCustomerId, new Stripe.CustomerUpdateOptions
                        {
                            Address = new Stripe.AddressOptions
                            {
                                Country = countryCode
                            }
                        });
                        
                        _logger.LogInformation(
                            "[Stripe] Updated customer {CustomerId} address with country {CountryCode}",
                            customer.StripeCustomerId,
                            countryCode
                        );
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to update customer address for {CustomerId}", customer.StripeCustomerId);
                }
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
                Customer = customer.StripeCustomerId,
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
                    }
                },
                Metadata = new Dictionary<string, string>
                {
                    { "customer_id", customer.Id.ToString() },
                    { "company_id", dto.CompanyId.ToString() }
                },
                CustomerUpdate = new Stripe.Checkout.SessionCustomerUpdateOptions
                {
                    Address = "auto"
                }
            };
            
            // Set default billing address to company's country
            if (!string.IsNullOrEmpty(countryCode))
            {
                sessionOptions.CustomerUpdate = new Stripe.Checkout.SessionCustomerUpdateOptions
                {
                    Address = "auto",
                    Name = "auto"
                };
                
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

            if (!string.IsNullOrWhiteSpace(dto.BookingNumber))
            {
                sessionOptions.PaymentIntentData.Metadata!["booking_number"] = dto.BookingNumber;
                sessionOptions.Metadata!["booking_number"] = dto.BookingNumber;
            }

            // Only set up Stripe Connect transfers in production when company has a valid Stripe account
            // In development, skip transfers to avoid "stripe_balance.stripe_transfers" capability errors
            if (!string.IsNullOrWhiteSpace(company.StripeAccountId) && !_environment.IsDevelopment())
            {
                string? destination = null;

                try
                {
                    destination = _encryptionService.Decrypt(company.StripeAccountId);
                }
                catch (FormatException)
                {
                    destination = company.StripeAccountId;
                }
                catch (CryptographicException)
                {
                    destination = company.StripeAccountId;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to decrypt Stripe account ID for company {CompanyId}", company.Id);
                }

                if (!string.IsNullOrWhiteSpace(destination))
                {
                    if (destination == company.StripeAccountId)
                    {
                        try
                        {
                            company.StripeAccountId = _encryptionService.Encrypt(destination);
                            _context.Companies.Update(company);
                            await _context.SaveChangesAsync();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to re-encrypt Stripe account ID for company {CompanyId}", company.Id);
                        }
                    }

                    sessionOptions.PaymentIntentData.TransferData = new Stripe.Checkout.SessionPaymentIntentDataTransferDataOptions
                    {
                        Destination = destination
                    };
                }
            }
            else if (!string.IsNullOrWhiteSpace(company.StripeAccountId) && _environment.IsDevelopment())
            {
                _logger.LogWarning(
                    "[Stripe] Development mode: Skipping transfer setup for company {CompanyId} ({CompanyName}). " +
                    "All payments will go to the platform Stripe account.",
                    company.Id,
                    company.CompanyName
                );
            }

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
                TransferDestination = sessionOptions.PaymentIntentData?.TransferData?.Destination
            });

            var session = await _stripeService.CreateCheckoutSessionAsync(sessionOptions, companyId: company.Id);

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

            // Create payment intent
            var paymentIntent = await _stripeService.CreatePaymentIntentAsync(
                processPaymentDto.Amount,
                processPaymentDto.Currency,
                customer.StripeCustomerId!,
                processPaymentDto.PaymentMethodId);

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
                CompanyId = Guid.Empty, // Will be set based on reservation/rental
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
            var paymentIntent = await _stripeService.ConfirmPaymentIntentAsync(paymentIntentId);
            
            // Update payment record
            var payment = await _context.Payments
                .FirstOrDefaultAsync(p => p.StripePaymentIntentId == paymentIntentId);
            
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
    /// Create a refund
    /// </summary>
    [HttpPost("refund")]
    public async Task<ActionResult<object>> CreateRefund(RefundPaymentDto refundDto)
    {
        try
        {
            var payment = await _context.Payments.FindAsync(refundDto.PaymentId);
            if (payment == null)
                return NotFound("Payment not found");

            if (string.IsNullOrEmpty(payment.StripePaymentIntentId))
                return BadRequest("Payment does not have a Stripe payment intent");

            var refund = await _stripeService.CreateRefundAsync(
                payment.StripePaymentIntentId, 
                refundDto.Amount);

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
                currency);

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
            var cancelIntent = await _stripeService.CancelPaymentIntentAsync(payment.SecurityDepositPaymentIntentId);

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
                createDto.StripePaymentMethodId);

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
        if (account == null) return;

        try
        {
            // Find company by Stripe account ID
            var companies = await _context.Companies.ToListAsync();
            Company? targetCompany = null;

            foreach (var company in companies)
            {
                if (!string.IsNullOrEmpty(company.StripeAccountId))
                {
                    try
                    {
                        var decryptedId = _encryptionService.Decrypt(company.StripeAccountId);
                        if (decryptedId == account.Id)
                        {
                            targetCompany = company;
                            break;
                        }
                    }
                    catch
                    {
                        // Skip if decryption fails
                        continue;
                    }
                }
            }

            if (targetCompany != null)
            {
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

                await _context.SaveChangesAsync();

                _logger.LogInformation("Updated Stripe account status for company {CompanyId}", targetCompany.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling account.updated webhook");
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
            if (!string.IsNullOrEmpty(company.StripeAccountId))
            {
                try
                {
                    var decryptedId = _encryptionService.Decrypt(company.StripeAccountId);
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
