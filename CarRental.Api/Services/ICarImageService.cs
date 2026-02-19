using CarRental.Api.DTOs;

namespace CarRental.Api.Services;

public interface ICarImageService
{
    Task<List<CarSearchResultDto>> SearchAsync(CarSearchRequestDto request,
        Action<string, int>? onProgress = null, CancellationToken ct = default);
    Task<CarProcessResultDto> ProcessAndUploadAsync(CarProcessRequestDto request, CancellationToken ct = default);
}
