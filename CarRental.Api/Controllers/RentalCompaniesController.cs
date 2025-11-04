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
using CarRental.Api.DTOs;
using CarRental.Api.Models;
using CarRental.Api.Services;

namespace CarRental.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RentalCompaniesController : ControllerBase
{
    private readonly CarRentalDbContext _context;
    private readonly IStripeService _stripeService;
    private readonly ILogger<RentalCompaniesController> _logger;

    public RentalCompaniesController(
        CarRentalDbContext context, 
        IStripeService stripeService, 
        ILogger<RentalCompaniesController> logger)
    {
        _context = context;
        _stripeService = stripeService;
        _logger = logger;
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
                StripeAccountId = c.StripeAccountId,
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
                BookingIntegrated = c.BookingIntegrated,
                CompanyPath = c.CompanyPath,
                Subdomain = c.Subdomain,
                PrimaryColor = c.PrimaryColor,
                SecondaryColor = c.SecondaryColor,
                LogoUrl = c.LogoUrl,
                FaviconUrl = c.FaviconUrl,
                CustomCss = c.CustomCss,
                Country = c.Country,
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
            StripeAccountId = company.StripeAccountId,
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
            BookingIntegrated = company.BookingIntegrated,
            CompanyPath = company.CompanyPath,
            Subdomain = company.Subdomain,
            PrimaryColor = company.PrimaryColor,
            SecondaryColor = company.SecondaryColor,
            LogoUrl = company.LogoUrl,
            FaviconUrl = company.FaviconUrl,
            CustomCss = company.CustomCss,
            Country = company.Country,
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
            StripeAccountId = company.StripeAccountId,
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
            BookingIntegrated = company.BookingIntegrated,
            CompanyPath = company.CompanyPath,
            Subdomain = company.Subdomain,
            PrimaryColor = company.PrimaryColor,
            SecondaryColor = company.SecondaryColor,
            LogoUrl = company.LogoUrl,
            FaviconUrl = company.FaviconUrl,
            CustomCss = company.CustomCss,
            Country = company.Country,
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

        var company = new RentalCompany
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
            BookingIntegrated = createCompanyDto.BookingIntegrated,
            CompanyPath = createCompanyDto.CompanyPath,
            Subdomain = createCompanyDto.Subdomain,
            PrimaryColor = createCompanyDto.PrimaryColor,
            SecondaryColor = createCompanyDto.SecondaryColor,
            LogoUrl = createCompanyDto.LogoUrl,
            FaviconUrl = createCompanyDto.FaviconUrl,
            CustomCss = createCompanyDto.CustomCss,
            Country = createCompanyDto.Country,
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
                    "US"); // Default to US, country can be specified per location
                
                company.StripeAccountId = stripeAccount.Id;
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
                StripeAccountId = company.StripeAccountId,
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
                BookingIntegrated = company.BookingIntegrated,
                CompanyPath = company.CompanyPath,
                Subdomain = company.Subdomain,
                PrimaryColor = company.PrimaryColor,
                SecondaryColor = company.SecondaryColor,
                LogoUrl = company.LogoUrl,
                FaviconUrl = company.FaviconUrl,
                CustomCss = company.CustomCss,
                Country = company.Country,
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

        if (updateCompanyDto.BookingIntegrated != null)
            company.BookingIntegrated = updateCompanyDto.BookingIntegrated;

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
            company.Country = updateCompanyDto.Country;

        if (updateCompanyDto.IsActive.HasValue)
            company.IsActive = updateCompanyDto.IsActive.Value;

        company.UpdatedAt = DateTime.UtcNow;

        try
        {
            _context.Companies.Update(company);
            await _context.SaveChangesAsync();

            // Update Stripe Connect account if exists
            if (!string.IsNullOrEmpty(company.StripeAccountId))
            {
                try
                {
                    await _stripeService.GetConnectedAccountAsync(company.StripeAccountId);
                    // Additional Stripe account updates can be added here
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to update Stripe Connect account {StripeAccountId}", company.StripeAccountId);
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

        var hasActiveReservations = await _context.Reservations
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
            TotalReservations = await _context.Reservations.CountAsync(r => r.CompanyId == id),
            ActiveReservations = await _context.Reservations.CountAsync(r => r.CompanyId == id && r.Status == "Confirmed"),
            TotalRentals = await _context.Rentals.CountAsync(r => r.CompanyId == id),
            ActiveRentals = await _context.Rentals.CountAsync(r => r.CompanyId == id && r.Status == "active"),
            TotalRevenue = await _context.Payments.Where(p => p.CompanyId == id && p.Status == "succeeded")
                .SumAsync(p => p.Amount),
            AverageRating = await _context.Reviews.Where(r => r.CompanyId == id)
                .AverageAsync(r => (double?)r.Rating),
            LastActivity = await _context.Reservations
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

        var query = _context.Reservations
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
    /// Setup Stripe Connect account for company
    /// </summary>
    [HttpPost("{id}/stripe/setup")]
    public async Task<ActionResult<object>> SetupStripeAccount(Guid id)
    {
        var company = await _context.Companies.FindAsync(id);
        if (company == null)
            return NotFound();

        if (!string.IsNullOrEmpty(company.StripeAccountId))
            return BadRequest("Company already has a Stripe account");

        try
        {
            var stripeAccount = await _stripeService.CreateConnectedAccountAsync(
                company.Email, 
                "US"); // Default to US - country information now stored in locations

            company.StripeAccountId = stripeAccount.Id;
            _context.Companies.Update(company);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                StripeAccountId = stripeAccount.Id,
                Status = stripeAccount.DetailsSubmitted ? "completed" : "pending",
                RequiresAction = !stripeAccount.DetailsSubmitted
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting up Stripe account for company {CompanyId}", id);
            return BadRequest($"Error setting up Stripe account: {ex.Message}");
        }
    }

    /// <summary>
    /// Get Stripe Connect account status
    /// </summary>
    [HttpGet("{id}/stripe/status")]
    public async Task<ActionResult<object>> GetStripeAccountStatus(Guid id)
    {
        var company = await _context.Companies.FindAsync(id);
        if (company == null)
            return NotFound();

        if (string.IsNullOrEmpty(company.StripeAccountId))
            return BadRequest("Company does not have a Stripe account");

        try
        {
            var stripeAccount = await _stripeService.GetConnectedAccountAsync(company.StripeAccountId);
            
            return Ok(new
            {
                StripeAccountId = stripeAccount.Id,
                Status = stripeAccount.DetailsSubmitted ? "completed" : "pending",
                ChargesEnabled = stripeAccount.ChargesEnabled,
                PayoutsEnabled = stripeAccount.PayoutsEnabled,
                RequiresAction = !stripeAccount.DetailsSubmitted,
                Country = stripeAccount.Country,
                Created = stripeAccount.Created
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Stripe account status for company {CompanyId}", id);
            return BadRequest($"Error getting Stripe account status: {ex.Message}");
        }
    }
}
