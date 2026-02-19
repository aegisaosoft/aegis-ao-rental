namespace CarRental.Api.DTOs;

public class CarSearchRequestDto
{
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int MaxResults { get; set; } = 10;
}

public class CarSearchResultDto
{
    public string Id { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty; // "cars.com" или "google"
}

public class CarProcessRequestDto
{
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string SourceImageUrl { get; set; } = string.Empty;
}

public class CarProcessResultDto
{
    public string BlobUrl { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Статус фонового поиска изображений
/// </summary>
public class CarSearchJobStatus
{
    public string JobId { get; set; } = string.Empty;
    public string Status { get; set; } = "pending"; // pending, searching_cars, searching_google, completed, error
    public string Message { get; set; } = string.Empty;
    public int FoundCount { get; set; }
    public List<CarSearchResultDto> Results { get; set; } = new();
    public DateTime StartedAt { get; set; }
    public double ElapsedSeconds { get; set; }
}
