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
using CarRental.Api.DTOs;
using CarRental.Api.Models;
using CarRental.Api.Extensions;

namespace CarRental.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ViolationsController : ControllerBase
{
    private readonly CarRentalDbContext _context;
    private readonly ILogger<ViolationsController> _logger;

    public ViolationsController(
        CarRentalDbContext context,
        ILogger<ViolationsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Get violations for a company with pagination and date filtering
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResult<ViolationDto>), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GetViolations(
        [FromQuery] Guid companyId,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page <= 0) page = 1;
        if (pageSize <= 0) pageSize = 20;
        if (pageSize > 100) pageSize = 100; // Limit page size

        try
        {
            // Get company ID from context (set by CompanyMiddleware) or from query parameter
            var resolvedCompanyId = HttpContext.GetCompanyIdAsGuid() ?? companyId;

            if (resolvedCompanyId == Guid.Empty)
            {
                return BadRequest(new { message = "Company ID is required" });
            }

            var query = _context.Violations
                .Include(v => v.Company)
                .Where(v => v.CompanyId == resolvedCompanyId && v.IsActive)
                .AsQueryable();

            // Filter by date range - use issue_date, start_date, or created_at
            if (dateFrom.HasValue)
            {
                var dateFromValue = dateFrom.Value.Date;
                query = query.Where(v => 
                    (v.IssueDate.HasValue && v.IssueDate.Value.Date >= dateFromValue) ||
                    (v.StartDate.HasValue && v.StartDate.Value.Date >= dateFromValue) ||
                    (!v.IssueDate.HasValue && !v.StartDate.HasValue && v.CreatedAt.Date >= dateFromValue));
            }

            if (dateTo.HasValue)
            {
                var dateToValue = dateTo.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(v => 
                    (v.IssueDate.HasValue && v.IssueDate.Value <= dateToValue) ||
                    (v.StartDate.HasValue && v.StartDate.Value <= dateToValue) ||
                    (!v.IssueDate.HasValue && !v.StartDate.HasValue && v.CreatedAt <= dateToValue));
            }

            // Filter by payment status (convert string status to int)
            if (!string.IsNullOrWhiteSpace(status))
            {
                var statusLower = status.ToLower();
                int? paymentStatus = statusLower switch
                {
                    "pending" => 0,
                    "paid" => 1,
                    "overdue" => 2,
                    "cancelled" => 3,
                    _ => null
                };

                if (paymentStatus.HasValue)
                {
                    query = query.Where(v => v.PaymentStatus == paymentStatus.Value);
                }
            }

            // Get total count
            var totalCount = await query.CountAsync();

            // Apply pagination
            var violations = await query
                .OrderByDescending(v => v.IssueDate ?? v.StartDate ?? v.CreatedAt)
                .ThenByDescending(v => v.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Map to DTOs
            var result = violations.Select(v => new ViolationDto
            {
                Id = v.Id,
                CompanyId = v.CompanyId,
                CitationNumber = v.CitationNumber,
                NoticeNumber = v.NoticeNumber,
                Provider = v.Provider,
                Agency = v.Agency,
                Address = v.Address,
                Tag = v.Tag,
                State = v.State,
                IssueDate = v.IssueDate,
                StartDate = v.StartDate,
                EndDate = v.EndDate,
                Amount = v.Amount,
                Currency = v.Currency,
                PaymentStatus = v.PaymentStatus,
                FineType = v.FineType,
                Note = v.Note,
                Link = v.Link,
                IsActive = v.IsActive,
                CreatedAt = v.CreatedAt,
                UpdatedAt = v.UpdatedAt
            }).ToList();

            _logger.LogInformation(
                "Retrieved {Count} violations for company {CompanyId} (page {Page}, pageSize {PageSize}, total {Total})",
                result.Count,
                resolvedCompanyId,
                page,
                pageSize,
                totalCount
            );

            return Ok(new PaginatedResult<ViolationDto>(result, totalCount, page, pageSize));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving violations for company {CompanyId}", companyId);
            
            // Check if it's a database table/column missing error
            if (ex.Message.Contains("does not exist") || ex.Message.Contains("violation") || 
                (ex.InnerException != null && (ex.InnerException.Message.Contains("does not exist") || ex.InnerException.Message.Contains("violation"))))
            {
                _logger.LogWarning("Violations table may not exist. Returning empty result. Please run the database migration: create_violations_table.sql");
                // Return empty data instead of error so frontend doesn't break
                return Ok(new PaginatedResult<ViolationDto>(new List<ViolationDto>(), 0, page, pageSize));
            }
            
            return StatusCode(500, new { message = "An error occurred while retrieving violations", error = ex.Message });
        }
    }
}
