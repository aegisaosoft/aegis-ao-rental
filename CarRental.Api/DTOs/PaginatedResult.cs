namespace CarRental.Api.DTOs;

public class PaginatedResult<T>
{
    public PaginatedResult()
    {
    }

    public PaginatedResult(IReadOnlyList<T> items, int totalCount, int page, int pageSize)
    {
        Items = items;
        TotalCount = totalCount;
        Page = page;
        PageSize = pageSize;
    }

    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();

    public int TotalCount { get; set; }

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling((double)TotalCount / PageSize);
}

