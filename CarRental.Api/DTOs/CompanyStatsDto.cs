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

namespace CarRental.Api.DTOs;

public class CompanyStatsDto
{
    public int TotalVehicles { get; set; }
    public int ActiveVehicles { get; set; }
    public int TotalReservations { get; set; }
    public int ActiveReservations { get; set; }
    public int TotalRentals { get; set; }
    public int ActiveRentals { get; set; }
    public decimal TotalRevenue { get; set; }
    public double? AverageRating { get; set; }
    public DateTime? LastActivity { get; set; }
}

public class RevenueReportDto
{
    public string CompanyName { get; set; } = string.Empty;
    public DateRange Period { get; set; } = new();
    public RevenueSummary Summary { get; set; } = new();
    public List<DailyRevenue> DailyRevenue { get; set; } = new();
}

public class DateRange
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
}

public class RevenueSummary
{
    public decimal TotalRevenue { get; set; }
    public int TotalTransactions { get; set; }
    public decimal AverageTransaction { get; set; }
}

public class DailyRevenue
{
    public DateTime Date { get; set; }
    public decimal TotalAmount { get; set; }
    public int TransactionCount { get; set; }
}

public class StripeAccountStatusDto
{
    public string StripeAccountId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool ChargesEnabled { get; set; }
    public bool PayoutsEnabled { get; set; }
    public bool RequiresAction { get; set; }
    public string? Country { get; set; }
    public DateTime Created { get; set; }
}

public class CompanySearchDto
{
    public string? Search { get; set; }
    public string? State { get; set; }
    public string? Country { get; set; }
    public bool? IsActive { get; set; }
    public DateTime? CreatedFrom { get; set; }
    public DateTime? CreatedTo { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
