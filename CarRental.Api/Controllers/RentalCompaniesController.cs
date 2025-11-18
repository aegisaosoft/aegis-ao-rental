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

using System;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CarRental.Api.Data;
using CarRental.Api.DTOs;
using CarRental.Api.DTOs.Stripe;
using CarRental.Api.Models;
using CarRental.Api.Services;
using CarRental.Api.Helpers;
using Stripe;

namespace CarRental.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RentalCompaniesController : ControllerBase
{
    private readonly CarRentalDbContext _context;
    private readonly IStripeService _stripeService;
    private readonly ILogger<RentalCompaniesController> _logger;
    private readonly IEncryptionService _encryptionService;
    private readonly IStripeConnectService _stripeConnectService;

    public RentalCompaniesController(
        CarRentalDbContext context, 
        IStripeService stripeService, 
        ILogger<RentalCompaniesController> logger,
        IEncryptionService encryptionService,
        IStripeConnectService stripeConnectService)
    {
        _context = context;
        _stripeService = stripeService;
        _logger = logger;
        _encryptionService = encryptionService;
        _stripeConnectService = stripeConnectService;
    }

    private async Task<string?> ResolveStripeAccountIdAsync(Company company)
    {
        if (string.IsNullOrWhiteSpace(company.StripeAccountId))
            return null;

        try
        {
            return _encryptionService.Decrypt(company.StripeAccountId);
        }
        catch (FormatException)
        {
            return await ReEncryptPlainStripeAccountIdAsync(company, company.StripeAccountId!);
        }
        catch (CryptographicException)
        {
            return await ReEncryptPlainStripeAccountIdAsync(company, company.StripeAccountId!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt Stripe account ID for company {CompanyId}", company.Id);
            return null;
        }
    }

    private async Task<string?> ReEncryptPlainStripeAccountIdAsync(Company company, string plaintext)
    {
        if (string.IsNullOrWhiteSpace(plaintext))
            return null;

        try
        {
            company.StripeAccountId = _encryptionService.Encrypt(plaintext);
            _context.Companies.Update(company);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to re-encrypt Stripe account ID for company {CompanyId}", company.Id);
        }

        return plaintext;
    }

    /// <summary>
    /// Get all rental companies with optional filtering
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<RentalCompanyDto>>> GetRentalCompanies(
        string? search = null,
        bool? isActive = null,
        int page = 1,
        int pageSize = 20)
    {
        var query = _context.Companies.AsQueryable();

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(c => 
                c.CompanyName.Contains(search) || 
                c.Email.Contains(search));
        }

        if (isActive.HasValue)
            query = query.Where(c => c.IsActive == isActive.Value);

        var companies = await query
            .OrderBy(c => c.CompanyName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new RentalCompanyDto
            {
                CompanyId = c.Id,
                CompanyName = c.CompanyName,
                Email = c.Email,
                Website = c.Website,
                StripeAccountId = null,
                TaxId = c.TaxId,
                VideoLink = c.VideoLink,
                BannerLink = c.BannerLink,
                LogoLink = c.LogoLink,
                Motto = c.Motto,
                MottoDescription = c.MottoDescription,
                Invitation = c.Invitation,
                Texts = c.Texts,
                BackgroundLink = c.BackgroundLink,
                About = c.About,
                TermsOfUse = c.TermsOfUse,
                BookingIntegrated = c.BookingIntegrated,
                CompanyPath = c.CompanyPath,
                Subdomain = c.Subdomain,
                PrimaryColor = c.PrimaryColor,
                SecondaryColor = c.SecondaryColor,
                Currency = c.Currency,
                LogoUrl = c.LogoUrl,
                FaviconUrl = c.FaviconUrl,
                CustomCss = c.CustomCss,
                Country = c.Country,
                BlinkKey = c.BlinkKey,
                SecurityDeposit = c.SecurityDeposit,
                IsSecurityDepositMandatory = c.IsSecurityDepositMandatory,
                IsActive = c.IsActive,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt
            })
            .ToListAsync();

        return Ok(companies);
    }

    /// <summary>
    /// Get a specific rental company by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<RentalCompanyDto>> GetRentalCompany(Guid id)
    {
        var company = await _context.Companies.FindAsync(id);

        if (company == null)
            return NotFound();

        var companyDto = new RentalCompanyDto
        {
            CompanyId = company.Id,
            CompanyName = company.CompanyName,
            Email = company.Email,
            Website = company.Website,
            StripeAccountId = null,
            TaxId = company.TaxId,
            VideoLink = company.VideoLink,
            BannerLink = company.BannerLink,
            LogoLink = company.LogoLink,
            Motto = company.Motto,
            MottoDescription = company.MottoDescription,
            Invitation = company.Invitation,
            Texts = company.Texts,
            BackgroundLink = company.BackgroundLink,
            About = company.About,
            TermsOfUse = company.TermsOfUse,
            BookingIntegrated = company.BookingIntegrated,
            CompanyPath = company.CompanyPath,
            Subdomain = company.Subdomain,
            PrimaryColor = company.PrimaryColor,
            SecondaryColor = company.SecondaryColor,
            Currency = company.Currency,
            LogoUrl = company.LogoUrl,
            FaviconUrl = company.FaviconUrl,
            CustomCss = company.CustomCss,
            Country = company.Country,
            BlinkKey = company.BlinkKey,
            SecurityDeposit = company.SecurityDeposit,
            IsSecurityDepositMandatory = company.IsSecurityDepositMandatory,
            IsActive = company.IsActive,
            CreatedAt = company.CreatedAt,
            UpdatedAt = company.UpdatedAt
        };

        return Ok(companyDto);
    }

    /// <summary>
    /// Get rental company by email
    /// </summary>
    [HttpGet("email/{email}")]
    public async Task<ActionResult<RentalCompanyDto>> GetRentalCompanyByEmail(string email)
    {
        var company = await _context.Companies
            .FirstOrDefaultAsync(c => c.Email == email);

        if (company == null)
            return NotFound();

        var companyDto = new RentalCompanyDto
        {
            CompanyId = company.Id,
            CompanyName = company.CompanyName,
            Email = company.Email,
            Website = company.Website,
            StripeAccountId = null,
            TaxId = company.TaxId,
            VideoLink = company.VideoLink,
            BannerLink = company.BannerLink,
            LogoLink = company.LogoLink,
            Motto = company.Motto,
            MottoDescription = company.MottoDescription,
            Invitation = company.Invitation,
            Texts = company.Texts,
            BackgroundLink = company.BackgroundLink,
            About = company.About,
            TermsOfUse = company.TermsOfUse,
            BookingIntegrated = company.BookingIntegrated,
            CompanyPath = company.CompanyPath,
            Subdomain = company.Subdomain,
            PrimaryColor = company.PrimaryColor,
            SecondaryColor = company.SecondaryColor,
            Currency = company.Currency,
            LogoUrl = company.LogoUrl,
            FaviconUrl = company.FaviconUrl,
            CustomCss = company.CustomCss,
            Country = company.Country,
            BlinkKey = company.BlinkKey,
            SecurityDeposit = company.SecurityDeposit,
            IsSecurityDepositMandatory = company.IsSecurityDepositMandatory,
            IsActive = company.IsActive,
            CreatedAt = company.CreatedAt,
            UpdatedAt = company.UpdatedAt
        };

        return Ok(companyDto);
    }

    /// <summary>
    /// Create a new rental company
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<RentalCompanyDto>> CreateRentalCompany(CreateRentalCompanyDto createCompanyDto)
    {
        // Check if company with email already exists
        var existingCompany = await _context.Companies
            .FirstOrDefaultAsync(c => c.Email == createCompanyDto.Email);

        if (existingCompany != null)
            return Conflict("Company with this email already exists");

        var company = new Company
        {
            CompanyName = createCompanyDto.CompanyName,
            Email = createCompanyDto.Email,
            Website = createCompanyDto.Website,
            TaxId = createCompanyDto.TaxId,
            VideoLink = createCompanyDto.VideoLink,
            BannerLink = createCompanyDto.BannerLink,
            LogoLink = createCompanyDto.LogoLink,
            Motto = createCompanyDto.Motto,
            MottoDescription = createCompanyDto.MottoDescription,
            Invitation = createCompanyDto.Invitation,
            Texts = createCompanyDto.Texts,
            BackgroundLink = createCompanyDto.BackgroundLink,
            About = createCompanyDto.About,
            BookingIntegrated = createCompanyDto.BookingIntegrated ? "true" : "false",
            CompanyPath = createCompanyDto.CompanyPath,
            Subdomain = createCompanyDto.Subdomain,
            PrimaryColor = createCompanyDto.PrimaryColor,
            SecondaryColor = createCompanyDto.SecondaryColor,
            LogoUrl = createCompanyDto.LogoUrl,
            FaviconUrl = createCompanyDto.FaviconUrl,
            CustomCss = createCompanyDto.CustomCss,
            Country = createCompanyDto.Country,
            Currency = CurrencyHelper.ResolveCurrency(createCompanyDto.Currency, createCompanyDto.Country),
            SecurityDeposit = createCompanyDto.SecurityDeposit ?? 1000m,
            IsSecurityDepositMandatory = createCompanyDto.IsSecurityDepositMandatory ?? true,
            IsActive = true
        };

        try
        {
            _context.Companies.Add(company);
            await _context.SaveChangesAsync();

            // Create Stripe Connect account
            try
            {
                var stripeAccount = await _stripeService.CreateConnectedAccountAsync(
                    company.Email, 
                    "individual",
                    createCompanyDto.Country ?? "US"); // Use country from DTO or default to US
                
                company.StripeAccountId = _encryptionService.Encrypt(stripeAccount.Id);
                _context.Companies.Update(company);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create Stripe Connect account for company {Email}", company.Email);
                // Continue without Stripe account for now
            }

            var companyDto = new RentalCompanyDto
            {
                CompanyId = company.Id,
                CompanyName = company.CompanyName,
                Email = company.Email,
                Website = company.Website,
                StripeAccountId = null,
                TaxId = company.TaxId,
                VideoLink = company.VideoLink,
                BannerLink = company.BannerLink,
                LogoLink = company.LogoLink,
                Motto = company.Motto,
                MottoDescription = company.MottoDescription,
                Invitation = company.Invitation,
                Texts = company.Texts,
                BackgroundLink = company.BackgroundLink,
                About = company.About,
                TermsOfUse = company.TermsOfUse,
                BookingIntegrated = company.BookingIntegrated,
                CompanyPath = company.CompanyPath,
                Subdomain = company.Subdomain,
                PrimaryColor = company.PrimaryColor,
                SecondaryColor = company.SecondaryColor,
                Currency = company.Currency,
                LogoUrl = company.LogoUrl,
                FaviconUrl = company.FaviconUrl,
                CustomCss = company.CustomCss,
                Country = company.Country,
                BlinkKey = company.BlinkKey,
                SecurityDeposit = company.SecurityDeposit,
                IsSecurityDepositMandatory = company.IsSecurityDepositMandatory,
                IsActive = company.IsActive,
                CreatedAt = company.CreatedAt,
                UpdatedAt = company.UpdatedAt
            };

            return CreatedAtAction(nameof(GetRentalCompany), new { id = company.Id }, companyDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating rental company");
            return BadRequest("Error creating rental company");
        }
    }

    /// <summary>
    /// Update a rental company
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateRentalCompany(Guid id, UpdateRentalCompanyDto updateCompanyDto)
    {
        _logger.LogInformation("UpdateRentalCompany called with SecurityDeposit={SecurityDeposit}, IsSecurityDepositMandatory={IsSecurityDepositMandatory}", 
            updateCompanyDto.SecurityDeposit, updateCompanyDto.IsSecurityDepositMandatory);
        
        var company = await _context.Companies.FindAsync(id);

        if (company == null)
            return NotFound();

        // Check if email is being changed and if it already exists
        if (!string.IsNullOrEmpty(updateCompanyDto.Email) && updateCompanyDto.Email != company.Email)
        {
            var existingCompany = await _context.Companies
                .FirstOrDefaultAsync(c => c.Email == updateCompanyDto.Email && c.Id != id);

            if (existingCompany != null)
                return Conflict("Company with this email already exists");
        }

        var originalCountry = company.Country;
        var countryUpdated = false;

        // Update fields
        if (!string.IsNullOrEmpty(updateCompanyDto.CompanyName))
            company.CompanyName = updateCompanyDto.CompanyName;

        if (!string.IsNullOrEmpty(updateCompanyDto.Email))
            company.Email = updateCompanyDto.Email;

        if (updateCompanyDto.Website != null)
            company.Website = updateCompanyDto.Website;

        if (updateCompanyDto.TaxId != null)
            company.TaxId = updateCompanyDto.TaxId;

        if (updateCompanyDto.VideoLink != null)
            company.VideoLink = updateCompanyDto.VideoLink;

        if (updateCompanyDto.BannerLink != null)
            company.BannerLink = updateCompanyDto.BannerLink;

        if (updateCompanyDto.LogoLink != null)
            company.LogoLink = updateCompanyDto.LogoLink;

        if (updateCompanyDto.Motto != null)
            company.Motto = updateCompanyDto.Motto;

        if (updateCompanyDto.MottoDescription != null)
            company.MottoDescription = updateCompanyDto.MottoDescription;

        if (updateCompanyDto.Invitation != null)
            company.Invitation = updateCompanyDto.Invitation;

        if (updateCompanyDto.Texts != null)
            company.Texts = updateCompanyDto.Texts;

        if (updateCompanyDto.BackgroundLink != null)
            company.BackgroundLink = updateCompanyDto.BackgroundLink;

        if (updateCompanyDto.About != null)
            company.About = updateCompanyDto.About;

        if (updateCompanyDto.TermsOfUse != null)
            company.TermsOfUse = updateCompanyDto.TermsOfUse;

        if (updateCompanyDto.BookingIntegrated.HasValue)
            company.BookingIntegrated = updateCompanyDto.BookingIntegrated.Value ? "true" : "false";

        if (updateCompanyDto.CompanyPath != null)
            company.CompanyPath = updateCompanyDto.CompanyPath;

        if (updateCompanyDto.Subdomain != null)
            company.Subdomain = updateCompanyDto.Subdomain;

        if (updateCompanyDto.PrimaryColor != null)
            company.PrimaryColor = updateCompanyDto.PrimaryColor;

        if (updateCompanyDto.SecondaryColor != null)
            company.SecondaryColor = updateCompanyDto.SecondaryColor;

        if (updateCompanyDto.LogoUrl != null)
            company.LogoUrl = updateCompanyDto.LogoUrl;

        if (updateCompanyDto.FaviconUrl != null)
            company.FaviconUrl = updateCompanyDto.FaviconUrl;

        if (updateCompanyDto.CustomCss != null)
            company.CustomCss = updateCompanyDto.CustomCss;

        if (updateCompanyDto.Country != null)
        {
            company.Country = updateCompanyDto.Country;
            countryUpdated = !string.Equals(originalCountry, updateCompanyDto.Country, StringComparison.OrdinalIgnoreCase);
        }

        if (!string.IsNullOrWhiteSpace(updateCompanyDto.Currency))
        {
            company.Currency = CurrencyHelper.ResolveCurrency(updateCompanyDto.Currency, company.Country);
        }
        else if (countryUpdated)
        {
            company.Currency = CurrencyHelper.GetCurrencyForCountry(company.Country);
        }

        if (updateCompanyDto.Language != null)
            company.Language = updateCompanyDto.Language;

        if (updateCompanyDto.BlinkKey != null)
            company.BlinkKey = updateCompanyDto.BlinkKey;

        if (updateCompanyDto.IsActive.HasValue)
            company.IsActive = updateCompanyDto.IsActive.Value;

        if (updateCompanyDto.SecurityDeposit.HasValue)
            company.SecurityDeposit = updateCompanyDto.SecurityDeposit.Value;

        // Always update IsSecurityDepositMandatory if provided
        if (updateCompanyDto.IsSecurityDepositMandatory.HasValue)
        {
            _logger.LogInformation("Updating IsSecurityDepositMandatory: Current={Current}, New={New}", 
                company.IsSecurityDepositMandatory, updateCompanyDto.IsSecurityDepositMandatory.Value);
            company.IsSecurityDepositMandatory = updateCompanyDto.IsSecurityDepositMandatory.Value;
        }

        company.UpdatedAt = DateTime.UtcNow;

        try
        {
            _context.Companies.Update(company);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Successfully saved company {CompanyId}. IsSecurityDepositMandatory is now: {IsSecurityDepositMandatory}", 
                company.Id, company.IsSecurityDepositMandatory);

            // Update Stripe Connect account if exists
            if (!string.IsNullOrEmpty(company.StripeAccountId))
            {
                var stripeAccountId = await ResolveStripeAccountIdAsync(company);
                if (!string.IsNullOrEmpty(stripeAccountId))
                {
                    try
                    {
                        await _stripeService.GetAccountAsync(stripeAccountId);
                        // Additional Stripe account updates can be added here
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to update Stripe Connect account for company {CompanyId}", company.Id);
                    }
                }
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating rental company {CompanyId}", id);
            return BadRequest("Error updating rental company");
        }
    }

    /// <summary>
    /// Clear the About field for a rental company
    /// </summary>
    [HttpDelete("{id}/about")]
    [ActionName("ClearAbout")]
    public async Task<IActionResult> ClearRentalCompanyAbout(Guid id)
    {
        try
        {
            var company = await _context.Companies.FindAsync(id);

            if (company == null)
            {
                return NotFound();
            }

            company.About = null;
            company.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("About field cleared for rental company {CompanyId}", id);

            return Ok(new
            {
                id = company.Id,
                companyName = company.CompanyName,
                about = (string?)null,
                updatedAt = company.UpdatedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing about field for rental company {CompanyId}", id);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Update only the Terms of Use field for a rental company
    /// </summary>
    [HttpPut("{id}/terms-of-use")]
    public async Task<IActionResult> UpdateRentalCompanyTermsOfUse(Guid id, [FromBody] UpdateTermsOfUseDto dto)
    {
        try
        {
            var company = await _context.Companies.FindAsync(id);

            if (company == null)
            {
                return NotFound();
            }

            // Allow setting to null by checking if the property was provided
            // If TermsOfUse is explicitly null in the request, clear it
            // If it's a string (empty or with content), set it
            company.TermsOfUse = dto.TermsOfUse;
            company.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Terms of Use updated for rental company {CompanyId}", id);

            return Ok(new
            {
                id = company.Id,
                companyName = company.CompanyName,
                termsOfUse = company.TermsOfUse,
                updatedAt = company.UpdatedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating Terms of Use for rental company {CompanyId}", id);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Clear the Terms of Use field for a rental company
    /// </summary>
    [HttpDelete("{id}/terms-of-use")]
    [ActionName("ClearTermsOfUse")]
    public async Task<IActionResult> ClearRentalCompanyTermsOfUse(Guid id)
    {
        try
        {
            var company = await _context.Companies.FindAsync(id);

            if (company == null)
            {
                return NotFound();
            }

            company.TermsOfUse = null;
            company.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Terms of Use cleared for rental company {CompanyId}", id);

            return Ok(new
            {
                id = company.Id,
                companyName = company.CompanyName,
                termsOfUse = (string?)null,
                updatedAt = company.UpdatedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing Terms of Use for rental company {CompanyId}", id);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Delete a rental company
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteRentalCompany(Guid id)
    {
        var company = await _context.Companies.FindAsync(id);

        if (company == null)
            return NotFound();

        // Check if company has active vehicles, reservations, or rentals
        var hasActiveVehicles = await _context.Vehicles
            .AnyAsync(v => v.CompanyId == id && v.Status != VehicleStatus.OutOfService);

        var hasActiveReservations = await _context.Bookings
            .AnyAsync(r => r.CompanyId == id && r.Status == "Confirmed");

        var hasActiveRentals = await _context.Rentals
            .AnyAsync(r => r.CompanyId == id && r.Status == "active");

        if (hasActiveVehicles || hasActiveReservations || hasActiveRentals)
            return BadRequest("Cannot delete company with active vehicles, reservations, or rentals");

        try
        {
            _context.Companies.Remove(company);
            await _context.SaveChangesAsync();
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting rental company {CompanyId}", id);
            return BadRequest("Error deleting rental company");
        }
    }

    /// <summary>
    /// Activate/deactivate a rental company
    /// </summary>
    [HttpPost("{id}/toggle-status")]
    public async Task<IActionResult> ToggleCompanyStatus(Guid id)
    {
        var company = await _context.Companies.FindAsync(id);

        if (company == null)
            return NotFound();

        company.IsActive = !company.IsActive;
        company.UpdatedAt = DateTime.UtcNow;

        try
        {
            _context.Companies.Update(company);
            await _context.SaveChangesAsync();
            return Ok(new { IsActive = company.IsActive });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling company status {CompanyId}", id);
            return BadRequest("Error updating company status");
        }
    }

    /// <summary>
    /// Get company statistics
    /// </summary>
    [HttpGet("{id}/stats")]
    public async Task<ActionResult<object>> GetCompanyStats(Guid id)
    {
        var company = await _context.Companies.FindAsync(id);

        if (company == null)
            return NotFound();

        var stats = new
        {
            TotalVehicles = await _context.Vehicles.CountAsync(v => v.CompanyId == id),
            ActiveVehicles = await _context.Vehicles.CountAsync(v => v.CompanyId == id && v.Status == VehicleStatus.Available),
            TotalReservations = await _context.Bookings.CountAsync(r => r.CompanyId == id),
            ActiveReservations = await _context.Bookings.CountAsync(r => r.CompanyId == id && r.Status == "Confirmed"),
            TotalRentals = await _context.Rentals.CountAsync(r => r.CompanyId == id),
            ActiveRentals = await _context.Rentals.CountAsync(r => r.CompanyId == id && r.Status == "active"),
            TotalRevenue = await _context.Payments.Where(p => p.CompanyId == id && p.Status == "succeeded")
                .SumAsync(p => p.Amount),
            AverageRating = await _context.Reviews.Where(r => r.CompanyId == id)
                .AverageAsync(r => (double?)r.Rating),
            LastActivity = await _context.Bookings
                .Where(r => r.CompanyId == id)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => r.CreatedAt)
                .FirstOrDefaultAsync()
        };

        return Ok(stats);
    }

    /// <summary>
    /// Get company vehicles
    /// </summary>
    [HttpGet("{id}/vehicles")]
    public async Task<ActionResult<IEnumerable<VehicleDto>>> GetCompanyVehicles(
        Guid id,
        string? status = null,
        int page = 1,
        int pageSize = 20)
    {
        var query = _context.Vehicles
            .Include(v => v.VehicleModel)
            .Where(v => v.CompanyId == id);

        if (!string.IsNullOrEmpty(status))
            query = query.Where(v => v.Status.ToString() == status);

        var allVehicles = await query.ToListAsync();
        
        // Load Model for each VehicleModel that has one
        foreach (var vehicle in allVehicles.Where(v => v.VehicleModel != null))
        {
            var vm = vehicle.VehicleModel!;
            await _context.Entry(vm)
                .Reference(v => v.Model)
                .LoadAsync();
            
            if (vm.Model != null)
            {
                await _context.Entry(vm.Model)
                    .Reference(m => m.Category)
                    .LoadAsync();
            }
        }
        
        // Now order by loaded data and paginate
        var vehiclesList = allVehicles
            .OrderBy(v => v.VehicleModel?.Model?.Make ?? "")
            .ThenBy(v => v.VehicleModel?.Model?.ModelName ?? "")
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var vehicles = vehiclesList.Select(v =>
        {
            var vm = v.VehicleModel;
            var matchingModel = vm?.Model;

            return new VehicleDto
            {
                VehicleId = v.Id,
                CompanyId = v.CompanyId,
                CategoryId = matchingModel?.CategoryId,
                CategoryName = matchingModel?.Category?.CategoryName,
                Make = matchingModel?.Make ?? "",
                Model = matchingModel?.ModelName ?? "",
                Year = matchingModel?.Year ?? 0,
                Color = v.Color,
                LicensePlate = v.LicensePlate,
                Vin = v.Vin,
                Mileage = v.Mileage,
                FuelType = matchingModel?.FuelType,
                Transmission = v.Transmission,
                Seats = v.Seats,
                DailyRate = vm?.DailyRate ?? 0, // Rate from catalog
                Status = v.Status.ToString(),
                Location = v.Location,
                ImageUrl = v.ImageUrl,
                Features = v.Features,
                CreatedAt = v.CreatedAt,
                UpdatedAt = v.UpdatedAt
            };
        }).ToList();

        return Ok(vehicles);
    }

    /// <summary>
    /// Get company reservations
    /// </summary>
    [HttpGet("{id}/reservations")]
    public async Task<ActionResult<IEnumerable<ReservationDto>>> GetCompanyReservations(
        Guid id,
        string? status = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int page = 1,
        int pageSize = 20)
    {
        var company = await _context.Companies.FindAsync(id);
        if (company == null)
            return NotFound();

        var query = _context.Bookings
            .Include(r => r.Customer)
            .Include(r => r.Vehicle)
                .ThenInclude(v => v.VehicleModel)
            .Where(r => r.CompanyId == id);

        if (!string.IsNullOrEmpty(status))
            query = query.Where(r => r.Status == status);

        if (fromDate.HasValue)
            query = query.Where(r => r.PickupDate >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(r => r.ReturnDate <= toDate.Value);

        var allReservations = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        
        // Load Model for each VehicleModel that has one
        foreach (var reservation in allReservations.Where(r => r.Vehicle?.VehicleModel != null))
        {
            await _context.Entry(reservation.Vehicle.VehicleModel!)
                .Reference(vm => vm.Model)
                .LoadAsync();
        }
        
        var reservations = allReservations.Select(r => new ReservationDto
        {
            Id = r.Id,
            CustomerId = r.CustomerId,
            VehicleId = r.VehicleId,
            CompanyId = r.CompanyId,
            BookingNumber = r.BookingNumber,
            PickupDate = r.PickupDate,
            ReturnDate = r.ReturnDate,
            PickupLocation = r.PickupLocation,
            ReturnLocation = r.ReturnLocation,
            DailyRate = r.DailyRate,
            TotalDays = r.TotalDays,
            Subtotal = r.Subtotal,
            TaxAmount = r.TaxAmount,
            InsuranceAmount = r.InsuranceAmount,
            AdditionalFees = r.AdditionalFees,
            TotalAmount = r.TotalAmount,
            Status = r.Status,
            Notes = r.Notes,
            CreatedAt = r.CreatedAt,
            UpdatedAt = r.UpdatedAt,
            CustomerName = r.Customer.FirstName + " " + r.Customer.LastName,
            CustomerEmail = r.Customer.Email,
            VehicleName = (r.Vehicle?.VehicleModel?.Model != null) ? 
                r.Vehicle.VehicleModel.Model.Make + " " + r.Vehicle.VehicleModel.Model.ModelName + " (" + r.Vehicle.VehicleModel.Model.Year + ")" : 
                "Unknown Vehicle",
            LicensePlate = r.Vehicle?.LicensePlate ?? "",
            CompanyName = company.CompanyName
        }).ToList();

        return Ok(reservations);
    }

    /// <summary>
    /// Get company revenue report
    /// </summary>
    [HttpGet("{id}/revenue")]
    public async Task<ActionResult<object>> GetCompanyRevenue(
        Guid id,
        DateTime? fromDate = null,
        DateTime? toDate = null)
    {
        var company = await _context.Companies.FindAsync(id);
        if (company == null)
            return NotFound();

        var query = _context.Payments
            .Where(p => p.CompanyId == id && p.Status == "succeeded");

        if (fromDate.HasValue)
            query = query.Where(p => p.CreatedAt >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(p => p.CreatedAt <= toDate.Value);

        var revenue = await query
            .GroupBy(p => p.CreatedAt.Date)
            .Select(g => new
            {
                Date = g.Key,
                TotalAmount = g.Sum(p => p.Amount),
                TransactionCount = g.Count()
            })
            .OrderBy(r => r.Date)
            .ToListAsync();

        var totalRevenue = revenue.Sum(r => r.TotalAmount);
        var totalTransactions = revenue.Sum(r => r.TransactionCount);
        var averageTransaction = totalTransactions > 0 ? totalRevenue / totalTransactions : 0;

        return Ok(new
        {
            CompanyName = company.CompanyName,
            Period = new
            {
                From = fromDate ?? revenue.FirstOrDefault()?.Date,
                To = toDate ?? revenue.LastOrDefault()?.Date
            },
            Summary = new
            {
                TotalRevenue = totalRevenue,
                TotalTransactions = totalTransactions,
                AverageTransaction = averageTransaction
            },
            DailyRevenue = revenue
        });
    }

    /// <summary>
    /// Create Stripe Connect account for company
    /// </summary>
    [HttpPost("{id}/stripe/connect/create")]
    public async Task<ActionResult<object>> CreateConnectAccount(Guid id)
    {
        var (success, accountId, error) = await _stripeConnectService.CreateConnectedAccountAsync(id);
        
        if (!success)
            return BadRequest(new { error });

        return Ok(new
        {
            success = true,
            accountId = accountId,
            message = "Stripe Connect account created successfully"
        });
    }

    /// <summary>
    /// Start Stripe Connect onboarding flow
    /// </summary>
    [HttpPost("{id}/stripe/connect/onboard")]
    public async Task<ActionResult<object>> StartOnboarding(
        Guid id, 
        [FromBody] CreateAccountLinkDto dto)
    {
        if (string.IsNullOrEmpty(dto.ReturnUrl) || string.IsNullOrEmpty(dto.RefreshUrl))
            return BadRequest("ReturnUrl and RefreshUrl are required");

        var (success, onboardingUrl, error) = await _stripeConnectService.StartOnboardingAsync(
            id, 
            dto.ReturnUrl, 
            dto.RefreshUrl
        );

        if (!success)
            return BadRequest(new { error });

        return Ok(new
        {
            success = true,
            onboardingUrl = onboardingUrl,
            expiresAt = DateTime.UtcNow.AddHours(1)
        });
    }

    /// <summary>
    /// Complete onboarding (called after user returns from Stripe)
    /// </summary>
    [HttpPost("{id}/stripe/connect/complete-onboarding")]
    public async Task<ActionResult<object>> CompleteOnboarding(Guid id)
    {
        var company = await _context.Companies.FindAsync(id);
        if (company == null)
            return NotFound();

        if (string.IsNullOrEmpty(company.StripeAccountId))
            return BadRequest("Company does not have a Stripe account");

        try
        {
            var stripeAccountId = await ResolveStripeAccountIdAsync(company);
            if (string.IsNullOrEmpty(stripeAccountId))
                return BadRequest("Unable to retrieve Stripe account ID");

            // Sync account status from Stripe
            await _stripeConnectService.SyncAccountStatusAsync(stripeAccountId);

            // Mark onboarding session as complete
            var session = await _context.StripeOnboardingSessions
                .Where(s => s.CompanyId == id && !s.Completed)
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync();

            if (session != null)
            {
                session.Completed = true;
                session.CompletedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            // Get updated status
            var status = await _stripeConnectService.GetAccountStatusAsync(id);

            return Ok(new
            {
                success = true,
                status = status,
                message = "Onboarding completed successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing onboarding for company {CompanyId}", id);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get detailed Stripe Connect account status
    /// </summary>
    [HttpGet("{id}/stripe/connect/status")]
    public async Task<ActionResult<DTOs.Stripe.StripeAccountStatusDto>> GetConnectAccountStatus(Guid id)
    {
        var status = await _stripeConnectService.GetAccountStatusAsync(id);
        return Ok(status);
    }

    /// <summary>
    /// Sync account status from Stripe
    /// </summary>
    [HttpPost("{id}/stripe/connect/sync")]
    public async Task<ActionResult<object>> SyncAccountStatus(Guid id)
    {
        var company = await _context.Companies.FindAsync(id);
        if (company == null)
            return NotFound();

        if (string.IsNullOrEmpty(company.StripeAccountId))
            return BadRequest("Company does not have a Stripe account");

        try
        {
            var stripeAccountId = await ResolveStripeAccountIdAsync(company);
            if (string.IsNullOrEmpty(stripeAccountId))
                return BadRequest("Unable to retrieve Stripe account ID");

            await _stripeConnectService.SyncAccountStatusAsync(stripeAccountId);

            var status = await _stripeConnectService.GetAccountStatusAsync(id);

            return Ok(new
            {
                success = true,
                status = status,
                syncedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing account status for company {CompanyId}", id);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get Stripe Connect dashboard link (for Express accounts)
    /// </summary>
    [HttpPost("{id}/stripe/connect/dashboard-link")]
    public async Task<ActionResult<object>> GetDashboardLink(Guid id)
    {
        var company = await _context.Companies.FindAsync(id);
        if (company == null)
            return NotFound();

        if (string.IsNullOrEmpty(company.StripeAccountId))
            return BadRequest("Company does not have a Stripe account");

        try
        {
            var stripeAccountId = await ResolveStripeAccountIdAsync(company);
            if (string.IsNullOrEmpty(stripeAccountId))
                return BadRequest("Unable to retrieve Stripe account ID");

            // Create login link for Express dashboard
            var loginLinkService = new AccountLinkService();
            var options = new AccountLinkCreateOptions
            {
                Account = stripeAccountId,
                RefreshUrl = $"{Request.Scheme}://{Request.Host}/company/{id}/stripe/refresh",
                ReturnUrl = $"{Request.Scheme}://{Request.Host}/company/{id}/stripe/return",
                Type = "account_onboarding"
            };

            var loginLink = await loginLinkService.CreateAsync(options);

            return Ok(new
            {
                success = true,
                url = loginLink.Url,
                expiresAt = DateTime.UtcNow.AddHours(1)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating dashboard link for company {CompanyId}", id);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get company financial summary with platform fees
    /// </summary>
    [HttpGet("{id}/financials")]
    public async Task<ActionResult<object>> GetFinancials(
        Guid id,
        DateTime? fromDate = null,
        DateTime? toDate = null)
    {
        var company = await _context.Companies
            .Include(c => c.Bookings)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (company == null)
            return NotFound();

        var query = _context.Bookings
            .Where(b => b.CompanyId == id && 
                       (b.Status == BookingStatus.Confirmed || 
                        b.Status == BookingStatus.PickedUp || 
                        b.Status == BookingStatus.Returned));

        if (fromDate.HasValue)
            query = query.Where(b => b.CreatedAt >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(b => b.CreatedAt <= toDate.Value);

        var bookings = await query.ToListAsync();

        var totalRevenue = bookings.Sum(b => b.TotalAmount);
        var platformFees = bookings.Sum(b => b.PlatformFeeAmount);
        var netRevenue = bookings.Sum(b => b.NetAmount ?? 0m);

        var transfers = await _context.StripeTransfers
            .Where(t => t.CompanyId == id)
            .ToListAsync();

        var transferredAmount = transfers
            .Where(t => t.Status == "paid")
            .Sum(t => t.NetAmount);

        var pendingTransfers = transfers
            .Where(t => t.Status == "pending")
            .Sum(t => t.NetAmount);

        return Ok(new
        {
            companyId = id,
            companyName = company.CompanyName,
            platformFeePercentage = company.PlatformFeePercentage,
            period = new
            {
                from = fromDate ?? bookings.Min(b => (DateTime?)b.CreatedAt),
                to = toDate ?? bookings.Max(b => (DateTime?)b.CreatedAt)
            },
            revenue = new
            {
                totalRevenue = totalRevenue,
                platformFees = platformFees,
                netRevenue = netRevenue
            },
            transfers = new
            {
                transferred = transferredAmount,
                pending = pendingTransfers,
                totalTransfers = transfers.Count
            },
            bookings = new
            {
                total = bookings.Count,
                confirmed = bookings.Count(b => b.Status == BookingStatus.Confirmed),
                completed = bookings.Count(b => b.Status == BookingStatus.Returned)
            }
        });
    }
}
