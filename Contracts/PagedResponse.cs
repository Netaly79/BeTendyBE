namespace BeTendyBE.Contracts;

public sealed class PagedResponse<T>
{
    public required List<T> Items { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int Total { get; init; }
}