using CarRental.Api.DTOs;

namespace CarRental.Api.Services;

public interface ICarsScraper
{
    Task<List<CarSearchResultDto>> SearchAsync(string make, string model, int maxResults,
        Action<string, int>? onProgress = null, CancellationToken ct = default);
}
