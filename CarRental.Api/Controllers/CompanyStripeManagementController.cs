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
[Route("api/companies/{companyId}/stripe")]
[Authorize(Roles = "admin,mainadmin")]
public class CompanyStripeManagementController : ControllerBase
{
    private readonly IStripeConnectService _stripeConnectService;
    private readonly ILogger<CompanyStripeManagementController> _logger;

    public CompanyStripeManagementController(
        IStripeConnectService stripeConnectService,
        ILogger<CompanyStripeManagementController> logger)
    {
        _stripeConnectService = stripeConnectService;
        _logger = logger;
    }

    /// <summary>
    /// Setup Stripe account for a company
    /// </summary>
    [HttpPost("setup")]
    public async Task<ActionResult> SetupStripeAccount(Guid companyId)
    {
        try
        {
            // 1. Create connected account
            var (success, accountId, error) = await _stripeConnectService.CreateConnectedAccountAsync(companyId);

            if (!success)
            {
                return BadRequest(new { error });
            }

            // 2. Generate onboarding link
            var returnUrl = $"{Request.Scheme}://{Request.Host}/admin/companies/{companyId}/stripe/complete";
            var refreshUrl = $"{Request.Scheme}://{Request.Host}/admin/companies/{companyId}/stripe/reauth";

            var (linkSuccess, onboardingUrl, linkError) = await _stripeConnectService.StartOnboardingAsync(
                companyId,
                returnUrl,
                refreshUrl
            );

            if (!linkSuccess)
            {
                return BadRequest(new { error = linkError });
            }

            return Ok(new
            {
                accountId,
                onboardingUrl,
                message = "Stripe account created. Complete onboarding at the provided URL."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting up Stripe account for company {CompanyId}", companyId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get Stripe account status for a company
    /// </summary>
    [HttpGet("status")]
    public async Task<ActionResult> GetStatus(Guid companyId)
    {
        try
        {
            var status = await _stripeConnectService.GetAccountStatusAsync(companyId);
            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Stripe status for company {CompanyId}", companyId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Suspend company's Stripe account
    /// </summary>
    [HttpPost("suspend")]
    public async Task<ActionResult> Suspend(Guid companyId, [FromBody] SuspendAccountDto dto)
    {
        try
        {
            var status = await _stripeConnectService.GetAccountStatusAsync(companyId);
            
            if (string.IsNullOrEmpty(status.StripeAccountId))
            {
                return BadRequest(new { error = "Company does not have a Stripe account" });
            }

            var (success, error) = await _stripeConnectService.SuspendAccountAsync(
                status.StripeAccountId,
                dto.Reason
            );

            if (!success)
            {
                return BadRequest(new { error });
            }

            return Ok(new { message = "Stripe account suspended successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error suspending Stripe account for company {CompanyId}", companyId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Reactivate company's Stripe account
    /// </summary>
    [HttpPost("reactivate")]
    public async Task<ActionResult> Reactivate(Guid companyId)
    {
        try
        {
            var status = await _stripeConnectService.GetAccountStatusAsync(companyId);
            
            if (string.IsNullOrEmpty(status.StripeAccountId))
            {
                return BadRequest(new { error = "Company does not have a Stripe account" });
            }

            var (success, error) = await _stripeConnectService.ReactivateAccountAsync(status.StripeAccountId);

            if (!success)
            {
                return BadRequest(new { error });
            }

            return Ok(new { message = "Stripe account reactivated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reactivating Stripe account for company {CompanyId}", companyId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Delete company's Stripe account
    /// </summary>
    [HttpDelete]
    public async Task<ActionResult> Delete(Guid companyId)
    {
        try
        {
            var status = await _stripeConnectService.GetAccountStatusAsync(companyId);
            
            if (string.IsNullOrEmpty(status.StripeAccountId))
            {
                return BadRequest(new { error = "Company does not have a Stripe account" });
            }

            var (success, error) = await _stripeConnectService.DeleteAccountAsync(status.StripeAccountId);

            if (!success)
            {
                return BadRequest(new { error });
            }

            return Ok(new { message = "Stripe account deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting Stripe account for company {CompanyId}", companyId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
}

