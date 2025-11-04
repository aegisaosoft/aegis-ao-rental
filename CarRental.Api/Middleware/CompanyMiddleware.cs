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
using System.Threading.Tasks;
using CarRental.Api.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace CarRental.Api.Middleware
{
    public class CompanyMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<CompanyMiddleware> _logger;

        public CompanyMiddleware(RequestDelegate next, ILogger<CompanyMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, ICompanyService companyService)
        {
            string? companyId = null;
            string? source = null;

            try
            {
                // Log all incoming headers for debugging (only in development)
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug(
                        "CompanyMiddleware: Request to {Path} from host {Host}, Headers: X-Company-Id={HeaderCompanyId}",
                        context.Request.Path,
                        context.Request.Host.Host,
                        context.Request.Headers.ContainsKey("X-Company-Id") ? context.Request.Headers["X-Company-Id"].ToString() : "none"
                    );
                }

                // Priority 1: Try to get from header (sent by Node.js proxy)
                if (context.Request.Headers.TryGetValue("X-Company-Id", out var headerCompanyId) &&
                    !string.IsNullOrWhiteSpace(headerCompanyId))
                {
                    companyId = headerCompanyId.ToString();
                    source = "header";
                    _logger.LogInformation(
                        "CompanyMiddleware: Found company ID {CompanyId} from X-Company-Id header",
                        companyId
                    );
                }
                // Priority 2: Try to get from query string (fallback)
                else if (context.Request.Query.TryGetValue("companyId", out var queryCompanyId) &&
                         !string.IsNullOrWhiteSpace(queryCompanyId))
                {
                    companyId = queryCompanyId.ToString();
                    source = "query";
                }
                // Priority 3: Try to determine from hostname (if request directly to API)
                else
                {
                    var hostname = context.Request.Host.Host.ToLowerInvariant();
                    
                    // Skip resolution for localhost
                    if (hostname != "localhost" && hostname != "127.0.0.1")
                    {
                        var company = await companyService.GetCompanyByFullDomainAsync(hostname);
                        
                        if (company != null)
                        {
                            companyId = company.Id.ToString();
                            source = "hostname";
                            _logger.LogDebug(
                                "Resolved company {CompanyId} ({CompanyName}) from hostname {Hostname}",
                                companyId,
                                company.CompanyName,
                                hostname
                            );
                        }
                    }
                }

                // Store company ID in HttpContext.Items
                if (!string.IsNullOrEmpty(companyId))
                {
                    context.Items["CompanyId"] = companyId;
                    
                    // Validate that company exists and is active
                    if (Guid.TryParse(companyId, out var guid))
                    {
                        var company = await companyService.GetCompanyByIdAsync(guid);
                        
                        if (company != null)
                        {
                            context.Items["Company"] = company;
                            _logger.LogDebug(
                                "Company context set: {CompanyName} (ID: {CompanyId}) from {Source}",
                                company.CompanyName,
                                companyId,
                                source
                            );
                        }
                        else
                        {
                            _logger.LogWarning(
                                "Company ID {CompanyId} from {Source} not found or inactive",
                                companyId,
                                source
                            );
                            // Clear invalid company ID
                            context.Items.Remove("CompanyId");
                        }
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Invalid company ID format: {CompanyId} from {Source}",
                            companyId,
                            source
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CompanyMiddleware");
                // Don't throw - continue processing even if company resolution fails
            }

            await _next(context);
        }
    }

    // Extension method to add middleware to pipeline
    public static class CompanyMiddlewareExtensions
    {
        public static IApplicationBuilder UseCompanyMiddleware(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<CompanyMiddleware>();
        }
    }
}

