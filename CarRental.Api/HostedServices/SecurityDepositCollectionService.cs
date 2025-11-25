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

using System;
using System.Collections.Generic;
using CarRental.Api.Data;
using CarRental.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Stripe;

namespace CarRental.Api.HostedServices;

/// <summary>
/// Background service that authorizes security deposit holds shortly before the rental start date.
/// </summary>
public class SecurityDepositCollectionService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SecurityDepositCollectionService> _logger;
    private static readonly TimeSpan PollInterval = TimeSpan.FromHours(12);

    public SecurityDepositCollectionService(
        IServiceProvider serviceProvider,
        ILogger<SecurityDepositCollectionService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Security deposit collection service starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDueDepositsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Graceful shutdown requested.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while processing scheduled security deposits.");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("Security deposit collection service stopping.");
    }

    private async Task ProcessDueDepositsAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CarRentalDbContext>();
        var stripeService = scope.ServiceProvider.GetRequiredService<IStripeService>();

        var utcNow = DateTime.UtcNow;
        var today = utcNow.Date;
        var windowEnd = today.AddDays(14);

        var payments = await dbContext.Payments
            .Include(p => p.Reservation)
            .Include(p => p.Customer)
            .Where(p => p.SecurityDepositAmount.HasValue
                        && p.SecurityDepositAmount > 0
                        && p.SecurityDepositStatus == "scheduled"
                        && p.Reservation != null
                        && p.Reservation.PickupDate.Date >= today
                        && p.Reservation.PickupDate.Date <= windowEnd)
            .ToListAsync(stoppingToken);

        if (payments.Count == 0)
        {
            _logger.LogDebug("No security deposits scheduled for authorization in the current window ({Start} - {End}).", today, windowEnd);
            return;
        }

        _logger.LogInformation("Processing {Count} scheduled security deposits.", payments.Count);

        foreach (var payment in payments)
        {
            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            await AuthorizeSecurityDepositAsync(dbContext, stripeService, payment, stoppingToken);
        }
    }

    private async Task AuthorizeSecurityDepositAsync(
        CarRentalDbContext dbContext,
        IStripeService stripeService,
        Models.Payment payment,
        CancellationToken cancellationToken)
    {
        var securityDepositAmount = payment.SecurityDepositAmount;
        if (!securityDepositAmount.HasValue || securityDepositAmount.Value <= 0)
        {
            return;
        }

        if (payment.Reservation == null)
        {
            payment.SecurityDepositStatus = "failed";
            payment.FailureReason = "Booking details missing for scheduled security deposit.";
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        if (string.IsNullOrWhiteSpace(payment.StripePaymentMethodId))
        {
            payment.SecurityDepositStatus = "failed";
            payment.FailureReason = "No saved payment method available for security deposit authorization.";
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        var customer = payment.Customer ?? await dbContext.Customers
            .FirstOrDefaultAsync(c => c.Id == payment.CustomerId, cancellationToken);

        if (customer == null)
        {
            payment.SecurityDepositStatus = "failed";
            payment.FailureReason = "Customer record missing for security deposit authorization.";
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        if (string.IsNullOrWhiteSpace(customer.StripeCustomerId))
        {
            try
            {
                customer = await stripeService.CreateCustomerAsync(customer);
                dbContext.Customers.Update(customer);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                payment.SecurityDepositStatus = "failed";
                payment.FailureReason = $"Unable to create Stripe customer: {ex.Message}";
                await dbContext.SaveChangesAsync(cancellationToken);

                _logger.LogError(ex,
                    "[SecurityDeposit] Failed to create Stripe customer for booking {BookingId} (payment {PaymentId}).",
                    payment.ReservationId,
                    payment.Id);
                return;
            }
        }

        payment.SecurityDepositStatus = "processing";
        payment.FailureReason = null;
        await dbContext.SaveChangesAsync(cancellationToken);

        var metadata = new Dictionary<string, string>
        {
            { "payment_type", "security_deposit" },
            { "booking_id", payment.ReservationId?.ToString() ?? string.Empty },
            { "booking_number", payment.Reservation.BookingNumber },
            { "company_id", payment.CompanyId.ToString() },
            { "customer_id", payment.CustomerId.ToString() },
            { "pickup_date", payment.Reservation.PickupDate.ToString("O") }
        };

        try
        {
            var intent = await stripeService.CreatePaymentIntentAsync(
                securityDepositAmount.Value,
                payment.Currency,
                customer.StripeCustomerId ?? string.Empty,
                payment.StripePaymentMethodId,
                metadata,
                captureImmediately: false,
                requestExtendedAuthorization: true,
                companyId: payment.CompanyId);

            var confirmedIntent = await stripeService.ConfirmPaymentIntentAsync(intent.Id, payment.CompanyId);

            if (confirmedIntent.Status is "requires_capture" or "succeeded")
            {
                payment.SecurityDepositStatus = confirmedIntent.Status == "succeeded" ? "captured" : "authorized";
                payment.SecurityDepositPaymentIntentId = confirmedIntent.Id;
                payment.SecurityDepositChargeId = confirmedIntent.LatestChargeId;
                payment.SecurityDepositAuthorizedAt = DateTime.UtcNow;
                payment.FailureReason = null;

                _logger.LogInformation(
                    "[SecurityDeposit] Security deposit authorized for booking {BookingId} (payment {PaymentId}) with status {Status}.",
                    payment.ReservationId,
                    payment.Id,
                    confirmedIntent.Status);
            }
            else
            {
                payment.SecurityDepositStatus = "failed";
                payment.FailureReason = $"Security deposit intent returned status '{confirmedIntent.Status}'.";

                _logger.LogWarning(
                    "[SecurityDeposit] Security deposit intent returned unexpected status {Status} for booking {BookingId} (payment {PaymentId}).",
                    confirmedIntent.Status,
                    payment.ReservationId,
                    payment.Id);
            }
        }
        catch (StripeException ex)
        {
            payment.SecurityDepositStatus = "failed";
            payment.FailureReason = ex.Message;

            _logger.LogError(ex,
                "[SecurityDeposit] Stripe error while authorizing security deposit for booking {BookingId} (payment {PaymentId}).",
                payment.ReservationId,
                payment.Id);
        }
        catch (Exception ex)
        {
            payment.SecurityDepositStatus = "failed";
            payment.FailureReason = ex.Message;

            _logger.LogError(ex,
                "[SecurityDeposit] Unexpected error while authorizing security deposit for booking {BookingId} (payment {PaymentId}).",
                payment.ReservationId,
                payment.Id);
        }
        finally
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}

