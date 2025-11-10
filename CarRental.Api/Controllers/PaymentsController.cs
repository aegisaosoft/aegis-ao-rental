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
using CarRental.Api.Data;
using CarRental.Api.DTOs;
using CarRental.Api.Models;
using CarRental.Api.Services;
using Stripe;
using CarRental.Api.DTOs.Payments;
using System.Security.Cryptography;

namespace CarRental.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly CarRentalDbContext _context;
    private readonly IStripeService _stripeService;
    private readonly ILogger<PaymentsController> _logger;
    private readonly IEncryptionService _encryptionService;

    public PaymentsController(
        CarRentalDbContext context, 
        IStripeService stripeService, 
        ILogger<PaymentsController> logger,
        IEncryptionService encryptionService)
    {
        _context = context;
        _stripeService = stripeService;
        _logger = logger;
        _encryptionService = encryptionService;
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
            Status = "pending"
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

            var lineItem = new Stripe.Checkout.SessionLineItemOptions
            {
                Quantity = 1,
                PriceData = new Stripe.Checkout.SessionLineItemPriceDataOptions
                {
                    Currency = dto.Currency.ToLowerInvariant(),
                    UnitAmount = amountInCents,
                    ProductData = new Stripe.Checkout.SessionLineItemPriceDataProductDataOptions
                    {
                        Name = string.IsNullOrWhiteSpace(dto.Description) ? "Vehicle Booking" : dto.Description,
                    }
                }
            };

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

            if (dto.ReservationId.HasValue)
            {
                sessionOptions.PaymentIntentData.Metadata!["reservation_id"] = dto.ReservationId.Value.ToString();
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

            var session = await _stripeService.CreateCheckoutSessionAsync(sessionOptions);

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
            var stripeEvent = await _stripeService.ConstructWebhookEventAsync(json, signature, "");
            
            switch (stripeEvent.Type)
            {
                case "payment_intent.succeeded":
                    await HandlePaymentIntentSucceeded(stripeEvent);
                    break;
                case "payment_intent.payment_failed":
                    await HandlePaymentIntentFailed(stripeEvent);
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

        var payment = await _context.Payments
            .FirstOrDefaultAsync(p => p.StripePaymentIntentId == paymentIntent.Id);

        if (payment != null)
        {
            payment.Status = "succeeded";
            payment.ProcessedAt = DateTime.UtcNow;
            payment.StripeChargeId = paymentIntent.LatestCharge?.Id;
            
            _context.Payments.Update(payment);
            await _context.SaveChangesAsync();
        }
    }

    private async Task HandlePaymentIntentFailed(Event stripeEvent)
    {
        var paymentIntent = stripeEvent.Data.Object as PaymentIntent;
        if (paymentIntent == null) return;

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
}
