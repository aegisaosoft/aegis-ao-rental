using System.ComponentModel.DataAnnotations;
using CarRental.Api.Data;
using CarRental.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CarRental.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = "mainadmin,admin")]
public class UsersController : ControllerBase
{
    private readonly CarRentalDbContext _context;

    public UsersController(CarRentalDbContext context)
    {
        _context = context;
    }

    private string? GetCurrentUserId() =>
        User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    private string? GetCurrentUserRole() =>
        User.FindFirst(ClaimTypes.Role)?.Value ?? User.FindFirst("role")?.Value;

    private static bool IsMainAdmin(string? role) =>
        string.Equals(role, "mainadmin", StringComparison.OrdinalIgnoreCase);

    [HttpGet]
    public async Task<ActionResult<IEnumerable<AegisUserResponse>>> GetUsers([FromQuery] bool includeInactive = true)
    {
        var query = _context.AegisUsers.AsNoTracking();
        if (!includeInactive)
        {
            query = query.Where(u => u.IsActive);
        }

        var users = await query
            .OrderBy(u => u.CreatedAt)
            .ToListAsync();

        return Ok(users.Select(MapToResponse));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AegisUserResponse>> GetUser(Guid id)
    {
        var role = GetCurrentUserRole();
        var user = await _context.AegisUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
        if (user == null)
        {
            return NotFound(new { error = "User not found" });
        }

        if (!IsMainAdmin(role) && string.Equals(user.Role, "mainadmin", StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        return Ok(MapToResponse(user));
    }

    [HttpPost]
    public async Task<ActionResult<AegisUserResponse>> CreateUser([FromBody] CreateUserRequest request)
    {
        var callerRole = GetCurrentUserRole();
        var isMainAdmin = IsMainAdmin(callerRole);
        
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var normalizedUserId = request.UserId.Trim().ToLowerInvariant();
        var existing = await _context.AegisUsers.FirstOrDefaultAsync(u => u.UserId.ToLower() == normalizedUserId);
        if (existing != null)
        {
            return Conflict(new { error = "A user with this login already exists" });
        }

        var normalizedRole = NormalizeRole(request.Role);
        if (normalizedRole == null)
        {
            return BadRequest(new { error = "Invalid role. Allowed values: mainadmin, admin, agent" });
        }

        if (!isMainAdmin && normalizedRole == "mainadmin")
        {
            return Forbid();
        }

        var now = DateTime.UtcNow;
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Trim().Length < 8)
        {
            return BadRequest(new { error = "Password must be at least 8 characters long." });
        }

        var user = new AegisUser
        {
            UserId = request.UserId.Trim(),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            Role = normalizedRole,
            IsActive = request.IsActive ?? true,
            CreatedAt = now,
            UpdatedAt = now,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password.Trim())
        };

        _context.AegisUsers.Add(user);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetUser), new { id = user.Id }, MapToResponse(user));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AegisUserResponse>> UpdateUser(Guid id, [FromBody] UpdateUserRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var role = GetCurrentUserRole();
        var isMainAdmin = IsMainAdmin(role);

        var user = await _context.AegisUsers.FirstOrDefaultAsync(u => u.Id == id);
        if (user == null)
        {
            return NotFound(new { error = "User not found" });
        }

        if (!isMainAdmin && string.Equals(user.Role, "mainadmin", StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        if (!string.IsNullOrWhiteSpace(request.UserId) && !string.Equals(request.UserId.Trim(), user.UserId, StringComparison.OrdinalIgnoreCase))
        {
            var normalized = request.UserId.Trim().ToLowerInvariant();
            var existing = await _context.AegisUsers.AnyAsync(u => u.Id != id && u.UserId.ToLower() == normalized);
            if (existing)
            {
                return Conflict(new { error = "A user with this login already exists" });
            }
            user.UserId = request.UserId.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.FirstName))
        {
            user.FirstName = request.FirstName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.LastName))
        {
            user.LastName = request.LastName.Trim();
        }

        if (request.Phone != null)
        {
            user.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            var normalizedRole = NormalizeRole(request.Role);
            if (normalizedRole == null)
            {
                return BadRequest(new { error = "Invalid role. Allowed values: mainadmin, admin, agent" });
            }
            if (!isMainAdmin && normalizedRole == "mainadmin")
            {
                return Forbid();
            }
            user.Role = normalizedRole;
        }

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            if (!isMainAdmin && !string.Equals(user.Role, "admin", StringComparison.OrdinalIgnoreCase) && !string.Equals(user.Role, "agent", StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            if (request.Password.Trim().Length < 8)
            {
                return BadRequest(new { error = "Password must be at least 8 characters long." });
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password.Trim());
            _context.Entry(user).Property(u => u.PasswordHash).IsModified = true;
        }

        if (request.IsActive.HasValue)
        {
            if (!isMainAdmin)
            {
                return Forbid();
            }
            user.IsActive = request.IsActive.Value;
        }

        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(MapToResponse(user));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        if (!IsMainAdmin(GetCurrentUserRole()))
        {
            return Forbid();
        }

        var user = await _context.AegisUsers.FirstOrDefaultAsync(u => u.Id == id);
        if (user == null)
        {
            return NotFound(new { error = "User not found" });
        }

        _context.AegisUsers.Remove(user);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private static AegisUserResponse MapToResponse(AegisUser user) => new()
    {
        Id = user.Id,
        UserId = user.UserId,
        FirstName = user.FirstName,
        LastName = user.LastName,
        Phone = user.Phone,
        Role = user.Role,
        IsActive = user.IsActive,
        CreatedAt = user.CreatedAt,
        UpdatedAt = user.UpdatedAt,
        LastLogin = user.LastLogin
    };

    public class AegisUserResponse
    {
        public Guid Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string Role { get; set; } = "agent";
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? LastLogin { get; set; }
    }

    public class CreateUserRequest
    {
        [Required]
        [MaxLength(255)]
        public string UserId { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string LastName { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? Phone { get; set; }

        [MaxLength(50)]
        public string Role { get; set; } = "agent";
        public bool? IsActive { get; set; }

        [Required]
        [MinLength(8)]
        [MaxLength(100)]
        public string Password { get; set; } = string.Empty;
    }

    public class UpdateUserRequest
    {
        [MaxLength(255)]
        public string? UserId { get; set; }

        [MaxLength(100)]
        public string? FirstName { get; set; }

        [MaxLength(100)]
        public string? LastName { get; set; }

        [MaxLength(50)]
        public string? Phone { get; set; }

        [MaxLength(50)]
        public string? Role { get; set; }
        public bool? IsActive { get; set; }

        [MinLength(8)]
        [MaxLength(100)]
        public string? Password { get; set; }
    }

    private static string? NormalizeRole(string? role)
    {
        var normalized = string.IsNullOrWhiteSpace(role) ? "agent" : role.Trim().ToLowerInvariant();
        return normalized switch
        {
            "mainadmin" => "mainadmin",
            "admin" => "admin",
            "agent" => "agent",
            _ => null
        };
    }
}

