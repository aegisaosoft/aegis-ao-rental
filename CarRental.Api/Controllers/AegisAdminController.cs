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
    /// Register a new aegis admin/agent user
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    public async Task<ActionResult<object>> Register([FromBody] AegisAdminRegisterDto dto)
    {
        // Check if userid already exists (case-insensitive)
        if (await _context.AegisUsers.AnyAsync(u => u.UserId.ToLower() == dto.UserId.ToLower()))
        {
            return BadRequest(new { message = "User ID already registered" });
        }

        // Validate role
        if (dto.Role != "agent" && dto.Role != "admin")
        {
            return BadRequest(new { message = "Invalid role. Must be 'agent' or 'admin'" });
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

        // Generate JWT token
        var token = _jwtService.GenerateToken(aegisUser.Id.ToString(), aegisUser.Role);

        return CreatedAtAction(nameof(Register), new LoginResponseDto
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
    public async Task<ActionResult<AegisAdminResponseDto>> GetProfile()
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

        return Ok(userDto);
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

