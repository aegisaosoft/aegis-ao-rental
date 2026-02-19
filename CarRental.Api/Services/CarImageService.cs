using CarRental.Api.DTOs;

namespace CarRental.Api.Services;

/// <summary>
/// Оркестратор: скрейпер → скачивание → Python обработка → blob upload
/// </summary>
public class CarImageService : ICarImageService
{
    private readonly ICarsScraper _scraper;
    private readonly IPythonProcessor _pythonProcessor;
    private readonly IAzureBlobStorageService _blobService;
    private readonly HttpClient _httpClient;
    private readonly ILogger<CarImageService> _logger;
    private const string BlobContainerName = "models";

    public CarImageService(
        ICarsScraper scraper,
        IPythonProcessor pythonProcessor,
        IAzureBlobStorageService blobService,
        HttpClient httpClient,
        ILogger<CarImageService> logger)
    {
        _scraper = scraper;
        _pythonProcessor = pythonProcessor;
        _blobService = blobService;
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<List<CarSearchResultDto>> SearchAsync(CarSearchRequestDto request,
        Action<string, int>? onProgress = null, CancellationToken ct = default)
    {
        return await _scraper.SearchAsync(
            request.Make, request.Model, request.MaxResults, onProgress, ct);
    }

    public async Task<CarProcessResultDto> ProcessAndUploadAsync(CarProcessRequestDto request, CancellationToken ct = default)
    {
        string? tempImagePath = null;
        string? processedPath = null;

        try
        {
            // 1. Скачиваем картинку во временную папку
            _logger.LogInformation("CarImageService: Starting download from {Url}", request.SourceImageUrl);
            tempImagePath = await DownloadImageAsync(request.SourceImageUrl, ct);
            if (tempImagePath == null)
            {
                _logger.LogWarning("CarImageService: Download failed for {Url}", request.SourceImageUrl);
                return new CarProcessResultDto
                {
                    Status = "error_download",
                    FileName = string.Empty,
                    BlobUrl = string.Empty
                };
            }

            _logger.LogInformation("CarImageService: Downloaded image to {TempPath}", tempImagePath);

            // 2. Обрабатываем Python скриптом (удаление фона, нормализация)
            processedPath = await _pythonProcessor.ProcessImageAsync(tempImagePath, ct);
            if (processedPath == null)
            {
                return new CarProcessResultDto
                {
                    Status = "error_processing",
                    FileName = string.Empty,
                    BlobUrl = string.Empty
                };
            }

            _logger.LogInformation("CarImageService: Processed image → {ProcessedPath}", processedPath);

            // 3. Загружаем в Azure Blob Storage
            var blobFileName = $"{request.Make.ToUpperInvariant().Trim().Replace(" ", "_")}_{request.Model.ToUpperInvariant().Trim().Replace(" ", "_")}.png";

            string blobUrl;
            await using (var fileStream = File.OpenRead(processedPath))
            {
                blobUrl = await _blobService.UploadFileAsync(
                    fileStream,
                    BlobContainerName,
                    blobFileName,
                    "image/png");
            }

            _logger.LogInformation("CarImageService: Uploaded to blob → {BlobUrl}", blobUrl);

            return new CarProcessResultDto
            {
                BlobUrl = blobUrl,
                FileName = blobFileName,
                Status = "success"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CarImageService: Error processing {Make} {Model}", request.Make, request.Model);
            return new CarProcessResultDto
            {
                Status = "error",
                FileName = string.Empty,
                BlobUrl = string.Empty
            };
        }
        finally
        {
            // Очистка временных файлов
            CleanupFile(tempImagePath);
            // processedPath не удаляем — он в cars_output/, PythonProcessor сам чистит input
        }
    }

    private async Task<string?> DownloadImageAsync(string imageUrl, CancellationToken ct)
    {
        try
        {
            var response = await _httpClient.GetAsync(imageUrl, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("CarImageService: Failed to download {Url}, HTTP {StatusCode}",
                    imageUrl, response.StatusCode);
                return null;
            }

            var tempDir = Path.Combine(Path.GetTempPath(), "car_images");
            Directory.CreateDirectory(tempDir);

            string extension;
            try
            {
                extension = Path.GetExtension(new Uri(imageUrl).AbsolutePath);
            }
            catch
            {
                extension = "";
            }
            if (string.IsNullOrEmpty(extension) || extension.Length > 5) extension = ".jpg";

            var tempPath = Path.Combine(tempDir, $"{Guid.NewGuid()}{extension}");

            await using var fileStream = File.Create(tempPath);
            await response.Content.CopyToAsync(fileStream, ct);

            _logger.LogInformation("CarImageService: Downloaded {Size} bytes from {Url}",
                fileStream.Length, imageUrl);

            return tempPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CarImageService: Error downloading {Url}", imageUrl);
            return null;
        }
    }

    private void CleanupFile(string? path)
    {
        if (path == null) return;
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CarImageService: Failed to cleanup {Path}", path);
        }
    }
}
