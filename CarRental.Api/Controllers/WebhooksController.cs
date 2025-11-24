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
using Microsoft.Extensions.DependencyInjection;
using Stripe;
using CarRental.Api.Data;
using CarRental.Api.Models;
using CarRental.Api.Services;
using System.IO;
using System.Text;
using System.Linq;
using BCrypt.Net;
using Microsoft.Extensions.Configuration;

namespace CarRental.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WebhooksController : ControllerBase
{
    private readonly CarRentalDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<WebhooksController> _logger;
    private readonly IEncryptionService _encryptionService;

    public WebhooksController(
        CarRentalDbContext context,
        IConfiguration configuration,
        ILogger<WebhooksController> logger,
        IEncryptionService encryptionService)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
        _encryptionService = encryptionService;
    }

    [HttpPost("stripe")]
    public async Task<IActionResult> StripeWebhook()
    {
        _logger.LogInformation("==================== WEBHOOK ENDPOINT HIT ====================");
        _logger.LogInformation("Request Method: {Method}, Path: {Path}", HttpContext.Request.Method, HttpContext.Request.Path);
        _logger.LogInformation("Headers: {Headers}", string.Join(", ", HttpContext.Request.Headers.Select(h => $"{h.Key}={h.Value}")));
        
        try
        {
            var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
            _logger.LogInformation("Webhook body length: {Length} bytes", json?.Length ?? 0);

            try
            {
                var stripeEvent = EventUtility.ParseEvent(json);
                var signatureHeader = Request.Headers["Stripe-Signature"];

                // Verify webhook signature if webhook secret is configured
                var webhookSecret = _configuration["Stripe:WebhookSecret"];
                if (!string.IsNullOrEmpty(webhookSecret))
                {
                    try
                    {
                        stripeEvent = EventUtility.ConstructEvent(
                            json,
                            signatureHeader,
                            webhookSecret
                        );
                    }
                    catch (StripeException ex)
                    {
                        _logger.LogError(ex, "Stripe webhook signature verification failed");
                        // Return 200 OK even for signature failures to prevent retry loop
                        return Ok(new { received = false, error = "Invalid signature" });
                    }
                }

                _logger.LogInformation("Stripe webhook received: {EventType} [{EventId}]", 
                    stripeEvent.Type, stripeEvent.Id);

            // Handle the event
            switch (stripeEvent.Type)
            {
                case "payment_intent.succeeded":
                    await HandlePaymentIntentSucceeded(stripeEvent);
                    break;

                case "payment_intent.payment_failed":
                    await HandlePaymentIntentFailed(stripeEvent);
                    break;

                case "charge.succeeded":
                    await HandleChargeSucceeded(stripeEvent);
                    break;
                
                case "payment.created":
                    // Stripe Connect payment created - check if it's for a booking
                    await HandlePaymentCreated(stripeEvent);
                    break;

                case "charge.failed":
                    await HandleChargeFailed(stripeEvent);
                    break;

                case "charge.refunded":
                    await HandleChargeRefunded(stripeEvent);
                    break;

                case "checkout.session.completed":
                    await HandleCheckoutSessionCompleted(stripeEvent);
                    break;

                // Stripe Connect events
                case "transfer.created":
                case "payout.paid":
                case "payout.failed":
                case "account.application.authorized":
                case "account.application.deauthorized":
                    _logger.LogInformation("Stripe Connect event received (not processed): {EventType}", stripeEvent.Type);
                    break;
                
                case "account.updated":
                    await HandleAccountUpdated(stripeEvent);
                    break;

                // Additional payment intent events
                case "payment_intent.created":
                case "payment_intent.amount_capturable_updated":
                case "payment_intent.canceled":
                case "payment_intent.processing":
                    _logger.LogInformation("Payment intent event received (not processed): {EventType}", stripeEvent.Type);
                    break;

                // Additional charge events
                case "charge.updated":
                    // Handle charge.updated - it might be a succeeded charge
                    await HandleChargeUpdated(stripeEvent);
                    break;
                    
                case "charge.pending":
                case "charge.captured":
                case "charge.expired":
                    _logger.LogInformation("Charge event received (not processed): {EventType}", stripeEvent.Type);
                    break;

                // Checkout session events
                case "checkout.session.async_payment_succeeded":
                case "checkout.session.async_payment_failed":
                case "checkout.session.expired":
                    _logger.LogInformation("Checkout session event received (not processed): {EventType}", stripeEvent.Type);
                    break;

                default:
                    _logger.LogInformation("Unhandled Stripe event type: {EventType}", stripeEvent.Type);
                    break;
            }

            return Ok(new { received = true });
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "Stripe webhook error: {Message}", ex.Message);
                // Still return 200 to prevent Stripe from retrying
                return Ok(new { received = false, error = "Stripe error", message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Stripe webhook: {Message}", ex.Message);
                // Still return 200 to prevent Stripe from retrying
                return Ok(new { received = false, error = "Internal error", message = ex.Message });
            }
        }
        catch (Exception outerEx)
        {
            _logger.LogError(outerEx, "Fatal error in webhook handler: {Message}", outerEx.Message);
            // Always return 200 OK to prevent Stripe retry loops
            return Ok(new { received = false, error = "Fatal error", message = outerEx.Message });
        }
    }

    private async Task HandlePaymentIntentSucceeded(Event stripeEvent)
    {
        try
        {
            var paymentIntent = stripeEvent.Data.Object as PaymentIntent;
            if (paymentIntent == null) return;

            _logger.LogInformation("Payment succeeded: {PaymentIntentId}, Amount: {Amount}", 
                paymentIntent.Id, paymentIntent.Amount);

            // Find the payment record
            var payment = await _context.Payments
                .Include(p => p.Reservation)
                .FirstOrDefaultAsync(p => p.StripePaymentIntentId == paymentIntent.Id);

            if (payment != null)
            {
                payment.Status = "succeeded";
                payment.ProcessedAt = DateTime.UtcNow;
                payment.UpdatedAt = DateTime.UtcNow;

                // Update booking status from Pending to Confirmed
                if (payment.Reservation != null && payment.Reservation.Status == "Pending")
                {
                    payment.Reservation.Status = "Confirmed";
                    _logger.LogInformation("Updated booking {BookingNumber} status from Pending to Confirmed after payment_intent.succeeded", 
                        payment.Reservation.BookingNumber);
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Updated payment status for booking {BookingId}", 
                    payment.ReservationId);

                // Send invitation email after payment if booking was confirmed
                if (payment.Reservation != null && payment.Reservation.Status == "Confirmed")
                {
                    await SendInvitationEmailAfterPayment(payment.Reservation);
                }
            }
            else
            {
                // Payment record not found - try to find booking by StripePaymentIntentId
                var booking = await _context.Bookings
                    .Include(b => b.Customer)
                    .Include(b => b.Company)
                    .FirstOrDefaultAsync(b => b.StripePaymentIntentId == paymentIntent.Id);
                
                if (booking != null)
                {
                    _logger.LogInformation("Creating payment record for booking {BookingNumber}", booking.BookingNumber);
                    
                    // Create payment record
                    var newPayment = new Payment
                    {
                        CustomerId = booking.CustomerId,
                        CompanyId = booking.CompanyId,
                        ReservationId = booking.Id,
                        Amount = paymentIntent.Amount / 100m, // Stripe amount is in cents
                        Currency = paymentIntent.Currency?.ToUpper() ?? "USD",
                        PaymentType = "full_payment",
                        PaymentMethod = "card",
                        StripePaymentIntentId = paymentIntent.Id,
                        Status = "succeeded",
                        ProcessedAt = DateTime.UtcNow
                    };
                    
                    _context.Payments.Add(newPayment);
                    
                    // Update booking status
                    if (booking.Status == "Pending")
                    {
                        booking.Status = "Confirmed";
                        _logger.LogInformation("Updated booking {BookingNumber} status from Pending to Confirmed", 
                            booking.BookingNumber);
                    }
                    
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Payment record created and booking updated for {BookingNumber}", 
                        booking.BookingNumber);

                    // Send invitation email after payment if booking was confirmed
                    if (booking.Status == "Confirmed")
                    {
                        await SendInvitationEmailAfterPayment(booking);
                    }
                }
                else
                {
                    _logger.LogWarning("Payment and Booking not found for PaymentIntent: {PaymentIntentId}", 
                        paymentIntent.Id);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling payment_intent.succeeded for event {EventId}", stripeEvent.Id);
        }
    }

    private async Task HandlePaymentIntentFailed(Event stripeEvent)
    {
        try
        {
            var paymentIntent = stripeEvent.Data.Object as PaymentIntent;
            if (paymentIntent == null) return;

            _logger.LogWarning("Payment failed: {PaymentIntentId}", paymentIntent.Id);

            var payment = await _context.Payments
                .FirstOrDefaultAsync(p => p.StripePaymentIntentId == paymentIntent.Id);

            if (payment != null)
            {
                payment.Status = "failed";
                payment.FailureReason = paymentIntent.LastPaymentError?.Message ?? "Payment failed";
                payment.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Updated payment status to failed for booking {BookingId}", 
                    payment.ReservationId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling payment_intent.payment_failed for event {EventId}", stripeEvent.Id);
        }
    }

    private async Task HandleChargeSucceeded(Event stripeEvent)
    {
        try
        {
            var charge = stripeEvent.Data.Object as Charge;
            if (charge == null) return;

            _logger.LogInformation("Charge succeeded: {ChargeId}, PaymentIntent: {PaymentIntentId}, Amount: {Amount}", 
                charge.Id, charge.PaymentIntentId, charge.Amount);

            var payment = await _context.Payments
                .Include(p => p.Reservation)
                .FirstOrDefaultAsync(p => p.StripeChargeId == charge.Id || 
                                         p.StripePaymentIntentId == charge.PaymentIntentId);

            if (payment != null)
            {
                payment.StripeChargeId = charge.Id;
                payment.Status = "succeeded";
                payment.ProcessedAt = DateTime.UtcNow;
                payment.UpdatedAt = DateTime.UtcNow;

                // Update booking status from Pending to Confirmed
                if (payment.Reservation != null && payment.Reservation.Status == "Pending")
                {
                    payment.Reservation.Status = "Confirmed";
                    _logger.LogInformation("Updated booking {BookingNumber} status from Pending to Confirmed after charge succeeded", 
                        payment.Reservation.BookingNumber);
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("Payment updated for booking {BookingId}", payment.ReservationId);

                // Send invitation email after payment if booking was confirmed
                if (payment.Reservation != null && payment.Reservation.Status == "Confirmed")
                {
                    await SendInvitationEmailAfterPayment(payment.Reservation);
                }
            }
            else
            {
                // Payment not found - try to find booking and create payment
                var booking = await _context.Bookings
                    .Include(b => b.Customer)
                    .Include(b => b.Company)
                    .FirstOrDefaultAsync(b => b.StripePaymentIntentId == charge.PaymentIntentId);
                
                if (booking != null)
                {
                    _logger.LogInformation("Creating payment record for booking {BookingNumber} from charge.succeeded", 
                        booking.BookingNumber);
                    
                    // Create payment record
                    var newPayment = new Payment
                    {
                        CustomerId = booking.CustomerId,
                        CompanyId = booking.CompanyId,
                        ReservationId = booking.Id,
                        Amount = charge.Amount / 100m, // Stripe amount is in cents
                        Currency = charge.Currency?.ToUpper() ?? "USD",
                        PaymentType = "full_payment",
                        PaymentMethod = "card",
                        StripePaymentIntentId = charge.PaymentIntentId,
                        StripeChargeId = charge.Id,
                        Status = "succeeded",
                        ProcessedAt = DateTime.UtcNow
                    };
                    
                    _context.Payments.Add(newPayment);
                    
                    // Update booking status
                    if (booking.Status == "Pending")
                    {
                        booking.Status = "Confirmed";
                        _logger.LogInformation("Updated booking {BookingNumber} status from Pending to Confirmed", 
                            booking.BookingNumber);
                    }
                    
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Payment record created and booking updated for {BookingNumber}", 
                        booking.BookingNumber);

                    // Send invitation email after payment if booking was confirmed
                    if (booking.Status == "Confirmed")
                    {
                        await SendInvitationEmailAfterPayment(booking);
                    }
                }
                else
                {
                    _logger.LogWarning("Payment and Booking not found for charge {ChargeId}, PaymentIntent: {PaymentIntentId}", 
                        charge.Id, charge.PaymentIntentId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling charge.succeeded for event {EventId}", stripeEvent.Id);
        }
    }

    private async Task HandleChargeFailed(Event stripeEvent)
    {
        try
        {
            var charge = stripeEvent.Data.Object as Charge;
            if (charge == null) return;

            _logger.LogWarning("Charge failed: {ChargeId}", charge.Id);

            var payment = await _context.Payments
                .FirstOrDefaultAsync(p => p.StripeChargeId == charge.Id || 
                                         p.StripePaymentIntentId == charge.PaymentIntentId);

            if (payment != null)
            {
                payment.Status = "failed";
                payment.FailureReason = charge.FailureMessage ?? "Charge failed";
                payment.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling charge.failed for event {EventId}", stripeEvent.Id);
        }
    }

    private async Task HandleChargeUpdated(Event stripeEvent)
    {
        try
        {
            var charge = stripeEvent.Data.Object as Charge;
            if (charge == null) return;

            _logger.LogInformation("Charge updated: {ChargeId}, Status: {Status}, PaymentIntent: {PaymentIntentId}, Amount: {Amount}", 
                charge.Id, charge.Status, charge.PaymentIntentId, charge.Amount);

            // Only process if status is succeeded
            if (charge.Status != "succeeded")
            {
                _logger.LogInformation("Charge status is {Status}, not processing", charge.Status);
                return;
            }

            var payment = await _context.Payments
                .Include(p => p.Reservation)
                .FirstOrDefaultAsync(p => p.StripeChargeId == charge.Id || 
                                         p.StripePaymentIntentId == charge.PaymentIntentId);

            if (payment != null)
            {
                payment.StripeChargeId = charge.Id;
                payment.Status = "succeeded";
                payment.ProcessedAt = DateTime.UtcNow;
                payment.UpdatedAt = DateTime.UtcNow;

                // Update booking status from Pending to Confirmed
                if (payment.Reservation != null && payment.Reservation.Status == "Pending")
                {
                    payment.Reservation.Status = "Confirmed";
                    _logger.LogInformation("Updated booking {BookingNumber} status from Pending to Confirmed after charge.updated", 
                        payment.Reservation.BookingNumber);
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("Payment updated for booking {BookingId}", payment.ReservationId);

                // Send invitation email after payment if booking was confirmed
                if (payment.Reservation != null && payment.Reservation.Status == "Confirmed")
                {
                    await SendInvitationEmailAfterPayment(payment.Reservation);
                }
            }
            else
            {
                // Payment not found - try to find booking and create payment
                var booking = await _context.Bookings
                    .Include(b => b.Customer)
                    .Include(b => b.Company)
                    .FirstOrDefaultAsync(b => b.StripePaymentIntentId == charge.PaymentIntentId);
                
                if (booking != null)
                {
                    _logger.LogInformation("Creating payment record for booking {BookingNumber} from charge.updated (status: {Status})", 
                        booking.BookingNumber, charge.Status);
                    
                    // Create payment record
                    var newPayment = new Payment
                    {
                        CustomerId = booking.CustomerId,
                        CompanyId = booking.CompanyId,
                        ReservationId = booking.Id,
                        Amount = charge.Amount / 100m, // Stripe amount is in cents
                        Currency = charge.Currency?.ToUpper() ?? "USD",
                        PaymentType = "full_payment",
                        PaymentMethod = "card",
                        StripePaymentIntentId = charge.PaymentIntentId,
                        StripeChargeId = charge.Id,
                        Status = "succeeded",
                        ProcessedAt = DateTime.UtcNow
                    };
                    
                    _context.Payments.Add(newPayment);
                    
                    // Update booking status
                    if (booking.Status == "Pending")
                    {
                        booking.Status = "Confirmed";
                        _logger.LogInformation("Updated booking {BookingNumber} status from Pending to Confirmed", 
                            booking.BookingNumber);
                    }
                    
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Payment record created and booking updated for {BookingNumber}", 
                        booking.BookingNumber);

                    // Send invitation email after payment if booking was confirmed
                    if (booking.Status == "Confirmed")
                    {
                        await SendInvitationEmailAfterPayment(booking);
                    }
                }
                else
                {
                    _logger.LogWarning("Payment and Booking not found for charge {ChargeId}, PaymentIntent: {PaymentIntentId}", 
                        charge.Id, charge.PaymentIntentId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling charge.updated for event {EventId}", stripeEvent.Id);
        }
    }

    private async Task HandleChargeRefunded(Event stripeEvent)
    {
        try
        {
            var charge = stripeEvent.Data.Object as Charge;
            if (charge == null) return;

            _logger.LogInformation("Charge refunded: {ChargeId}, Amount: {Amount}", 
                charge.Id, charge.AmountRefunded);

            var payment = await _context.Payments
                .FirstOrDefaultAsync(p => p.StripeChargeId == charge.Id || 
                                         p.StripePaymentIntentId == charge.PaymentIntentId);

            if (payment != null)
            {
                payment.Status = "refunded";
                payment.RefundAmount = charge.AmountRefunded / 100m; // Convert from cents
                payment.RefundDate = DateTime.UtcNow;
                payment.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Updated payment status to refunded for booking {BookingId}", 
                    payment.ReservationId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling charge.refunded for event {EventId}", stripeEvent.Id);
        }
    }

    private async Task HandleCheckoutSessionCompleted(Event stripeEvent)
    {
        try
        {
            var session = stripeEvent.Data.Object as Stripe.Checkout.Session;
            if (session == null)
            {
                _logger.LogWarning("Checkout session is null for event {EventId}", stripeEvent.Id);
                return;
            }

            _logger.LogInformation("Checkout session completed: {SessionId}, PaymentIntent: {PaymentIntentId}", 
                session.Id, session.PaymentIntentId);

            // Log all metadata for debugging
            if (session.Metadata != null && session.Metadata.Any())
            {
                _logger.LogInformation("Session metadata: {Metadata}", 
                    string.Join(", ", session.Metadata.Select(kvp => $"{kvp.Key}={kvp.Value}")));
            }
            else
            {
                _logger.LogWarning("No metadata found in checkout session {SessionId}", session.Id);
            }

            // Check if this is a security deposit payment
            if (session.Metadata != null && session.Metadata.ContainsKey("type") && 
                session.Metadata["type"] == "security_deposit" && 
                session.Metadata.ContainsKey("booking_id"))
            {
                var bookingIdStr = session.Metadata["booking_id"];
                _logger.LogInformation("Processing security deposit for booking_id: {BookingId}", bookingIdStr);
                
                if (Guid.TryParse(bookingIdStr, out var bookingId))
                {
                    var booking = await _context.Bookings.FindAsync(bookingId);
                    if (booking != null)
                    {
                        _logger.LogInformation("Found booking {BookingNumber}, current status: {Status}, current PI: {CurrentPI}", 
                            booking.BookingNumber, booking.Status, booking.SecurityDepositPaymentIntentId ?? "null");
                        
                        // Store the payment intent ID
                        booking.SecurityDepositPaymentIntentId = session.PaymentIntentId;
                        booking.SecurityDepositStatus = "authorized";
                        booking.SecurityDepositAuthorizedAt = DateTime.UtcNow;
                        
                        // Update booking status to Active (security deposit paid)
                        if (booking.Status == "Confirmed")
                        {
                            booking.Status = "Active";
                            _logger.LogInformation("Updated booking {BookingId} status to Active after security deposit payment", 
                                bookingId);
                        }
                        else
                        {
                            _logger.LogInformation("Booking {BookingId} status is {Status}, not updating to Active", 
                                bookingId, booking.Status);
                        }
                        
                        await _context.SaveChangesAsync();
                        
                        _logger.LogInformation("Security deposit payment processed for booking {BookingId}, PaymentIntent: {PaymentIntentId}", 
                            bookingId, session.PaymentIntentId);
                        return;
                    }
                    else
                    {
                        _logger.LogWarning("Booking not found for ID: {BookingId}", bookingId);
                    }
                }
                else
                {
                    _logger.LogWarning("Failed to parse booking_id: {BookingId}", bookingIdStr);
                }
            }
            else
            {
                _logger.LogInformation("Not a security deposit payment (missing metadata or wrong type)");
            }

            // Find regular payment by payment intent
            var payment = await _context.Payments
                .Include(p => p.Reservation)
                .FirstOrDefaultAsync(p => p.StripePaymentIntentId == session.PaymentIntentId);

            if (payment != null)
            {
                payment.Status = "succeeded";
                payment.ProcessedAt = DateTime.UtcNow;
                payment.UpdatedAt = DateTime.UtcNow;

                // Update booking status from Pending to Confirmed when initial payment succeeds
                if (payment.Reservation != null && payment.Reservation.Status == "Pending")
                {
                    payment.Reservation.Status = "Confirmed";
                    _logger.LogInformation("Updated booking {BookingNumber} status from Pending to Confirmed after payment", 
                        payment.Reservation.BookingNumber);
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Payment status updated to succeeded for booking {BookingId}", 
                    payment.ReservationId);

                // Send invitation email after payment if booking was confirmed
                if (payment.Reservation != null && payment.Reservation.Status == "Confirmed")
                {
                    await SendInvitationEmailAfterPayment(payment.Reservation);
                }
            }
            else
            {
                // Payment not found, try to find booking by metadata (booking_id)
                if (session.Metadata != null && session.Metadata.ContainsKey("booking_id"))
                {
                    var bookingIdStr = session.Metadata["booking_id"];
                    if (Guid.TryParse(bookingIdStr, out var bookingId))
                    {
                        var booking = await _context.Bookings
                            .Include(b => b.Customer)
                            .Include(b => b.Company)
                            .FirstOrDefaultAsync(b => b.Id == bookingId);
                            
                        if (booking != null)
                        {
                            // Create payment record
                            var newPayment = new Payment
                            {
                                CustomerId = booking.CustomerId,
                                CompanyId = booking.CompanyId,
                                ReservationId = booking.Id,
                                Amount = (decimal)(session.AmountTotal ?? 0) / 100m, // Convert from cents
                                Currency = session.Currency?.ToUpper() ?? "USD",
                                PaymentType = "booking",
                                PaymentMethod = "stripe_checkout",
                                StripePaymentIntentId = session.PaymentIntentId,
                                Status = "succeeded",
                                ProcessedAt = DateTime.UtcNow,
                                CreatedAt = DateTime.UtcNow
                            };
                            
                            _context.Payments.Add(newPayment);
                            
                            // Update booking status and payment intent ID
                            if (booking.Status == "Pending")
                            {
                                booking.Status = "Confirmed";
                                _logger.LogInformation("Updated booking {BookingNumber} status from Pending to Confirmed (via metadata)", 
                                    booking.BookingNumber);
                            }
                            
                            booking.StripePaymentIntentId = session.PaymentIntentId;
                            
                            await _context.SaveChangesAsync();
                            
                            _logger.LogInformation("Created payment record and updated booking {BookingNumber} (via checkout.session.completed metadata)", 
                                booking.BookingNumber);

                            // Send invitation email after payment if booking was confirmed
                            if (booking.Status == "Confirmed")
                            {
                                await SendInvitationEmailAfterPayment(booking);
                            }
                        }
                        else
                        {
                            _logger.LogWarning("Booking not found for ID: {BookingId} from checkout session metadata", bookingId);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Failed to parse booking_id from checkout session metadata: {BookingId}", bookingIdStr);
                    }
                }
                else
                {
                    _logger.LogWarning("Payment not found for checkout session: {SessionId}, and no booking_id in session metadata", session.Id);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling checkout.session.completed for event {EventId}", stripeEvent.Id);
        }
    }

    private async Task HandlePaymentCreated(Event stripeEvent)
    {
        try
        {
            _logger.LogInformation("Processing payment.created event");
            
            // This is a Stripe Connect event - the payment object structure is different
            // Try to get payment intent ID from the event data
            var paymentData = stripeEvent.Data.Object as Newtonsoft.Json.Linq.JObject;
            if (paymentData == null)
            {
                _logger.LogWarning("payment.created event has no data");
                return;
            }

            // Try to get the source payment intent
            var sourceTransferId = paymentData["source_transfer"]?.ToString();
            _logger.LogInformation("Payment created with source_transfer: {SourceTransfer}", sourceTransferId);
            
            // For now, just log it - we'll handle booking updates when charge.succeeded fires
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling payment.created for event {EventId}", stripeEvent.Id);
        }
    }

    private async Task SendInvitationEmailAfterPayment(Reservation reservation)
    {
        try
        {
            _logger.LogInformation(
                "SendInvitationEmailAfterPayment: Starting for booking {BookingId}, CustomerId: {CustomerId}",
                reservation.Id,
                reservation.CustomerId);

            // Load customer with tracking to check current state
            var customer = await _context.Customers
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == reservation.CustomerId);
            
            if (customer == null)
            {
                _logger.LogWarning(
                    "SendInvitationEmailAfterPayment: Customer {CustomerId} not found for booking {BookingId}",
                    reservation.CustomerId,
                    reservation.Id);
                return;
            }

            // Skip if customer has already logged in (they already have access)
            if (customer.LastLogin.HasValue)
            {
                _logger.LogInformation(
                    "SendInvitationEmailAfterPayment: Skipping - Customer {CustomerId} has already logged in (LastLogin: {LastLogin})",
                    customer.Id,
                    customer.LastLogin);
                return; // Customer already has access
            }

            // Check if customer has an email address
            if (string.IsNullOrWhiteSpace(customer.Email))
            {
                _logger.LogWarning(
                    "SendInvitationEmailAfterPayment: Skipping - Customer {CustomerId} has no email address set",
                    customer.Id);
                return; // Cannot send email without an email address
            }

            // Reload customer with tracking to update it
            // Use a database transaction to prevent race conditions from multiple webhook events
            var customerToUpdate = await _context.Customers.FindAsync(reservation.CustomerId);
            if (customerToUpdate == null)
            {
                _logger.LogWarning(
                    "SendInvitationEmailAfterPayment: Customer {CustomerId} not found when trying to update password",
                    reservation.CustomerId);
                return;
            }

            // Double-check: If customer already has a password hash, another webhook may have already processed this
            // Reload to get the latest state (in case another webhook updated it)
            await _context.Entry(customerToUpdate).ReloadAsync();
            
            // Check if customer has an email address after reload
            if (string.IsNullOrWhiteSpace(customerToUpdate.Email))
            {
                _logger.LogWarning(
                    "SendInvitationEmailAfterPayment: Skipping - Customer {CustomerId} has no email address set after reload",
                    customerToUpdate.Id);
                return; // Cannot send email without an email address
            }
            
            // If customer already has a password hash and was recently updated, skip (another webhook already sent invitation)
            if (!string.IsNullOrEmpty(customerToUpdate.PasswordHash) && 
                customerToUpdate.UpdatedAt > DateTime.UtcNow.AddMinutes(-5))
            {
                _logger.LogInformation(
                    "SendInvitationEmailAfterPayment: Skipping - Customer {CustomerId} already has a password hash and was recently updated (UpdatedAt: {UpdatedAt}), invitation email likely already sent by another webhook",
                    customerToUpdate.Id,
                    customerToUpdate.UpdatedAt);
                return; // Another webhook already sent the invitation
            }

            // Only generate password if customer doesn't have one yet
            // If customer already has a password hash (from a previous booking), don't overwrite it
            string temporaryPassword;
            if (string.IsNullOrEmpty(customerToUpdate.PasswordHash))
            {
                // Generate a temporary password for the invitation email
                var random = new Random();
                const string chars = "ABCDEFGHJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
                temporaryPassword = new string(Enumerable.Repeat(chars, 12)
                    .Select(s => s[random.Next(s.Length)]).ToArray());

                // Update customer's password with the new temporary password
                customerToUpdate.PasswordHash = BCrypt.Net.BCrypt.HashPassword(temporaryPassword);
                customerToUpdate.UpdatedAt = DateTime.UtcNow;
                _context.Customers.Update(customerToUpdate);
                await _context.SaveChangesAsync(); // Save password update before sending email
                
                _logger.LogInformation(
                    "SendInvitationEmailAfterPayment: Generated new password for customer {CustomerId}",
                    customerToUpdate.Id);
            }
            else
            {
                // Customer already has a password - we can't retrieve it from the hash
                // Skip sending invitation email as we don't have the password to include
                _logger.LogInformation(
                    "SendInvitationEmailAfterPayment: Skipping - Customer {CustomerId} already has a password hash (from previous booking), cannot send invitation with password",
                    customerToUpdate.Id);
                return;
            }

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
                customerToUpdate.Email,
                reservation.BookingNumber);

            // Prepare booking details
            var vehicleName = vehicle?.VehicleModel?.Model != null
                ? $"{vehicle.VehicleModel.Model.Make} {vehicle.VehicleModel.Model.ModelName} ({vehicle.VehicleModel.Model.Year})"
                : "Vehicle";

            var invitationUrl = $"{Request.Scheme}://{Request.Host}/login?email={Uri.EscapeDataString(customerToUpdate.Email)}";

            // Send invitation email with booking details and password
            // Use customerToUpdate for name since we have it loaded with tracking
            var customerName = (!string.IsNullOrWhiteSpace(customerToUpdate.FirstName) || !string.IsNullOrWhiteSpace(customerToUpdate.LastName))
                ? $"{customerToUpdate.FirstName} {customerToUpdate.LastName}".Trim()
                : customerToUpdate.Email;
            
            var emailSent = await multiTenantEmailService.SendInvitationEmailWithBookingDetailsAsync(
                reservation.CompanyId,
                customerToUpdate.Email,
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
                    customerToUpdate.Email,
                    reservation.BookingNumber);
            }
            else
            {
                _logger.LogWarning(
                    "SendInvitationEmailAfterPayment: Failed to send invitation email to {Email} for booking {BookingNumber}",
                    customerToUpdate.Email,
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
}

