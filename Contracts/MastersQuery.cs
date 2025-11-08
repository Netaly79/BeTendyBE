namespace BeTendyBE.Contracts;

public sealed class MastersQuery
{
    public string? Skill { get; init; }      // ?skill=haircut
    public string? Address { get; init; }    // ?address=kyiv

    public int Page { get; init; } = 1;      // ?page=1
    public int PageSize { get; init; } = 20; // ?pageSize=20
}