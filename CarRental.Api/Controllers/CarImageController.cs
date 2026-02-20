using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using CarRental.Api.DTOs;
using CarRental.Api.Services;

namespace CarRental.Api.Controllers;

[ApiController]
[Route("api/car-images")]
[Authorize]
public class CarImageController : ControllerBase
{
    private readonly ICarImageService _carImageService;
    private readonly CarImageSearchJobStore _jobStore;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CarImageController> _logger;

    public CarImageController(
        ICarImageService carImageService,
        CarImageSearchJobStore jobStore,
        IServiceScopeFactory scopeFactory,
        ILogger<CarImageController> logger)
    {
        _carImageService = carImageService;
        _jobStore = jobStore;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Запустить поиск фото в фоне, вернуть jobId для polling
    /// </summary>
    [HttpPost("search")]
    public IActionResult Search([FromBody] CarSearchRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Make) || string.IsNullOrWhiteSpace(request.Model))
            return BadRequest(new { error = "Make and Model are required" });

        // Cleanup старых jobs
        _jobStore.Cleanup();

        // Создаём job
        var jobId = _jobStore.CreateJob();

        _logger.LogInformation("CarImage Search started: jobId={JobId}, {Make} {Model}, max={MaxResults}",
            jobId, request.Make, request.Model, request.MaxResults);

        // Запускаем поиск в фоне
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<ICarImageService>();

                _jobStore.UpdateStatus(jobId, "searching_google", $"Searching Google Images for {request.Make} {request.Model}...");

                var results = await service.SearchAsync(request, (status, count) =>
                {
                    var message = status switch
                    {
                        "searching_google" => count > 0
                            ? $"Found {count} images so far..."
                            : $"Searching Google Images for {request.Make} {request.Model}...",
                        "completed" => $"Search complete. Found {count} images.",
                        _ => $"Searching... ({count} found)"
                    };
                    _jobStore.UpdateStatus(jobId, status, message, count);
                });

                _jobStore.UpdateStatus(jobId, "completed", $"Search complete. Found {results.Count} images.", results.Count, results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CarImage Search job {JobId} failed", jobId);
                _jobStore.UpdateStatus(jobId, "error", $"Search failed: {ex.Message}");
            }
        });

        // Return directly — StandardizedResponseFilter wraps it in { result: ... }
        return Ok(new { jobId });
    }

    /// <summary>
    /// Получить статус фонового поиска (polling каждые 2 секунды)
    /// </summary>
    [HttpGet("search/status/{jobId}")]
    public IActionResult SearchStatus(string jobId)
    {
        var job = _jobStore.GetJob(jobId);
        if (job == null)
            return NotFound(new { error = "Job not found" });

        // Return directly — StandardizedResponseFilter wraps it in { result: ... }
        return Ok(job);
    }

    /// <summary>
    /// Скачать, обработать Python скриптом и загрузить в Blob Storage
    /// </summary>
    [HttpPost("process")]
    public async Task<IActionResult> Process([FromBody] CarProcessRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Make) || string.IsNullOrWhiteSpace(request.Model))
            return BadRequest(new { error = "Make and Model are required" });

        if (string.IsNullOrWhiteSpace(request.SourceImageUrl))
            return BadRequest(new { error = "SourceImageUrl is required" });

        _logger.LogInformation("CarImage Process: {Make} {Model}, source={Url}",
            request.Make, request.Model, request.SourceImageUrl);

        // Не передаём HTTP CancellationToken — обработка должна завершиться
        // даже если клиент отключился (Python + upload могут занять >30 сек)
        var result = await _carImageService.ProcessAndUploadAsync(request, CancellationToken.None);

        if (result.Status != "success")
        {
            _logger.LogWarning("CarImage Process failed: status={Status}, file={FileName}", result.Status, result.FileName);
        }

        // Return DTO directly — StandardizedResponseFilter wraps it in { result: ... }
        return Ok(result);
    }
}
