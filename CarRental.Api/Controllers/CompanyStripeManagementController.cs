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
using CarRental.Api.Data;
using CarRental.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Stripe;

namespace CarRental.Api.Controllers;

[ApiController]
[Route("api/companies/{companyId}/stripe")]
[Authorize] // Require authentication for all endpoints
[Tags("Company Stripe Management")]
public class CompanyStripeManagementController : ControllerBase
{
    private readonly IStripeConnectService _stripeConnectService;
    private readonly IStripeService _stripeService;
    private readonly IEncryptionService _encryptionService;
    private readonly CarRentalDbContext _context;
    private readonly ILogger<CompanyStripeManagementController> _logger;
    private readonly IConfiguration _configuration;

    public CompanyStripeManagementController(
        IStripeConnectService stripeConnectService,
        IStripeService stripeService,
        IEncryptionService encryptionService,
        CarRentalDbContext context,
        ILogger<CompanyStripeManagementController> logger,
        IConfiguration configuration)
    {
        _stripeConnectService = stripeConnectService;
        _stripeService = stripeService;
        _encryptionService = encryptionService;
        _context = context;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Check if the current user is an aegis user (not a customer)
    /// Only aegis users are allowed to access admin endpoints
    /// </summary>
    private bool HasAdminPrivileges()
    {
        // Get user ID from claims
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? User.FindFirst("role")?.Value;
        
        // Check if user is an aegis user FIRST (regardless of customer_id claim)
        // Aegis users are authenticated via /api/aegis-admin/login
        // We check if the user ID exists in AegisUsers table
        if (!string.IsNullOrEmpty(userId) && Guid.TryParse(userId, out var userGuid))
        {
            var aegisUser = _context.AegisUsers.FirstOrDefault(u => u.Id == userGuid);
            if (aegisUser != null)
            {
                // Only allow aegis users with admin or mainadmin role
                var aegisRole = aegisUser.Role?.ToLowerInvariant();
                return aegisRole == "admin" || aegisRole == "mainadmin";
            }
        }
        return false;
    }

    /// <summary>
    /// Get the frontend URL for redirects (e.g., Stripe onboarding return URLs)
    /// </summary>
    private string GetFrontendUrl(Guid companyId)
    {
        var host = HttpContext.Request.Host.Host;
        var scheme = HttpContext.Request.Scheme;

        // Development - use localhost:4000 for frontend
        if (host.Contains("localhost") || host == "127.0.0.1")
        {
            return "http://localhost:4000"; // Frontend runs on port 4000
        }

        // Production - try to get company subdomain
        var company = _context.Companies.Find(companyId);
        if (company != null && !string.IsNullOrWhiteSpace(company.Subdomain))
        {
            var frontendUrl = $"https://{company.Subdomain.ToLower()}.aegis-rental.com";
            _logger.LogInformation("Using company subdomain for frontend URL: {FrontendUrl} (company: {CompanyName})", frontendUrl, company.CompanyName);
            return frontendUrl;
        }

        // Fallback: Check configuration
        var configuredFrontendUrl = _configuration["FrontendUrl"] 
            ?? _configuration["FRONTEND_URL"]
            ?? Environment.GetEnvironmentVariable("FRONTEND_URL")
            ?? Environment.GetEnvironmentVariable("FrontendUrl");

        if (!string.IsNullOrWhiteSpace(configuredFrontendUrl))
        {
            _logger.LogInformation("Using configured frontend URL: {FrontendUrl}", configuredFrontendUrl);
            return configuredFrontendUrl.TrimEnd('/');
        }

        // Last resort: Use request host (but log warning)
        _logger.LogWarning("No frontend URL configured. Using request host: {Host}. This may be incorrect for Stripe redirects.", host);
        return $"{scheme}://{HttpContext.Request.Host}";
    }

    /// <summary>
    /// Setup Stripe account for a company
    /// </summary>
    [HttpPost("setup")]
    public async Task<ActionResult> SetupStripeAccount(Guid companyId)
    {
        if (!HasAdminPrivileges())
        {
            return Forbid("Admin privileges required");
        }

        try
        {
            _logger.LogInformation("Setting up Stripe account for company {CompanyId}", companyId);

            // 1. Create connected account
            var (success, accountId, error) = await _stripeConnectService.CreateConnectedAccountAsync(companyId);

            if (!success)
            {
                _logger.LogError("Failed to create Stripe account for company {CompanyId}: {Error}", companyId, error);
                return BadRequest(new { error = error ?? "Failed to create Stripe account" });
            }

            _logger.LogInformation("Stripe account created successfully for company {CompanyId}: {AccountId}", companyId, accountId);

            // 2. Generate onboarding link - use frontend URL, not backend URL
            var frontendUrl = GetFrontendUrl(companyId);
            var returnUrl = $"{frontendUrl}/companies/{companyId}/stripe/complete";
            var refreshUrl = $"{frontendUrl}/companies/{companyId}/stripe/reauth";

            _logger.LogInformation("Creating onboarding link for company {CompanyId}. ReturnUrl: {ReturnUrl}, RefreshUrl: {RefreshUrl}", 
                companyId, returnUrl, refreshUrl);

            var (linkSuccess, onboardingUrl, linkError) = await _stripeConnectService.StartOnboardingAsync(
                companyId,
                returnUrl,
                refreshUrl
            );

            if (!linkSuccess)
            {
                _logger.LogError("Failed to create onboarding link for company {CompanyId}: {Error}", companyId, linkError);
                return BadRequest(new { error = linkError ?? "Failed to create onboarding link" });
            }

            _logger.LogInformation("Stripe setup completed successfully for company {CompanyId}", companyId);

            return Ok(new
            {
                accountId,
                onboardingUrl,
                message = "Stripe account created. Complete onboarding at the provided URL."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting up Stripe account for company {CompanyId}. Exception: {ExceptionMessage}, StackTrace: {StackTrace}", 
                companyId, ex.Message, ex.StackTrace);
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }

    /// <summary>
    /// Get Stripe account status for a company (from database cache)
    /// </summary>
    [HttpGet("status")]
    public async Task<ActionResult> GetStatus(Guid companyId)
    {
        if (!HasAdminPrivileges())
        {
            return Forbid("Admin privileges required");
        }

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
    /// Get real-time Stripe account status directly from Stripe API
    /// </summary>
    [HttpGet("status/live")]
    public async Task<IActionResult> GetStripeAccountStatus(Guid companyId)
    {
        if (!HasAdminPrivileges())
        {
            return Forbid("Admin privileges required");
        }

        try
        {
            var company = await _context.Companies.FindAsync(companyId);
            if (company == null)
            {
                return NotFound(new { error = "Company not found" });
            }

            if (company.StripeSettingsId == null)
            {
                return Ok(new { status = "not_created" });
            }

            var stripeCompany = await _context.StripeCompanies
                .FirstOrDefaultAsync(sc => sc.CompanyId == companyId && sc.SettingsId == company.StripeSettingsId.Value);

            if (stripeCompany == null || string.IsNullOrEmpty(stripeCompany.StripeAccountId))
            {
                return Ok(new { status = "not_created" });
            }

            // Decrypt the Stripe account ID
            string stripeAccountId;
            try
            {
                stripeAccountId = _encryptionService.Decrypt(stripeCompany.StripeAccountId);
            }
            catch
            {
                // Account ID might not be encrypted, use as-is
                stripeAccountId = stripeCompany.StripeAccountId;
            }

            // Get account directly from Stripe API (pass companyId to use company-specific Stripe settings)
            var account = await _stripeService.GetAccountAsync(stripeAccountId, companyId);

            return Ok(new
            {
                accountId = account.Id,
                detailsSubmitted = account.DetailsSubmitted,
                chargesEnabled = account.ChargesEnabled,
                payoutsEnabled = account.PayoutsEnabled,
                email = account.Email,
                country = account.Country,
                type = account.Type
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Stripe account status for company {CompanyId}", companyId);
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }

    /// <summary>
    /// Sync/Fetch account status from Stripe API and update database
    /// </summary>
    [HttpPost("sync")]
    public async Task<ActionResult> SyncAccount(Guid companyId)
    {
        if (!HasAdminPrivileges())
        {
            return Forbid("Admin privileges required");
        }

        try
        {
            var status = await _stripeConnectService.GetAccountStatusAsync(companyId);
            
            if (string.IsNullOrEmpty(status.StripeAccountId))
            {
                return BadRequest(new { error = "Company does not have a Stripe account" });
            }

            await _stripeConnectService.SyncAccountStatusAsync(status.StripeAccountId, companyId);

            // Return updated status
            var updatedStatus = await _stripeConnectService.GetAccountStatusAsync(companyId);
            return Ok(new 
            { 
                message = "Account status synced successfully",
                status = updatedStatus
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing Stripe account for company {CompanyId}", companyId);
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }

    /// <summary>
    /// Suspend company's Stripe account
    /// </summary>
    [HttpPost("suspend")]
    public async Task<ActionResult> Suspend(Guid companyId, [FromBody] SuspendAccountDto dto)
    {
        if (!HasAdminPrivileges())
        {
            return Forbid("Admin privileges required");
        }

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
        if (!HasAdminPrivileges())
        {
            return Forbid("Admin privileges required");
        }

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
        if (!HasAdminPrivileges())
        {
            return Forbid("Admin privileges required");
        }

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

    /// <summary>
    /// Refresh Stripe onboarding link (called when user needs to re-authenticate)
    /// </summary>
    [HttpGet("reauth")]
    public async Task<IActionResult> RefreshOnboarding(Guid companyId)
    {
        if (!HasAdminPrivileges())
        {
            return Forbid("Admin privileges required");
        }

        try
        {
            var company = await _context.Companies.FindAsync(companyId);
            if (company == null)
            {
                return NotFound(new { error = "Company not found" });
            }

            if (company.StripeSettingsId == null)
            {
                return BadRequest(new { error = "Company does not have Stripe settings configured" });
            }

            var stripeCompany = await _context.StripeCompanies
                .FirstOrDefaultAsync(sc => sc.CompanyId == companyId && sc.SettingsId == company.StripeSettingsId.Value);

            if (stripeCompany == null || string.IsNullOrEmpty(stripeCompany.StripeAccountId))
            {
                return BadRequest(new { error = "No Stripe account found" });
            }

            // Decrypt the Stripe account ID
            string stripeAccountId;
            try
            {
                stripeAccountId = _encryptionService.Decrypt(stripeCompany.StripeAccountId);
            }
            catch
            {
                // Account ID might not be encrypted, use as-is
                stripeAccountId = stripeCompany.StripeAccountId;
            }

            // Create new onboarding link with same URLs - use frontend URL, not backend URL
            var frontendUrl = GetFrontendUrl(companyId);
            var returnUrl = $"{frontendUrl}/companies/{companyId}/stripe/complete";
            var refreshUrl = $"{frontendUrl}/companies/{companyId}/stripe/reauth";


            AccountLink accountLink;
            try
            {
                accountLink = await _stripeService.CreateAccountLinkAsync(
                    stripeAccountId,
                    returnUrl,
                    refreshUrl,
                    companyId
                );
            }
            catch (InvalidOperationException invalidOpEx)
            {
                // Account is disabled - return error with helpful message
                var errorMessage = invalidOpEx.Message;
                _logger.LogWarning("Cannot create account link for company {CompanyId}, account {AccountId}. Account is disabled: {Message}", 
                    companyId, stripeAccountId, errorMessage);
                
                return BadRequest(new { 
                    error = "Cannot create onboarding link", 
                    message = errorMessage,
                    accountStatus = "disabled",
                    suggestion = "The Stripe account is disabled. Please resolve the issues in the Stripe dashboard, or use 'Setup Stripe Account' to create a new account."
                });
            }
            catch (StripeException stripeEx)
            {
                _logger.LogError(stripeEx, "Stripe API error creating account link for company {CompanyId}, account {AccountId}. Stripe Error: {StripeError}, Message: {Message}", 
                    companyId, stripeAccountId, stripeEx.StripeError?.Code, stripeEx.Message);
                return StatusCode(500, new { 
                    error = "Failed to create Stripe onboarding link", 
                    message = stripeEx.Message,
                    stripeError = stripeEx.StripeError?.Code 
                });
            }

            // Update company with new link
            company.StripeOnboardingLink = accountLink.Url;
            company.StripeOnboardingLinkExpiresAt = DateTime.UtcNow.AddHours(1);
            company.UpdatedAt = DateTime.UtcNow;

            // Save onboarding session
            var session = new StripeOnboardingSession
            {
                CompanyId = companyId,
                AccountLinkUrl = accountLink.Url,
                ReturnUrl = returnUrl,
                RefreshUrl = refreshUrl,
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                Completed = false
            };

            _context.StripeOnboardingSessions.Add(session);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Refreshed onboarding link for company {CompanyId}, account {AccountId}", 
                companyId, stripeAccountId);

            // Check if client wants JSON response (for API calls) or redirect (for browser)
            if (Request.Headers.Accept.ToString().Contains("application/json") || 
                Request.Query.ContainsKey("json"))
            {
                return Ok(new { onboardingUrl = accountLink.Url, url = accountLink.Url });
            }

            // Redirect to the new onboarding URL (for browser requests)
            return Redirect(accountLink.Url);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing onboarding link for company {CompanyId}", companyId);
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }
}

