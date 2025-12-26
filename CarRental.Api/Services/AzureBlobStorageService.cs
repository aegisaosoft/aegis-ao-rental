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

using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace CarRental.Api.Services;

public interface IAzureBlobStorageService
{
    Task<string> UploadFileAsync(Stream fileStream, string containerName, string blobPath, string contentType);
    Task<Stream?> DownloadFileAsync(string containerName, string blobPath);
    Task<bool> DeleteFileAsync(string containerName, string blobPath);
    Task<bool> FileExistsAsync(string containerName, string blobPath);
    Task<IEnumerable<string>> ListFilesAsync(string containerName, string prefix);
    string GetBlobUrl(string containerName, string blobPath);
    Task<bool> IsConfiguredAsync();
}

public class AzureBlobStorageService : IAzureBlobStorageService
{
    private readonly ISettingsService _settingsService;
    private readonly ILogger<AzureBlobStorageService> _logger;
    private readonly IConfiguration _configuration;
    
    private const string ConnectionStringSettingKey = "azure.storage.connectionString";
    private const string ContainerNameSettingKey = "azure.storage.containerName";

    private BlobServiceClient? _cachedClient;
    private string? _cachedConnectionString;
    private string? _cachedStorageAccountUrl;

    public AzureBlobStorageService(
        ISettingsService settingsService,
        IConfiguration configuration, 
        ILogger<AzureBlobStorageService> logger)
    {
        _settingsService = settingsService;
        _configuration = configuration;
        _logger = logger;
    }

    private async Task<(BlobServiceClient? client, string? storageUrl)> GetBlobClientAsync()
    {
        // First try to get from database settings
        var connectionString = await _settingsService.GetValueAsync(ConnectionStringSettingKey);
        
        // Fallback to configuration if not in database
        if (string.IsNullOrEmpty(connectionString))
        {
            connectionString = _configuration["AzureStorage:ConnectionString"];
        }
        
        if (string.IsNullOrEmpty(connectionString))
        {
            return (null, null);
        }

        // Check if we need to create a new client (connection string changed)
        if (_cachedClient != null && _cachedConnectionString == connectionString)
        {
            return (_cachedClient, _cachedStorageAccountUrl);
        }

        try
        {
            _cachedClient = new BlobServiceClient(connectionString);
            _cachedStorageAccountUrl = _cachedClient.Uri.ToString().TrimEnd('/');
            _cachedConnectionString = connectionString;
            _logger.LogInformation("Azure Blob Storage client initialized: {Url}", _cachedStorageAccountUrl);
            return (_cachedClient, _cachedStorageAccountUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Azure Blob Storage client");
            return (null, null);
        }
    }

    public async Task<bool> IsConfiguredAsync()
    {
        var (client, _) = await GetBlobClientAsync();
        return client != null;
    }

    public async Task<string> UploadFileAsync(Stream fileStream, string containerName, string blobPath, string contentType)
    {
        var (client, _) = await GetBlobClientAsync();
        
        if (client == null)
        {
            throw new InvalidOperationException("Azure Blob Storage is not configured. Please configure it in Settings > Azure Blob Storage.");
        }

        try
        {
            var containerClient = client.GetBlobContainerClient(containerName);
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);

            var blobClient = containerClient.GetBlobClient(blobPath);
            
            var blobHttpHeaders = new BlobHttpHeaders
            {
                ContentType = contentType
            };

            await blobClient.UploadAsync(fileStream, new BlobUploadOptions
            {
                HttpHeaders = blobHttpHeaders
            });

            var blobUrl = blobClient.Uri.ToString();
            _logger.LogInformation("File uploaded to blob storage: {BlobUrl}", blobUrl);
            
            return blobUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file to blob storage: {Container}/{Path}", containerName, blobPath);
            throw;
        }
    }

    public async Task<Stream?> DownloadFileAsync(string containerName, string blobPath)
    {
        var (client, _) = await GetBlobClientAsync();
        
        if (client == null)
        {
            return null;
        }

        try
        {
            var containerClient = client.GetBlobContainerClient(containerName);
            
            // Check if container exists first
            if (!await containerClient.ExistsAsync())
            {
                _logger.LogWarning("Container {Container} does not exist", containerName);
                return null;
            }
            
            var blobClient = containerClient.GetBlobClient(blobPath);

            if (!await blobClient.ExistsAsync())
            {
                _logger.LogWarning("Blob not found: {Container}/{Path}", containerName, blobPath);
                return null;
            }

            var response = await blobClient.DownloadStreamingAsync();
            return response.Value.Content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading file from blob storage: {Container}/{Path}", containerName, blobPath);
            return null;
        }
    }

    public async Task<bool> DeleteFileAsync(string containerName, string blobPath)
    {
        var (client, _) = await GetBlobClientAsync();
        
        if (client == null)
        {
            return false;
        }

        try
        {
            var containerClient = client.GetBlobContainerClient(containerName);
            var blobClient = containerClient.GetBlobClient(blobPath);

            var response = await blobClient.DeleteIfExistsAsync();
            _logger.LogInformation("Blob deleted: {Container}/{Path}, existed: {Existed}", containerName, blobPath, response.Value);
            
            return response.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file from blob storage: {Container}/{Path}", containerName, blobPath);
            return false;
        }
    }

    public async Task<bool> FileExistsAsync(string containerName, string blobPath)
    {
        var (client, _) = await GetBlobClientAsync();
        
        if (client == null)
        {
            return false;
        }

        try
        {
            var containerClient = client.GetBlobContainerClient(containerName);
            
            // Check if container exists first
            if (!await containerClient.ExistsAsync())
            {
                return false;
            }
            
            var blobClient = containerClient.GetBlobClient(blobPath);

            return await blobClient.ExistsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if file exists in blob storage: {Container}/{Path}", containerName, blobPath);
            return false;
        }
    }

    public async Task<IEnumerable<string>> ListFilesAsync(string containerName, string prefix)
    {
        var (client, _) = await GetBlobClientAsync();
        
        if (client == null)
        {
            return Enumerable.Empty<string>();
        }

        try
        {
            var containerClient = client.GetBlobContainerClient(containerName);
            
            // Check if container exists first
            if (!await containerClient.ExistsAsync())
            {
                _logger.LogInformation("Container {Container} does not exist yet, returning empty list", containerName);
                return Enumerable.Empty<string>();
            }
            
            var blobs = new List<string>();

            await foreach (var blobItem in containerClient.GetBlobsAsync(prefix: prefix))
            {
                blobs.Add(blobItem.Name);
            }

            return blobs;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing files in blob storage: {Container}/{Prefix}", containerName, prefix);
            return Enumerable.Empty<string>();
        }
    }

    public string GetBlobUrl(string containerName, string blobPath)
    {
        // This is synchronous, so we use cached values if available
        if (string.IsNullOrEmpty(_cachedStorageAccountUrl))
        {
            return string.Empty;
        }

        return $"{_cachedStorageAccountUrl}/{containerName}/{blobPath}";
    }
}

/// <summary>
/// Fallback service that uses local file system when Azure Blob Storage is not configured
/// </summary>
public class LocalFileStorageService : IAzureBlobStorageService
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<LocalFileStorageService> _logger;
    private readonly string _baseUrl;

    public LocalFileStorageService(
        IWebHostEnvironment environment, 
        ILogger<LocalFileStorageService> logger,
        IConfiguration configuration)
    {
        _environment = environment;
        _logger = logger;
        _baseUrl = configuration["ApiBaseUrl"] ?? "";
        _logger.LogWarning("Using local file storage. Files will NOT persist on Azure App Service!");
    }

    public Task<bool> IsConfiguredAsync() => Task.FromResult(true);

    public async Task<string> UploadFileAsync(Stream fileStream, string containerName, string blobPath, string contentType)
    {
        var localPath = Path.Combine(_environment.ContentRootPath, "wwwroot", containerName, blobPath);
        var directory = Path.GetDirectoryName(localPath);
        
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using (var fileStreamOut = new FileStream(localPath, FileMode.Create))
        {
            await fileStream.CopyToAsync(fileStreamOut);
        }

        _logger.LogInformation("File saved locally: {Path}", localPath);
        
        // Return URL path (relative to API)
        return $"/{containerName}/{blobPath}";
    }

    public Task<Stream?> DownloadFileAsync(string containerName, string blobPath)
    {
        var localPath = Path.Combine(_environment.ContentRootPath, "wwwroot", containerName, blobPath);
        
        if (!File.Exists(localPath))
        {
            return Task.FromResult<Stream?>(null);
        }

        return Task.FromResult<Stream?>(new FileStream(localPath, FileMode.Open, FileAccess.Read));
    }

    public Task<bool> DeleteFileAsync(string containerName, string blobPath)
    {
        var localPath = Path.Combine(_environment.ContentRootPath, "wwwroot", containerName, blobPath);
        
        if (File.Exists(localPath))
        {
            File.Delete(localPath);
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    public Task<bool> FileExistsAsync(string containerName, string blobPath)
    {
        var localPath = Path.Combine(_environment.ContentRootPath, "wwwroot", containerName, blobPath);
        return Task.FromResult(File.Exists(localPath));
    }

    public Task<IEnumerable<string>> ListFilesAsync(string containerName, string prefix)
    {
        var basePath = Path.Combine(_environment.ContentRootPath, "wwwroot", containerName);
        var searchPath = Path.Combine(basePath, prefix.Replace('/', Path.DirectorySeparatorChar));
        var directory = Path.GetDirectoryName(searchPath);

        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            return Task.FromResult<IEnumerable<string>>(Enumerable.Empty<string>());
        }

        var files = Directory.GetFiles(directory)
            .Select(f => Path.GetRelativePath(basePath, f).Replace(Path.DirectorySeparatorChar, '/'));

        return Task.FromResult(files);
    }

    public string GetBlobUrl(string containerName, string blobPath)
    {
        return $"{_baseUrl}/{containerName}/{blobPath}";
    }
}
