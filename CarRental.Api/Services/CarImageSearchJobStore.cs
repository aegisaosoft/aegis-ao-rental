using System.Collections.Concurrent;
using CarRental.Api.DTOs;

namespace CarRental.Api.Services;

/// <summary>
/// In-memory хранилище для фоновых задач поиска изображений.
/// Singleton — живёт всё время работы приложения.
/// </summary>
public class CarImageSearchJobStore
{
    private readonly ConcurrentDictionary<string, CarSearchJobStatus> _jobs = new();

    public string CreateJob()
    {
        var jobId = Guid.NewGuid().ToString("N")[..12];
        _jobs[jobId] = new CarSearchJobStatus
        {
            JobId = jobId,
            Status = "pending",
            Message = "Job created, starting search...",
            StartedAt = DateTime.UtcNow
        };
        return jobId;
    }

    public CarSearchJobStatus? GetJob(string jobId)
    {
        _jobs.TryGetValue(jobId, out var job);
        if (job != null)
        {
            job.ElapsedSeconds = (DateTime.UtcNow - job.StartedAt).TotalSeconds;
        }
        return job;
    }

    public void UpdateStatus(string jobId, string status, string message, int? foundCount = null, List<CarSearchResultDto>? results = null)
    {
        if (_jobs.TryGetValue(jobId, out var job))
        {
            job.Status = status;
            job.Message = message;
            if (foundCount.HasValue) job.FoundCount = foundCount.Value;
            if (results != null) job.Results = results;
            job.ElapsedSeconds = (DateTime.UtcNow - job.StartedAt).TotalSeconds;
        }
    }

    /// <summary>
    /// Удаляем старые jobs (>10 минут) чтобы не копить память
    /// </summary>
    public void Cleanup()
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-10);
        foreach (var kvp in _jobs)
        {
            if (kvp.Value.StartedAt < cutoff)
            {
                _jobs.TryRemove(kvp.Key, out _);
            }
        }
    }
}
