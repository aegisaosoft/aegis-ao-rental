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
using CarRental.Api.Data;
using CarRental.Api.DTOs.Stripe;
using CarRental.Api.Services;

namespace CarRental.Api.Controllers;

[ApiController]
[Route("api/bookings/{bookingId}/[controller]")]
public class BookingPaymentsController : ControllerBase
{
    private readonly CarRentalDbContext _context;
    private readonly IStripeConnectService _stripeConnectService;
    private readonly ILogger<BookingPaymentsController> _logger;

    public BookingPaymentsController(
        CarRentalDbContext context,
        IStripeConnectService stripeConnectService,
        ILogger<BookingPaymentsController> logger)
    {
        _context = context;
        _stripeConnectService = stripeConnectService;
        _logger = logger;
    }

    /// <summary>
    /// Transfer booking funds to connected account
    /// </summary>
    [HttpPost("transfer")]
    public async Task<ActionResult<object>> TransferFunds(Guid bookingId)
    {
        var booking = await _context.Bookings
            .Include(b => b.Company)
            .FirstOrDefaultAsync(b => b.Id == bookingId);

        if (booking == null)
            return NotFound("Booking not found");

        if (booking.Status != Models.BookingStatus.Confirmed && 
            booking.Status != Models.BookingStatus.PickedUp)
            return BadRequest("Can only transfer funds for confirmed or picked up bookings");

        var (success, transferId, error) = await _stripeConnectService.TransferBookingFundsAsync(bookingId);

        if (!success)
            return BadRequest(new { error });

        var transfer = await _context.StripeTransfers.FindAsync(transferId);

        return Ok(new
        {
            success = true,
            transferId = transfer?.StripeTransferId,
            amount = transfer?.Amount,
            platformFee = transfer?.PlatformFee,
            netAmount = transfer?.NetAmount,
            status = transfer?.Status,
            message = "Funds transferred successfully"
        });
    }

    /// <summary>
    /// Authorize security deposit for booking
    /// </summary>
    [HttpPost("security-deposit/authorize")]
    public async Task<ActionResult<object>> AuthorizeSecurityDeposit(
        Guid bookingId,
        [FromBody] SecurityDepositAuthorizationDto dto)
    {
        if (dto.Amount <= 0)
            return BadRequest("Amount must be greater than zero");

        if (string.IsNullOrEmpty(dto.PaymentMethodId))
            return BadRequest("PaymentMethodId is required");

        var (success, paymentIntentId, error) = await _stripeConnectService.AuthorizeSecurityDepositAsync(
            bookingId,
            dto.PaymentMethodId,
            dto.Amount
        );

        if (!success)
            return BadRequest(new { error });

        return Ok(new
        {
            success = true,
            paymentIntentId = paymentIntentId,
            amount = dto.Amount,
            status = "authorized",
            message = "Security deposit authorized successfully"
        });
    }

    /// <summary>
    /// Get transfer status for booking
    /// </summary>
    [HttpGet("transfer/status")]
    public async Task<ActionResult<object>> GetTransferStatus(Guid bookingId)
    {
        var transfer = await _context.StripeTransfers
            .Where(t => t.BookingId == bookingId)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync();

        if (transfer == null)
            return NotFound("No transfer found for this booking");

        return Ok(new
        {
            transferId = transfer.StripeTransferId,
            amount = transfer.Amount,
            platformFee = transfer.PlatformFee,
            netAmount = transfer.NetAmount,
            currency = transfer.Currency,
            status = transfer.Status,
            transferredAt = transfer.TransferredAt,
            createdAt = transfer.CreatedAt
        });
    }
}

