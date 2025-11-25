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
[Authorize] // Require authentication for most endpoints (status endpoint allows anonymous)
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
    /// Check if the current user has admin privileges
    /// Allows both Aegis users AND Customers with admin/mainadmin roles
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
        
        if (!string.IsNullOrEmpty(userId) && Guid.TryParse(userId, out var userGuid))
        {
            // Check 1: Aegis users with admin/mainadmin role
            var aegisUser = _context.AegisUsers.FirstOrDefault(u => u.Id == userGuid);
            if (aegisUser != null)
            {
                var aegisRole = aegisUser.Role?.ToLowerInvariant();
                var hasAdminRole = aegisRole == "admin" || aegisRole == "mainadmin";
                
                _logger.LogInformation("HasAdminPrivileges: Aegis user found. Role: {Role}, HasAdminRole: {HasAdminRole}", 
                    aegisRole, hasAdminRole);
                
                if (hasAdminRole)
                {
                    return true;
                }
            }
            
            // Check 2: Customers with admin/mainadmin role
            var customer = _context.Customers.FirstOrDefault(c => c.Id == userGuid);
            if (customer != null)
            {
                var customerRole = customer.Role?.ToLowerInvariant();
                var hasAdminRole = customerRole == "admin" || customerRole == "mainadmin";
                
                _logger.LogInformation("HasAdminPrivileges: Customer found. Role: {Role}, HasAdminRole: {HasAdminRole}", 
                    customerRole, hasAdminRole);
                
                if (hasAdminRole)
                {
                    return true;
                }
            }
            
            // Also check role from token claims (fallback)
            if (!string.IsNullOrEmpty(role))
            {
                var roleLower = role.ToLowerInvariant();
                var hasAdminRole = roleLower == "admin" || roleLower == "mainadmin";
                
                if (hasAdminRole)
                {
                    _logger.LogInformation("HasAdminPrivileges: Admin role found in token claims. Role: {Role}", role);
                    return true;
                }
            }
            
            _logger.LogWarning("HasAdminPrivileges: User {UserId} does not have admin privileges (not an aegis admin or customer admin)", userId);
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
    /// Validate that Stripe account ID comes from matching records across all three tables:
    /// 1. company.StripeSettingsId must match a stripe_settings record
    /// 2. stripe_company must exist with matching company_id and settings_id
    /// 3. All three IDs must be consistent
    /// </summary>
    private async Task<(bool isValid, string? errorMessage, StripeCompany? stripeCompany, StripeSettings? stripeSettings)> ValidateStripeAccountConsistencyAsync(Guid companyId)
    {
        // 1. Get company and verify it has StripeSettingsId
        var company = await _context.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == companyId);

        if (company == null)
        {
            return (false, "Company not found", null, null);
        }

        if (!company.StripeSettingsId.HasValue)
        {
            return (false, "Company does not have Stripe settings configured", null, null);
        }

        var companyStripeSettingsId = company.StripeSettingsId.Value;

        // 2. Verify stripe_settings exists and matches
        var stripeSettings = await _context.StripeSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(ss => ss.Id == companyStripeSettingsId);

        if (stripeSettings == null)
        {
            _logger.LogError("Validation failed: Company {CompanyId} has StripeSettingsId {StripeSettingsId} but stripe_settings record not found", 
                companyId, companyStripeSettingsId);
            return (false, $"Stripe settings record not found for StripeSettingsId {companyStripeSettingsId}", null, null);
        }

        // 3. Verify stripe_company exists with matching company_id and settings_id
        var stripeCompany = await _context.StripeCompanies
            .AsNoTracking()
            .FirstOrDefaultAsync(sc => sc.CompanyId == companyId && sc.SettingsId == companyStripeSettingsId);

        if (stripeCompany == null)
        {
            _logger.LogError("Validation failed: stripe_company record not found for CompanyId {CompanyId} and SettingsId {SettingsId}", 
                companyId, companyStripeSettingsId);
            return (false, $"Stripe company record not found for CompanyId {companyId} and SettingsId {companyStripeSettingsId}", null, stripeSettings);
        }

        // 4. Verify all three IDs match
        if (stripeCompany.CompanyId != companyId)
        {
            _logger.LogError("Validation failed: stripe_company.CompanyId {StripeCompanyId} does not match company.Id {CompanyId}", 
                stripeCompany.CompanyId, companyId);
            return (false, $"Stripe company CompanyId mismatch: expected {companyId}, found {stripeCompany.CompanyId}", stripeCompany, stripeSettings);
        }

        if (stripeCompany.SettingsId != companyStripeSettingsId)
        {
            _logger.LogError("Validation failed: stripe_company.SettingsId {StripeCompanySettingsId} does not match company.StripeSettingsId {CompanyStripeSettingsId}", 
                stripeCompany.SettingsId, companyStripeSettingsId);
            return (false, $"Stripe company SettingsId mismatch: expected {companyStripeSettingsId}, found {stripeCompany.SettingsId}", stripeCompany, stripeSettings);
        }

        if (stripeSettings.Id != companyStripeSettingsId)
        {
            _logger.LogError("Validation failed: stripe_settings.Id {StripeSettingsId} does not match company.StripeSettingsId {CompanyStripeSettingsId}", 
                stripeSettings.Id, companyStripeSettingsId);
            return (false, $"Stripe settings Id mismatch: expected {companyStripeSettingsId}, found {stripeSettings.Id}", stripeCompany, stripeSettings);
        }

        // All validations passed
        _logger.LogInformation("Validation passed: All three records match - CompanyId={CompanyId}, StripeSettingsId={StripeSettingsId}, StripeCompanyId={StripeCompanyId}", 
            companyId, companyStripeSettingsId, stripeCompany.Id);
        return (true, null, stripeCompany, stripeSettings);
    }

    /// <summary>
    /// Get the frontend URL for redirects (e.g., Stripe onboarding return URLs)
    /// Detects whether request is from admin app or web app and returns appropriate URL
    /// </summary>
    private string GetFrontendUrl(Guid companyId)
    {
        var host = HttpContext.Request.Host.Host;
        var scheme = HttpContext.Request.Scheme;
        
        // Priority 1: Check query parameter for explicit source
        var sourceParamValue = HttpContext.Request.Query["source"];
        var sourceParam = sourceParamValue.ToString().ToLowerInvariant();
        var hasSourceParam = !string.IsNullOrWhiteSpace(sourceParam);
        _logger.LogInformation("GetFrontendUrl: Query parameter 'source' = '{SourceParam}' (raw: '{RawSource}', hasValue: {HasValue})", 
            sourceParam, sourceParamValue.ToString(), hasSourceParam);
        _logger.LogInformation("GetFrontendUrl: All query parameters: {QueryString}", HttpContext.Request.QueryString);
        
        if (hasSourceParam)
        {
            if (sourceParam == "admin")
            {
                // Admin app: port 4000 (local), admin.aegis-rental.com (production)
                if (host.Contains("localhost") || host == "127.0.0.1")
                {
                    _logger.LogInformation("GetFrontendUrl: Query parameter indicates admin app. Using https://localhost:4000");
                    return "https://localhost:4000";
                }
                else
                {
                    _logger.LogInformation("GetFrontendUrl: Query parameter indicates admin app. Using admin.aegis-rental.com");
                    return "https://admin.aegis-rental.com";
                }
            }
            else if (sourceParam == "web")
            {
                // Web app: port 3000 (local), <subdomain>.aegis-rental.com (production)
                if (host.Contains("localhost") || host == "127.0.0.1")
                {
                    _logger.LogInformation("GetFrontendUrl: Query parameter indicates web app. Using https://localhost:3000");
                    return "https://localhost:3000";
                }
                else
                {
                    // Try to get company subdomain for web app
                    var company = _context.Companies.Find(companyId);
                    if (company != null && !string.IsNullOrWhiteSpace(company.Subdomain))
                    {
                        var frontendUrl = $"https://{company.Subdomain.ToLower()}.aegis-rental.com";
                        _logger.LogInformation("GetFrontendUrl: Query parameter indicates web app. Using company subdomain: {FrontendUrl} (company: {CompanyName})", frontendUrl, company.CompanyName);
                        return frontendUrl;
                    }
                }
            }
        }
        
        // Priority 2: Check Origin or Referer header to determine if request is from admin or web app
        var origin = HttpContext.Request.Headers["Origin"].ToString();
        var referer = HttpContext.Request.Headers["Referer"].ToString();
        var sourceUrl = !string.IsNullOrEmpty(origin) ? origin : referer;
        
        _logger.LogInformation("GetFrontendUrl: Origin: {Origin}, Referer: {Referer}, SourceUrl: {SourceUrl}", origin, referer, sourceUrl);
        
        var isAdminApp = false;
        var isWebApp = false;
        
        if (!string.IsNullOrEmpty(sourceUrl))
        {
            // Admin app: port 4000 (local), admin.aegis-rental.com (production)
            isAdminApp = sourceUrl.Contains("localhost:4000") || 
                        sourceUrl.Contains("admin.aegis-rental.com");
            
            // Web app: port 3000 (local), <subdomain>.aegis-rental.com (production)
            isWebApp = sourceUrl.Contains("localhost:3000") || 
                      (sourceUrl.Contains(".aegis-rental.com") && !sourceUrl.Contains("admin.aegis-rental.com"));
        }

        // Development - detect which app called
        if (host.Contains("localhost") || host == "127.0.0.1")
        {
            if (isAdminApp)
            {
                // Admin app: port 4000
                _logger.LogInformation("GetFrontendUrl: Header detection indicates admin app. Using https://localhost:4000");
                return "https://localhost:4000";
            }
            else if (isWebApp)
            {
                // Web app: port 3000
                _logger.LogInformation("GetFrontendUrl: Header detection indicates web app. Using https://localhost:3000");
                return "https://localhost:3000";
            }
            else
            {
                // Default to web app for localhost if we can't determine
                _logger.LogWarning("GetFrontendUrl: Could not determine source from headers. Defaulting to web app (https://localhost:3000)");
                return "https://localhost:3000";
            }
        }

        // Production - detect which app called
        if (isAdminApp)
        {
            _logger.LogInformation("GetFrontendUrl: Header detection indicates admin app. Using admin.aegis-rental.com");
            return "https://admin.aegis-rental.com";
        }
        else if (isWebApp)
        {
            // Try to get company subdomain for web app
            var company = _context.Companies.Find(companyId);
            if (company != null && !string.IsNullOrWhiteSpace(company.Subdomain))
            {
                var frontendUrl = $"https://{company.Subdomain.ToLower()}.aegis-rental.com";
                _logger.LogInformation("GetFrontendUrl: Header detection indicates web app. Using company subdomain: {FrontendUrl} (company: {CompanyName})", frontendUrl, company.CompanyName);
                return frontendUrl;
            }
        }

        // Fallback: Check configuration
        var configuredFrontendUrl = _configuration["FrontendUrl"] 
            ?? _configuration["FRONTEND_URL"]
            ?? Environment.GetEnvironmentVariable("FRONTEND_URL")
            ?? Environment.GetEnvironmentVariable("FrontendUrl");

        if (!string.IsNullOrWhiteSpace(configuredFrontendUrl))
        {
            _logger.LogInformation("GetFrontendUrl: Using configured frontend URL: {FrontendUrl}", configuredFrontendUrl);
            return configuredFrontendUrl.TrimEnd('/');
        }

        // Last resort: Use request host (but log warning)
        _logger.LogWarning("GetFrontendUrl: No frontend URL configured. Using request host: {Host}. This may be incorrect for Stripe redirects.", host);
        return $"{scheme}://{HttpContext.Request.Host}";
    }

    /// <summary>
    /// Setup Stripe account for a company
    /// Requires: Aegis user OR Customer with admin/mainadmin role
    /// </summary>
    [HttpPost("setup")]
    public async Task<ActionResult> SetupStripeAccount(Guid companyId)
    {
        // Double authorization: Authentication + Admin privileges
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            _logger.LogWarning("SetupStripeAccount: Unauthenticated request for company {CompanyId}", companyId);
            return Unauthorized("Authentication required");
        }
        
        if (!HasAdminPrivileges())
        {
            _logger.LogWarning("SetupStripeAccount: Access denied for company {CompanyId}. Admin privileges required.", companyId);
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
            // Check if this is from admin app - if so, use admin app's complete route, otherwise use web app's complete page
            var sourceParam = HttpContext.Request.Query["source"].ToString().ToLowerInvariant();
            var returnUrl = (!string.IsNullOrWhiteSpace(sourceParam) && sourceParam == "admin")
                ? $"{frontendUrl}/companies/{companyId}/stripe/complete"
                : $"{frontendUrl}/companies/{companyId}/stripe/complete";
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
    /// Check if Stripe account exists for a company (public endpoint for booking availability)
    /// Allows anonymous access - only returns whether Stripe account exists
    /// Route: GET /api/companies/{companyId}/stripe/check-account
    /// </summary>
    [HttpGet("check-account")]
    [AllowAnonymous]
    public async Task<ActionResult> CheckStripeAccount(Guid companyId)
    {
        _logger.LogInformation("CheckStripeAccount: Method called for company {CompanyId}. Route: /api/companies/{{CompanyId}}/stripe/check-account", companyId);
        
        try
        {
            // Check if company exists and is active
            var company = await _context.Companies
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == companyId && c.IsActive);
            
            if (company == null)
            {
                _logger.LogWarning("CheckStripeAccount: Company {CompanyId} not found or inactive", companyId);
                return Ok(new { hasStripeAccount = false, message = "Company not found or inactive" });
            }
            
            // Check if Stripe account exists in stripe_company table
            if (!company.StripeSettingsId.HasValue)
            {
                _logger.LogInformation("CheckStripeAccount: Company {CompanyId} does not have StripeSettingsId", companyId);
                return Ok(new { hasStripeAccount = false });
            }
            
            var stripeCompany = await _context.StripeCompanies
                .AsNoTracking()
                .FirstOrDefaultAsync(sc => sc.CompanyId == companyId && sc.SettingsId == company.StripeSettingsId.Value);
            
            var hasStripeAccount = stripeCompany != null && !string.IsNullOrEmpty(stripeCompany.StripeAccountId);
            
            _logger.LogInformation("CheckStripeAccount: Company {CompanyId} - HasStripeAccount: {HasStripeAccount}", companyId, hasStripeAccount);
            
            return Ok(new { hasStripeAccount });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking Stripe account for company {CompanyId}", companyId);
            return Ok(new { hasStripeAccount = false, error = "Error checking Stripe account" });
        }
    }

    /// <summary>
    /// Get Stripe account status for a company (from database cache)
    /// Read access: Aegis users OR Customers with admin/mainadmin role OR authenticated customers (for booking)
    /// Route: GET /api/companies/{companyId}/stripe/status
    /// </summary>
    [HttpGet("status")]
    public async Task<ActionResult> GetStatus(Guid companyId)
    {
        _logger.LogInformation("GetStatus: Method called for company {CompanyId}. Route: /api/companies/{{CompanyId}}/stripe/status. IsAuthenticated: {IsAuthenticated}", 
            companyId, User.Identity?.IsAuthenticated ?? false);
        
        // Check if company exists and is active (required for all access)
        var company = await _context.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == companyId && c.IsActive);
        
        if (company == null)
        {
            _logger.LogWarning("GetStatus: Company {CompanyId} not found or inactive", companyId);
            return NotFound(new { error = "Company not found or inactive" });
        }
        
        // Check authorization - user must be authenticated
        bool hasAccess = false;
        
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            _logger.LogWarning("GetStatus: Unauthenticated request for company {CompanyId}", companyId);
            return Unauthorized("Authentication required");
        }
        
        // Authenticated users - check authorization
        if (HasAdminPrivileges())
        {
            // Aegis admin/mainadmin can access any company
            hasAccess = true;
            _logger.LogInformation("GetStatus: Aegis admin accessing company {CompanyId}", companyId);
        }
        else if (IsCustomer())
        {
            // Authenticated customers can check Stripe status for booking (any company they're booking from)
            hasAccess = true;
            _logger.LogInformation("GetStatus: Authenticated customer accessing company {CompanyId} for booking", companyId);
        }
        
        if (!hasAccess)
        {
            _logger.LogWarning("GetStatus: Access denied for company {CompanyId}. User is not authorized.", companyId);
            return Forbid("Access denied. Admin privileges required or customer must be authenticated.");
        }
        
        try
        {
            _logger.LogInformation("GetStatus: Fetching Stripe account status for company {CompanyId}", companyId);
            var status = await _stripeConnectService.GetAccountStatusAsync(companyId);
            
            _logger.LogInformation("GetStatus: Successfully retrieved Stripe status for company {CompanyId}. HasAccount: {HasAccount}, AccountId: {AccountId}", 
                companyId, 
                !string.IsNullOrEmpty(status?.StripeAccountId), 
                status?.StripeAccountId ?? "none");
            
            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Stripe status for company {CompanyId}: {Message}", companyId, ex.Message);
            
            // Return 503 for timeout or service unavailable errors
            if (ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("timed out", StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(503, new { error = "Service temporarily unavailable. Please try again." });
            }
            
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get real-time Stripe account status directly from Stripe API
    /// Requires: Aegis user OR Customer with admin/mainadmin role
    /// </summary>
    [HttpGet("status/live")]
    public async Task<IActionResult> GetStripeAccountStatus(Guid companyId)
    {
        // Double authorization: Authentication + Admin privileges
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            _logger.LogWarning("GetStripeAccountStatus: Unauthenticated request for company {CompanyId}", companyId);
            return Unauthorized("Authentication required");
        }
        
        if (!HasAdminPrivileges())
        {
            _logger.LogWarning("GetStripeAccountStatus: Access denied for company {CompanyId}. Admin privileges required.", companyId);
            return Forbid("Admin privileges required");
        }

        try
        {
            // Validate that all three records match: company, stripe_settings, and stripe_company
            var (isValid, errorMessage, stripeCompany, stripeSettings) = await ValidateStripeAccountConsistencyAsync(companyId);
            
            if (!isValid)
            {
                if (errorMessage?.Contains("not found") == true || errorMessage?.Contains("not have Stripe settings") == true)
                {
                    return Ok(new { status = "not_created" });
                }
                _logger.LogError("GetAccountStatus: Validation failed - {ErrorMessage}", errorMessage);
                return StatusCode(500, new { error = $"Stripe account validation failed: {errorMessage}" });
            }

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
    /// Requires: Aegis user OR Customer with admin/mainadmin role
    /// </summary>
    [HttpPost("sync")]
    public async Task<ActionResult> SyncAccount(Guid companyId)
    {
        // Double authorization: Authentication + Admin privileges
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            _logger.LogWarning("SyncAccount: Unauthenticated request for company {CompanyId}", companyId);
            return Unauthorized("Authentication required");
        }
        
        if (!HasAdminPrivileges())
        {
            _logger.LogWarning("SyncAccount: Access denied for company {CompanyId}. Admin privileges required.", companyId);
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
    /// Requires: Aegis user OR Customer with admin/mainadmin role
    /// </summary>
    [HttpPost("suspend")]
    public async Task<ActionResult> Suspend(Guid companyId, [FromBody] SuspendAccountDto dto)
    {
        // Double authorization: Authentication + Admin privileges
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            _logger.LogWarning("Suspend: Unauthenticated request for company {CompanyId}", companyId);
            return Unauthorized("Authentication required");
        }
        
        if (!HasAdminPrivileges())
        {
            _logger.LogWarning("Suspend: Access denied for company {CompanyId}. Admin privileges required.", companyId);
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
    /// Requires: Aegis user OR Customer with admin/mainadmin role
    /// </summary>
    [HttpPost("reactivate")]
    public async Task<ActionResult> Reactivate(Guid companyId)
    {
        // Double authorization: Authentication + Admin privileges
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            _logger.LogWarning("Reactivate: Unauthenticated request for company {CompanyId}", companyId);
            return Unauthorized("Authentication required");
        }
        
        if (!HasAdminPrivileges())
        {
            _logger.LogWarning("Reactivate: Access denied for company {CompanyId}. Admin privileges required.", companyId);
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
    /// Requires: Aegis user OR Customer with admin/mainadmin role
    /// </summary>
    [HttpDelete]
    public async Task<ActionResult> Delete(Guid companyId)
    {
        // Double authorization: Authentication + Admin privileges
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            _logger.LogWarning("Delete: Unauthenticated request for company {CompanyId}", companyId);
            return Unauthorized("Authentication required");
        }
        
        if (!HasAdminPrivileges())
        {
            _logger.LogWarning("Delete: Access denied for company {CompanyId}. Admin privileges required.", companyId);
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
    /// Requires: Aegis user OR Customer with admin/mainadmin role OR customer (for their own company)
    /// </summary>
    [HttpGet("reauth")]
    public async Task<IActionResult> RefreshOnboarding(Guid companyId)
    {
        // Double authorization: Authentication + (Admin privileges OR Customer access)
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            _logger.LogWarning("RefreshOnboarding: Unauthenticated request for company {CompanyId}", companyId);
            return Unauthorized("Authentication required");
        }
        
        bool hasAccess = false;
        
        if (HasAdminPrivileges())
        {
            // Aegis admin/mainadmin can access any company
            hasAccess = true;
            _logger.LogInformation("RefreshOnboarding: Aegis admin accessing company {CompanyId}", companyId);
        }
        else if (IsCustomer())
        {
            // Customer can only access their own company
            hasAccess = await CanCustomerAccessCompany(companyId);
            if (hasAccess)
            {
                _logger.LogInformation("RefreshOnboarding: Customer accessing their own company {CompanyId}", companyId);
            }
            else
            {
                _logger.LogWarning("RefreshOnboarding: Customer attempted to access company {CompanyId} without permission", companyId);
            }
        }
        
        if (!hasAccess)
        {
            _logger.LogWarning("RefreshOnboarding: Access denied for company {CompanyId}. Admin privileges required or customer must own the company.", companyId);
            return Forbid("Access denied. Admin privileges required or customer must own the company.");
        }

        try
        {
            // Validate that all three records match: company, stripe_settings, and stripe_company
            var (isValid, errorMessage, stripeCompany, stripeSettings) = await ValidateStripeAccountConsistencyAsync(companyId);
            
            if (!isValid)
            {
                _logger.LogError("RefreshOnboarding: Validation failed - {ErrorMessage}", errorMessage);
                return BadRequest(new { error = $"Stripe account validation failed: {errorMessage}" });
            }

            if (stripeCompany == null || string.IsNullOrEmpty(stripeCompany.StripeAccountId))
            {
                return BadRequest(new { error = "No Stripe account found" });
            }

            // Fetch the company entity to update it
            var company = await _context.Companies.FindAsync(companyId);
            if (company == null)
            {
                _logger.LogError("RefreshOnboarding: Company {CompanyId} not found", companyId);
                return NotFound(new { error = $"Company {companyId} not found" });
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
            // Both admin and web apps use the same complete route: /companies/{companyId}/stripe/complete
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
                var invalidOpErrorMessage = invalidOpEx.Message;
                _logger.LogWarning("Cannot create account link for company {CompanyId}, account {AccountId}. Account is disabled: {Message}", 
                    companyId, stripeAccountId, invalidOpErrorMessage);
                
                return BadRequest(new { 
                    error = "Cannot create onboarding link", 
                    message = invalidOpErrorMessage,
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

