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
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly CarRentalDbContext _context;
    private readonly IJwtService _jwtService;
    private readonly ISessionService _sessionService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        CarRentalDbContext context,
        IJwtService jwtService,
        ISessionService sessionService,
        ILogger<AuthController> logger)
    {
        _context = context;
        _jwtService = jwtService;
        _sessionService = sessionService;
        _logger = logger;
    }

    /// <summary>
    /// Register a new customer
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    public async Task<ActionResult<object>> Register([FromBody] RegisterDto dto)
    {
        // Check if email already exists
        if (await _context.Customers.AnyAsync(c => c.Email == dto.Email))
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
        var token = _jwtService.GenerateToken(customer.CustomerId.ToString(), "user");

        return CreatedAtAction(nameof(Register), new
        {
            token,
            user = new
            {
                customerId = customer.CustomerId,
                email = customer.Email,
                firstName = customer.FirstName,
                lastName = customer.LastName,
                phone = customer.Phone,
                isVerified = customer.IsVerified
            }
        });
    }

    /// <summary>
    /// Login customer (supports all roles: customer, worker, admin, mainadmin)
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    public async Task<ActionResult<object>> Login([FromBody] LoginDto dto)
    {
        var customer = await _context.Customers
            .Include(c => c.Company)
            .FirstOrDefaultAsync(c => c.Email == dto.Email);

        if (customer == null || customer.PasswordHash == null || !customer.IsActive || !BCrypt.Net.BCrypt.Verify(dto.Password, customer.PasswordHash))
        {
            return Unauthorized(new { message = "Invalid email or password" });
        }

        // Update last login
        customer.LastLogin = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Generate JWT token with customer role and company information
        var token = _jwtService.GenerateToken(
            customer.CustomerId.ToString(), 
            customer.Role, 
            customer.CompanyId?.ToString(), 
            customer.Company?.CompanyName
        );

        // Store customer session information
        var session = new CustomerSession
        {
            CustomerId = customer.CustomerId.ToString(),
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

        return Ok(new
        {
            token,
            user = new
            {
                customerId = customer.CustomerId,
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

        // Get customer profile (works for all roles: customer, worker, admin, mainadmin)
        var customer = await _context.Customers
            .Include(c => c.Company)
            .FirstOrDefaultAsync(c => c.CustomerId == customerId);

        if (customer == null)
        {
            return NotFound(new { message = "Customer not found" });
        }

        var customerDto = new CustomerDto
        {
            CustomerId = customer.CustomerId,
            Email = customer.Email,
            FirstName = customer.FirstName,
            LastName = customer.LastName,
            Phone = customer.Phone,
            DateOfBirth = customer.DateOfBirth,
            DriversLicenseNumber = customer.DriversLicenseNumber,
            DriversLicenseState = customer.DriversLicenseState,
            DriversLicenseExpiry = customer.DriversLicenseExpiry,
            Address = customer.Address,
            City = customer.City,
            State = customer.State,
            Country = customer.Country,
            PostalCode = customer.PostalCode,
            StripeCustomerId = customer.StripeCustomerId,
            IsVerified = customer.IsVerified,
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
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var customerId))
            {
                return Unauthorized(new { message = "Invalid token" });
            }

            var customer = await _context.Customers.FindAsync(customerId);
            if (customer == null)
            {
                return NotFound(new { message = "Customer not found" });
            }

            // Update fields if provided
            if (!string.IsNullOrEmpty(dto.FirstName))
                customer.FirstName = dto.FirstName;
            
            if (!string.IsNullOrEmpty(dto.LastName))
                customer.LastName = dto.LastName;
            
            if (!string.IsNullOrEmpty(dto.Phone))
                customer.Phone = dto.Phone;
            
            if (!string.IsNullOrEmpty(dto.Email))
                customer.Email = dto.Email;

            customer.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // Load company for response
            await _context.Entry(customer)
                .Reference(c => c.Company)
                .LoadAsync();

            var customerDto = new CustomerDto
            {
                CustomerId = customer.CustomerId,
                Email = customer.Email,
                FirstName = customer.FirstName,
                LastName = customer.LastName,
                Phone = customer.Phone,
                DateOfBirth = customer.DateOfBirth,
                DriversLicenseNumber = customer.DriversLicenseNumber,
                DriversLicenseState = customer.DriversLicenseState,
                DriversLicenseExpiry = customer.DriversLicenseExpiry,
                Address = customer.Address,
                City = customer.City,
                State = customer.State,
                Country = customer.Country,
                PostalCode = customer.PostalCode,
                StripeCustomerId = customer.StripeCustomerId,
                IsVerified = customer.IsVerified,
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
}

public class UpdateCustomerProfileDto
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
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
