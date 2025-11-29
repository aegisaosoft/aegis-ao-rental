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
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using CarRental.Api.Data;
using CarRental.Api.DTOs;
using CarRental.Api.Models;
using CarRental.Api.Services;
using BCrypt.Net;

namespace CarRental.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly CarRentalDbContext _context;
    private readonly IJwtService _jwtService;
    private readonly ISessionService _sessionService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;
    private readonly MultiTenantEmailService _emailService;

    public AuthController(
        CarRentalDbContext context,
        IJwtService jwtService,
        ISessionService sessionService,
        IConfiguration configuration,
        ILogger<AuthController> logger,
        MultiTenantEmailService emailService)
    {
        _context = context;
        _jwtService = jwtService;
        _sessionService = sessionService;
        _configuration = configuration;
        _logger = logger;
        _emailService = emailService;
    }

    /// <summary>
    /// Register a new customer
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    public async Task<ActionResult<object>> Register([FromBody] RegisterDto dto)
    {
        // Check if email already exists (case-insensitive)
        if (await _context.Customers.AnyAsync(c => EF.Functions.ILike(c.Email, dto.Email)))
        {
            return BadRequest(new { message = "Email already registered" });
        }

        // Create new customer
        var customer = new Customer
        {
            Email = dto.Email,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Phone = dto.Phone,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            IsVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Customers.Add(customer);
        await _context.SaveChangesAsync();

        // Generate JWT token
        var token = _jwtService.GenerateToken(customer.Id.ToString(), "user");

        // Store customer session information
        var session = new CustomerSession
        {
            CustomerId = customer.Id.ToString(),
            Email = customer.Email,
            FirstName = customer.FirstName,
            LastName = customer.LastName,
            Role = "user",
            CompanyId = customer.CompanyId?.ToString(),
            CompanyName = customer.Company?.CompanyName,
            LoginTime = DateTime.UtcNow,
            LastActivity = DateTime.UtcNow,
            IsActive = true
        };
        _sessionService.SetCustomerSession(token, session);

        return CreatedAtAction(nameof(Register), new LoginResponseDto
        {
            Result = new LoginResultDto
            {
                Token = token,
                User = new
                {
                    customerId = customer.Id,
                    email = customer.Email,
                    firstName = customer.FirstName,
                    lastName = customer.LastName,
                    phone = customer.Phone,
                    role = "user",
                    companyId = customer.CompanyId,
                    companyName = customer.Company?.CompanyName,
                    isVerified = customer.IsVerified,
                    isActive = customer.IsActive,
                    lastLogin = customer.LastLogin
                }
            },
            Reason = 0,
            Message = null,
            StackTrace = null
        });
    }

    /// <summary>
    /// Login customer (supports all roles: customer, worker, admin, mainadmin, designer)
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    public async Task<ActionResult<object>> Login([FromBody] LoginDto dto)
    {
        var customer = await _context.Customers
            .Include(c => c.Company)
            .FirstOrDefaultAsync(c => EF.Functions.ILike(c.Email, dto.Email));

        // Check if customer exists
        if (customer == null)
        {
            _logger.LogWarning("Login attempt with non-existent email: {Email}", dto.Email);
            return Unauthorized(new { message = "Invalid email or password" });
        }

        // Check if password hash exists
        if (customer.PasswordHash == null)
        {
            _logger.LogWarning("Login attempt for customer {Email} with no password hash", dto.Email);
            return Unauthorized(new { message = "Invalid email or password" });
        }

        // Check if account is active
        if (!customer.IsActive)
        {
            _logger.LogWarning("Login attempt for inactive account: {Email}", dto.Email);
            return Unauthorized(new { message = "Your account has been deactivated. Please contact support." });
        }

        // Verify password
        if (!BCrypt.Net.BCrypt.Verify(dto.Password, customer.PasswordHash))
        {
            _logger.LogWarning("Login attempt with incorrect password for: {Email}", dto.Email);
            return Unauthorized(new { message = "Invalid email or password" });
        }

        // Update last login
        customer.LastLogin = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Generate JWT token with customer role and company information
        var token = _jwtService.GenerateToken(
            customer.Id.ToString(), 
            customer.Role, 
            customer.CompanyId?.ToString(), 
            customer.Company?.CompanyName
        );

        // Store customer session information
        var session = new CustomerSession
        {
            CustomerId = customer.Id.ToString(),
            Email = customer.Email,
            FirstName = customer.FirstName,
            LastName = customer.LastName,
            Role = customer.Role,
            CompanyId = customer.CompanyId?.ToString(),
            CompanyName = customer.Company?.CompanyName,
            LoginTime = DateTime.UtcNow,
            LastActivity = DateTime.UtcNow,
            IsActive = true
        };
        _sessionService.SetCustomerSession(token, session);

        return Ok(new LoginResponseDto
        {
            Result = new LoginResultDto
            {
                Token = token,
                User = new
                {
                    customerId = customer.Id,
                    email = customer.Email,
                    firstName = customer.FirstName,
                    lastName = customer.LastName,
                    phone = customer.Phone,
                    role = customer.Role,
                    companyId = customer.CompanyId,
                    companyName = customer.Company?.CompanyName,
                    isVerified = customer.IsVerified,
                    isActive = customer.IsActive,
                    lastLogin = customer.LastLogin
                }
            },
            Reason = 0,
            Message = null,
            StackTrace = null
        });
    }

    /// <summary>
    /// Get current user profile (works for customers with all roles)
    /// </summary>
    [HttpGet("profile")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    public async Task<ActionResult<object>> GetProfile()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var customerId))
        {
            return Unauthorized(new { message = "Invalid token" });
        }

        // Get customer profile (works for all roles: customer, worker, admin, mainadmin, designer)
        var customer = await _context.Customers
            .Include(c => c.Company)
            .FirstOrDefaultAsync(c => c.Id == customerId);

        if (customer == null)
        {
            return NotFound(new { message = "Customer not found" });
        }

        var customerDto = new CustomerDto
        {
            CustomerId = customer.Id,
            Email = customer.Email,
            FirstName = customer.FirstName,
            LastName = customer.LastName,
            Phone = customer.Phone,
            DateOfBirth = customer.DateOfBirth,
            Address = customer.Address,
            City = customer.City,
            State = customer.State,
            Country = customer.Country,
            PostalCode = customer.PostalCode,
            StripeCustomerId = customer.StripeCustomerId,
            IsVerified = customer.IsVerified,
            CustomerType = customer.CustomerType.ToString(),
            CreatedAt = customer.CreatedAt,
            UpdatedAt = customer.UpdatedAt,
            Role = customer.Role,
            CompanyId = customer.CompanyId,
            CompanyName = customer.Company?.CompanyName
        };

        return Ok(customerDto);
    }

    /// <summary>
    /// Update customer profile
    /// </summary>
        [HttpPut("profile")]
        [Authorize]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<CustomerDto>> UpdateProfile([FromBody] UpdateCustomerProfileDto dto)
        {
            try
            {
                // Check if dto is null (shouldn't happen, but handle gracefully)
                if (dto == null)
                {
                    _logger.LogWarning("UpdateProfile called with null DTO");
                    return BadRequest(new { message = "Request body is required" });
                }

                // Get user ID first (needed for both early return and update logic)
                var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                
                if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var customerId))
                {
                    return Unauthorized(new { message = "Invalid token" });
                }

                // Check if there's anything to update
                var hasUpdates = !string.IsNullOrWhiteSpace(dto.FirstName) ||
                                !string.IsNullOrWhiteSpace(dto.LastName) ||
                                !string.IsNullOrWhiteSpace(dto.Phone) ||
                                !string.IsNullOrWhiteSpace(dto.Email) ||
                                !string.IsNullOrWhiteSpace(dto.CurrentPassword) ||
                                !string.IsNullOrWhiteSpace(dto.NewPassword);

                if (!hasUpdates)
                {
                    _logger.LogInformation("UpdateProfile called with no updates");
                    // Return current profile if no updates
                    var currentCustomer = await _context.Customers
                        .Include(c => c.Company)
                        .FirstOrDefaultAsync(c => c.Id == customerId);
                    if (currentCustomer == null)
                    {
                        return NotFound(new { message = "Customer not found" });
                    }
                    var currentCustomerDto = new CustomerDto
                    {
                        CustomerId = currentCustomer.Id,
                        Email = currentCustomer.Email,
                        FirstName = currentCustomer.FirstName,
                        LastName = currentCustomer.LastName,
                        Phone = currentCustomer.Phone,
                        DateOfBirth = currentCustomer.DateOfBirth,
                        Address = currentCustomer.Address,
                        City = currentCustomer.City,
                        State = currentCustomer.State,
                        Country = currentCustomer.Country,
                        PostalCode = currentCustomer.PostalCode,
                        StripeCustomerId = currentCustomer.StripeCustomerId,
                        IsVerified = currentCustomer.IsVerified,
                        CustomerType = currentCustomer.CustomerType.ToString(),
                        CreatedAt = currentCustomer.CreatedAt,
                        UpdatedAt = currentCustomer.UpdatedAt,
                        Role = currentCustomer.Role,
                        CompanyId = currentCustomer.CompanyId,
                        CompanyName = currentCustomer.Company?.CompanyName
                    };
                    return Ok(currentCustomerDto);
                }

                // Normalize empty strings to null to avoid validation issues
                if (dto.FirstName != null && string.IsNullOrWhiteSpace(dto.FirstName))
                {
                    dto.FirstName = null;
                    ModelState.Remove(nameof(dto.FirstName));
                }
                if (dto.LastName != null && string.IsNullOrWhiteSpace(dto.LastName))
                {
                    dto.LastName = null;
                    ModelState.Remove(nameof(dto.LastName));
                }
                if (dto.Phone != null && string.IsNullOrWhiteSpace(dto.Phone))
                {
                    dto.Phone = null;
                    ModelState.Remove(nameof(dto.Phone));
                }
                // Handle Email - normalize empty strings and clear validation errors
                if (dto.Email != null)
                {
                    if (string.IsNullOrWhiteSpace(dto.Email))
                    {
                        dto.Email = null;
                    }
                    // Remove any existing validation errors for Email
                    ModelState.Remove(nameof(dto.Email));
                    // Only validate email format if it's not null/empty
                    if (!string.IsNullOrEmpty(dto.Email))
                    {
                        var emailAttr = new EmailAddressAttribute();
                        if (!emailAttr.IsValid(dto.Email))
                        {
                            ModelState.AddModelError(nameof(dto.Email), "The Email field is not a valid email address.");
                        }
                    }
                }
                if (dto.CurrentPassword != null && string.IsNullOrWhiteSpace(dto.CurrentPassword))
                {
                    dto.CurrentPassword = null;
                    ModelState.Remove(nameof(dto.CurrentPassword));
                }
                if (dto.NewPassword != null && string.IsNullOrWhiteSpace(dto.NewPassword))
                {
                    dto.NewPassword = null;
                    ModelState.Remove(nameof(dto.NewPassword));
                }

                // Re-validate after normalization
                TryValidateModel(dto);

                // Check model validation after normalization
                if (!ModelState.IsValid)
                {
                    var errors = ModelState
                        .Where(x => x.Value?.Errors.Count > 0)
                        .SelectMany(x => x.Value!.Errors.Select(e => $"{x.Key}: {e.ErrorMessage}"))
                        .ToList();
                    
                    _logger.LogWarning("Model validation failed for UpdateProfile: {Errors}. DTO: FirstName={FirstName}, LastName={LastName}, Email={Email}, Phone={Phone}, HasCurrentPassword={HasCurrentPassword}, HasNewPassword={HasNewPassword}",
                        string.Join(", ", errors),
                        dto.FirstName ?? "null",
                        dto.LastName ?? "null",
                        dto.Email ?? "null",
                        dto.Phone ?? "null",
                        !string.IsNullOrEmpty(dto.CurrentPassword),
                        !string.IsNullOrEmpty(dto.NewPassword));
                    return BadRequest(new { message = "Validation failed", errors = errors });
                }

            // Load customer for update (userIdClaim and customerId already declared above)
            var customer = await _context.Customers.FindAsync(customerId);
            if (customer == null)
            {
                return NotFound(new { message = "Customer not found" });
            }

            // Update password if provided
            if (!string.IsNullOrEmpty(dto.CurrentPassword) && !string.IsNullOrEmpty(dto.NewPassword))
            {
                // Check if customer has a password set
                if (string.IsNullOrEmpty(customer.PasswordHash))
                {
                    return BadRequest(new { message = "Password cannot be updated. No password is currently set for this account." });
                }

                // Verify current password (both are checked for null/empty above, so use null-forgiving operator)
                if (!BCrypt.Net.BCrypt.Verify(dto.CurrentPassword!, customer.PasswordHash!))
                {
                    _logger.LogWarning("Password update attempt with incorrect current password for customer: {CustomerId}", customerId);
                    return BadRequest(new { message = "Current password is incorrect" });
                }

                // Validate new password (checked for null/empty above)
                if (dto.NewPassword!.Length < 6)
                {
                    return BadRequest(new { message = "New password must be at least 6 characters long" });
                }

                // Hash and update password (NewPassword is checked for null/empty above)
                customer.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword!);
                _logger.LogInformation("Password updated successfully for customer: {CustomerId}", customerId);
            }

            // Update fields if provided (FirstName, LastName, and Email are required, so only update if non-empty)
            if (dto.FirstName != null && !string.IsNullOrWhiteSpace(dto.FirstName))
            {
                customer.FirstName = dto.FirstName.Trim();
            }
            
            if (dto.LastName != null && !string.IsNullOrWhiteSpace(dto.LastName))
            {
                customer.LastName = dto.LastName.Trim();
            }
            
            // Phone is nullable, so we can set it to null if empty
            if (dto.Phone != null)
            {
                customer.Phone = string.IsNullOrWhiteSpace(dto.Phone) ? null : dto.Phone.Trim();
            }
            
            if (dto.Email != null && !string.IsNullOrWhiteSpace(dto.Email))
            {
                var email = dto.Email.Trim();
                // Validate email format
                try
                {
                    var emailAddress = new System.Net.Mail.MailAddress(email);
                    customer.Email = emailAddress.Address;
                }
                catch
                {
                    return BadRequest(new { message = "Invalid email format" });
                }
            }

            customer.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // Load company for response
            await _context.Entry(customer)
                .Reference(c => c.Company)
                .LoadAsync();

            var customerDto = new CustomerDto
            {
                CustomerId = customer.Id,
                Email = customer.Email,
                FirstName = customer.FirstName,
                LastName = customer.LastName,
                Phone = customer.Phone,
                DateOfBirth = customer.DateOfBirth,
                Address = customer.Address,
                City = customer.City,
                State = customer.State,
                Country = customer.Country,
                PostalCode = customer.PostalCode,
                StripeCustomerId = customer.StripeCustomerId,
                IsVerified = customer.IsVerified,
            CustomerType = customer.CustomerType.ToString(),
                CreatedAt = customer.CreatedAt,
                UpdatedAt = customer.UpdatedAt,
                Role = customer.Role,
                CompanyId = customer.CompanyId,
                CompanyName = customer.Company?.CompanyName
            };

            return Ok(customerDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating profile");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Logout and clear session
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    public IActionResult Logout()
    {
        try
        {
            // Get token from Authorization header
            var authHeader = Request.Headers["Authorization"].FirstOrDefault();
            if (authHeader != null && authHeader.StartsWith("Bearer "))
            {
                var token = authHeader.Substring("Bearer ".Length).Trim();
                _sessionService.RemoveCustomerSession(token);
            }

            return Ok(new { message = "Logged out successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout");
            return StatusCode(500, "Internal server error");
        }
    }


    /// <summary>
    /// Get current user information (protected endpoint)
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GetCurrentUser()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var customerId))
        {
            return Unauthorized(new { message = "Invalid token" });
        }

        var customer = await _context.Customers
            .Include(c => c.Company)
            .FirstOrDefaultAsync(c => c.Id == customerId);

        if (customer == null)
        {
            return NotFound(new { message = "Customer not found" });
        }

        return Ok(new
        {
            email = customer.Email,
            name = $"{customer.FirstName} {customer.LastName}".Trim(),
            userId = customer.Id,
            tenantId = customer.CompanyId?.ToString(),
            companyId = customer.CompanyId,
            companyName = customer.Company?.CompanyName,
            role = customer.Role,
            authenticated = true
        });
    }

    /// <summary>
    /// Request password reset - sends reset link to email
    /// </summary>
    [HttpPost("forgot-password")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(dto.Email))
            {
                return BadRequest(new { message = "Email is required" });
            }

            // Find customer by email (case-insensitive)
            var customer = await _context.Customers
                .Include(c => c.Company)
                .FirstOrDefaultAsync(c => EF.Functions.ILike(c.Email, dto.Email));

            // If customer doesn't exist, return specific response to redirect to signup
            if (customer == null)
            {
                _logger.LogInformation("Password reset requested for non-existent email: {Email}", dto.Email);
                return Ok(new { 
                    message = "Email not found", 
                    emailNotFound = true,
                    redirectToSignup = true 
                });
            }

            if (!customer.IsActive)
            {
                _logger.LogInformation("Password reset requested for inactive account: {Email}", dto.Email);
                return Ok(new { 
                    message = "Account is not active", 
                    emailNotFound = false,
                    redirectToSignup = false 
                });
            }
            {
                // Generate a secure random token
                var token = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
                    .Replace("+", "-")
                    .Replace("/", "_")
                    .Replace("=", "");

                // Store token in customer record
                customer.Token = token;
                customer.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Password reset token generated for customer {Email}, CustomerId: {CustomerId}", customer.Email, customer.Id);

                // Get company ID (use customer's company or default to first company if none)
                var companyId = customer.CompanyId ?? await _context.Companies
                    .Where(c => c.IsActive)
                    .Select(c => c.Id)
                    .FirstOrDefaultAsync();

                if (companyId == Guid.Empty)
                {
                    _logger.LogWarning("No active company found for password reset email");
                    return Ok(new { message = "If the email exists, a password reset link has been sent." });
                }

                // Determine language from company and get frontend URL
                var company = await _context.Companies.FindAsync(companyId);
                
                // Build reset password URL using company subdomain
                var frontendUrl = GetFrontendUrl(companyId, company);
                var resetUrl = $"{frontendUrl}/reset-password?token={Uri.EscapeDataString(token)}&email={Uri.EscapeDataString(customer.Email)}";
                var languageCode = company?.Language?.ToLower() ?? "en";
                var language = LanguageCodes.FromCode(languageCode);

                // Send password reset email
                var customerName = !string.IsNullOrWhiteSpace(customer.FirstName) || !string.IsNullOrWhiteSpace(customer.LastName)
                    ? $"{customer.FirstName} {customer.LastName}".Trim()
                    : customer.Email;

                var subject = language == EmailLanguage.Spanish 
                    ? "Restablecer contraseña" 
                    : language == EmailLanguage.Portuguese 
                        ? "Redefinir senha" 
                        : "Reset Your Password";

                var htmlContent = $@"
                    <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px;'>
                        <h2 style='color: #333;'>{subject}</h2>
                        <p>Hello {customerName},</p>
                        <p>You requested to reset your password. Click the link below to set a new password:</p>
                        <p style='margin: 30px 0;'>
                            <a href='{resetUrl}' style='background-color: #4CAF50; color: white; padding: 12px 24px; text-decoration: none; border-radius: 4px; display: inline-block;'>
                                Reset Password
                            </a>
                        </p>
                        <p>Or copy and paste this link into your browser:</p>
                        <p style='word-break: break-all; color: #666;'>{resetUrl}</p>
                        <p style='color: #999; font-size: 12px; margin-top: 30px;'>
                            This link will expire in 24 hours. If you didn't request this, please ignore this email.
                        </p>
                    </div>";

                var emailSent = await _emailService.SendEmailAsync(
                    companyId,
                    customer.Email,
                    subject,
                    htmlContent,
                    null,
                    language
                );

                if (emailSent)
                {
                    _logger.LogInformation("Password reset email sent successfully to {Email}", customer.Email);
                }
                else
                {
                    _logger.LogWarning("Failed to send password reset email to {Email}", customer.Email);
                }
            }

            // Return success message
            return Ok(new { 
                message = "If the email exists, a password reset link has been sent.",
                emailNotFound = false,
                redirectToSignup = false
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing forgot password request for {Email}", dto.Email);
            return StatusCode(500, new { message = "An error occurred while processing your request." });
        }
    }

    /// <summary>
    /// Reset password using token
    /// </summary>
    [HttpPost("reset-password")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(dto.Token))
            {
                return BadRequest(new { message = "Token is required" });
            }

            if (string.IsNullOrWhiteSpace(dto.Email))
            {
                return BadRequest(new { message = "Email is required" });
            }

            if (string.IsNullOrWhiteSpace(dto.NewPassword) || dto.NewPassword.Length < 6)
            {
                return BadRequest(new { message = "Password must be at least 6 characters long" });
            }

            // Find customer by email and token
            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => EF.Functions.ILike(c.Email, dto.Email) && c.Token == dto.Token);

            if (customer == null)
            {
                _logger.LogWarning("Invalid password reset token for email: {Email}", dto.Email);
                return BadRequest(new { message = "Invalid or expired reset token." });
            }

            if (!customer.IsActive)
            {
                return BadRequest(new { message = "Account is not active." });
            }

            // Update password and clear token
            customer.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
            customer.Token = null; // Delete token so it can't be used again
            customer.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Password reset successful for customer {Email}, CustomerId: {CustomerId}", customer.Email, customer.Id);

            return Ok(new { message = "Password has been reset successfully. You can now login with your new password." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting password for {Email}", dto.Email);
            return StatusCode(500, new { message = "An error occurred while resetting your password." });
        }
    }

    private string GetFrontendUrl(Guid? companyId = null, Company? company = null)
    {
        var host = HttpContext.Request.Host.Host;
        var scheme = HttpContext.Request.Scheme;

        // Development
        if (host.Contains("localhost") || host == "127.0.0.1")
        {
            // Use https for localhost:3000 to match frontend configuration
            return "https://localhost:3000"; // Match your frontend port
        }

        // Production - build URL from company subdomain
        if (companyId.HasValue && companyId.Value != Guid.Empty)
        {
            // Load company if not provided
            if (company == null)
            {
                company = _context.Companies.Find(companyId.Value);
            }

            if (company != null && !string.IsNullOrWhiteSpace(company.Subdomain))
            {
                // Build frontend URL using company subdomain: {subdomain}.aegis-rental.com
                var frontendUrl = $"https://{company.Subdomain.ToLower()}.aegis-rental.com";
                _logger.LogInformation("Using company subdomain for frontend URL: {FrontendUrl} (company: {CompanyName})", frontendUrl, company.CompanyName);
                return frontendUrl;
            }
        }

        // Fallback: Check configuration for frontend URL
        var configuredFrontendUrl = _configuration["FrontendUrl"] 
            ?? _configuration["FRONTEND_URL"]
            ?? Environment.GetEnvironmentVariable("FRONTEND_URL")
            ?? Environment.GetEnvironmentVariable("FrontendUrl");

        if (!string.IsNullOrWhiteSpace(configuredFrontendUrl))
        {
            _logger.LogInformation("Using configured frontend URL: {FrontendUrl}", configuredFrontendUrl);
            return configuredFrontendUrl.TrimEnd('/');
        }

        // Last resort: Try to derive from request host (for subdomain-based deployments)
        // If backend is on api subdomain, frontend might be on root or www
        if (host.Contains("api.") || host.Contains("-api"))
        {
            // Replace api subdomain with root or www
            var frontendHost = host.Replace("api.", "").Replace("-api", "");
            _logger.LogWarning("Deriving frontend URL from request host: {FrontendHost}. Consider configuring FrontendUrl or ensuring company has subdomain.", frontendHost);
            return $"{scheme}://{frontendHost}";
        }

        // Last resort: Use request host (but log warning)
        _logger.LogWarning("No frontend URL configured and no company subdomain found. Using request host: {Host}. This may be incorrect for password reset links.", host);
        return $"{scheme}://{HttpContext.Request.Host}";
    }
}

public class UpdateCustomerProfileDto
{
    [MaxLength(100)]
    public string? FirstName { get; set; }
    
    [MaxLength(100)]
    public string? LastName { get; set; }
    
    [MaxLength(50)]
    public string? Phone { get; set; }
    
    [EmailAddress]
    [MaxLength(255)]
    public string? Email { get; set; }
    
    [MaxLength(500)]
    public string? CurrentPassword { get; set; }
    
    [MaxLength(500)]
    public string? NewPassword { get; set; }
}

public class RegisterDto
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Phone { get; set; }
}

public class LoginDto
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class ForgotPasswordDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}

public class ResetPasswordDto
{
    [Required]
    public string Token { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(6)]
    public string NewPassword { get; set; } = string.Empty;
}
