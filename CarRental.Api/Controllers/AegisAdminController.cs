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
using Microsoft.EntityFrameworkCore;
using CarRental.Api.Data;
using CarRental.Api.DTOs;
using CarRental.Api.Models;
using CarRental.Api.Services;
using BCrypt.Net;

namespace CarRental.Api.Controllers;

[ApiController]
[Route("api/aegis-admin")]
public class AegisAdminController : ControllerBase
{
    private readonly CarRentalDbContext _context;
    private readonly IJwtService _jwtService;
    private readonly ILogger<AegisAdminController> _logger;

    public AegisAdminController(
        CarRentalDbContext context,
        IJwtService jwtService,
        ILogger<AegisAdminController> logger)
    {
        _context = context;
        _jwtService = jwtService;
        _logger = logger;
    }

    /// <summary>
    /// Register a new aegis admin/agent user (public endpoint for set-new-client flow)
    /// </summary>
    [HttpPost("register")]
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    public async Task<ActionResult<object>> Register([FromBody] AegisAdminRegisterDto dto)
    {
        _logger.LogInformation("Register attempt for UserId: {UserId}, Role: {Role}", dto.UserId, dto.Role);
        
        // Check if userid already exists (case-insensitive)
        if (await _context.AegisUsers.AnyAsync(u => u.UserId.ToLower() == dto.UserId.ToLower()))
        {
            _logger.LogWarning("Registration attempt with existing UserId: {UserId}", dto.UserId);
            return BadRequest(new { message = "User ID already registered" });
        }

        // Validate role
        if (dto.Role != "agent" && dto.Role != "admin" && dto.Role != "designer")
        {
            _logger.LogWarning("Registration attempt with invalid role: {Role}", dto.Role);
            return BadRequest(new { message = "Invalid role. Must be 'agent', 'admin', or 'designer'" });
        }

        // Create new aegis user
        var aegisUser = new AegisUser
        {
            UserId = dto.UserId,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Phone = dto.Phone,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Role = dto.Role,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.AegisUsers.Add(aegisUser);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Aegis user created successfully. UserId: {UserId}, AegisUserId: {AegisUserId}, Role: {Role}", 
            aegisUser.UserId, aegisUser.Id, aegisUser.Role);

        // Generate JWT token
        var token = _jwtService.GenerateToken(aegisUser.Id.ToString(), aegisUser.Role);

        var response = new LoginResponseDto
        {
            Result = new LoginResultDto
            {
                Token = token,
                User = new AegisAdminResponseDto
                {
                    UserId = aegisUser.UserId,
                    AegisUserId = aegisUser.Id,
                    FirstName = aegisUser.FirstName,
                    LastName = aegisUser.LastName,
                    Phone = aegisUser.Phone,
                    Role = aegisUser.Role,
                    IsActive = aegisUser.IsActive,
                    LastLogin = aegisUser.LastLogin,
                    CreatedAt = aegisUser.CreatedAt
                }
            },
            Reason = 0,
            Message = null,
            StackTrace = null
        };

        _logger.LogInformation("Returning LoginResponseDto with UserId: {UserId}", aegisUser.UserId);
        return CreatedAtAction(nameof(Register), response);
    }

    /// <summary>
    /// Login aegis admin/agent user
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    public async Task<ActionResult<object>> Login([FromBody] AegisAdminLoginDto dto)
    {
        var aegisUser = await _context.AegisUsers
            .FirstOrDefaultAsync(u => u.UserId.ToLower() == dto.UserId.ToLower());

        // Check if user exists
        if (aegisUser == null)
        {
            _logger.LogWarning("Login attempt with non-existent userid: {UserId}", dto.UserId);
            return Unauthorized(new { message = "Invalid user ID or password" });
        }

        // Check if password hash exists
        if (aegisUser.PasswordHash == null)
        {
            _logger.LogWarning("Login attempt for user {UserId} with no password hash", dto.UserId);
            return Unauthorized(new { message = "Invalid user ID or password" });
        }

        // Check if account is active
        if (!aegisUser.IsActive)
        {
            _logger.LogWarning("Login attempt for inactive account: {UserId}", dto.UserId);
            return Unauthorized(new { message = "Your account has been deactivated. Please contact support." });
        }

        // Verify password
        if (!BCrypt.Net.BCrypt.Verify(dto.Password, aegisUser.PasswordHash))
        {
            _logger.LogWarning("Login attempt with incorrect password for: {UserId}", dto.UserId);
            return Unauthorized(new { message = "Invalid user ID or password" });
        }

        // Update last login
        aegisUser.LastLogin = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Generate JWT token with role
        var token = _jwtService.GenerateToken(aegisUser.Id.ToString(), aegisUser.Role);

        return Ok(new LoginResponseDto
        {
            Result = new LoginResultDto
            {
                Token = token,
                User = new AegisAdminResponseDto
                {
                    UserId = aegisUser.UserId,
                    AegisUserId = aegisUser.Id,
                    FirstName = aegisUser.FirstName,
                    LastName = aegisUser.LastName,
                    Phone = aegisUser.Phone,
                    Role = aegisUser.Role,
                    IsActive = aegisUser.IsActive,
                    LastLogin = aegisUser.LastLogin,
                    CreatedAt = aegisUser.CreatedAt
                }
            },
            Reason = 0,
            Message = null,
            StackTrace = null
        });
    }

    /// <summary>
    /// Get current aegis admin user profile
    /// </summary>
    [HttpGet("profile")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    public async Task<ActionResult<object>> GetProfile()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var aegisUserId))
        {
            return Unauthorized(new { message = "Invalid token" });
        }

        // Get aegis user profile
        var aegisUser = await _context.AegisUsers
            .FirstOrDefaultAsync(u => u.Id == aegisUserId);

        if (aegisUser == null)
        {
            return NotFound(new { message = "User not found" });
        }

        var userDto = new AegisAdminResponseDto
        {
            UserId = aegisUser.UserId,
            AegisUserId = aegisUser.Id,
            FirstName = aegisUser.FirstName,
            LastName = aegisUser.LastName,
            Phone = aegisUser.Phone,
            Role = aegisUser.Role,
            IsActive = aegisUser.IsActive,
            LastLogin = aegisUser.LastLogin,
            CreatedAt = aegisUser.CreatedAt
        };

        // Return in the format AuthContext expects (wrapped in result or direct)
        return Ok(new { result = userDto, user = userDto });
    }

    /// <summary>
    /// Get Aegis user by userId (email) - public endpoint for set-new-client flow
    /// </summary>
    [HttpGet("userid/{userId}")]
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<AegisAdminResponseDto>> GetUserByUserId(string userId)
    {
        var decodedUserId = Uri.UnescapeDataString(userId);
        var aegisUser = await _context.AegisUsers
            .FirstOrDefaultAsync(u => u.UserId.ToLower() == decodedUserId.ToLower());

        if (aegisUser == null)
        {
            return NotFound(new { message = "User not found" });
        }

        var userDto = new AegisAdminResponseDto
        {
            UserId = aegisUser.UserId,
            AegisUserId = aegisUser.Id,
            FirstName = aegisUser.FirstName,
            LastName = aegisUser.LastName,
            Phone = aegisUser.Phone,
            Role = aegisUser.Role,
            IsActive = aegisUser.IsActive,
            LastLogin = aegisUser.LastLogin,
            CreatedAt = aegisUser.CreatedAt
        };

        return Ok(userDto);
    }

    /// <summary>
    /// Check email availability and create Aegis user if needed (public endpoint for set-new-client flow)
    /// Checks: 1) Customer with company, 2) Customer without company or no customer, 3) Aegis user existence
    /// </summary>
    [HttpPost("check-email-and-setup")]
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public async Task<ActionResult<object>> CheckEmailAndSetup([FromBody] CheckEmailAndSetupRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new { error = "Email is required" });
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { error = "Password is required" });
        }

        var email = request.Email.Trim().ToLowerInvariant();
        var password = request.Password;

        // 1. Check if customer exists and has companyId
        var customer = await _context.Customers
            .Include(c => c.Company)
            .FirstOrDefaultAsync(c => EF.Functions.ILike(c.Email, email));

        bool hasCustomerWithCompany = customer != null && customer.CompanyId.HasValue;
        string? customerCompanyName = null;
        if (hasCustomerWithCompany && customer != null && customer.Company != null)
        {
            customerCompanyName = customer.Company.CompanyName;
        }

        // 2. Check if company exists with this email
        var company = await _context.Companies
            .FirstOrDefaultAsync(c => c.Email.ToLower() == email);

        bool companyExists = company != null;

        // 3. Check if Aegis user exists (check both original email and lowercase)
        // Use case-insensitive comparison to catch all variations
        var aegisUser = await _context.AegisUsers
            .FirstOrDefaultAsync(u => u.UserId.ToLower() == email || u.UserId.ToLower() == request.Email.ToLower());

        bool aegisUserExists = aegisUser != null;
        bool aegisUserCreated = false;

        // 3.5. If Aegis user exists, verify password and check for customer with admin role
        if (aegisUserExists && aegisUser != null)
        {
            // Verify password matches
            if (BCrypt.Net.BCrypt.Verify(password, aegisUser.PasswordHash))
            {
                // Check if customer exists with this email and has admin role
                var customerWithAdminRole = await _context.Customers
                    .Include(c => c.Company)
                    .FirstOrDefaultAsync(c => EF.Functions.ILike(c.Email, email) && 
                        (c.Role.ToLower() == "admin" || c.Role.ToLower() == "mainadmin"));

                if (customerWithAdminRole != null && customerWithAdminRole.CompanyId.HasValue)
                {
                    // Customer exists with admin role and has a company - authenticate and redirect to edit company page
                    string adminToken = _jwtService.GenerateToken(aegisUser.Id.ToString(), aegisUser.Role);
                    
                    // Update last login
                    aegisUser.LastLogin = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                    
                    AegisAdminResponseDto adminUserDto = new AegisAdminResponseDto
                    {
                        UserId = aegisUser.UserId,
                        AegisUserId = aegisUser.Id,
                        FirstName = aegisUser.FirstName,
                        LastName = aegisUser.LastName,
                        Phone = aegisUser.Phone,
                        Role = aegisUser.Role,
                        IsActive = aegisUser.IsActive,
                        LastLogin = aegisUser.LastLogin,
                        CreatedAt = aegisUser.CreatedAt
                    };

                    return Ok(new
                    {
                        email = request.Email,
                        emailAvailable = false,
                        hasCustomerWithCompany = true,
                        customerCompanyName = customerWithAdminRole.Company?.CompanyName ?? (string?)null,
                        companyExists = true,
                        companyId = customerWithAdminRole.CompanyId.Value,
                        aegisUserExists = true,
                        aegisUserCreated = false,
                        token = adminToken,
                        user = adminUserDto,
                        authenticated = true,
                        redirectTo = $"/companies/{customerWithAdminRole.CompanyId.Value}",
                        messages = new List<string> { "Authentication successful", $"Redirecting to company: {customerWithAdminRole.Company?.CompanyName ?? "Unknown"}" },
                        message = $"Authentication successful. Redirecting to company: {customerWithAdminRole.Company?.CompanyName ?? "Unknown"}"
                    });
                }
                else if (customerWithAdminRole != null && !customerWithAdminRole.CompanyId.HasValue)
                {
                    // Customer exists with admin role but no company - show error
                    return Ok(new
                    {
                        email = request.Email,
                        emailAvailable = false,
                        hasCustomerWithCompany = false,
                        customerCompanyName = (string?)null,
                        companyExists = false,
                        aegisUserExists = true,
                        aegisUserCreated = false,
                        token = (string?)null,
                        user = (AegisAdminResponseDto?)null,
                        authenticated = false,
                        redirectTo = (string?)null,
                        messages = new List<string> { "Error", "Customer account exists with admin role but no company is associated. Please contact support." },
                        message = "Customer account exists with admin role but no company is associated. Please contact support."
                    });
                }
                // If customer doesn't exist or doesn't have admin role, continue with normal flow
            }
            else
            {
                // Password doesn't match - show error
                return Ok(new
                {
                    email = request.Email,
                    emailAvailable = false,
                    hasCustomerWithCompany = hasCustomerWithCompany,
                    customerCompanyName = customerCompanyName ?? (string?)null,
                    companyExists = companyExists,
                    aegisUserExists = true,
                    aegisUserCreated = false,
                    token = (string?)null,
                    user = (AegisAdminResponseDto?)null,
                    authenticated = false,
                    redirectTo = (string?)null,
                    messages = new List<string> { "Error", "Invalid password. Please check your password and try again." },
                    message = "Invalid password. Please check your password and try again."
                });
            }
        }

        // 4. If email is available (no customer with company) and Aegis user doesn't exist, create it
        bool emailAvailable = !hasCustomerWithCompany;
        
        if (emailAvailable && !aegisUserExists)
        {
            // Final check right before creating - use a transaction-like approach
            // Check one more time with a fresh query to ensure user doesn't exist
            var finalCheck = await _context.AegisUsers
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId.ToLower() == email || u.UserId.ToLower() == request.Email.ToLower());
            
            if (finalCheck != null)
            {
                // User was created between our checks - use existing user
                _logger.LogInformation("Aegis user found on final check (race condition). Email: {Email}", request.Email);
                aegisUser = await _context.AegisUsers
                    .FirstOrDefaultAsync(u => u.UserId.ToLower() == email || u.UserId.ToLower() == request.Email.ToLower());
                aegisUserExists = aegisUser != null;
            }
            else
            {
                // User definitely doesn't exist - safe to create
                try
                {
                    // Extract name from email (before @) or use defaults
                    var emailParts = email.Split('@');
                    var namePart = emailParts.Length > 0 && !string.IsNullOrEmpty(emailParts[0]) ? emailParts[0] : "User";
                    var firstNameParts = namePart.Split('.');
                    var firstName = firstNameParts.Length > 0 && !string.IsNullOrEmpty(firstNameParts[0]) ? firstNameParts[0] : "User";
                    var lastNameParts = namePart.Contains('.') ? namePart.Split('.').Skip(1).ToArray() : Array.Empty<string>();
                    var lastName = lastNameParts.Length > 0 ? string.Join(" ", lastNameParts) : "";

                    // If lastName is empty, use a default value (backend requires non-empty)
                    if (string.IsNullOrWhiteSpace(lastName))
                    {
                        lastName = "User";
                    }

                    // Capitalize names
                    if (firstName.Length > 0)
                    {
                        firstName = char.ToUpperInvariant(firstName[0]) + (firstName.Length > 1 ? firstName.Substring(1).ToLowerInvariant() : "");
                    }
                    lastName = string.Join(" ", lastName.Split(' ').Select(n => 
                        n.Length > 0 ? char.ToUpperInvariant(n[0]) + (n.Length > 1 ? n.Substring(1).ToLowerInvariant() : "") : n));

                    // Create new Aegis user
                    var newAegisUser = new AegisUser
                    {
                        UserId = request.Email, // Use original email case
                        FirstName = firstName,
                        LastName = lastName,
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                        Role = "designer",
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    // Check one final time before adding to context - use a fresh query
                    var preAddCheck = await _context.AegisUsers
                        .AsNoTracking()
                        .AnyAsync(u => u.UserId.ToLower() == request.Email.ToLower() || u.UserId.ToLower() == email);
                    
                    if (preAddCheck)
                    {
                        // User was just created - fetch it
                        _logger.LogInformation("Aegis user found just before add (race condition). Email: {Email}", request.Email);
                        aegisUser = await _context.AegisUsers
                            .FirstOrDefaultAsync(u => u.UserId.ToLower() == email || u.UserId.ToLower() == request.Email.ToLower());
                        aegisUserExists = aegisUser != null;
                        // Don't create - user already exists
                    }
                    else
                    {
                        // Final check: ensure the entity isn't already being tracked
                        var trackedEntity = _context.ChangeTracker.Entries<AegisUser>()
                            .FirstOrDefault(e => e.Entity.UserId.ToLower() == request.Email.ToLower() || e.Entity.UserId.ToLower() == email);
                        
                        if (trackedEntity != null)
                        {
                            // Entity is already being tracked - use it
                            _logger.LogInformation("Aegis user found in change tracker (race condition). Email: {Email}", request.Email);
                            aegisUser = trackedEntity.Entity;
                            aegisUserExists = true;
                        }
                        else
                        {
                            // Safe to add - no user exists and none is being tracked
                            _context.AegisUsers.Add(newAegisUser);
                            try
                            {
                                await _context.SaveChangesAsync();
                                aegisUserCreated = true;
                                aegisUser = newAegisUser;
                                _logger.LogInformation("Aegis user created during email check. Email: {Email}, AegisUserId: {AegisUserId}", 
                                    request.Email, newAegisUser.Id);
                            }
                            catch (Microsoft.EntityFrameworkCore.DbUpdateException saveEx)
                            {
                                // If SaveChanges fails with duplicate key, fetch existing user
                                var innerEx = saveEx.InnerException;
                                if (innerEx != null && innerEx.Message.Contains("duplicate key") && innerEx.Message.Contains("aegis_users_userid_key"))
                                {
                                    _logger.LogWarning("Duplicate key during SaveChanges - fetching existing user. Email: {Email}", request.Email);
                                    // Remove the entity we tried to add
                                    _context.Entry(newAegisUser).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
                                    
                                    // Fetch the existing user
                                    aegisUser = await _context.AegisUsers
                                        .FirstOrDefaultAsync(u => u.UserId.ToLower() == email || u.UserId.ToLower() == request.Email.ToLower());
                                    aegisUserExists = aegisUser != null;
                                    aegisUserCreated = false;
                                }
                                else
                                {
                                    throw; // Re-throw if it's a different error
                                }
                            }
                        }
                    }
                }
                catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
                {
                    // Check if it's a duplicate key error (PostgreSQL error code 23505)
                    var innerException = ex.InnerException;
                    if (innerException != null && innerException.Message.Contains("duplicate key") && innerException.Message.Contains("aegis_users_userid_key"))
                    {
                        // Duplicate key error - user was created between our check and save
                        // Re-fetch the user
                        _logger.LogWarning("Aegis user already exists (race condition or duplicate). Email: {Email}", request.Email);
                        aegisUser = await _context.AegisUsers
                            .FirstOrDefaultAsync(u => u.UserId.ToLower() == email || u.UserId.ToLower() == request.Email.ToLower());
                        aegisUserExists = aegisUser != null;
                        aegisUserCreated = false; // User already existed, we didn't create it
                    }
                    else
                    {
                        // Re-throw if it's a different error
                        _logger.LogError(ex, "Error creating Aegis user. Email: {Email}", request.Email);
                        throw;
                    }
                }
            }
        }

        // 5. If email is available and Aegis user exists (created or already existed), generate token
        string? token = null;
        AegisAdminResponseDto? userDto = null;
        if (emailAvailable && aegisUser != null)
        {
            // Verify password matches (if user was just created, password is already hashed)
            // If user already existed, we need to verify the password
            if (aegisUserCreated || BCrypt.Net.BCrypt.Verify(password, aegisUser.PasswordHash))
            {
                // Generate JWT token
                token = _jwtService.GenerateToken(aegisUser.Id.ToString(), aegisUser.Role);
                
                // Create user DTO for response
                userDto = new AegisAdminResponseDto
                {
                    UserId = aegisUser.UserId,
                    AegisUserId = aegisUser.Id,
                    FirstName = aegisUser.FirstName,
                    LastName = aegisUser.LastName,
                    Phone = aegisUser.Phone,
                    Role = aegisUser.Role,
                    IsActive = aegisUser.IsActive,
                    LastLogin = aegisUser.LastLogin,
                    CreatedAt = aegisUser.CreatedAt
                };
                
                // Update last login if user already existed
                if (!aegisUserCreated && aegisUser != null)
                {
                    aegisUser.LastLogin = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }
            }
        }

        // Build messages based on status
        var messages = new List<string>();
        
        if (emailAvailable)
        {
            messages.Add("Email Available");
            messages.Add("This email is available for registration.");
        }
        else
        {
            // Email not available - show error
            if (hasCustomerWithCompany)
            {
                messages.Add($"Error: This email is registered to {customerCompanyName}");
            }
            else
            {
                messages.Add("Error: Email is not available for registration");
            }
        }
        
        if (!companyExists)
        {
            messages.Add("No Company Found");
            messages.Add("No company exists with this email address.");
        }
        
        if (emailAvailable && !aegisUserExists && !aegisUserCreated)
        {
            messages.Add("Use this email and password to create an Aegis admin account");
            messages.Add("The account will have the \"designer\" role");
            messages.Add("After creation, you can log in and create a company");
        }

        // Return response
        return Ok(new
        {
            email = request.Email,
            emailAvailable = emailAvailable,
            hasCustomerWithCompany = hasCustomerWithCompany,
            customerCompanyName = customerCompanyName,
            companyExists = companyExists,
            aegisUserExists = aegisUserExists || aegisUserCreated,
            aegisUserCreated = aegisUserCreated,
            token = token, // Include token if authentication successful
            user = userDto, // Include user info if authentication successful
            authenticated = token != null,
            redirectTo = token != null ? (emailAvailable ? "/companies/new" : null) : null, // Redirect URL if authenticated (admin role redirects are handled earlier)
            messages = messages, // Include all messages
            message = hasCustomerWithCompany 
                ? $"Error: This email is registered to {customerCompanyName}"
                : (!emailAvailable ? "Error: Email is not available for registration" : (messages.Count > 0 ? string.Join(" ", messages) : "Email Available"))
        });
    }

    /// <summary>
    /// Logout aegis admin user
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    public IActionResult Logout()
    {
        return Ok(new { message = "Logged out successfully" });
    }
}

public class CheckEmailAndSetupRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

