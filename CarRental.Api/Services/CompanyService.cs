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
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CarRental.Api.Models;
using CarRental.Api.Data;
using Microsoft.EntityFrameworkCore;
using CarRental.Api.Helpers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace CarRental.Api.Services
{
    public interface ICompanyService
    {
        Task<Company?> GetCompanyByIdAsync(Guid companyId);
        Task<Company?> GetCompanyBySubdomainAsync(string subdomain);
        Task<Company?> GetCompanyByFullDomainAsync(string fullDomain);
        Task<Dictionary<string, Guid>> GetDomainMappingAsync();
        Task<IEnumerable<Company>> GetAllActiveCompaniesAsync();
        Task<IEnumerable<Company>> GetAllCompaniesAsync();
        Task<Company> CreateCompanyAsync(Company company);
        Task<Company> UpdateCompanyAsync(Company company);
        Task DeleteCompanyAsync(Guid companyId);
        void InvalidateCache();
    }

    public class CompanyService : ICompanyService
    {
        private readonly CarRentalDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<CompanyService> _logger;
        private const string DOMAIN_MAPPING_CACHE_KEY = "domain_mapping";
        private const int CACHE_DURATION_MINUTES = 15;

        public CompanyService(
            CarRentalDbContext context, 
            IMemoryCache cache,
            ILogger<CompanyService> logger)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
        }

        public async Task<Company?> GetCompanyByIdAsync(Guid companyId)
        {
            return await _context.Companies
                .FirstOrDefaultAsync(c => c.Id == companyId && c.IsActive);
        }

        public async Task<Company?> GetCompanyBySubdomainAsync(string subdomain)
        {
            var normalizedSubdomain = subdomain.ToLowerInvariant().Trim();
            
            return await _context.Companies
                .FirstOrDefaultAsync(c => 
                    c.Subdomain != null && 
                    c.Subdomain.ToLower() == normalizedSubdomain && 
                    c.IsActive);
        }

        public async Task<Company?> GetCompanyByFullDomainAsync(string fullDomain)
        {
            // Extract subdomain from full domain
            // company1.aegis-rental.com -> company1
            var parts = fullDomain.ToLowerInvariant().Split('.');
            if (parts.Length > 0)
            {
                var subdomain = parts[0];
                return await GetCompanyBySubdomainAsync(subdomain);
            }
            
            return null;
        }

        public async Task<Dictionary<string, Guid>> GetDomainMappingAsync()
        {
            // Try to get from cache
            if (_cache.TryGetValue(DOMAIN_MAPPING_CACHE_KEY, out Dictionary<string, Guid>? cachedMapping))
            {
                _logger.LogDebug("Domain mapping retrieved from cache");
                return cachedMapping!;
            }

            // Load from database
            var companies = await _context.Companies
                .Where(c => c.IsActive && !string.IsNullOrEmpty(c.Subdomain))
                .Select(c => new { c.Id, c.Subdomain })
                .ToListAsync();

            // Create mapping: full domain -> company ID
            var mapping = companies.ToDictionary(
                c => $"{c.Subdomain!.ToLower()}.aegis-rental.com",
                c => c.Id
            );

            // Cache the mapping
            _cache.Set(DOMAIN_MAPPING_CACHE_KEY, mapping, TimeSpan.FromMinutes(CACHE_DURATION_MINUTES));
            
            _logger.LogInformation("Loaded {Count} company domain mappings from database", mapping.Count);
            
            return mapping;
        }

        public async Task<IEnumerable<Company>> GetAllActiveCompaniesAsync()
        {
            return await _context.Companies
                .Where(c => c.IsActive)
                .OrderBy(c => c.CompanyName)
                .ToListAsync();
        }

        public async Task<IEnumerable<Company>> GetAllCompaniesAsync()
        {
            return await _context.Companies
                .OrderBy(c => c.CompanyName)
                .ToListAsync();
        }

        public async Task<Company> CreateCompanyAsync(Company company)
        {
            // Ensure subdomain is lowercase and trimmed
            if (!string.IsNullOrEmpty(company.Subdomain))
            {
                company.Subdomain = company.Subdomain.ToLower().Trim();
            }
            
            // Check if subdomain already exists
            if (!string.IsNullOrEmpty(company.Subdomain))
            {
                var existingCompany = await _context.Companies
                    .AnyAsync(c => c.Subdomain != null && c.Subdomain.ToLower() == company.Subdomain);
                
                if (existingCompany)
                {
                    throw new InvalidOperationException($"A company with subdomain '{company.Subdomain}' already exists");
                }
            }

            company.Id = Guid.NewGuid();
            company.CreatedAt = DateTime.UtcNow;
            company.UpdatedAt = DateTime.UtcNow;
            company.IsActive = true;
            company.Currency = CurrencyHelper.ResolveCurrency(company.Currency, company.Country);

            _context.Companies.Add(company);
            await _context.SaveChangesAsync();

            // Invalidate cache
            InvalidateCache();

            _logger.LogInformation(
                "Created new company: {CompanyName} with subdomain {Subdomain} (ID: {CompanyId})", 
                company.CompanyName, 
                company.Subdomain,
                company.Id
            );

            return company;
        }

        public async Task<Company> UpdateCompanyAsync(Company company)
        {
            company.Currency = CurrencyHelper.ResolveCurrency(company.Currency, company.Country);
            company.UpdatedAt = DateTime.UtcNow;

            _context.Companies.Update(company);
            await _context.SaveChangesAsync();

            // Invalidate cache
            InvalidateCache();

            _logger.LogInformation(
                "Updated company: {CompanyName} (ID: {CompanyId})", 
                company.CompanyName,
                company.Id
            );

            return company;
        }

        public async Task DeleteCompanyAsync(Guid companyId)
        {
            var company = await _context.Companies.FindAsync(companyId);
            
            if (company == null)
            {
                throw new KeyNotFoundException($"Company with ID {companyId} not found");
            }

            // Soft delete
            company.IsActive = false;
            company.UpdatedAt = DateTime.UtcNow;
            
            await _context.SaveChangesAsync();

            // Invalidate cache
            InvalidateCache();

            _logger.LogInformation(
                "Soft deleted company: {CompanyName} (ID: {CompanyId})", 
                company.CompanyName,
                companyId
            );
        }

        public void InvalidateCache()
        {
            _cache.Remove(DOMAIN_MAPPING_CACHE_KEY);
            _logger.LogDebug("Domain mapping cache invalidated");
        }
    }
}

