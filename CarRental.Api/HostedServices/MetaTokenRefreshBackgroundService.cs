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

using CarRental.Api.Models;
using CarRental.Api.Services;

namespace CarRental.Api.HostedServices;

/// <summary>
/// Background service that automatically refreshes Meta tokens before they expire
/// Runs daily to check for tokens expiring within 7 days
/// </summary>
public class MetaTokenRefreshBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MetaTokenRefreshBackgroundService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(24); // Run daily
    private const int DaysBeforeExpirationToRefresh = 7;

    public MetaTokenRefreshBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<MetaTokenRefreshBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Meta Token Refresh Background Service started");

        // Wait a bit on startup
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RefreshExpiringTokensAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Meta Token Refresh Background Service");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }
    }

    private async Task RefreshExpiringTokensAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var credentialsRepo = scope.ServiceProvider.GetRequiredService<ICompanyMetaCredentialsRepository>();
        var oauthService = scope.ServiceProvider.GetRequiredService<IMetaOAuthService>();

        var expiringCredentials = await credentialsRepo.GetExpiringTokensAsync(DaysBeforeExpirationToRefresh);
        var count = 0;

        foreach (var credentials in expiringCredentials)
        {
            if (stoppingToken.IsCancellationRequested)
                break;

            try
            {
                _logger.LogInformation(
                    "Refreshing Meta token for company {CompanyId}. Expires: {ExpiresAt}",
                    credentials.CompanyId,
                    credentials.TokenExpiresAt);

                await oauthService.RefreshLongLivedTokenAsync(credentials.CompanyId);
                count++;

                _logger.LogInformation(
                    "Successfully refreshed Meta token for company {CompanyId}",
                    credentials.CompanyId);

                // Small delay between refreshes to avoid rate limiting
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to refresh Meta token for company {CompanyId}",
                    credentials.CompanyId);

                // Mark as expired if refresh failed
                try
                {
                    await credentialsRepo.UpdateStatusAsync(
                        credentials.CompanyId,
                        MetaCredentialStatus.TokenExpired);
                }
                catch { /* Ignore status update errors */ }
            }
        }

        if (count > 0)
        {
            _logger.LogInformation("Refreshed {Count} Meta tokens", count);
        }
    }
}
