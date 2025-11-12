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
using Microsoft.Extensions.Hosting;

namespace CarRental.Api.Middleware
{
    public class CompanyMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<CompanyMiddleware> _logger;
        private readonly IWebHostEnvironment _environment;

        public CompanyMiddleware(RequestDelegate next, ILogger<CompanyMiddleware> logger, IWebHostEnvironment environment)
        {
            _next = next;
            _logger = logger;
            _environment = environment;
        }

        public async Task InvokeAsync(HttpContext context, ICompanyService companyService)
        {
            string? companyId = null;
            string? source = null;

            try
            {
                // Check if EF Core is available (database might not be initialized yet)
                // This prevents errors during app startup when EF Core is still initializing
                if (companyService == null)
                {
                    _logger.LogWarning("CompanyMiddleware: ICompanyService is null, skipping company resolution");
                    await _next(context);
                    return;
                }
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
                    // Handle case where query parameter might be duplicated (take first value)
                    var queryValue = queryCompanyId.ToString();
                    // If comma-separated, take the first one
                    if (queryValue.Contains(','))
                    {
                        queryValue = queryValue.Split(',')[0].Trim();
                        _logger.LogWarning(
                            "CompanyMiddleware: Duplicate companyId in query, using first value: {CompanyId}",
                            queryValue
                        );
                    }
                    companyId = queryValue;
                    source = "query";
                    _logger.LogInformation(
                        "CompanyMiddleware: Found company ID {CompanyId} from query parameter",
                        companyId
                    );
                }
                // Priority 3: Try to determine from hostname (if request directly to API)
                else
                {
                    // Check X-Forwarded-Host first (for proxy requests), then Host header
                    var forwardedHost = context.Request.Headers["X-Forwarded-Host"].ToString();
                    var hostname = !string.IsNullOrEmpty(forwardedHost) 
                        ? forwardedHost.Split(',')[0].Trim().ToLowerInvariant().Split(':')[0] // Take first host, remove port
                        : context.Request.Host.Host.ToLowerInvariant();
                    
                    _logger.LogDebug(
                        "CompanyMiddleware: Trying hostname resolution. X-Forwarded-Host: {ForwardedHost}, Host: {Host}, Using: {Hostname}",
                        forwardedHost,
                        context.Request.Host.Host,
                        hostname
                    );
                    
                    // Development fallback: Use miamilifecars as default company in development on localhost
                    if (string.IsNullOrEmpty(companyId) && 
                        (hostname == "localhost" || hostname == "127.0.0.1") &&
                        _environment.IsDevelopment())
                    {
                        var defaultCompany = await companyService.GetCompanyBySubdomainAsync("miamilifecars");
                        if (defaultCompany != null)
                        {
                            companyId = defaultCompany.Id.ToString();
                            source = "dev-default";
                            _logger.LogInformation(
                                "Development mode: Using default company {CompanyId} ({CompanyName}) for localhost",
                                companyId,
                                defaultCompany.CompanyName
                            );
                        }
                        else
                        {
                            _logger.LogWarning(
                                "Development mode: Default company with subdomain 'miamilifecars' not found. Please ensure this company exists in the database."
                            );
                        }
                    }
                    // Try to resolve from hostname for non-localhost
                    else if (hostname != "localhost" && hostname != "127.0.0.1" && !hostname.Contains("azurewebsites.net"))
                    {
                        var company = await companyService.GetCompanyByFullDomainAsync(hostname);
                        
                        if (company != null)
                        {
                            companyId = company.Id.ToString();
                            source = "hostname";
                            _logger.LogInformation(
                                "Resolved company {CompanyId} ({CompanyName}) from hostname {Hostname}",
                                companyId,
                                company.CompanyName,
                                hostname
                            );
                        }
                        else
                        {
                            _logger.LogWarning(
                                "Could not resolve company from hostname {Hostname}",
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
            catch (System.ArgumentException ex) when (ex.Message.Contains("not a valid Type token") || ex.Message.Contains("metadataToken"))
            {
                // This is a critical EF Core deployment issue - compiled models are corrupted
                _logger.LogCritical(
                    ex,
                    "CRITICAL: Entity Framework model resolution failed. This indicates a deployment/build issue. " +
                    "The deployed assemblies are corrupted or mismatched. " +
                    "Company resolution disabled. Please rebuild and redeploy the application. " +
                    "Error: {ErrorMessage}",
                    ex.Message
                );
                // Don't throw - continue processing even if company resolution fails
            }
            catch (System.BadImageFormatException ex)
            {
                // This indicates corrupted DLLs in deployment
                _logger.LogCritical(
                    ex,
                    "CRITICAL: BadImageFormatException in CompanyMiddleware. " +
                    "The deployed assemblies are corrupted. " +
                    "Company resolution disabled. Please rebuild and redeploy the application. " +
                    "Error: {ErrorMessage}",
                    ex.Message
                );
                // Don't throw - continue processing even if company resolution fails
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CompanyMiddleware: {ErrorMessage}", ex.Message);
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

