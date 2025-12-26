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

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CarRental.Api.Data;
using CarRental.Api.Models;
using CarRental.Api.DTOs;
using CarRental.Api.Services;

namespace CarRental.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "admin,mainadmin")]
public class StripeSettingsController : ControllerBase
{
    private readonly CarRentalDbContext _context;
    private readonly ILogger<StripeSettingsController> _logger;

    public StripeSettingsController(CarRentalDbContext context, ILogger<StripeSettingsController> logger)
    {
        _context = context;
        _logger = logger;
    }


    /// <summary>
    /// Get all Stripe settings
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<StripeSettingsDto>>> GetStripeSettings()
    {
        try
        {
            _logger.LogInformation("[StripeSettings] Fetching all Stripe settings from database");
            
            var settings = await _context.StripeSettings
                .OrderBy(s => s.Name)
                .ToListAsync();

            _logger.LogInformation("[StripeSettings] Retrieved {Count} settings from database", settings.Count);

            // Return keys as-is (encrypted) - GUI will decrypt them
            var result = settings.Select(s => new StripeSettingsDto
            {
                Id = s.Id,
                Name = s.Name,
                SecretKey = s.SecretKey,
                PublishableKey = s.PublishableKey,
                WebhookSecret = s.WebhookSecret,
                CreatedAt = s.CreatedAt,
                UpdatedAt = s.UpdatedAt
            }).ToList();

            _logger.LogInformation("[StripeSettings] Returning {Count} settings to client", result.Count);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving Stripe settings from database");
            return StatusCode(500, new { error = "Failed to retrieve Stripe settings" });
        }
    }

    /// <summary>
    /// Get a specific Stripe setting by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<StripeSettingsDto>> GetStripeSetting(Guid id)
    {
        try
        {
            var setting = await _context.StripeSettings
                .FirstOrDefaultAsync(s => s.Id == id);

            if (setting == null)
                return NotFound(new { error = "Stripe setting not found" });

            // Return keys as-is (encrypted) - GUI will decrypt them
            var dto = new StripeSettingsDto
            {
                Id = setting.Id,
                Name = setting.Name,
                SecretKey = setting.SecretKey,
                PublishableKey = setting.PublishableKey,
                WebhookSecret = setting.WebhookSecret,
                CreatedAt = setting.CreatedAt,
                UpdatedAt = setting.UpdatedAt
            };

            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving Stripe setting {Id}", id);
            return StatusCode(500, new { error = "Failed to retrieve Stripe setting" });
        }
    }

    /// <summary>
    /// Create a new Stripe setting
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<StripeSettingsDto>> CreateStripeSetting([FromBody] CreateStripeSettingsDto dto)
    {
        try
        {
            // Validate name uniqueness
            var existing = await _context.StripeSettings
                .FirstOrDefaultAsync(s => s.Name == dto.Name);

            if (existing != null)
                return BadRequest(new { error = $"Stripe setting with name '{dto.Name}' already exists" });

            // Store keys as-is (GUI already encrypts them before sending)
            var setting = new StripeSettings
            {
                Id = Guid.NewGuid(),
                Name = dto.Name,
                SecretKey = dto.SecretKey,
                PublishableKey = dto.PublishableKey,
                WebhookSecret = dto.WebhookSecret,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.StripeSettings.Add(setting);
            await _context.SaveChangesAsync();

            // Return keys as-is (encrypted) - GUI will decrypt them
            var result = new StripeSettingsDto
            {
                Id = setting.Id,
                Name = setting.Name,
                SecretKey = setting.SecretKey,
                PublishableKey = setting.PublishableKey,
                WebhookSecret = setting.WebhookSecret,
                CreatedAt = setting.CreatedAt,
                UpdatedAt = setting.UpdatedAt
            };

            return CreatedAtAction(nameof(GetStripeSetting), new { id = setting.Id }, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Stripe setting");
            return StatusCode(500, new { error = "Failed to create Stripe setting" });
        }
    }

    /// <summary>
    /// Update an existing Stripe setting
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<StripeSettingsDto>> UpdateStripeSetting(Guid id, [FromBody] UpdateStripeSettingsDto dto)
    {
        try
        {
            var setting = await _context.StripeSettings.FindAsync(id);

            if (setting == null)
                return NotFound(new { error = "Stripe setting not found" });

            // Validate name uniqueness if name is being changed
            if (dto.Name != null && dto.Name != setting.Name)
            {
                var existing = await _context.StripeSettings
                    .FirstOrDefaultAsync(s => s.Name == dto.Name && s.Id != id);

                if (existing != null)
                    return BadRequest(new { error = $"Stripe setting with name '{dto.Name}' already exists" });
            }

            // Update fields (keys are already encrypted by GUI)
            if (dto.Name != null)
                setting.Name = dto.Name;
            if (dto.SecretKey != null)
                setting.SecretKey = dto.SecretKey;
            if (dto.PublishableKey != null)
                setting.PublishableKey = dto.PublishableKey;
            if (dto.WebhookSecret != null)
                setting.WebhookSecret = dto.WebhookSecret;
            setting.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Return keys as-is (encrypted) - GUI will decrypt them
            var result = new StripeSettingsDto
            {
                Id = setting.Id,
                Name = setting.Name,
                SecretKey = setting.SecretKey,
                PublishableKey = setting.PublishableKey,
                WebhookSecret = setting.WebhookSecret,
                CreatedAt = setting.CreatedAt,
                UpdatedAt = setting.UpdatedAt
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating Stripe setting {Id}", id);
            return StatusCode(500, new { error = "Failed to update Stripe setting" });
        }
    }

    /// <summary>
    /// Test connection to Stripe using the specified settings
    /// </summary>
    [HttpPost("{id}/test-connection")]
    public async Task<IActionResult> TestStripeConnection(Guid id)
    {
        try
        {
            var setting = await _context.StripeSettings
                .FirstOrDefaultAsync(s => s.Id == id);

            if (setting == null)
                return NotFound(new { success = false, error = "Stripe setting not found" });

            if (string.IsNullOrEmpty(setting.SecretKey))
                return BadRequest(new { success = false, error = "Secret key is not configured" });

            // Decrypt the secret key
            string secretKey;
            try
            {
                var encryptionService = HttpContext.RequestServices.GetRequiredService<IEncryptionService>();
                secretKey = encryptionService.Decrypt(setting.SecretKey);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to decrypt secret key for setting {Id}, trying as plaintext", id);
                secretKey = setting.SecretKey;
            }

            // Validate key format
            if (!secretKey.StartsWith("sk_"))
            {
                return BadRequest(new { 
                    success = false, 
                    error = "Invalid secret key format. Key should start with 'sk_'" 
                });
            }

            // Determine environment from key
            var isTestMode = secretKey.StartsWith("sk_test_");
            var isLiveMode = secretKey.StartsWith("sk_live_");

            // Test connection by retrieving account balance
            try
            {
                var requestOptions = new Stripe.RequestOptions { ApiKey = secretKey };
                var balanceService = new Stripe.BalanceService();
                var balance = await balanceService.GetAsync(requestOptions: requestOptions);

                // Connection successful
                return Ok(new { 
                    success = true, 
                    message = "Connection successful",
                    environment = isTestMode ? "test" : (isLiveMode ? "live" : "unknown"),
                    settingsName = setting.Name,
                    availableBalance = balance.Available?.FirstOrDefault()?.Amount ?? 0,
                    currency = balance.Available?.FirstOrDefault()?.Currency ?? "usd"
                });
            }
            catch (Stripe.StripeException stripeEx)
            {
                _logger.LogWarning(stripeEx, "Stripe connection test failed for setting {Id}: {Message}", id, stripeEx.Message);
                
                string errorMessage = stripeEx.StripeError?.Code switch
                {
                    "api_key_expired" => "API key has expired",
                    "invalid_api_key" => "Invalid API key",
                    "rate_limit" => "Rate limit exceeded, please try again later",
                    _ => stripeEx.StripeError?.Message ?? stripeEx.Message
                };

                return Ok(new { 
                    success = false, 
                    error = errorMessage,
                    stripeErrorCode = stripeEx.StripeError?.Code,
                    environment = isTestMode ? "test" : (isLiveMode ? "live" : "unknown")
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing Stripe connection for setting {Id}", id);
            return StatusCode(500, new { success = false, error = "Failed to test Stripe connection" });
        }
    }

    /// <summary>
    /// Delete a Stripe setting
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteStripeSetting(Guid id)
    {
        try
        {
            var setting = await _context.StripeSettings
                .FirstOrDefaultAsync(s => s.Id == id);

            if (setting == null)
                return NotFound(new { error = "Stripe setting not found" });

            // Check if any companies reference this setting via stripe_company table
            var stripeCompaniesUsing = await _context.StripeCompanies
                .AnyAsync(sc => sc.SettingsId == id);

            if (stripeCompaniesUsing)
            {
                return BadRequest(new { error = "Cannot delete Stripe setting that is in use by companies" });
            }

            // Check if any companies reference this setting via companies.stripe_settings_id
            var companiesUsing = await _context.Companies
                .AnyAsync(c => c.StripeSettingsId == id);

            if (companiesUsing)
            {
                return BadRequest(new { error = "Cannot delete Stripe setting that is in use by companies" });
            }

            _context.StripeSettings.Remove(setting);
            await _context.SaveChangesAsync();

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting Stripe setting {Id}", id);
            return StatusCode(500, new { error = "Failed to delete Stripe setting" });
        }
    }
}

