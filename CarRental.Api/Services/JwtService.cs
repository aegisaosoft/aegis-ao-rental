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

using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace CarRental.Api.Services;

public interface IJwtService
{
    string GenerateToken(string customerId, string role = "customer", string? companyId = null, string? companyName = null);
    ClaimsPrincipal? ValidateToken(string token);
}

public class JwtService : IJwtService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<JwtService> _logger;

    public JwtService(IConfiguration configuration, ILogger<JwtService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Decodes JWT secret key from string to bytes.
    /// Tries base64 decoding first, falls back to UTF-8 encoding for backward compatibility.
    /// This method is used by both Program.cs (for middleware configuration) and JwtService (for token generation/validation).
    /// </summary>
    public static byte[] DecodeJwtKey(string? secretKey, ILogger? logger = null)
    {
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            throw new InvalidOperationException("JWT secret key is null or empty");
        }

        try
        {
            // Trim whitespace that might be in config
            var trimmedKey = secretKey.Trim();
            var keyBytes = Convert.FromBase64String(trimmedKey);
            logger?.LogInformation("[JwtService] Secret key decoded as base64, key length: {Length} bytes", keyBytes.Length);
            return keyBytes;
        }
        catch (FormatException ex)
        {
            logger?.LogWarning(ex, "[JwtService] Failed to decode secret key as base64, falling back to UTF-8 encoding.");
            // Fallback: try UTF-8 encoding (for backward compatibility)
            var keyBytes = Encoding.UTF8.GetBytes(secretKey);
            logger?.LogInformation("[JwtService] Secret key treated as UTF-8, key length: {Length} bytes", keyBytes.Length);
            return keyBytes;
        }
    }

    public string GenerateToken(string customerId, string role = "customer", string? companyId = null, string? companyName = null)
    {
        // Use same configuration reading logic as Program.cs to ensure consistency
        var jwtSettings = _configuration.GetSection("Jwt");
        var jwtSettingsLegacy = _configuration.GetSection("JwtSettings");
        var secretKey = jwtSettings["Key"] ?? jwtSettingsLegacy["SecretKey"] ?? "e8Xgin/OtynoYVm8o7jiNjB9/Fke1Q6RxjH3hJsRpTE=";
        var issuer = jwtSettings["Issuer"] ?? jwtSettingsLegacy["Issuer"] ?? "CarRentalAPI";
        var audience = jwtSettings["Audience"] ?? jwtSettingsLegacy["Audience"] ?? "CarRentalClients";
        var expiryMinutes = int.Parse(jwtSettingsLegacy["ExpiryMinutes"] ?? "60");

        _logger.LogInformation("[JwtService] Generating token with key from: {Source}", 
            jwtSettings["Key"] != null ? "Jwt:Key" : "JwtSettings:SecretKey");

        // Convert key to bytes using shared helper method
        var key = DecodeJwtKey(secretKey, _logger);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, customerId),
            new Claim("customer_id", customerId)
        };

        // Add role claims - if mainadmin, also add admin role for compatibility
        claims.Add(new Claim(ClaimTypes.Role, role));
        if (role.ToLowerInvariant() == "mainadmin")
        {
            // Mainadmin should also have admin role
            claims.Add(new Claim(ClaimTypes.Role, "admin"));
        }

        // Add company information if provided (for workers/admins)
        if (!string.IsNullOrEmpty(companyId))
        {
            claims.Add(new Claim("company_id", companyId));
        }
        if (!string.IsNullOrEmpty(companyName))
        {
            claims.Add(new Claim("company_name", companyName));
        }

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(expiryMinutes),
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature)
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    public ClaimsPrincipal? ValidateToken(string token)
    {
        try
        {
            // Use same configuration reading logic as Program.cs to ensure consistency
            var jwtSettings = _configuration.GetSection("Jwt");
            var jwtSettingsLegacy = _configuration.GetSection("JwtSettings");
            var secretKey = jwtSettings["Key"] ?? jwtSettingsLegacy["SecretKey"] ?? "e8Xgin/OtynoYVm8o7jiNjB9/Fke1Q6RxjH3hJsRpTE=";
            var issuer = jwtSettings["Issuer"] ?? jwtSettingsLegacy["Issuer"] ?? "CarRentalAPI";
            var audience = jwtSettings["Audience"] ?? jwtSettingsLegacy["Audience"] ?? "CarRentalClients";

            // Convert key to bytes using shared helper method
            var key = DecodeJwtKey(secretKey, _logger);

            var tokenHandler = new JwtSecurityTokenHandler();
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = issuer,
                ValidateAudience = true,
                ValidAudience = audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            var principal = tokenHandler.ValidateToken(token, validationParameters, out SecurityToken validatedToken);
            return principal;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "JWT token validation failed");
            return null;
        }
    }
}
