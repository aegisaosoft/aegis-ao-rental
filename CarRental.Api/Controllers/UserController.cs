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
[Authorize]
public class UserController : ControllerBase
{
    private readonly CarRentalDbContext _context;
    private readonly IJwtService _jwtService;
    private readonly ILogger<UserController> _logger;

    public UserController(
        CarRentalDbContext context,
        IJwtService jwtService,
        ILogger<UserController> logger)
    {
        _context = context;
        _jwtService = jwtService;
        _logger = logger;
    }

    /// <summary>
    /// Get all users (admin/mainadmin only)
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "admin,mainadmin")]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetUsers()
    {
        var users = await _context.Users
            .Include(u => u.Company)
            .Select(u => new UserDto
            {
                UserId = u.UserId,
                Email = u.Email,
                FirstName = u.FirstName,
                LastName = u.LastName,
                Phone = u.Phone,
                Role = u.Role,
                CompanyId = u.CompanyId,
                CompanyName = u.Company != null ? u.Company.CompanyName : null,
                IsActive = u.IsActive,
                LastLogin = u.LastLogin,
                CreatedAt = u.CreatedAt,
                UpdatedAt = u.UpdatedAt
            })
            .ToListAsync();

        return Ok(users);
    }

    /// <summary>
    /// Get user by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<UserDto>> GetUser(Guid id)
    {
        var currentUserId = GetCurrentUserId();
        var currentUserRole = GetCurrentUserRole();

        // Users can only view their own profile unless they're admin/mainadmin
        if (currentUserId != id && !IsAdminOrMainAdmin(currentUserRole))
        {
            return Forbid();
        }

        var user = await _context.Users
            .Include(u => u.Company)
            .FirstOrDefaultAsync(u => u.UserId == id);

        if (user == null)
        {
            return NotFound(new { message = "User not found" });
        }

        var userDto = new UserDto
        {
            UserId = user.UserId,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Phone = user.Phone,
            Role = user.Role,
            CompanyId = user.CompanyId,
            CompanyName = user.Company?.CompanyName,
            IsActive = user.IsActive,
            LastLogin = user.LastLogin,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };

        return Ok(userDto);
    }

    /// <summary>
    /// Create a new user (admin/mainadmin only)
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "admin,mainadmin")]
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<ActionResult<UserDto>> CreateUser([FromBody] UserRegisterDto dto)
    {
        // Check if email already exists
        if (await _context.Users.AnyAsync(u => u.Email == dto.Email))
        {
            return BadRequest(new { message = "Email already registered" });
        }

        // Validate role
        if (!IsValidRole(dto.Role))
        {
            return BadRequest(new { message = "Invalid role. Must be worker, admin, or mainadmin" });
        }

        // Validate company assignment
        if (dto.CompanyId.HasValue)
        {
            var companyExists = await _context.RentalCompanies.AnyAsync(c => c.CompanyId == dto.CompanyId.Value);
            if (!companyExists)
            {
                return BadRequest(new { message = "Company not found" });
            }
        }

        var user = new User
        {
            Email = dto.Email,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Phone = dto.Phone,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Role = dto.Role,
            CompanyId = dto.CompanyId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var userDto = new UserDto
        {
            UserId = user.UserId,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Phone = user.Phone,
            Role = user.Role,
            CompanyId = user.CompanyId,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };

        return CreatedAtAction(nameof(GetUser), new { id = user.UserId }, userDto);
    }

    /// <summary>
    /// Update user (admin/mainadmin or own profile)
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<UserDto>> UpdateUser(Guid id, [FromBody] UserUpdateDto dto)
    {
        var currentUserId = GetCurrentUserId();
        var currentUserRole = GetCurrentUserRole();

        // Users can only update their own profile unless they're admin/mainadmin
        if (currentUserId != id && !IsAdminOrMainAdmin(currentUserRole))
        {
            return Forbid();
        }

        var user = await _context.Users.FindAsync(id);
        if (user == null)
        {
            return NotFound(new { message = "User not found" });
        }

        // Non-admin users can't change their role or company
        if (!IsAdminOrMainAdmin(currentUserRole))
        {
            dto.Role = null;
            dto.CompanyId = null;
            dto.IsActive = null;
        }

        // Validate role if provided
        if (!string.IsNullOrEmpty(dto.Role) && !IsValidRole(dto.Role))
        {
            return BadRequest(new { message = "Invalid role. Must be worker, admin, or mainadmin" });
        }

        // Validate company assignment if provided
        if (dto.CompanyId.HasValue)
        {
            var companyExists = await _context.RentalCompanies.AnyAsync(c => c.CompanyId == dto.CompanyId.Value);
            if (!companyExists)
            {
                return BadRequest(new { message = "Company not found" });
            }
        }

        // Update fields
        if (!string.IsNullOrEmpty(dto.FirstName))
            user.FirstName = dto.FirstName;
        if (!string.IsNullOrEmpty(dto.LastName))
            user.LastName = dto.LastName;
        if (dto.Phone != null)
            user.Phone = dto.Phone;
        if (!string.IsNullOrEmpty(dto.Role))
            user.Role = dto.Role;
        if (dto.CompanyId.HasValue)
            user.CompanyId = dto.CompanyId;
        if (dto.IsActive.HasValue)
            user.IsActive = dto.IsActive.Value;

        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var userDto = new UserDto
        {
            UserId = user.UserId,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Phone = user.Phone,
            Role = user.Role,
            CompanyId = user.CompanyId,
            IsActive = user.IsActive,
            LastLogin = user.LastLogin,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };

        return Ok(userDto);
    }

    /// <summary>
    /// Change password
    /// </summary>
    [HttpPost("{id}/change-password")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<ActionResult> ChangePassword(Guid id, [FromBody] ChangePasswordDto dto)
    {
        var currentUserId = GetCurrentUserId();
        var currentUserRole = GetCurrentUserRole();

        // Users can only change their own password unless they're admin/mainadmin
        if (currentUserId != id && !IsAdminOrMainAdmin(currentUserRole))
        {
            return Forbid();
        }

        var user = await _context.Users.FindAsync(id);
        if (user == null)
        {
            return NotFound(new { message = "User not found" });
        }

        // Verify current password
        if (!BCrypt.Net.BCrypt.Verify(dto.CurrentPassword, user.PasswordHash))
        {
            return BadRequest(new { message = "Current password is incorrect" });
        }

        // Update password
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new { message = "Password changed successfully" });
    }

    /// <summary>
    /// Delete user (mainadmin only)
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "mainadmin")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<ActionResult> DeleteUser(Guid id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
        {
            return NotFound(new { message = "User not found" });
        }

        // Prevent deleting own account
        var currentUserId = GetCurrentUserId();
        if (currentUserId == id)
        {
            return BadRequest(new { message = "Cannot delete your own account" });
        }

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// Get current user profile
    /// </summary>
    [HttpGet("profile")]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    public async Task<ActionResult<UserDto>> GetProfile()
    {
        var userId = GetCurrentUserId();
        return await GetUser(userId);
    }

    // Helper methods
    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("Invalid token");
        }
        return userId;
    }

    private string GetCurrentUserRole()
    {
        return User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "worker";
    }

    private bool IsAdminOrMainAdmin(string role)
    {
        return role == "admin" || role == "mainadmin";
    }

    private bool IsValidRole(string role)
    {
        return role == "worker" || role == "admin" || role == "mainadmin";
    }
}
