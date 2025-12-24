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
/// Repository interface for vehicle social posts
/// </summary>
public interface IVehicleSocialPostRepository
{
    Task<VehicleSocialPost?> GetByVehicleAndPlatformAsync(Guid companyId, Guid vehicleId, SocialPlatform platform);
    Task<IEnumerable<VehicleSocialPost>> GetByVehicleAsync(Guid companyId, Guid vehicleId);
    Task<IEnumerable<VehicleSocialPost>> GetByVehicleIdsAsync(Guid companyId, IEnumerable<Guid> vehicleIds);
    Task<IEnumerable<VehicleSocialPost>> GetByCompanyAsync(Guid companyId);
    Task SaveAsync(VehicleSocialPost post);
    Task UpdateAsync(VehicleSocialPost post);
    Task MarkAsDeletedAsync(Guid companyId, Guid vehicleId, SocialPlatform platform);
    Task DeleteAsync(Guid id);
}

/// <summary>
/// EF Core implementation of IVehicleSocialPostRepository
/// </summary>
public class VehicleSocialPostRepository : IVehicleSocialPostRepository
{
    private readonly CarRentalDbContext _context;

    public VehicleSocialPostRepository(CarRentalDbContext context)
    {
        _context = context;
    }

    public async Task<VehicleSocialPost?> GetByVehicleAndPlatformAsync(Guid companyId, Guid vehicleId, SocialPlatform platform)
    {
        return await _context.VehicleSocialPosts
            .Where(p => p.CompanyId == companyId)
            .Where(p => p.VehicleId == vehicleId)
            .Where(p => p.Platform == platform)
            .Where(p => p.IsActive)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<VehicleSocialPost>> GetByVehicleAsync(Guid companyId, Guid vehicleId)
    {
        return await _context.VehicleSocialPosts
            .Where(p => p.CompanyId == companyId)
            .Where(p => p.VehicleId == vehicleId)
            .Where(p => p.IsActive)
            .ToListAsync();
    }

    public async Task<IEnumerable<VehicleSocialPost>> GetByVehicleIdsAsync(Guid companyId, IEnumerable<Guid> vehicleIds)
    {
        var ids = vehicleIds.ToList();
        return await _context.VehicleSocialPosts
            .Where(p => p.CompanyId == companyId)
            .Where(p => ids.Contains(p.VehicleId))
            .Where(p => p.IsActive)
            .ToListAsync();
    }

    public async Task<IEnumerable<VehicleSocialPost>> GetByCompanyAsync(Guid companyId)
    {
        return await _context.VehicleSocialPosts
            .Where(p => p.CompanyId == companyId)
            .Where(p => p.IsActive)
            .ToListAsync();
    }

    public async Task SaveAsync(VehicleSocialPost post)
    {
        // Check for existing active post for same vehicle/platform
        var existing = await _context.VehicleSocialPosts
            .FirstOrDefaultAsync(p => 
                p.CompanyId == post.CompanyId && 
                p.VehicleId == post.VehicleId && 
                p.Platform == post.Platform &&
                p.IsActive);

        if (existing != null)
        {
            // Mark old post as inactive
            existing.IsActive = false;
        }

        post.CreatedAt = DateTime.UtcNow;
        post.UpdatedAt = DateTime.UtcNow;
        _context.VehicleSocialPosts.Add(post);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(VehicleSocialPost post)
    {
        var existing = await _context.VehicleSocialPosts.FindAsync(post.Id);

        if (existing != null)
        {
            existing.PostId = post.PostId;
            existing.Permalink = post.Permalink;
            existing.Caption = post.Caption;
            existing.ImageUrl = post.ImageUrl;
            existing.DailyRate = post.DailyRate;
            existing.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }
    }

    public async Task MarkAsDeletedAsync(Guid companyId, Guid vehicleId, SocialPlatform platform)
    {
        var post = await _context.VehicleSocialPosts
            .FirstOrDefaultAsync(p =>
                p.CompanyId == companyId &&
                p.VehicleId == vehicleId &&
                p.Platform == platform &&
                p.IsActive);

        if (post != null)
        {
            post.IsActive = false;
            post.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task DeleteAsync(Guid id)
    {
        var post = await _context.VehicleSocialPosts.FindAsync(id);

        if (post != null)
        {
            _context.VehicleSocialPosts.Remove(post);
            await _context.SaveChangesAsync();
        }
    }
}
