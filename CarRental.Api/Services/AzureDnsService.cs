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
    /// Extracts the actual Key Vault certificate name from the App Service compound name
    /// </summary>
    /// <returns>The actual certificate name to use in Key Vault</returns>
    private string GetActualKeyVaultCertificateName()
    {
        // The certificate name in App Service is a compound name like:
        // aegis_rentals-kv-aegis-acmebot-rypt-wildcard-aegis-rental-com
        // But in Key Vault, the actual certificate name is just:
        // wildcard-aegis-rental-com
        // 
        // We need to extract the actual Key Vault certificate name from the App Service friendly name
        string actualCertName = _certificateName;
        
        // If the certificate name contains the Key Vault name, extract the actual cert name
        // Format: {resourceGroup}-{keyVaultName}-{actualCertName}
        if (_certificateName.Contains(_keyVaultName))
        {
            var parts = _certificateName.Split(new[] { _keyVaultName + "-" }, StringSplitOptions.None);
            if (parts.Length > 1)
            {
                actualCertName = parts[1];
                _logger.LogInformation("Extracted actual certificate name: {ActualCert} from App Service name: {AppServiceCert}", 
                    actualCertName, _certificateName);
            }
        }
        
        return actualCertName;
    }

    /// <summary>
    /// Gets certificate thumbprint from App Service certificate resource (preferred) or Key Vault (fallback)
    /// </summary>
    private async Task<string?> GetCertificateThumbprintAsync()
    {
        // First, try to get thumbprint from App Service certificate resource (if already imported)
        // This avoids needing Key Vault read permissions
        try
        {
            _logger.LogInformation("Attempting to get certificate thumbprint from App Service certificate resource...");
            _logger.LogInformation("Looking for certificate: {CertName} in resource group: {ResourceGroup}", 
                _certificateName, _resourceGroup);
            
            var armClient = new ArmClient(_credential);
            var certificateResourceId = new ResourceIdentifier(
                $"/subscriptions/{_subscriptionId}/resourceGroups/{_resourceGroup}/providers/Microsoft.Web/certificates/{_certificateName}"
            );
            
            _logger.LogInformation("Certificate resource ID: {ResourceId}", certificateResourceId);
            
            try
            {
                var certificateResource = armClient.GetAppCertificateResource(certificateResourceId);
                var cert = await certificateResource.GetAsync();
                
                if (cert?.Value?.Data != null)
                {
                    _logger.LogInformation("Certificate found in App Service. Name: {Name}, Thumbprint: {Thumbprint}, KeyVaultId: {KeyVaultId}", 
                        cert.Value.Data.Name, cert.Value.Data.ThumbprintString ?? "null", cert.Value.Data.KeyVaultId?.ToString() ?? "null");
                    
                    if (!string.IsNullOrEmpty(cert.Value.Data.ThumbprintString))
                    {
                        var thumbprint = cert.Value.Data.ThumbprintString.ToUpperInvariant().Replace(" ", "").Replace("-", "");
                        _logger.LogInformation("Certificate thumbprint retrieved from App Service certificate resource: {Thumbprint}", thumbprint);
                        return thumbprint;
                    }
                    else
                    {
                        _logger.LogWarning("Certificate found in App Service but thumbprint is empty");
                    }
                }
                else
                {
                    _logger.LogWarning("Certificate resource returned null data");
                }
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                _logger.LogWarning("Certificate '{CertName}' not found in App Service (404). " +
                    "Certificate resource path: /subscriptions/{SubId}/resourceGroups/{RG}/providers/Microsoft.Web/certificates/{CertName}. " +
                    "Will try Key Vault as fallback...", 
                    _certificateName, _subscriptionId, _resourceGroup, _certificateName);
            }
            catch (Azure.RequestFailedException ex)
            {
                _logger.LogWarning(ex, "Azure error getting certificate from App Service. Status: {Status}, Message: {Message}. Will try Key Vault...", 
                    ex.Status, ex.Message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting certificate from App Service, will try Key Vault...");
        }
        
        // Fallback: Try to get from Key Vault (requires Key Vault read permissions)
        try
        {
            var actualCertName = GetActualKeyVaultCertificateName();

            _logger.LogInformation("Getting certificate from Key Vault: {Vault}/{Cert}", 
                _keyVaultName, actualCertName);

            // Get certificate from Key Vault
            var keyVaultUri = $"https://{_keyVaultName}.vault.azure.net";
            var certificateClient = new CertificateClient(new Uri(keyVaultUri), _credential);
            
            var certificate = await certificateClient.GetCertificateAsync(actualCertName);
            
            if (certificate?.Value == null)
            {
                _logger.LogWarning("Certificate not found in Key Vault: {Cert}", actualCertName);
                return null;
            }

            // Get thumbprint from certificate
            var certBytes = certificate.Value.Cer;
            if (certBytes == null || certBytes.Length == 0)
            {
                _logger.LogWarning("Certificate data is empty");
                return null;
            }

            // Load certificate using X509CertificateLoader
            using var x509Cert = X509CertificateLoader.LoadCertificate(certBytes);
            var thumbprint = x509Cert.Thumbprint?.ToUpperInvariant().Replace(" ", "").Replace("-", "");
            
            if (string.IsNullOrEmpty(thumbprint))
            {
                _logger.LogWarning("Certificate thumbprint is null or empty");
                return null;
            }
            
            _logger.LogInformation("Certificate thumbprint retrieved from Key Vault: {Thumbprint}", thumbprint);
            return thumbprint;
        }
        catch (Azure.RequestFailedException azureEx) when (azureEx.Status == 403)
        {
            _logger.LogError(azureEx, "Access denied to Key Vault. The service principal (appid={AppId}) needs 'Key Vault Secrets User' or 'Key Vault Certificate User' role on Key Vault '{Vault}'. " +
                "Alternatively, ensure the certificate is imported into App Service first.",
                "ebfa979f-7c84-4b30-90d5-ea8b53fb5866", _keyVaultName);
            return null;
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

            // Wait for domain to be fully registered
            await Task.Delay(TimeSpan.FromSeconds(10));
            
            // Get certificate thumbprint - try App Service first, then Key Vault
            var thumbprint = await GetCertificateThumbprintAsync();
            if (string.IsNullOrEmpty(thumbprint))
            {
                _logger.LogError("Certificate thumbprint not found! " +
                    "The certificate may not be imported into App Service, or the service principal lacks Key Vault permissions. " +
                    "Please either: 1) Import the certificate into App Service manually, or " +
                    "2) Grant the service principal 'Key Vault Secrets User' or 'Key Vault Certificate User' role on Key Vault '{Vault}'.",
                    _keyVaultName);
                throw new InvalidOperationException($"Certificate thumbprint is required but was not found. " +
                    $"Service principal may need Key Vault permissions, or certificate needs to be imported into App Service first. " +
                    $"Key Vault: {_keyVaultName}, Certificate: {_certificateName}");
            }

            _logger.LogInformation("Key Vault certificate thumbprint retrieved: {Thumbprint}", thumbprint);
            
            // Import certificate into App Service from Key Vault - REQUIRED
            _logger.LogInformation("Importing certificate from Key Vault into App Service...");
            var importSuccess = await ImportCertificateFromKeyVaultAsync(thumbprint);
            
            if (!importSuccess)
            {
                _logger.LogWarning("Certificate import may have failed, but will attempt binding anyway");
            }
            
            // Wait for certificate to be available in App Service
            _logger.LogInformation("Waiting 20 seconds for certificate to be available in App Service...");
            await Task.Delay(TimeSpan.FromSeconds(20));
            
            // Verify certificate is available in App Service by checking if we can find it
            _logger.LogInformation("Verifying certificate is available in App Service...");
            bool certAvailable = await VerifyCertificateInAppServiceAsync(thumbprint);
            if (!certAvailable)
            {
                _logger.LogWarning("Certificate with thumbprint {Thumbprint} may not be available in App Service yet. " +
                    "Will attempt binding anyway - App Service may access it directly from Key Vault.", thumbprint);
            }
            else
            {
                _logger.LogInformation("Certificate verified in App Service with thumbprint: {Thumbprint}", thumbprint);
            }
            
            // Get existing binding to preserve its state
            HostNameBindingData? existingBinding = null;
            try
            {
                var existingBindingResource = await _appService.GetSiteHostNameBindingAsync(fullDomain);
                if (existingBindingResource?.Value?.Data != null)
                {
                    existingBinding = existingBindingResource.Value.Data;
                    _logger.LogInformation("Found existing binding for {Domain}, current SSL state: {SslState}", 
                        fullDomain, existingBinding.SslState);
                }
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                _logger.LogInformation("No existing binding found for {Domain}, will create new one", fullDomain);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking for existing binding, will create new one");
            }
            
            // Create binding with SSL certificate from Key Vault
            // Normalize thumbprint to uppercase, no spaces/dashes for consistency
            var normalizedThumbprint = thumbprint.ToUpperInvariant().Replace(" ", "").Replace("-", "");
            
            _logger.LogInformation("Creating SSL binding with Key Vault certificate. " +
                "Domain: {Domain}, Thumbprint: {Thumbprint}", fullDomain, normalizedThumbprint);
            
            var hostNameBinding = new HostNameBindingData
            {
                SiteName = _appServiceName,
                HostNameType = AppServiceHostNameType.Verified,
                SslState = HostNameBindingSslState.SniEnabled,
                ThumbprintString = normalizedThumbprint  // Use normalized thumbprint
            };
            
            // Preserve existing properties if they exist
            if (existingBinding != null)
            {
                hostNameBinding.AzureResourceName = existingBinding.AzureResourceName;
                hostNameBinding.AzureResourceType = existingBinding.AzureResourceType;
                hostNameBinding.CustomHostNameDnsRecordType = existingBinding.CustomHostNameDnsRecordType;
                hostNameBinding.DomainId = existingBinding.DomainId;
                hostNameBinding.HostNameType = existingBinding.HostNameType;
            }

            // Try binding with retry logic
            int maxRetries = 5;
            int retryCount = 0;
            bool success = false;
            Exception? lastException = null;
            
            while (retryCount < maxRetries && !success)
            {
                try
                {
                    _logger.LogInformation("Attempting SSL binding with thumbprint {Thumbprint} (attempt {Attempt}/{MaxRetries})...", 
                        thumbprint, retryCount + 1, maxRetries);
                    
                    var result = await _appService.GetSiteHostNameBindings()
                        .CreateOrUpdateAsync(Azure.WaitUntil.Completed, fullDomain, hostNameBinding);
                    
                    // Verify the binding was created with SSL
                    if (result.Value.Data.SslState == HostNameBindingSslState.SniEnabled && 
                        !string.IsNullOrEmpty(result.Value.Data.ThumbprintString))
                    {
                        success = true;
                        _logger.LogInformation("SSL binding created successfully! Domain: {Domain}, Thumbprint: {Thumbprint}, SSL State: {SslState}", 
                            fullDomain, result.Value.Data.ThumbprintString, result.Value.Data.SslState);
                    }
                    else
                    {
                        _logger.LogWarning("Binding created but SSL not enabled. SSL State: {SslState}, Thumbprint: {Thumbprint}", 
                            result.Value.Data.SslState, result.Value.Data.ThumbprintString ?? "null");
                        throw new InvalidOperationException("Binding created but SSL was not enabled");
                    }
                }
                catch (Azure.RequestFailedException azureEx) when (retryCount < maxRetries - 1)
                {
                    lastException = azureEx;
                    retryCount++;
                    _logger.LogWarning("Failed to bind certificate (attempt {Attempt}/{MaxRetries}). " +
                        "Status: {Status}, Error: {Error}. Retrying in 15 seconds...", 
                        retryCount, maxRetries, azureEx.Status, azureEx.Message);
                    await Task.Delay(TimeSpan.FromSeconds(15));
                }
                catch (Exception ex) when (retryCount < maxRetries - 1)
                {
                    lastException = ex;
                    retryCount++;
                    _logger.LogWarning(ex, "Failed to bind certificate (attempt {Attempt}/{MaxRetries}), retrying in 15 seconds...", 
                        retryCount, maxRetries);
                    await Task.Delay(TimeSpan.FromSeconds(15));
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    break;
                }
            }
            
            if (!success)
            {
                var errorDetails = lastException is Azure.RequestFailedException azureErr
                    ? $"Azure Error: Status {azureErr.Status}, Message: {azureErr.Message}"
                    : $"Error: {lastException?.Message}";
                
                _logger.LogError(lastException, "Failed to bind certificate after {MaxRetries} attempts. {ErrorDetails}", 
                    maxRetries, errorDetails);
                
                // No fallback - Key Vault certificate is required
                _logger.LogError("All SSL binding attempts failed with Key Vault certificate. " +
                    "Domain: {Domain}, Thumbprint: {Thumbprint}. " +
                    "Please check: 1) Certificate is imported in App Service, 2) App Service has Key Vault access, " +
                    "3) Thumbprint matches the certificate in App Service.", 
                    fullDomain, thumbprint);
                return false;
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
                HostNameType = AppServiceHostNameType.Verified,
                SslState = HostNameBindingSslState.Disabled  // Start without SSL, will add it later
            };

            await _appService.GetSiteHostNameBindings()
                .CreateOrUpdateAsync(Azure.WaitUntil.Completed, fullDomain, hostNameBinding);

            _logger.LogInformation("Custom domain added, waiting 30 seconds for verification...");
            await Task.Delay(TimeSpan.FromSeconds(30));
            
            // Verify the domain was added successfully
            try
            {
                var binding = await _appService.GetSiteHostNameBindingAsync(fullDomain);
                if (binding?.Value?.Data != null)
                {
                    _logger.LogInformation("Domain verified successfully: {Domain}, HostNameType: {HostNameType}", 
                        fullDomain, binding.Value.Data.HostNameType);
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not verify domain binding, but continuing...");
            }

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
    /// Verifies that a certificate with the given thumbprint is available in App Service
    /// </summary>
    private async Task<bool> VerifyCertificateInAppServiceAsync(string thumbprint)
    {
        try
        {
            var armClient = new ArmClient(_credential);
            var certificateResourceId = new ResourceIdentifier(
                $"/subscriptions/{_subscriptionId}/resourceGroups/{_resourceGroup}/providers/Microsoft.Web/certificates/{_certificateName}"
            );
            
            try
            {
                var certificateResource = armClient.GetAppCertificateResource(certificateResourceId);
                var cert = await certificateResource.GetAsync();
                
                if (cert?.Value?.Data != null)
                {
                    var certThumbprint = cert.Value.Data.ThumbprintString;
                    if (certThumbprint?.Equals(thumbprint, StringComparison.OrdinalIgnoreCase) == true)
                    {
                        _logger.LogInformation("Certificate verified in App Service. Thumbprint matches: {Thumbprint}", thumbprint);
                        return true;
                    }
                    else
                    {
                        _logger.LogWarning("Certificate found in App Service but thumbprint doesn't match. " +
                            "Expected: {Expected}, Found: {Found}", thumbprint, certThumbprint ?? "null");
                        return false;
                    }
                }
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                _logger.LogWarning("Certificate resource not found in App Service. " +
                    "App Service may access it directly from Key Vault via managed identity.");
                return false;
            }
            
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error verifying certificate in App Service");
            return false;
        }
    }

    /// <summary>
    /// Imports certificate from Key Vault into App Service
    /// </summary>
    /// <returns>True if import was successful or certificate already exists, false otherwise</returns>
    private async Task<bool> ImportCertificateFromKeyVaultAsync(string thumbprint)
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
                    return true;
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
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking for existing certificate, will attempt to create new one");
            }
            
            // Import certificate from Key Vault
            var keyVaultResourceId = new ResourceIdentifier(
                $"/subscriptions/{_subscriptionId}/resourceGroups/{_resourceGroup}/providers/Microsoft.KeyVault/vaults/{_keyVaultName}"
            );
            
            // Use the actual Key Vault certificate name, not the App Service compound name
            var actualCertName = GetActualKeyVaultCertificateName();
            
            var certificateData = new AppCertificateData(_appService.Data.Location)
            {
                KeyVaultId = keyVaultResourceId,
                KeyVaultSecretName = actualCertName
            };
            
            try
            {
                // Create or update the certificate resource
                // Get subscription, then resource group, then certificate collection
                var subscription = await armClient.GetDefaultSubscriptionAsync();
                var resourceGroup = await subscription.GetResourceGroupAsync(_resourceGroup);
                var certificateCollection = resourceGroup.Value.GetAppCertificates();
                
                var result = await certificateCollection.CreateOrUpdateAsync(
                    Azure.WaitUntil.Completed,
                    _certificateName,
                    certificateData
                );
                
                _logger.LogInformation("Successfully imported certificate from Key Vault into App Service. " +
                    "Certificate thumbprint: {Thumbprint}, Resource ID: {ResourceId}", 
                    thumbprint, result.Value.Id);
                return true;
            }
            catch (Azure.RequestFailedException azureEx)
            {
                _logger.LogError(azureEx, "Failed to import certificate from Key Vault. Status: {Status}, Message: {Message}. " +
                    "This may be due to missing permissions or Key Vault access configuration.", 
                    azureEx.Status, azureEx.Message);
                return false;
            }
        }
        catch (Exception ex)
        {
            // Log warning but don't fail - the binding might still work if certificate is accessible via Key Vault
            _logger.LogWarning(ex, "Failed to import certificate from Key Vault into App Service. " +
                "The certificate may still be accessible if App Service has Key Vault access via managed identity.");
            return false;
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
