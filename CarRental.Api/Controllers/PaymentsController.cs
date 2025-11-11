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

    public PaymentsController(
        CarRentalDbContext context, 
        IStripeService stripeService, 
        ILogger<PaymentsController> logger,
        IEncryptionService encryptionService,
        IConfiguration configuration)
    {
        _context = context;
        _stripeService = stripeService;
        _logger = logger;
        _encryptionService = encryptionService;
        _configuration = configuration;
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

            var amountInCents = (long)Math.Round(dto.Amount * 100M, MidpointRounding.AwayFromZero);

            var bookingId = dto.BookingId;
#pragma warning disable CS0618
            bookingId ??= dto.ReservationId;
#pragma warning restore CS0618

            var lineItem = new Stripe.Checkout.SessionLineItemOptions
            {
                PriceData = new Stripe.Checkout.SessionLineItemPriceDataOptions
                {
                    Currency = dto.Currency.ToLower(),
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

            var sessionOptions = new Stripe.Checkout.SessionCreateOptions
            {
                Mode = "payment",
                Customer = customer.StripeCustomerId,
                SuccessUrl = dto.SuccessUrl,
                CancelUrl = dto.CancelUrl,
                LineItems = new List<Stripe.Checkout.SessionLineItemOptions> { lineItem },
                PaymentIntentData = new Stripe.Checkout.SessionPaymentIntentDataOptions
                {
                    Metadata = new Dictionary<string, string>
                    {
                        { "customer_id", customer.Id.ToString() },
                        { "company_id", dto.CompanyId.ToString() }
                    }
                }
            };

            if (bookingId.HasValue)
            {
                sessionOptions.PaymentIntentData.Metadata!["booking_id"] = bookingId.Value.ToString();
            }

            if (!string.IsNullOrWhiteSpace(dto.BookingNumber))
            {
                sessionOptions.PaymentIntentData.Metadata!["booking_number"] = dto.BookingNumber;
            }

            if (!string.IsNullOrWhiteSpace(company.StripeAccountId))
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

            var session = await _stripeService.CreateCheckoutSessionAsync(sessionOptions);

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
            var captureIntent = await _stripeService.CapturePaymentIntentAsync(
                payment.SecurityDepositPaymentIntentId,
                dto.Amount);

            var capturedAmount = captureIntent.AmountReceived > 0
                ? captureIntent.AmountReceived / 100m
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
            }
            else
            {
                _logger.LogWarning("Booking {BookingId} referenced in payment intent {PaymentIntentId} was not found", bookingId, paymentIntent.Id);
            }
        }

        await _context.SaveChangesAsync();
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
}
