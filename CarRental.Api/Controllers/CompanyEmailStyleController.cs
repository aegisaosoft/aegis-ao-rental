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
using Microsoft.EntityFrameworkCore;
using CarRental.Api.Data;
using CarRental.Api.Models;
using System.ComponentModel.DataAnnotations;

namespace CarRental.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CompanyEmailStyleController : ControllerBase
{
    private readonly CarRentalDbContext _context;
    private readonly ILogger<CompanyEmailStyleController> _logger;

    public CompanyEmailStyleController(CarRentalDbContext context, ILogger<CompanyEmailStyleController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Get email style for a company
    /// </summary>
    [HttpGet("company/{companyId}")]
    public async Task<ActionResult<CompanyEmailStyle>> GetCompanyEmailStyle(Guid companyId)
    {
        try
        {
            var style = await _context.CompanyEmailStyles
                .FirstOrDefaultAsync(s => s.CompanyId == companyId && s.IsActive);

            if (style == null)
            {
                // Create default style for company
                style = new CompanyEmailStyle
                {
                    CompanyId = companyId,
                    IsActive = true
                };

                _context.CompanyEmailStyles.Add(style);
                await _context.SaveChangesAsync();
            }

            return Ok(style);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting email style for company {CompanyId}", companyId);
            return BadRequest("Error retrieving email style");
        }
    }

    /// <summary>
    /// Create or update email style for a company
    /// </summary>
    [HttpPost("company/{companyId}")]
    public async Task<ActionResult<CompanyEmailStyle>> CreateOrUpdateEmailStyle(Guid companyId, CreateEmailStyleDto createDto)
    {
        try
        {
            // Check if company exists
            var company = await _context.RentalCompanies.FindAsync(companyId);
            if (company == null)
                return NotFound("Company not found");

            // Deactivate existing style
            var existingStyle = await _context.CompanyEmailStyles
                .FirstOrDefaultAsync(s => s.CompanyId == companyId && s.IsActive);

            if (existingStyle != null)
            {
                existingStyle.IsActive = false;
                existingStyle.UpdatedAt = DateTime.UtcNow;
            }

            // Create new style
            var newStyle = new CompanyEmailStyle
            {
                CompanyId = companyId,
                PrimaryColor = createDto.PrimaryColor,
                SecondaryColor = createDto.SecondaryColor,
                SuccessColor = createDto.SuccessColor,
                WarningColor = createDto.WarningColor,
                InfoColor = createDto.InfoColor,
                BackgroundColor = createDto.BackgroundColor,
                BorderColor = createDto.BorderColor,
                TextColor = createDto.TextColor,
                HeaderTextColor = createDto.HeaderTextColor,
                ButtonTextColor = createDto.ButtonTextColor,
                FooterColor = createDto.FooterColor,
                FooterTextColor = createDto.FooterTextColor,
                LogoUrl = createDto.LogoUrl,
                FooterText = createDto.FooterText,
                FontFamily = createDto.FontFamily,
                IsActive = true
            };

            _context.CompanyEmailStyles.Add(newStyle);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetCompanyEmailStyle), new { companyId }, newStyle);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating email style for company {CompanyId}", companyId);
            return BadRequest("Error creating email style");
        }
    }

    /// <summary>
    /// Update email style for a company
    /// </summary>
    [HttpPut("company/{companyId}")]
    public async Task<ActionResult<CompanyEmailStyle>> UpdateEmailStyle(Guid companyId, UpdateEmailStyleDto updateDto)
    {
        try
        {
            var style = await _context.CompanyEmailStyles
                .FirstOrDefaultAsync(s => s.CompanyId == companyId && s.IsActive);

            if (style == null)
                return NotFound("Email style not found");

            // Update properties
            if (!string.IsNullOrEmpty(updateDto.PrimaryColor))
                style.PrimaryColor = updateDto.PrimaryColor;
            if (!string.IsNullOrEmpty(updateDto.SecondaryColor))
                style.SecondaryColor = updateDto.SecondaryColor;
            if (!string.IsNullOrEmpty(updateDto.SuccessColor))
                style.SuccessColor = updateDto.SuccessColor;
            if (!string.IsNullOrEmpty(updateDto.WarningColor))
                style.WarningColor = updateDto.WarningColor;
            if (!string.IsNullOrEmpty(updateDto.InfoColor))
                style.InfoColor = updateDto.InfoColor;
            if (!string.IsNullOrEmpty(updateDto.BackgroundColor))
                style.BackgroundColor = updateDto.BackgroundColor;
            if (!string.IsNullOrEmpty(updateDto.BorderColor))
                style.BorderColor = updateDto.BorderColor;
            if (!string.IsNullOrEmpty(updateDto.TextColor))
                style.TextColor = updateDto.TextColor;
            if (!string.IsNullOrEmpty(updateDto.HeaderTextColor))
                style.HeaderTextColor = updateDto.HeaderTextColor;
            if (!string.IsNullOrEmpty(updateDto.ButtonTextColor))
                style.ButtonTextColor = updateDto.ButtonTextColor;
            if (!string.IsNullOrEmpty(updateDto.FooterColor))
                style.FooterColor = updateDto.FooterColor;
            if (!string.IsNullOrEmpty(updateDto.FooterTextColor))
                style.FooterTextColor = updateDto.FooterTextColor;
            if (updateDto.LogoUrl != null)
                style.LogoUrl = updateDto.LogoUrl;
            if (updateDto.FooterText != null)
                style.FooterText = updateDto.FooterText;
            if (!string.IsNullOrEmpty(updateDto.FontFamily))
                style.FontFamily = updateDto.FontFamily;

            style.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(style);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating email style for company {CompanyId}", companyId);
            return BadRequest("Error updating email style");
        }
    }

    /// <summary>
    /// Delete email style for a company
    /// </summary>
    [HttpDelete("company/{companyId}")]
    public async Task<ActionResult> DeleteEmailStyle(Guid companyId)
    {
        try
        {
            var style = await _context.CompanyEmailStyles
                .FirstOrDefaultAsync(s => s.CompanyId == companyId && s.IsActive);

            if (style == null)
                return NotFound("Email style not found");

            style.IsActive = false;
            style.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting email style for company {CompanyId}", companyId);
            return BadRequest("Error deleting email style");
        }
    }

    /// <summary>
    /// Get email style preview
    /// </summary>
    [HttpGet("preview/{companyId}")]
    public async Task<ActionResult<EmailStylePreviewDto>> GetEmailStylePreview(Guid companyId)
    {
        try
        {
            var style = await _context.CompanyEmailStyles
                .FirstOrDefaultAsync(s => s.CompanyId == companyId && s.IsActive);

            if (style == null)
                return NotFound("Email style not found");

            var preview = new EmailStylePreviewDto
            {
                CompanyId = style.CompanyId,
                PrimaryColor = style.PrimaryColor,
                SecondaryColor = style.SecondaryColor,
                SuccessColor = style.SuccessColor,
                WarningColor = style.WarningColor,
                InfoColor = style.InfoColor,
                BackgroundColor = style.BackgroundColor,
                BorderColor = style.BorderColor,
                TextColor = style.TextColor,
                HeaderTextColor = style.HeaderTextColor,
                ButtonTextColor = style.ButtonTextColor,
                FooterColor = style.FooterColor,
                FooterTextColor = style.FooterTextColor,
                LogoUrl = style.LogoUrl,
                FooterText = style.FooterText,
                FontFamily = style.FontFamily,
                PreviewHtml = GeneratePreviewHtml(style)
            };

            return Ok(preview);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting email style preview for company {CompanyId}", companyId);
            return BadRequest("Error generating preview");
        }
    }

    private string GeneratePreviewHtml(CompanyEmailStyle style)
    {
        return $@"
<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Email Style Preview</title>
    <style>
        body {{ font-family: {style.FontFamily}; line-height: 1.6; color: {style.TextColor}; margin: 0; padding: 0; background-color: #f4f4f4; }}
        .preview-container {{ max-width: 600px; margin: 20px auto; background-color: white; border-radius: 8px; overflow: hidden; box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1); }}
        .header {{ background-color: {style.PrimaryColor}; color: {style.HeaderTextColor}; padding: 20px; text-align: center; }}
        .content {{ padding: 30px; }}
        .preview-section {{ background-color: {style.BackgroundColor}; padding: 20px; margin: 20px 0; border-radius: 5px; border-left: 4px solid {style.PrimaryColor}; }}
        .cta-button {{ display: inline-block; background-color: {style.PrimaryColor}; color: {style.ButtonTextColor}; padding: 12px 24px; text-decoration: none; border-radius: 5px; margin: 10px 0; }}
        .success-badge {{ background-color: {style.SuccessColor}; color: white; padding: 5px 10px; border-radius: 3px; font-size: 12px; }}
        .warning-badge {{ background-color: {style.WarningColor}; color: white; padding: 5px 10px; border-radius: 3px; font-size: 12px; }}
        .info-badge {{ background-color: {style.InfoColor}; color: white; padding: 5px 10px; border-radius: 3px; font-size: 12px; }}
        .footer {{ background-color: {style.FooterColor}; color: {style.FooterTextColor}; padding: 20px; text-align: center; }}
    </style>
</head>
<body>
    <div class='preview-container'>
        <div class='header'>
            <h1>Email Style Preview</h1>
            <p>This is how your emails will look to customers</p>
        </div>
        
        <div class='content'>
            <div class='preview-section'>
                <h3 style='color: {style.PrimaryColor}; margin-top: 0;'>Booking Summary</h3>
                <p>Vehicle: <strong>Toyota Camry (2023)</strong></p>
                <p>Total Amount: <strong style='color: {style.PrimaryColor};'>$187.00</strong></p>
                <a href='#' class='cta-button'>Complete Booking & Pay Now</a>
            </div>
            
            <div class='preview-section'>
                <h3 style='color: {style.PrimaryColor}; margin-top: 0;'>Status Badges</h3>
                <p>Payment: <span class='success-badge'>Completed</span></p>
                <p>Booking: <span class='warning-badge'>Pending</span></p>
                <p>Support: <span class='info-badge'>Available</span></p>
            </div>
        </div>
        
        <div class='footer'>
            <p style='margin: 0; font-size: 12px;'>{style.FooterText ?? "© 2025 Car Rental System. All rights reserved."}</p>
        </div>
    </div>
</body>
</html>";
    }
}

public class CreateEmailStyleDto
{
    [Required]
    [RegularExpression(@"^#[0-9A-Fa-f]{6}$", ErrorMessage = "Primary color must be a valid hex color")]
    public string PrimaryColor { get; set; } = "#007bff";

    [RegularExpression(@"^#[0-9A-Fa-f]{6}$", ErrorMessage = "Secondary color must be a valid hex color")]
    public string SecondaryColor { get; set; } = "#6c757d";

    [RegularExpression(@"^#[0-9A-Fa-f]{6}$", ErrorMessage = "Success color must be a valid hex color")]
    public string SuccessColor { get; set; } = "#28a745";

    [RegularExpression(@"^#[0-9A-Fa-f]{6}$", ErrorMessage = "Warning color must be a valid hex color")]
    public string WarningColor { get; set; } = "#ffc107";

    [RegularExpression(@"^#[0-9A-Fa-f]{6}$", ErrorMessage = "Info color must be a valid hex color")]
    public string InfoColor { get; set; } = "#17a2b8";

    [RegularExpression(@"^#[0-9A-Fa-f]{6}$", ErrorMessage = "Background color must be a valid hex color")]
    public string BackgroundColor { get; set; } = "#f8f9fa";

    [RegularExpression(@"^#[0-9A-Fa-f]{6}$", ErrorMessage = "Border color must be a valid hex color")]
    public string BorderColor { get; set; } = "#dee2e6";

    [RegularExpression(@"^#[0-9A-Fa-f]{6}$", ErrorMessage = "Text color must be a valid hex color")]
    public string TextColor { get; set; } = "#333333";

    [RegularExpression(@"^#[0-9A-Fa-f]{6}$", ErrorMessage = "Header text color must be a valid hex color")]
    public string HeaderTextColor { get; set; } = "#ffffff";

    [RegularExpression(@"^#[0-9A-Fa-f]{6}$", ErrorMessage = "Button text color must be a valid hex color")]
    public string ButtonTextColor { get; set; } = "#ffffff";

    [RegularExpression(@"^#[0-9A-Fa-f]{6}$", ErrorMessage = "Footer color must be a valid hex color")]
    public string FooterColor { get; set; } = "#343a40";

    [RegularExpression(@"^#[0-9A-Fa-f]{6}$", ErrorMessage = "Footer text color must be a valid hex color")]
    public string FooterTextColor { get; set; } = "#ffffff";

    [MaxLength(500)]
    public string? LogoUrl { get; set; }

    [MaxLength(500)]
    public string? FooterText { get; set; }

    [MaxLength(100)]
    public string FontFamily { get; set; } = "Arial, sans-serif";
}

public class UpdateEmailStyleDto
{
    [RegularExpression(@"^#[0-9A-Fa-f]{6}$", ErrorMessage = "Primary color must be a valid hex color")]
    public string? PrimaryColor { get; set; }

    [RegularExpression(@"^#[0-9A-Fa-f]{6}$", ErrorMessage = "Secondary color must be a valid hex color")]
    public string? SecondaryColor { get; set; }

    [RegularExpression(@"^#[0-9A-Fa-f]{6}$", ErrorMessage = "Success color must be a valid hex color")]
    public string? SuccessColor { get; set; }

    [RegularExpression(@"^#[0-9A-Fa-f]{6}$", ErrorMessage = "Warning color must be a valid hex color")]
    public string? WarningColor { get; set; }

    [RegularExpression(@"^#[0-9A-Fa-f]{6}$", ErrorMessage = "Info color must be a valid hex color")]
    public string? InfoColor { get; set; }

    [RegularExpression(@"^#[0-9A-Fa-f]{6}$", ErrorMessage = "Background color must be a valid hex color")]
    public string? BackgroundColor { get; set; }

    [RegularExpression(@"^#[0-9A-Fa-f]{6}$", ErrorMessage = "Border color must be a valid hex color")]
    public string? BorderColor { get; set; }

    [RegularExpression(@"^#[0-9A-Fa-f]{6}$", ErrorMessage = "Text color must be a valid hex color")]
    public string? TextColor { get; set; }

    [RegularExpression(@"^#[0-9A-Fa-f]{6}$", ErrorMessage = "Header text color must be a valid hex color")]
    public string? HeaderTextColor { get; set; }

    [RegularExpression(@"^#[0-9A-Fa-f]{6}$", ErrorMessage = "Button text color must be a valid hex color")]
    public string? ButtonTextColor { get; set; }

    [RegularExpression(@"^#[0-9A-Fa-f]{6}$", ErrorMessage = "Footer color must be a valid hex color")]
    public string? FooterColor { get; set; }

    [RegularExpression(@"^#[0-9A-Fa-f]{6}$", ErrorMessage = "Footer text color must be a valid hex color")]
    public string? FooterTextColor { get; set; }

    [MaxLength(500)]
    public string? LogoUrl { get; set; }

    [MaxLength(500)]
    public string? FooterText { get; set; }

    [MaxLength(100)]
    public string? FontFamily { get; set; }
}

public class EmailStylePreviewDto
{
    public Guid CompanyId { get; set; }
    public string PrimaryColor { get; set; } = string.Empty;
    public string SecondaryColor { get; set; } = string.Empty;
    public string SuccessColor { get; set; } = string.Empty;
    public string WarningColor { get; set; } = string.Empty;
    public string InfoColor { get; set; } = string.Empty;
    public string BackgroundColor { get; set; } = string.Empty;
    public string BorderColor { get; set; } = string.Empty;
    public string TextColor { get; set; } = string.Empty;
    public string HeaderTextColor { get; set; } = string.Empty;
    public string ButtonTextColor { get; set; } = string.Empty;
    public string FooterColor { get; set; } = string.Empty;
    public string FooterTextColor { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string? FooterText { get; set; }
    public string FontFamily { get; set; } = string.Empty;
    public string PreviewHtml { get; set; } = string.Empty;
}
