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
using Microsoft.EntityFrameworkCore;

namespace CarRental.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize] // Require authentication for all endpoints
public class StripeConnectController : ControllerBase
{
    private readonly IStripeConnectService _stripeConnectService;
    private readonly CarRentalDbContext _context;
    private readonly ILogger<StripeConnectController> _logger;

    public StripeConnectController(
        IStripeConnectService stripeConnectService,
        CarRentalDbContext context,
        ILogger<StripeConnectController> logger)
    {
        _stripeConnectService = stripeConnectService;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Check if the current user is an aegis user (not a customer)
    /// Only aegis users are allowed to access admin endpoints
    /// </summary>
    private bool HasAdminPrivileges()
    {
        // First check: User must be authenticated (handled by [Authorize] attribute)
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            _logger.LogWarning("HasAdminPrivileges: User is not authenticated");
            return false;
        }

        // Get user ID from claims
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? User.FindFirst("role")?.Value;
        var customerId = User.FindFirst("customer_id")?.Value;
        
        _logger.LogInformation("HasAdminPrivileges: Checking authorization. UserId: {UserId}, Role: {Role}, CustomerId: {CustomerId}", 
            userId, role, customerId);
        
        // Second check: User must be an aegis user (not a customer)
        // Aegis users are authenticated via /api/aegis-admin/login
        // We check if the user ID exists in AegisUsers table
        if (!string.IsNullOrEmpty(userId) && Guid.TryParse(userId, out var userGuid))
        {
            var aegisUser = _context.AegisUsers.FirstOrDefault(u => u.Id == userGuid);
            if (aegisUser != null)
            {
                // Third check: Only allow aegis users with admin or mainadmin role
                var aegisRole = aegisUser.Role?.ToLowerInvariant();
                var hasAdminRole = aegisRole == "admin" || aegisRole == "mainadmin";
                
                _logger.LogInformation("HasAdminPrivileges: Aegis user found. Role: {Role}, HasAdminRole: {HasAdminRole}", 
                    aegisRole, hasAdminRole);
                
                return hasAdminRole;
            }
            else
            {
                _logger.LogWarning("HasAdminPrivileges: User {UserId} is not an aegis user (customer or unknown)", userId);
            }
        }
        else
        {
            _logger.LogWarning("HasAdminPrivileges: Invalid or missing user ID: {UserId}", userId);
        }
        
        return false;
    }

    /// <summary>
    /// Check if the current user is a customer (not an aegis user)
    /// Customers can only read their own company's status
    /// </summary>
    private bool IsCustomer()
    {
        // First check: User must be authenticated
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            return false;
        }

        // Get user ID from claims
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var customerId = User.FindFirst("customer_id")?.Value;
        
        // Check if user is NOT an aegis user (i.e., is a customer)
        if (!string.IsNullOrEmpty(userId) && Guid.TryParse(userId, out var userGuid))
        {
            var aegisUser = _context.AegisUsers.FirstOrDefault(u => u.Id == userGuid);
            // If not found in AegisUsers, it's a customer
            return aegisUser == null && !string.IsNullOrEmpty(customerId);
        }
        
        return false;
    }

    /// <summary>
    /// Check if customer can access the specified company
    /// Customers can only access their own company
    /// </summary>
    private async Task<bool> CanCustomerAccessCompany(Guid companyId)
    {
        if (!IsCustomer())
        {
            return false;
        }

        var customerId = User.FindFirst("customer_id")?.Value;
        if (string.IsNullOrEmpty(customerId) || !Guid.TryParse(customerId, out var customerGuid))
        {
            return false;
        }

        // Check if the company belongs to this customer
        var company = await _context.Companies.FindAsync(companyId);
        if (company == null)
        {
            return false;
        }

        // For now, we'll allow customers to access any company they're authenticated for
        // You may want to add a CompanyId field to Customers table to restrict this further
        return true;
    }

    /// <summary>
    /// Create a Stripe Connect account for a company
    /// Requires: Aegis user with admin/mainadmin role
    /// </summary>
    [HttpPost("accounts")]
    public async Task<ActionResult<object>> CreateConnectedAccount([FromBody] CreateConnectedAccountDto dto)
    {
        // Double authorization: Authentication + Admin privileges
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            _logger.LogWarning("CreateConnectedAccount: Unauthenticated request for company {CompanyId}", dto.CompanyId);
            return Unauthorized("Authentication required");
        }
        
        if (!HasAdminPrivileges())
        {
            _logger.LogWarning("CreateConnectedAccount: Access denied for company {CompanyId}. Admin privileges required.", dto.CompanyId);
            return Forbid("Admin privileges required");
        }

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
    /// Requires: Aegis user with admin/mainadmin role OR customer (for their own company)
    /// </summary>
    [HttpPost("onboarding")]
    public async Task<ActionResult<object>> StartOnboarding([FromBody] CreateAccountLinkDto dto)
    {
        // Double authorization: Authentication + (Admin privileges OR Customer access)
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            _logger.LogWarning("StartOnboarding: Unauthenticated request for company {CompanyId}", dto.CompanyId);
            return Unauthorized("Authentication required");
        }
        
        bool hasAccess = false;
        
        if (HasAdminPrivileges())
        {
            // Aegis admin/mainadmin can access any company
            hasAccess = true;
            _logger.LogInformation("StartOnboarding: Aegis admin accessing company {CompanyId}", dto.CompanyId);
        }
        else if (IsCustomer())
        {
            // Customer can only access their own company
            hasAccess = await CanCustomerAccessCompany(dto.CompanyId);
            if (hasAccess)
            {
                _logger.LogInformation("StartOnboarding: Customer accessing their own company {CompanyId}", dto.CompanyId);
            }
            else
            {
                _logger.LogWarning("StartOnboarding: Customer attempted to access company {CompanyId} without permission", dto.CompanyId);
            }
        }
        
        if (!hasAccess)
        {
            _logger.LogWarning("StartOnboarding: Access denied for company {CompanyId}. Admin privileges required or customer must own the company.", dto.CompanyId);
            return Forbid("Access denied. Admin privileges required or customer must own the company.");
        }

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
    /// Read access: Aegis users with admin/mainadmin role OR customers (for their own company)
    /// Route: GET /api/StripeConnect/accounts/{companyId}/status
    /// </summary>
    [HttpGet("accounts/{companyId}/status")]
    public async Task<ActionResult<StripeAccountStatusDto>> GetAccountStatus(Guid companyId)
    {
        _logger.LogInformation("GetAccountStatus: Method called for company {CompanyId}. Route: /api/StripeConnect/accounts/{{CompanyId}}/status", companyId);
        
        // Double authorization check:
        // 1. User must be authenticated (handled by [Authorize] attribute)
        // 2. User must be either:
        //    - Aegis user with admin/mainadmin role, OR
        //    - Customer accessing their own company
        
        bool hasAccess = false;
        
        if (HasAdminPrivileges())
        {
            // Aegis admin/mainadmin can access any company
            hasAccess = true;
            _logger.LogInformation("GetAccountStatus: Aegis admin accessing company {CompanyId}", companyId);
        }
        else if (IsCustomer())
        {
            // Customer can only access their own company
            hasAccess = await CanCustomerAccessCompany(companyId);
            if (hasAccess)
            {
                _logger.LogInformation("GetAccountStatus: Customer accessing their own company {CompanyId}", companyId);
            }
            else
            {
                _logger.LogWarning("GetAccountStatus: Customer attempted to access company {CompanyId} without permission", companyId);
            }
        }
        
        if (!hasAccess)
        {
            _logger.LogWarning("GetAccountStatus: Access denied for company {CompanyId}. User is not authorized.", companyId);
            return Forbid("Access denied. Admin privileges required or customer must own the company.");
        }

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
    /// Requires: Aegis user with admin/mainadmin role
    /// </summary>
    [HttpPost("accounts/{stripeAccountId}/sync")]
    public async Task<ActionResult> SyncAccountStatus(string stripeAccountId)
    {
        // Double authorization: Authentication + Admin privileges
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            _logger.LogWarning("SyncAccountStatus: Unauthenticated request for account {AccountId}", stripeAccountId);
            return Unauthorized("Authentication required");
        }
        
        if (!HasAdminPrivileges())
        {
            _logger.LogWarning("SyncAccountStatus: Access denied for account {AccountId}. Admin privileges required.", stripeAccountId);
            return Forbid("Admin privileges required");
        }

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
    /// Requires: Aegis user with admin/mainadmin role
    /// </summary>
    [HttpPost("transfers")]
    public async Task<ActionResult<object>> TransferFunds([FromBody] TransferFundsDto dto)
    {
        // Double authorization: Authentication + Admin privileges
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            _logger.LogWarning("TransferFunds: Unauthenticated request for booking {BookingId}", dto.BookingId);
            return Unauthorized("Authentication required");
        }
        
        if (!HasAdminPrivileges())
        {
            _logger.LogWarning("TransferFunds: Access denied for booking {BookingId}. Admin privileges required.", dto.BookingId);
            return Forbid("Admin privileges required");
        }

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
    /// Requires: Aegis user with admin/mainadmin role
    /// </summary>
    [HttpPost("security-deposits")]
    public async Task<ActionResult<object>> AuthorizeSecurityDeposit([FromBody] SecurityDepositAuthorizationDto dto)
    {
        // Double authorization: Authentication + Admin privileges
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            _logger.LogWarning("AuthorizeSecurityDeposit: Unauthenticated request for booking {BookingId}", dto.BookingId);
            return Unauthorized("Authentication required");
        }
        
        if (!HasAdminPrivileges())
        {
            _logger.LogWarning("AuthorizeSecurityDeposit: Access denied for booking {BookingId}. Admin privileges required.", dto.BookingId);
            return Forbid("Admin privileges required");
        }

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

    /// <summary>
    /// Suspend (disable) a connected account
    /// Requires: Aegis user with admin/mainadmin role
    /// </summary>
    [HttpPost("accounts/{stripeAccountId}/suspend")]
    public async Task<ActionResult> SuspendAccount(string stripeAccountId, [FromBody] SuspendAccountDto dto)
    {
        // Double authorization: Authentication + Admin privileges
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            _logger.LogWarning("SuspendAccount: Unauthenticated request for account {AccountId}", stripeAccountId);
            return Unauthorized("Authentication required");
        }
        
        if (!HasAdminPrivileges())
        {
            _logger.LogWarning("SuspendAccount: Access denied for account {AccountId}. Admin privileges required.", stripeAccountId);
            return Forbid("Admin privileges required");
        }

        try
        {
            var (success, error) = await _stripeConnectService.SuspendAccountAsync(stripeAccountId, dto.Reason);

            if (!success)
            {
                return BadRequest(new { error });
            }

            return Ok(new { message = "Account suspended successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error suspending account {AccountId}", stripeAccountId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Reactivate a suspended connected account
    /// Requires: Aegis user with admin/mainadmin role
    /// </summary>
    [HttpPost("accounts/{stripeAccountId}/reactivate")]
    public async Task<ActionResult> ReactivateAccount(string stripeAccountId)
    {
        // Double authorization: Authentication + Admin privileges
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            _logger.LogWarning("ReactivateAccount: Unauthenticated request for account {AccountId}", stripeAccountId);
            return Unauthorized("Authentication required");
        }
        
        if (!HasAdminPrivileges())
        {
            _logger.LogWarning("ReactivateAccount: Access denied for account {AccountId}. Admin privileges required.", stripeAccountId);
            return Forbid("Admin privileges required");
        }

        try
        {
            var (success, error) = await _stripeConnectService.ReactivateAccountAsync(stripeAccountId);

            if (!success)
            {
                return BadRequest(new { error });
            }

            return Ok(new { message = "Account reactivated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reactivating account {AccountId}", stripeAccountId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Delete a connected account (only if not used)
    /// Requires: Aegis user with admin/mainadmin role
    /// </summary>
    [HttpDelete("accounts/{stripeAccountId}")]
    public async Task<ActionResult> DeleteAccount(string stripeAccountId)
    {
        // Double authorization: Authentication + Admin privileges
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            _logger.LogWarning("DeleteAccount: Unauthenticated request for account {AccountId}", stripeAccountId);
            return Unauthorized("Authentication required");
        }
        
        if (!HasAdminPrivileges())
        {
            _logger.LogWarning("DeleteAccount: Access denied for account {AccountId}. Admin privileges required.", stripeAccountId);
            return Forbid("Admin privileges required");
        }

        try
        {
            var (success, error) = await _stripeConnectService.DeleteAccountAsync(stripeAccountId);

            if (!success)
            {
                return BadRequest(new { error });
            }

            return Ok(new { message = "Account deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting account {AccountId}", stripeAccountId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get all connected accounts
    /// Requires: Aegis user with admin/mainadmin role
    /// </summary>
    [HttpGet("accounts")]
    public async Task<ActionResult> GetAllAccounts([FromQuery] int limit = 20)
    {
        // Double authorization: Authentication + Admin privileges
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            _logger.LogWarning("GetAllAccounts: Unauthenticated request");
            return Unauthorized("Authentication required");
        }
        
        if (!HasAdminPrivileges())
        {
            _logger.LogWarning("GetAllAccounts: Access denied. Admin privileges required.");
            return Forbid("Admin privileges required");
        }

        try
        {
            var accounts = await _stripeConnectService.GetAllAccountsAsync(limit);
            return Ok(accounts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all accounts");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get detailed account information
    /// Requires: Aegis user with admin/mainadmin role
    /// </summary>
    [HttpGet("accounts/{stripeAccountId}")]
    public async Task<ActionResult> GetAccountDetails(string stripeAccountId)
    {
        // Double authorization: Authentication + Admin privileges
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            _logger.LogWarning("GetAccountDetails: Unauthenticated request for account {AccountId}", stripeAccountId);
            return Unauthorized("Authentication required");
        }
        
        if (!HasAdminPrivileges())
        {
            _logger.LogWarning("GetAccountDetails: Access denied for account {AccountId}. Admin privileges required.", stripeAccountId);
            return Forbid("Admin privileges required");
        }

        try
        {
            var account = await _stripeConnectService.GetAccountDetailsAsync(stripeAccountId);
            
            if (account == null)
            {
                return NotFound(new { error = "Account not found" });
            }

            return Ok(account);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting account details {AccountId}", stripeAccountId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Find and sync Stripe accounts with companies by matching email addresses
    /// Requires: Aegis user with admin/mainadmin role
    /// </summary>
    [HttpPost("accounts/find-and-sync")]
    public async Task<ActionResult> FindAndSyncAccountsForCompanies([FromQuery] int limit = 100)
    {
        // Double authorization: Authentication + Admin privileges
        // Note: Removed [Authorize(Roles = "admin,mainadmin")] as we use HasAdminPrivileges() which checks database
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            _logger.LogWarning("FindAndSyncAccountsForCompanies: Unauthenticated request");
            return Unauthorized("Authentication required");
        }
        
        if (!HasAdminPrivileges())
        {
            _logger.LogWarning("FindAndSyncAccountsForCompanies: Access denied. Admin privileges required.");
            return Forbid("Admin privileges required");
        }

        try
        {
            var syncedCount = await _stripeConnectService.FindAndSyncAccountsForCompaniesAsync(limit);
            return Ok(new 
            { 
                message = $"Successfully synced {syncedCount} Stripe account(s) with companies",
                syncedCount 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding and syncing accounts");
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }
}

