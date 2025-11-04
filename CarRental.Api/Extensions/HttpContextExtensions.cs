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
using CarRental.Api.Models;
using Microsoft.AspNetCore.Http;

namespace CarRental.Api.Extensions
{
    public static class HttpContextExtensions
    {
        /// <summary>
        /// Gets the current company ID from HttpContext
        /// </summary>
        public static string? GetCompanyId(this HttpContext context)
        {
            return context.Items["CompanyId"]?.ToString();
        }

        /// <summary>
        /// Gets the current company ID as Guid from HttpContext
        /// </summary>
        public static Guid? GetCompanyIdAsGuid(this HttpContext context)
        {
            var companyId = context.GetCompanyId();
            
            if (string.IsNullOrEmpty(companyId))
            {
                return null;
            }

            return Guid.TryParse(companyId, out var guid) ? guid : null;
        }

        /// <summary>
        /// Gets the current company from HttpContext
        /// </summary>
        public static RentalCompany? GetCompany(this HttpContext context)
        {
            return context.Items["Company"] as RentalCompany;
        }

        /// <summary>
        /// Checks if a company context is set
        /// </summary>
        public static bool HasCompanyContext(this HttpContext context)
        {
            return context.Items.ContainsKey("CompanyId") && 
                   !string.IsNullOrEmpty(context.GetCompanyId());
        }

        /// <summary>
        /// Requires company context - throws exception if not present
        /// </summary>
        public static Guid RequireCompanyId(this HttpContext context)
        {
            var companyId = context.GetCompanyIdAsGuid();
            
            if (!companyId.HasValue)
            {
                throw new InvalidOperationException("Company context is required but not present");
            }

            return companyId.Value;
        }
    }
}

