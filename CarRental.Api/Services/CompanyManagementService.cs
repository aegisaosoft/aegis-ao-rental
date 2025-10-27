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

using Microsoft.EntityFrameworkCore;
using CarRental.Api.Data;
using CarRental.Api.DTOs;
using CarRental.Api.Models;

namespace CarRental.Api.Services;

public interface ICompanyManagementService
{
    Task<CompanyStatsDto> GetCompanyStatsAsync(Guid companyId);
    Task<RevenueReportDto> GetRevenueReportAsync(Guid companyId, DateTime? fromDate, DateTime? toDate);
    Task<IEnumerable<VehicleDto>> GetCompanyVehiclesAsync(Guid companyId, VehicleSearchDto searchDto);
    Task<IEnumerable<ReservationDto>> GetCompanyReservationsAsync(Guid companyId, ReservationSearchDto searchDto);
    Task<bool> CanDeleteCompanyAsync(Guid companyId);
    Task<decimal> CalculateCompanyRevenueAsync(Guid companyId, DateTime? fromDate, DateTime? toDate);
    Task<int> GetActiveVehicleCountAsync(Guid companyId);
    Task<int> GetActiveReservationCountAsync(Guid companyId);
    Task<int> GetActiveRentalCountAsync(Guid companyId);
}

public class CompanyManagementService : ICompanyManagementService
{
    private readonly CarRentalDbContext _context;
    private readonly ILogger<CompanyManagementService> _logger;

    public CompanyManagementService(CarRentalDbContext context, ILogger<CompanyManagementService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<CompanyStatsDto> GetCompanyStatsAsync(Guid companyId)
    {
        try
        {
            var stats = new CompanyStatsDto
            {
                TotalVehicles = await _context.Vehicles.CountAsync(v => v.CompanyId == companyId),
                ActiveVehicles = await _context.Vehicles.CountAsync(v => v.CompanyId == companyId && v.IsActive && v.Status == "available"),
                TotalReservations = await _context.Reservations.CountAsync(r => r.CompanyId == companyId),
                ActiveReservations = await _context.Reservations.CountAsync(r => r.CompanyId == companyId && r.Status == "confirmed"),
                TotalRentals = await _context.Rentals.CountAsync(r => r.CompanyId == companyId),
                ActiveRentals = await _context.Rentals.CountAsync(r => r.CompanyId == companyId && r.Status == "active"),
                TotalRevenue = await _context.Payments.Where(p => p.CompanyId == companyId && p.Status == "succeeded")
                    .SumAsync(p => p.Amount),
                AverageRating = await _context.Reviews.Where(r => r.CompanyId == companyId)
                    .AverageAsync(r => (double?)r.Rating),
                LastActivity = await _context.Reservations
                    .Where(r => r.CompanyId == companyId)
                    .OrderByDescending(r => r.CreatedAt)
                    .Select(r => r.CreatedAt)
                    .FirstOrDefaultAsync()
            };

            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting company stats for {CompanyId}", companyId);
            throw;
        }
    }

    public async Task<RevenueReportDto> GetRevenueReportAsync(Guid companyId, DateTime? fromDate, DateTime? toDate)
    {
        try
        {
            var company = await _context.RentalCompanies.FindAsync(companyId);
            if (company == null)
                throw new ArgumentException("Company not found");

            var query = _context.Payments
                .Where(p => p.CompanyId == companyId && p.Status == "succeeded");

            if (fromDate.HasValue)
                query = query.Where(p => p.CreatedAt >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(p => p.CreatedAt <= toDate.Value);

            var dailyRevenue = await query
                .GroupBy(p => p.CreatedAt.Date)
                .Select(g => new DailyRevenue
                {
                    Date = g.Key,
                    TotalAmount = g.Sum(p => p.Amount),
                    TransactionCount = g.Count()
                })
                .OrderBy(r => r.Date)
                .ToListAsync();

            var totalRevenue = dailyRevenue.Sum(r => r.TotalAmount);
            var totalTransactions = dailyRevenue.Sum(r => r.TransactionCount);
            var averageTransaction = totalTransactions > 0 ? totalRevenue / totalTransactions : 0;

            return new RevenueReportDto
            {
                CompanyName = company.CompanyName,
                Period = new DateRange
                {
                    From = fromDate ?? dailyRevenue.FirstOrDefault()?.Date,
                    To = toDate ?? dailyRevenue.LastOrDefault()?.Date
                },
                Summary = new RevenueSummary
                {
                    TotalRevenue = totalRevenue,
                    TotalTransactions = totalTransactions,
                    AverageTransaction = averageTransaction
                },
                DailyRevenue = dailyRevenue
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting revenue report for company {CompanyId}", companyId);
            throw;
        }
    }

    public async Task<IEnumerable<VehicleDto>> GetCompanyVehiclesAsync(Guid companyId, VehicleSearchDto searchDto)
    {
        try
        {
            var query = _context.Vehicles
                .Include(v => v.Category)
                .Where(v => v.CompanyId == companyId);

            if (searchDto.CategoryId.HasValue)
                query = query.Where(v => v.CategoryId == searchDto.CategoryId.Value);

            if (!string.IsNullOrEmpty(searchDto.Make))
                query = query.Where(v => v.Make.Contains(searchDto.Make));

            if (!string.IsNullOrEmpty(searchDto.Model))
                query = query.Where(v => v.Model.Contains(searchDto.Model));

            if (searchDto.MinYear.HasValue)
                query = query.Where(v => v.Year >= searchDto.MinYear.Value);

            if (searchDto.MaxYear.HasValue)
                query = query.Where(v => v.Year <= searchDto.MaxYear.Value);

            if (searchDto.MinDailyRate.HasValue)
                query = query.Where(v => v.DailyRate >= searchDto.MinDailyRate.Value);

            if (searchDto.MaxDailyRate.HasValue)
                query = query.Where(v => v.DailyRate <= searchDto.MaxDailyRate.Value);

            if (!string.IsNullOrEmpty(searchDto.FuelType))
                query = query.Where(v => v.FuelType == searchDto.FuelType);

            if (!string.IsNullOrEmpty(searchDto.Transmission))
                query = query.Where(v => v.Transmission == searchDto.Transmission);

            if (searchDto.MinSeats.HasValue)
                query = query.Where(v => v.Seats >= searchDto.MinSeats.Value);

            if (!string.IsNullOrEmpty(searchDto.Location))
                query = query.Where(v => v.Location != null && v.Location.Contains(searchDto.Location));

            if (searchDto.IsActive.HasValue)
                query = query.Where(v => v.IsActive == searchDto.IsActive.Value);

            var vehicles = await query
                .OrderBy(v => v.Make)
                .ThenBy(v => v.Model)
                .Skip((searchDto.Page - 1) * searchDto.PageSize)
                .Take(searchDto.PageSize)
                .Select(v => new VehicleDto
                {
                    VehicleId = v.VehicleId,
                    CompanyId = v.CompanyId,
                    CategoryId = v.CategoryId,
                    Make = v.Make,
                    Model = v.Model,
                    Year = v.Year,
                    Color = v.Color,
                    LicensePlate = v.LicensePlate,
                    Vin = v.Vin,
                    Mileage = v.Mileage,
                    FuelType = v.FuelType,
                    Transmission = v.Transmission,
                    Seats = v.Seats,
                    DailyRate = v.DailyRate,
                    Status = v.Status,
                    Location = v.Location,
                    ImageUrl = v.ImageUrl,
                    Features = v.Features,
                    IsActive = v.IsActive,
                    CreatedAt = v.CreatedAt,
                    UpdatedAt = v.UpdatedAt,
                    CategoryName = v.Category != null ? v.Category.CategoryName : null
                })
                .ToListAsync();

            return vehicles;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting company vehicles for {CompanyId}", companyId);
            throw;
        }
    }

    public async Task<IEnumerable<ReservationDto>> GetCompanyReservationsAsync(Guid companyId, ReservationSearchDto searchDto)
    {
        try
        {
            var query = _context.Reservations
                .Include(r => r.Customer)
                .Include(r => r.Vehicle)
                .Where(r => r.CompanyId == companyId);

            if (searchDto.CustomerId.HasValue)
                query = query.Where(r => r.CustomerId == searchDto.CustomerId.Value);

            if (searchDto.VehicleId.HasValue)
                query = query.Where(r => r.VehicleId == searchDto.VehicleId.Value);

            if (!string.IsNullOrEmpty(searchDto.ReservationNumber))
                query = query.Where(r => r.ReservationNumber.Contains(searchDto.ReservationNumber));

            if (searchDto.PickupDateFrom.HasValue)
                query = query.Where(r => r.PickupDate >= searchDto.PickupDateFrom.Value);

            if (searchDto.PickupDateTo.HasValue)
                query = query.Where(r => r.PickupDate <= searchDto.PickupDateTo.Value);

            if (searchDto.ReturnDateFrom.HasValue)
                query = query.Where(r => r.ReturnDate >= searchDto.ReturnDateFrom.Value);

            if (searchDto.ReturnDateTo.HasValue)
                query = query.Where(r => r.ReturnDate <= searchDto.ReturnDateTo.Value);

            if (!string.IsNullOrEmpty(searchDto.Status))
                query = query.Where(r => r.Status == searchDto.Status);

            if (searchDto.CreatedFrom.HasValue)
                query = query.Where(r => r.CreatedAt >= searchDto.CreatedFrom.Value);

            if (searchDto.CreatedTo.HasValue)
                query = query.Where(r => r.CreatedAt <= searchDto.CreatedTo.Value);

            var reservations = await query
                .OrderByDescending(r => r.CreatedAt)
                .Skip((searchDto.Page - 1) * searchDto.PageSize)
                .Take(searchDto.PageSize)
                .Select(r => new ReservationDto
                {
                    ReservationId = r.ReservationId,
                    CustomerId = r.CustomerId,
                    VehicleId = r.VehicleId,
                    CompanyId = r.CompanyId,
                    ReservationNumber = r.ReservationNumber,
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
                    VehicleName = r.Vehicle.Make + " " + r.Vehicle.Model + " (" + r.Vehicle.Year + ")",
                    LicensePlate = r.Vehicle.LicensePlate
                })
                .ToListAsync();

            return reservations;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting company reservations for {CompanyId}", companyId);
            throw;
        }
    }

    public async Task<bool> CanDeleteCompanyAsync(Guid companyId)
    {
        try
        {
            var hasActiveVehicles = await _context.Vehicles
                .AnyAsync(v => v.CompanyId == companyId && v.IsActive);

            var hasActiveReservations = await _context.Reservations
                .AnyAsync(r => r.CompanyId == companyId && r.Status == "confirmed");

            var hasActiveRentals = await _context.Rentals
                .AnyAsync(r => r.CompanyId == companyId && r.Status == "active");

            return !hasActiveVehicles && !hasActiveReservations && !hasActiveRentals;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if company can be deleted {CompanyId}", companyId);
            return false;
        }
    }

    public async Task<decimal> CalculateCompanyRevenueAsync(Guid companyId, DateTime? fromDate, DateTime? toDate)
    {
        try
        {
            var query = _context.Payments
                .Where(p => p.CompanyId == companyId && p.Status == "succeeded");

            if (fromDate.HasValue)
                query = query.Where(p => p.CreatedAt >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(p => p.CreatedAt <= toDate.Value);

            return await query.SumAsync(p => p.Amount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating company revenue for {CompanyId}", companyId);
            return 0;
        }
    }

    public async Task<int> GetActiveVehicleCountAsync(Guid companyId)
    {
        try
        {
            return await _context.Vehicles
                .CountAsync(v => v.CompanyId == companyId && v.IsActive && v.Status == "available");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active vehicle count for {CompanyId}", companyId);
            return 0;
        }
    }

    public async Task<int> GetActiveReservationCountAsync(Guid companyId)
    {
        try
        {
            return await _context.Reservations
                .CountAsync(r => r.CompanyId == companyId && r.Status == "confirmed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active reservation count for {CompanyId}", companyId);
            return 0;
        }
    }

    public async Task<int> GetActiveRentalCountAsync(Guid companyId)
    {
        try
        {
            return await _context.Rentals
                .CountAsync(r => r.CompanyId == companyId && r.Status == "active");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active rental count for {CompanyId}", companyId);
            return 0;
        }
    }
}
