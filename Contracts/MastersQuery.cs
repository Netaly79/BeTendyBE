namespace BeTendyBE.Contracts;

public sealed class MastersQuery
{
    public string? Skill { get; init; }
    public string? Address { get; init; }

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}