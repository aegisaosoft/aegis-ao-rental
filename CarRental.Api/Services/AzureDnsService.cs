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
        var dnsZoneResource = armClient.GetDnsZoneResource(dnsZoneId);
        // Load DNS Zone data
        var dnsZoneResponse = Task.Run(async () => await dnsZoneResource.GetAsync()).Result;
        _dnsZone = dnsZoneResponse.Value;

        // Get App Service
        var appServiceId = new ResourceIdentifier(
            $"/subscriptions/{_subscriptionId}/resourceGroups/{_resourceGroup}/providers/Microsoft.Web/sites/{_appServiceName}"
        );
        var appServiceResource = armClient.GetWebSiteResource(appServiceId);
        // Load App Service data
        var appServiceResponse = Task.Run(async () => await appServiceResource.GetAsync()).Result;
        _appService = appServiceResponse.Value;

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
            _logger.LogInformation("Creating CNAME record for subdomain: {Subdomain}", subdomain);
            var cnameCreated = await CreateSubdomainAsync(subdomain);
            if (!cnameCreated)
            {
                _logger.LogError("Failed to create CNAME record for subdomain: {Subdomain}. Continuing anyway...", subdomain);
                // Don't return false - continue to try adding domain and SSL
            }
            else
            {
                _logger.LogInformation("CNAME record created successfully for subdomain: {Subdomain}", subdomain);
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

            // Load certificate using X509CertificateLoader (replaces obsolete constructor)
            // LoadCertificate handles both DER and PEM formats automatically
            using var x509Cert = X509CertificateLoader.LoadCertificate(certBytes);
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

            // Wait a bit for the domain to be fully registered in App Service
            await Task.Delay(TimeSpan.FromSeconds(5));
            
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
                // First, ensure the certificate is imported into App Service from Key Vault
                _logger.LogInformation("Importing certificate from Key Vault into App Service...");
                await ImportCertificateFromKeyVaultAsync(thumbprint);
                
                // Use Key Vault certificate thumbprint
                // Note: For Key Vault certificates, App Service must have access to the Key Vault
                // via managed identity or access policies. The certificate should be imported into
                // App Service or accessible via Key Vault reference.
                var hostNameBinding = new HostNameBindingData
                {
                    SiteName = _appServiceName,
                    HostNameType = AppServiceHostNameType.Verified,
                    SslState = HostNameBindingSslState.SniEnabled,
                    ThumbprintString = thumbprint
                };

                // Try binding with retry logic
                int maxRetries = 3;
                int retryCount = 0;
                bool success = false;
                
                while (retryCount < maxRetries && !success)
                {
                    try
                    {
                        await _appService.GetSiteHostNameBindings()
                            .CreateOrUpdateAsync(Azure.WaitUntil.Completed, fullDomain, hostNameBinding);
                        
                        success = true;
                        _logger.LogInformation("SSL binding created successfully with thumbprint: {Thumbprint}", thumbprint);
                    }
                    catch (Exception ex) when (retryCount < maxRetries - 1)
                    {
                        retryCount++;
                        _logger.LogWarning(ex, "Failed to bind certificate (attempt {Attempt}/{MaxRetries}), retrying in 5 seconds...", 
                            retryCount, maxRetries);
                        await Task.Delay(TimeSpan.FromSeconds(5));
                    }
                }
                
                if (!success)
                {
                    _logger.LogError("Failed to bind certificate after {MaxRetries} attempts. " +
                        "Ensure the certificate with thumbprint {Thumbprint} is imported into App Service " +
                        "or App Service has access to Key Vault via managed identity.", maxRetries, thumbprint);
                    throw new InvalidOperationException($"Failed to bind certificate after {maxRetries} attempts");
                }
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

            _logger.LogInformation("Creating CNAME record: {Subdomain} -> {Target}", recordSetName, targetHost);

            // Check if CNAME already exists
            bool cnameExists = await SubdomainExistsAsync(recordSetName);
            if (cnameExists)
            {
                _logger.LogInformation("CNAME record {Subdomain} already exists, updating it", recordSetName);
            }

            var cnameData = new DnsCnameRecordData
            {
                Cname = targetHost,
                TtlInSeconds = 3600
            };

            // Create or update the CNAME record
            await _dnsZone.GetDnsCnameRecords()
                .CreateOrUpdateAsync(Azure.WaitUntil.Completed, recordSetName, cnameData);

            _logger.LogInformation("Successfully created/updated CNAME record: {Subdomain}.{Zone} -> {Target}", 
                recordSetName, _dnsZone.Data.Name, targetHost);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create CNAME record for subdomain: {Subdomain}", subdomain);
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

    /// <summary>
    /// Imports certificate from Key Vault into App Service
    /// </summary>
    private async Task ImportCertificateFromKeyVaultAsync(string thumbprint)
    {
        try
        {
            _logger.LogInformation("Checking if certificate with thumbprint {Thumbprint} is already imported in App Service", thumbprint);
            
            // Check if certificate already exists in App Service by trying to get it
            var armClient = new ArmClient(_credential);
            var certificateResourceId = new ResourceIdentifier(
                $"/subscriptions/{_subscriptionId}/resourceGroups/{_resourceGroup}/providers/Microsoft.Web/certificates/{_certificateName}"
            );
            
            try
            {
                var existingCertificateResource = armClient.GetAppCertificateResource(certificateResourceId);
                var existingCert = await existingCertificateResource.GetAsync();
                
                // Check if thumbprint matches
                if (existingCert.Value.Data.ThumbprintString?.Equals(thumbprint, StringComparison.OrdinalIgnoreCase) == true)
                {
                    _logger.LogInformation("Certificate with thumbprint {Thumbprint} already imported in App Service", thumbprint);
                    return;
                }
                else
                {
                    _logger.LogInformation("Certificate exists but thumbprint doesn't match. Updating...");
                }
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                _logger.LogInformation("Certificate not found in App Service, importing from Key Vault...");
            }
            
            // Import certificate from Key Vault
            var keyVaultResourceId = new ResourceIdentifier(
                $"/subscriptions/{_subscriptionId}/resourceGroups/{_resourceGroup}/providers/Microsoft.KeyVault/vaults/{_keyVaultName}"
            );
            
            var certificateData = new AppCertificateData(_appService.Data.Location)
            {
                KeyVaultId = keyVaultResourceId,
                KeyVaultSecretName = _certificateName
            };
            
            // Create or update the certificate resource
            // Get subscription, then resource group, then certificate collection
            var subscription = await armClient.GetDefaultSubscriptionAsync();
            var resourceGroup = await subscription.GetResourceGroupAsync(_resourceGroup);
            var certificateCollection = resourceGroup.Value.GetAppCertificates();
            
            await certificateCollection.CreateOrUpdateAsync(
                Azure.WaitUntil.Completed,
                _certificateName,
                certificateData
            );
            
            _logger.LogInformation("Successfully imported certificate from Key Vault into App Service with thumbprint: {Thumbprint}", thumbprint);
        }
        catch (Exception ex)
        {
            // Log warning but don't fail - the binding might still work if certificate is accessible via Key Vault
            _logger.LogWarning(ex, "Failed to import certificate from Key Vault into App Service. " +
                "The certificate may still be accessible if App Service has Key Vault access via managed identity.");
        }
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
