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

using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Dns;
using Azure.ResourceManager.Dns.Models;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.AppService.Models;
using Azure.ResourceManager.KeyVault;
using Azure.Security.KeyVault.Certificates;
using Azure.Core;
using System.Security.Cryptography.X509Certificates;

namespace CarRental.Api.Services;

public interface IAzureDnsService
{
    Task<bool> CreateSubdomainAsync(string subdomain, string? customTarget = null);
    Task<bool> DeleteSubdomainAsync(string subdomain);
    Task<bool> SubdomainExistsAsync(string subdomain);
    string GetSubdomainUrl(string subdomain);
    Task<bool> CreateVerificationRecordAsync(string subdomain, string verificationId);
    Task<bool> AddCustomDomainToAppServiceAsync(string subdomain);
    Task<bool> EnableSslForDomainAsync(string subdomain);
    Task<bool> CreateSubdomainWithSslAsync(string subdomain);
}

public class AzureDnsService : IAzureDnsService
{
    private readonly DnsZoneResource _dnsZone;
    private readonly WebSiteResource _appService;
    private readonly ILogger<AzureDnsService> _logger;
    private readonly string _appServiceHost;
    private readonly string _appServiceName;
    private readonly string _keyVaultName;
    private readonly string _certificateName;
    private readonly ClientSecretCredential _credential;
    private readonly string _subscriptionId;
    private readonly string _resourceGroup;

    public AzureDnsService(ISettingsService settingsService, ILogger<AzureDnsService> logger)
    {
        _logger = logger;

        // Get Azure settings
        _subscriptionId = Task.Run(async () => await settingsService.GetValueAsync("azure.subscriptionId")).Result
            ?? throw new ArgumentException("azure.subscriptionId not configured");
        var tenantId = Task.Run(async () => await settingsService.GetValueAsync("azure.tenantId")).Result
            ?? throw new ArgumentException("azure.tenantId not configured");
        var clientId = Task.Run(async () => await settingsService.GetValueAsync("azure.clientId")).Result
            ?? throw new ArgumentException("azure.clientId not configured");
        var clientSecret = Task.Run(async () => await settingsService.GetValueAsync("azure.clientSecret")).Result
            ?? throw new ArgumentException("azure.clientSecret not configured");
        _resourceGroup = Task.Run(async () => await settingsService.GetValueAsync("azure.resourceGroup")).Result
            ?? throw new ArgumentException("azure.resourceGroup not configured");
        var zoneName = Task.Run(async () => await settingsService.GetValueAsync("azure.dnsZoneName")).Result
            ?? throw new ArgumentException("azure.dnsZoneName not configured");

        _appServiceHost = Task.Run(async () => await settingsService.GetValueAsync("azure.appServiceHost")).Result
            ?? throw new ArgumentException("azure.appServiceHost not configured");
        _appServiceName = Task.Run(async () => await settingsService.GetValueAsync("azure.appServiceName")).Result
            ?? throw new ArgumentException("azure.appServiceName not configured");
        _keyVaultName = Task.Run(async () => await settingsService.GetValueAsync("azure.keyVaultName")).Result
            ?? throw new ArgumentException("azure.keyVaultName not configured");
        _certificateName = Task.Run(async () => await settingsService.GetValueAsync("azure.certificateName")).Result
            ?? "aegis-rental-wildcard"; // Default certificate name

        // Validate GUIDs
        if (!Guid.TryParse(_subscriptionId, out _))
            throw new ArgumentException($"Invalid subscription ID: {_subscriptionId}");
        if (!Guid.TryParse(tenantId, out _))
            throw new ArgumentException($"Invalid tenant ID: {tenantId}");
        if (!Guid.TryParse(clientId, out _))
            throw new ArgumentException($"Invalid client ID: {clientId}");

        // Create credential
        _credential = new ClientSecretCredential(tenantId, clientId, clientSecret);

        // Create ARM client
        var armClient = new ArmClient(_credential);

        // Get DNS Zone
        var dnsZoneId = new ResourceIdentifier(
            $"/subscriptions/{_subscriptionId}/resourceGroups/{_resourceGroup}/providers/Microsoft.Network/dnsZones/{zoneName}"
        );
        _dnsZone = armClient.GetDnsZoneResource(dnsZoneId);

        // Get App Service
        var appServiceId = new ResourceIdentifier(
            $"/subscriptions/{_subscriptionId}/resourceGroups/{_resourceGroup}/providers/Microsoft.Web/sites/{_appServiceName}"
        );
        _appService = armClient.GetWebSiteResource(appServiceId);

        _logger.LogInformation(
            "AzureDnsService initialized - Zone: {Zone}, App: {App}, KeyVault: {Vault}, Cert: {Cert}",
            zoneName, _appServiceName, _keyVaultName, _certificateName
        );
    }

    /// <summary>
    /// Creates subdomain with DNS, App Service binding, and SSL from Key Vault
    /// </summary>
    public async Task<bool> CreateSubdomainWithSslAsync(string subdomain)
    {
        try
        {
            var fullDomain = $"{subdomain.ToLower()}.{_dnsZone.Data.Name}";
            
            _logger.LogInformation("Starting complete subdomain setup for: {Domain}", fullDomain);

            // Step 1: Get verification ID
            var verificationId = await GetAppServiceVerificationIdAsync();
            if (string.IsNullOrEmpty(verificationId))
            {
                _logger.LogError("Failed to get App Service verification ID");
                return false;
            }

            // Step 2: Create verification TXT record
            if (!await CreateVerificationRecordAsync(subdomain, verificationId))
            {
                _logger.LogError("Failed to create verification record");
                return false;
            }

            _logger.LogInformation("Waiting 30 seconds for DNS propagation...");
            await Task.Delay(30000);

            // Step 3: Create CNAME record
            if (!await CreateSubdomainAsync(subdomain))
            {
                _logger.LogError("Failed to create CNAME record");
                return false;
            }

            _logger.LogInformation("Waiting 30 seconds for CNAME propagation...");
            await Task.Delay(30000);

            // Step 4: Add custom domain to App Service
            if (!await AddCustomDomainToAppServiceAsync(subdomain))
            {
                _logger.LogError("Failed to add custom domain to App Service");
                return false;
            }

            // Step 5: Enable SSL with Key Vault certificate
            if (!await EnableSslForDomainAsync(subdomain))
            {
                _logger.LogError("Failed to enable SSL");
                return false;
            }

            _logger.LogInformation("Successfully completed setup for: {Domain}", fullDomain);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create subdomain with SSL: {Subdomain}", subdomain);
            return false;
        }
    }

    /// <summary>
    /// Gets certificate thumbprint from Key Vault
    /// </summary>
    private async Task<string?> GetCertificateThumbprintAsync()
    {
        try
        {
            _logger.LogInformation("Getting certificate from Key Vault: {Vault}/{Cert}", 
                _keyVaultName, _certificateName);

            // Get certificate from Key Vault
            var keyVaultUri = $"https://{_keyVaultName}.vault.azure.net";
            var certificateClient = new CertificateClient(new Uri(keyVaultUri), _credential);
            
            var certificate = await certificateClient.GetCertificateAsync(_certificateName);
            
            if (certificate?.Value == null)
            {
                _logger.LogWarning("Certificate not found in Key Vault: {Cert}", _certificateName);
                return null;
            }

            // Get thumbprint from certificate
            // The thumbprint is available in the certificate's X509Certificate2
            var certBytes = certificate.Value.Cer;
            if (certBytes == null || certBytes.Length == 0)
            {
                _logger.LogWarning("Certificate data is empty");
                return null;
            }

            // Convert to X509Certificate2 to get thumbprint
            using var x509Cert = new System.Security.Cryptography.X509Certificates.X509Certificate2(certBytes);
            var thumbprint = x509Cert.Thumbprint;
            
            _logger.LogInformation("Certificate thumbprint retrieved: {Thumbprint}", thumbprint);
            return thumbprint;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get certificate thumbprint from Key Vault");
            return null;
        }
    }

    /// <summary>
    /// Enables SSL using certificate from Key Vault
    /// </summary>
    public async Task<bool> EnableSslForDomainAsync(string subdomain)
    {
        try
        {
            var fullDomain = $"{subdomain.ToLower()}.{_dnsZone.Data.Name}";
            
            _logger.LogInformation("Enabling SSL for domain: {Domain}", fullDomain);

            // Get certificate thumbprint from Key Vault
            var thumbprint = await GetCertificateThumbprintAsync();
            if (string.IsNullOrEmpty(thumbprint))
            {
                _logger.LogWarning("Certificate not found, using managed certificate instead");
                
                // Fallback to managed certificate (App Service will auto-provision)
                var hostNameBinding = new HostNameBindingData
                {
                    SiteName = _appServiceName,
                    HostNameType = AppServiceHostNameType.Verified,
                    SslState = HostNameBindingSslState.SniEnabled
                };

                await _appService.GetSiteHostNameBindings()
                    .CreateOrUpdateAsync(Azure.WaitUntil.Completed, fullDomain, hostNameBinding);
            }
            else
            {
                // Use Key Vault certificate thumbprint
                var hostNameBinding = new HostNameBindingData
                {
                    SiteName = _appServiceName,
                    HostNameType = AppServiceHostNameType.Verified,
                    SslState = HostNameBindingSslState.SniEnabled,
                    ThumbprintString = thumbprint
                };

                await _appService.GetSiteHostNameBindings()
                    .CreateOrUpdateAsync(Azure.WaitUntil.Completed, fullDomain, hostNameBinding);
            }

            _logger.LogInformation("Successfully enabled SSL for: {Domain}", fullDomain);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enable SSL: {Subdomain}", subdomain);
            return false;
        }
    }

    private async Task<string?> GetAppServiceVerificationIdAsync()
    {
        try
        {
            var appServiceData = await _appService.GetAsync();
            return appServiceData.Value.Data.CustomDomainVerificationId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get verification ID");
            return null;
        }
    }

    public async Task<bool> AddCustomDomainToAppServiceAsync(string subdomain)
    {
        try
        {
            var fullDomain = $"{subdomain.ToLower()}.{_dnsZone.Data.Name}";
            
            _logger.LogInformation("Adding custom domain to App Service: {Domain}", fullDomain);

            var hostNameBinding = new HostNameBindingData
            {
                SiteName = _appServiceName,
                HostNameType = AppServiceHostNameType.Verified
            };

            await _appService.GetSiteHostNameBindings()
                .CreateOrUpdateAsync(Azure.WaitUntil.Completed, fullDomain, hostNameBinding);

            _logger.LogInformation("Successfully added custom domain: {Domain}", fullDomain);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add custom domain: {Subdomain}", subdomain);
            return false;
        }
    }

    public async Task<bool> CreateSubdomainAsync(string subdomain, string? customTarget = null)
    {
        try
        {
            var recordSetName = subdomain.ToLower().Trim();
            var targetHost = customTarget ?? _appServiceHost;

            _logger.LogInformation("Creating subdomain: {Subdomain} -> {Target}", recordSetName, targetHost);

            if (await SubdomainExistsAsync(recordSetName))
            {
                _logger.LogWarning("Subdomain {Subdomain} already exists", recordSetName);
                return false;
            }

            var cnameData = new DnsCnameRecordData
            {
                Cname = targetHost,
                TtlInSeconds = 3600
            };

            await _dnsZone.GetDnsCnameRecords()
                .CreateOrUpdateAsync(Azure.WaitUntil.Completed, recordSetName, cnameData);

            _logger.LogInformation("Successfully created subdomain: {Subdomain}.{Zone}", 
                recordSetName, _dnsZone.Data.Name);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create subdomain: {Subdomain}", subdomain);
            return false;
        }
    }

    public async Task<bool> DeleteSubdomainAsync(string subdomain)
    {
        try
        {
            var recordSetName = subdomain.ToLower().Trim();
            var cnameRecord = await _dnsZone.GetDnsCnameRecordAsync(recordSetName);

            if (cnameRecord?.Value == null)
            {
                _logger.LogWarning("Subdomain {Subdomain} not found", recordSetName);
                return false;
            }

            await cnameRecord.Value.DeleteAsync(Azure.WaitUntil.Completed);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete subdomain: {Subdomain}", subdomain);
            return false;
        }
    }

    public async Task<bool> SubdomainExistsAsync(string subdomain)
    {
        try
        {
            var result = await _dnsZone.GetDnsCnameRecordAsync(subdomain.ToLower().Trim());
            return result?.Value != null;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return false;
        }
    }

    public string GetSubdomainUrl(string subdomain)
    {
        return $"https://{subdomain.ToLower()}.{_dnsZone.Data.Name}";
    }

    public async Task<bool> CreateVerificationRecordAsync(string subdomain, string verificationId)
    {
        try
        {
            var recordSetName = $"asuid.{subdomain.ToLower()}";

            var txtData = new DnsTxtRecordData();
            txtData.DnsTxtRecords.Add(new DnsTxtRecordInfo { Values = { verificationId } });
            txtData.TtlInSeconds = 3600;

            await _dnsZone.GetDnsTxtRecords()
                .CreateOrUpdateAsync(Azure.WaitUntil.Completed, recordSetName, txtData);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create verification record");
            return false;
        }
    }
}
