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

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stripe;
using Stripe.Terminal;
using CarRental.Api.Data;
using CarRental.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TerminalController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly CarRentalDbContext _context;
    private readonly ILogger<TerminalController> _logger;

    public TerminalController(
        IConfiguration configuration, 
        CarRentalDbContext context,
        ILogger<TerminalController> logger)
    {
        _configuration = configuration;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Gets the Stripe account ID for a company from StripeCompany table
    /// </summary>
    private async Task<string?> GetStripeAccountIdAsync(Company company)
    {
        if (company.StripeSettingsId == null)
            return null;

        var stripeCompany = await _context.StripeCompanies
            .FirstOrDefaultAsync(sc => sc.CompanyId == company.Id && sc.SettingsId == company.StripeSettingsId.Value);

        return stripeCompany?.StripeAccountId;
    }

    /// <summary>
    /// Creates a connection token for Stripe Terminal
    /// </summary>
    [HttpPost("connection-token")]
    public async Task<IActionResult> CreateConnectionToken([FromBody] ConnectionTokenRequest request)
    {
        try
        {
            // Get company's Stripe account ID
            var company = await _context.Companies
                .FirstOrDefaultAsync(c => c.Id == request.CompanyId);

            if (company == null)
            {
                return NotFound(new { message = "Company not found" });
            }

            var stripeAccountId = await GetStripeAccountIdAsync(company);
            if (string.IsNullOrEmpty(stripeAccountId))
            {
                return BadRequest(new { message = "Company does not have a Stripe account connected" });
            }

            var requestOptions = new RequestOptions
            {
                StripeAccount = stripeAccountId
            };

            var service = new ConnectionTokenService();
            var options = new ConnectionTokenCreateOptions
            {
                // Can add location if needed
            };

            var connectionToken = await service.CreateAsync(options, requestOptions);

            _logger.LogInformation("Created connection token for company {CompanyId}", request.CompanyId);

            return Ok(new { secret = connectionToken.Secret });
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe error creating connection token for company {CompanyId}", request.CompanyId);
            return BadRequest(new { message = ex.Message, error = ex.StripeError?.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating connection token for company {CompanyId}", request.CompanyId);
            return StatusCode(500, new { message = "An error occurred while creating connection token" });
        }
    }

    /// <summary>
    /// Creates a payment intent for Stripe Terminal
    /// </summary>
    [HttpPost("create-payment-intent")]
    public async Task<IActionResult> CreatePaymentIntent([FromBody] CreateTerminalPaymentRequest request)
    {
        try
        {
            // Get company's Stripe account ID
            var company = await _context.Companies
                .FirstOrDefaultAsync(c => c.Id == request.CompanyId);

            if (company == null)
            {
                return NotFound(new { message = "Company not found" });
            }

            var stripeAccountId = await GetStripeAccountIdAsync(company);
            if (string.IsNullOrEmpty(stripeAccountId))
            {
                return BadRequest(new { message = "Company does not have a Stripe account connected" });
            }

            var requestOptions = new RequestOptions
            {
                StripeAccount = stripeAccountId
            };

            var options = new PaymentIntentCreateOptions
            {
                Amount = request.Amount,
                Currency = request.Currency?.ToLower() ?? "usd",
                CaptureMethod = request.CaptureMethod ?? "manual", // manual or automatic
                PaymentMethodTypes = new List<string> { "card_present" },
                Description = request.Description,
                Metadata = request.Metadata ?? new Dictionary<string, string>()
            };

            // Add booking ID to metadata if provided
            if (!string.IsNullOrEmpty(request.BookingId))
            {
                options.Metadata["booking_id"] = request.BookingId;
            }

            var service = new PaymentIntentService();
            var paymentIntent = await service.CreateAsync(options, requestOptions);

            _logger.LogInformation(
                "Created payment intent {PaymentIntentId} for company {CompanyId}, amount {Amount}", 
                paymentIntent.Id, 
                request.CompanyId, 
                request.Amount);

            return Ok(new 
            { 
                id = paymentIntent.Id,
                clientSecret = paymentIntent.ClientSecret,
                amount = paymentIntent.Amount,
                currency = paymentIntent.Currency,
                status = paymentIntent.Status
            });
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe error creating payment intent for company {CompanyId}", request.CompanyId);
            return BadRequest(new { message = ex.Message, error = ex.StripeError?.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating payment intent for company {CompanyId}", request.CompanyId);
            return StatusCode(500, new { message = "An error occurred while creating payment intent" });
        }
    }

    /// <summary>
    /// Captures a payment intent (for manual capture)
    /// </summary>
    [HttpPost("capture-payment-intent")]
    public async Task<IActionResult> CapturePaymentIntent([FromBody] CapturePaymentRequest request)
    {
        try
        {
            // Get company's Stripe account ID
            var company = await _context.Companies
                .FirstOrDefaultAsync(c => c.Id == request.CompanyId);

            if (company == null)
            {
                return NotFound(new { message = "Company not found" });
            }

            var stripeAccountId = await GetStripeAccountIdAsync(company);
            if (string.IsNullOrEmpty(stripeAccountId))
            {
                return BadRequest(new { message = "Company does not have a Stripe account connected" });
            }

            var requestOptions = new RequestOptions
            {
                StripeAccount = stripeAccountId
            };

            var service = new PaymentIntentService();
            var options = new PaymentIntentCaptureOptions
            {
                AmountToCapture = request.AmountToCapture
            };

            var paymentIntent = await service.CaptureAsync(request.PaymentIntentId, options, requestOptions);

            _logger.LogInformation(
                "Captured payment intent {PaymentIntentId} for company {CompanyId}", 
                request.PaymentIntentId, 
                request.CompanyId);

            return Ok(new 
            { 
                id = paymentIntent.Id,
                amount = paymentIntent.Amount,
                amountCaptured = paymentIntent.AmountCapturable,
                status = paymentIntent.Status
            });
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe error capturing payment intent {PaymentIntentId}", request.PaymentIntentId);
            return BadRequest(new { message = ex.Message, error = ex.StripeError?.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error capturing payment intent {PaymentIntentId}", request.PaymentIntentId);
            return StatusCode(500, new { message = "An error occurred while capturing payment" });
        }
    }

    /// <summary>
    /// Cancels a payment intent
    /// </summary>
    [HttpPost("cancel-payment-intent")]
    public async Task<IActionResult> CancelPaymentIntent([FromBody] CancelPaymentRequest request)
    {
        try
        {
            // Get company's Stripe account ID
            var company = await _context.Companies
                .FirstOrDefaultAsync(c => c.Id == request.CompanyId);

            if (company == null)
            {
                return NotFound(new { message = "Company not found" });
            }

            var stripeAccountId = await GetStripeAccountIdAsync(company);
            if (string.IsNullOrEmpty(stripeAccountId))
            {
                return BadRequest(new { message = "Company does not have a Stripe account connected" });
            }

            var requestOptions = new RequestOptions
            {
                StripeAccount = stripeAccountId
            };

            var service = new PaymentIntentService();
            var paymentIntent = await service.CancelAsync(request.PaymentIntentId, null, requestOptions);

            _logger.LogInformation(
                "Cancelled payment intent {PaymentIntentId} for company {CompanyId}", 
                request.PaymentIntentId, 
                request.CompanyId);

            return Ok(new 
            { 
                id = paymentIntent.Id,
                status = paymentIntent.Status
            });
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe error cancelling payment intent {PaymentIntentId}", request.PaymentIntentId);
            return BadRequest(new { message = ex.Message, error = ex.StripeError?.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling payment intent {PaymentIntentId}", request.PaymentIntentId);
            return StatusCode(500, new { message = "An error occurred while cancelling payment" });
        }
    }
}

// Request models
public class ConnectionTokenRequest
{
    public Guid CompanyId { get; set; }
}

public class CreateTerminalPaymentRequest
{
    public Guid CompanyId { get; set; }
    public long Amount { get; set; }
    public string? Currency { get; set; }
    public string? CaptureMethod { get; set; } // "manual" or "automatic"
    public string? Description { get; set; }
    public string? BookingId { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}

public class CapturePaymentRequest
{
    public Guid CompanyId { get; set; }
    public string PaymentIntentId { get; set; } = string.Empty;
    public long? AmountToCapture { get; set; }
}

public class CancelPaymentRequest
{
    public Guid CompanyId { get; set; }
    public string PaymentIntentId { get; set; } = string.Empty;
}

