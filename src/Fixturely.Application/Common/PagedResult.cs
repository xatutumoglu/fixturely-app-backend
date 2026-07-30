namespace Fixturely.Application.Common;

public sealed class PagedResult<T>
{
    public required IReadOnlyCollection<T> Items { get; init; }

    public required int PageNumber { get; init; }

    public required int PageSize { get; init; }

    public required int TotalCount { get; init; }

    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public sealed class PaginationRequest
{
    private const int MaxPageSize = 100;
    private int _pageSize = 20;

    public int PageNumber { get; set; } = 1;

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value is <= 0 or > MaxPageSize ? 20 : value;
    }
}
