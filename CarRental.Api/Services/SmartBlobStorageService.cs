/*
 * Smart Blob Storage Service
 * Automatically switches between Azure Blob Storage and Local File Storage
 * based on Azure Storage availability and configuration
 * Copyright (c) 2025 Alexander Orlov.
 */

using CarRental.Api.Services;

namespace CarRental.Api.Services;

/// <summary>
/// Smart wrapper that automatically chooses between Azure Blob Storage and Local File Storage
/// based on configuration availability. Provides seamless fallback for development environments.
/// </summary>
public class SmartBlobStorageService : IAzureBlobStorageService
{
    private readonly AzureBlobStorageService _azureService;
    private readonly LocalFileStorageService _localService;
    private readonly ILogger<SmartBlobStorageService> _logger;
    private bool? _azureAvailable = null; // Cache the result to avoid repeated checks

    public SmartBlobStorageService(
        AzureBlobStorageService azureService,
        LocalFileStorageService localService,
        ILogger<SmartBlobStorageService> logger)
    {
        _azureService = azureService;
        _localService = localService;
        _logger = logger;
    }

    /// <summary>
    /// Get the appropriate storage service (Azure or Local) based on availability
    /// </summary>
    private async Task<IAzureBlobStorageService> GetStorageServiceAsync()
    {
        // Use cached result if available
        if (_azureAvailable.HasValue)
        {
            return _azureAvailable.Value ? _azureService : _localService;
        }

        // Check if Azure is configured and available
        try
        {
            bool isConfigured = await _azureService.IsConfiguredAsync();
            _azureAvailable = isConfigured;

            if (isConfigured)
            {
                _logger.LogInformation("Azure Blob Storage is configured and available");
                return _azureService;
            }
            else
            {
                _logger.LogInformation("Azure Blob Storage not configured, using local file storage fallback");
                return _localService;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Azure Blob Storage check failed, falling back to local storage: {Message}", ex.Message);
            _azureAvailable = false;
            return _localService;
        }
    }

    public async Task<bool> IsConfiguredAsync()
    {
        try
        {
            var service = await GetStorageServiceAsync();
            return await service.IsConfiguredAsync();
        }
        catch
        {
            // If anything fails, return true for local storage (always available)
            return true;
        }
    }

    public async Task<string> UploadFileAsync(Stream fileStream, string containerName, string blobPath, string contentType)
    {
        try
        {
            var service = await GetStorageServiceAsync();
            var result = await service.UploadFileAsync(fileStream, containerName, blobPath, contentType);

            _logger.LogInformation("File uploaded using {ServiceType}: {BlobPath}",
                service.GetType().Name, blobPath);

            return result;
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Azure Blob Storage is not configured"))
        {
            // Force fallback to local storage if Azure suddenly becomes unavailable
            _logger.LogWarning("Azure Storage became unavailable during upload, falling back to local storage");
            _azureAvailable = false;
            return await _localService.UploadFileAsync(fileStream, containerName, blobPath, contentType);
        }
    }

    public async Task<Stream?> DownloadFileAsync(string containerName, string blobPath)
    {
        try
        {
            var service = await GetStorageServiceAsync();
            var result = await service.DownloadFileAsync(containerName, blobPath);

            if (result == null && service == _azureService)
            {
                // If Azure returns null, try local storage as fallback
                _logger.LogInformation("File not found in Azure Storage, trying local storage: {BlobPath}", blobPath);
                result = await _localService.DownloadFileAsync(containerName, blobPath);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error downloading from primary storage, trying fallback: {BlobPath}", blobPath);
            // Try the opposite service as fallback
            var fallbackService = _azureAvailable == true ? _localService : _azureService;
            try
            {
                return await fallbackService.DownloadFileAsync(containerName, blobPath);
            }
            catch
            {
                _logger.LogWarning("Fallback download also failed for: {BlobPath}", blobPath);
                return null;
            }
        }
    }

    public async Task<bool> DeleteFileAsync(string containerName, string blobPath)
    {
        try
        {
            var service = await GetStorageServiceAsync();
            var result = await service.DeleteFileAsync(containerName, blobPath);

            // Also try to delete from the other storage (cleanup)
            try
            {
                var fallbackService = service == _azureService ? _localService : _azureService;
                await fallbackService.DeleteFileAsync(containerName, blobPath);
            }
            catch
            {
                // Ignore fallback deletion errors
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file: {BlobPath}", blobPath);
            return false;
        }
    }

    public async Task<bool> FileExistsAsync(string containerName, string blobPath)
    {
        try
        {
            var service = await GetStorageServiceAsync();
            var exists = await service.FileExistsAsync(containerName, blobPath);

            if (!exists && service == _azureService)
            {
                // Check local storage as fallback
                exists = await _localService.FileExistsAsync(containerName, blobPath);
            }

            return exists;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking file existence: {BlobPath}", blobPath);
            return false;
        }
    }

    public async Task<IEnumerable<string>> ListFilesAsync(string containerName, string prefix)
    {
        try
        {
            var service = await GetStorageServiceAsync();
            var files = await service.ListFilesAsync(containerName, prefix);

            // If using Azure and no files found, also check local storage
            if (service == _azureService && !files.Any())
            {
                var localFiles = await _localService.ListFilesAsync(containerName, prefix);
                if (localFiles.Any())
                {
                    _logger.LogInformation("Found files in local storage when Azure had none: {Prefix}", prefix);
                    return localFiles;
                }
            }

            return files;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing files: {ContainerName}/{Prefix}", containerName, prefix);
            return Enumerable.Empty<string>();
        }
    }

    public string GetBlobUrl(string containerName, string blobPath)
    {
        try
        {
            // For URLs, we prefer Azure if it's configured since those URLs are more reliable
            if (_azureAvailable == true)
            {
                return _azureService.GetBlobUrl(containerName, blobPath);
            }
            else
            {
                return _localService.GetBlobUrl(containerName, blobPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting blob URL: {BlobPath}, using local URL", blobPath);
            return _localService.GetBlobUrl(containerName, blobPath);
        }
    }
}