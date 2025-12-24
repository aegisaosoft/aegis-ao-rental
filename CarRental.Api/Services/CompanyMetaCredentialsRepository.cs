/*
 *
 * Copyright (c) 2025 Alexander Orlov.
 * 34 Middletown Ave, Atlantic Highlands, NJ 07716
 *
 * THIS SOFTWARE IS THE CONFIDENTIAL AND PROPRIETARY INFORMATION OF
 * Alexander Orlov ("CONFIDENTIAL INFORMATION").
 *
 * Author: Alexander Orlov
 * Aegis AO Soft
 *
 */

using Microsoft.EntityFrameworkCore;
using CarRental.Api.Data;
using CarRental.Api.Models;

namespace CarRental.Api.Services;

/// <summary>
/// Repository interface for company Meta credentials
/// </summary>
public interface ICompanyMetaCredentialsRepository
{
    Task<CompanyMetaCredentials?> GetByCompanyIdAsync(Guid companyId);
    Task SaveAsync(CompanyMetaCredentials credentials);
    Task UpdateAsync(CompanyMetaCredentials credentials);
    Task DeleteAsync(Guid companyId);
    Task<IEnumerable<CompanyMetaCredentials>> GetExpiringTokensAsync(int daysUntilExpiration);
    Task<IEnumerable<CompanyMetaCredentials>> GetAllActiveAsync();
    Task UpdateStatusAsync(Guid companyId, MetaCredentialStatus status);
}

/// <summary>
/// EF Core implementation of ICompanyMetaCredentialsRepository
/// </summary>
public class CompanyMetaCredentialsRepository : ICompanyMetaCredentialsRepository
{
    private readonly CarRentalDbContext _context;

    public CompanyMetaCredentialsRepository(CarRentalDbContext context)
    {
        _context = context;
    }

    public async Task<CompanyMetaCredentials?> GetByCompanyIdAsync(Guid companyId)
    {
        return await _context.CompanyMetaCredentials
            .FirstOrDefaultAsync(c => c.CompanyId == companyId);
    }

    public async Task SaveAsync(CompanyMetaCredentials credentials)
    {
        var existing = await _context.CompanyMetaCredentials
            .FirstOrDefaultAsync(c => c.CompanyId == credentials.CompanyId);

        if (existing == null)
        {
            _context.CompanyMetaCredentials.Add(credentials);
        }
        else
        {
            // Update existing
            existing.UserAccessToken = credentials.UserAccessToken;
            existing.TokenExpiresAt = credentials.TokenExpiresAt;
            existing.PageId = credentials.PageId;
            existing.PageName = credentials.PageName;
            existing.PageAccessToken = credentials.PageAccessToken;
            existing.CatalogId = credentials.CatalogId;
            existing.PixelId = credentials.PixelId;
            existing.AvailablePages = credentials.AvailablePages;
            existing.InstagramAccountId = credentials.InstagramAccountId;
            existing.InstagramUsername = credentials.InstagramUsername;
            existing.Status = credentials.Status;
            existing.LastTokenRefresh = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(CompanyMetaCredentials credentials)
    {
        var existing = await _context.CompanyMetaCredentials
            .FirstOrDefaultAsync(c => c.CompanyId == credentials.CompanyId);

        if (existing != null)
        {
            existing.UserAccessToken = credentials.UserAccessToken;
            existing.TokenExpiresAt = credentials.TokenExpiresAt;
            existing.PageId = credentials.PageId;
            existing.PageName = credentials.PageName;
            existing.PageAccessToken = credentials.PageAccessToken;
            existing.CatalogId = credentials.CatalogId;
            existing.PixelId = credentials.PixelId;
            existing.AvailablePages = credentials.AvailablePages;
            existing.InstagramAccountId = credentials.InstagramAccountId;
            existing.InstagramUsername = credentials.InstagramUsername;
            existing.Status = credentials.Status;
            existing.LastTokenRefresh = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }
    }

    public async Task DeleteAsync(Guid companyId)
    {
        var credentials = await _context.CompanyMetaCredentials
            .FirstOrDefaultAsync(c => c.CompanyId == companyId);

        if (credentials != null)
        {
            _context.CompanyMetaCredentials.Remove(credentials);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<IEnumerable<CompanyMetaCredentials>> GetExpiringTokensAsync(int daysUntilExpiration)
    {
        var threshold = DateTime.UtcNow.AddDays(daysUntilExpiration);

        return await _context.CompanyMetaCredentials
            .Where(c => c.Status == MetaCredentialStatus.Active)
            .Where(c => c.TokenExpiresAt <= threshold)
            .ToListAsync();
    }

    public async Task<IEnumerable<CompanyMetaCredentials>> GetAllActiveAsync()
    {
        return await _context.CompanyMetaCredentials
            .Where(c => c.Status == MetaCredentialStatus.Active)
            .ToListAsync();
    }

    public async Task UpdateStatusAsync(Guid companyId, MetaCredentialStatus status)
    {
        var credentials = await _context.CompanyMetaCredentials
            .FirstOrDefaultAsync(c => c.CompanyId == companyId);

        if (credentials != null)
        {
            credentials.Status = status;
            await _context.SaveChangesAsync();
        }
    }
}
