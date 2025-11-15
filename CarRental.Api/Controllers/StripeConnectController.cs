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
using Microsoft.AspNetCore.Authorization;
using CarRental.Api.Services;
using CarRental.Api.DTOs.Stripe;

namespace CarRental.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class StripeConnectController : ControllerBase
{
    private readonly IStripeConnectService _stripeConnectService;
    private readonly ILogger<StripeConnectController> _logger;

    public StripeConnectController(
        IStripeConnectService stripeConnectService,
        ILogger<StripeConnectController> logger)
    {
        _stripeConnectService = stripeConnectService;
        _logger = logger;
    }

    /// <summary>
    /// Create a Stripe Connect account for a company
    /// </summary>
    [HttpPost("accounts")]
    public async Task<ActionResult<object>> CreateConnectedAccount([FromBody] CreateConnectedAccountDto dto)
    {
        try
        {
            var (success, accountId, error) = await _stripeConnectService.CreateConnectedAccountAsync(dto.CompanyId);

            if (!success)
            {
                return BadRequest(new { error });
            }

            return Ok(new
            {
                accountId,
                message = "Connected account created successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating connected account for company {CompanyId}", dto.CompanyId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Start onboarding process for a company
    /// </summary>
    [HttpPost("onboarding")]
    public async Task<ActionResult<object>> StartOnboarding([FromBody] CreateAccountLinkDto dto)
    {
        try
        {
            var (success, onboardingUrl, error) = await _stripeConnectService.StartOnboardingAsync(
                dto.CompanyId,
                dto.ReturnUrl,
                dto.RefreshUrl
            );

            if (!success)
            {
                return BadRequest(new { error });
            }

            return Ok(new
            {
                onboardingUrl,
                message = "Onboarding link created successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating onboarding link for company {CompanyId}", dto.CompanyId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get account status for a company
    /// </summary>
    [HttpGet("accounts/{companyId}/status")]
    public async Task<ActionResult<StripeAccountStatusDto>> GetAccountStatus(Guid companyId)
    {
        try
        {
            var status = await _stripeConnectService.GetAccountStatusAsync(companyId);
            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting account status for company {CompanyId}", companyId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Sync account status from Stripe
    /// </summary>
    [HttpPost("accounts/{stripeAccountId}/sync")]
    public async Task<ActionResult> SyncAccountStatus(string stripeAccountId)
    {
        try
        {
            await _stripeConnectService.SyncAccountStatusAsync(stripeAccountId);
            return Ok(new { message = "Account status synced successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing account status for {AccountId}", stripeAccountId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Transfer funds from a booking to the company's connected account
    /// </summary>
    [HttpPost("transfers")]
    public async Task<ActionResult<object>> TransferFunds([FromBody] TransferFundsDto dto)
    {
        try
        {
            var (success, transferId, error) = await _stripeConnectService.TransferBookingFundsAsync(dto.BookingId);

            if (!success)
            {
                return BadRequest(new { error });
            }

            return Ok(new
            {
                transferId,
                message = "Funds transferred successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error transferring funds for booking {BookingId}", dto.BookingId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Authorize security deposit on connected account
    /// </summary>
    [HttpPost("security-deposits")]
    public async Task<ActionResult<object>> AuthorizeSecurityDeposit([FromBody] SecurityDepositAuthorizationDto dto)
    {
        try
        {
            var (success, paymentIntentId, error) = await _stripeConnectService.AuthorizeSecurityDepositAsync(
                dto.BookingId,
                dto.PaymentMethodId,
                dto.Amount
            );

            if (!success)
            {
                return BadRequest(new { error });
            }

            return Ok(new
            {
                paymentIntentId,
                message = "Security deposit authorized successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error authorizing security deposit for booking {BookingId}", dto.BookingId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
}

