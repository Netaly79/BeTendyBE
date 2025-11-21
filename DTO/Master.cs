namespace BeTendyBE.DTO;
public sealed class MasterResponse
{
  public Guid Id { get; init; }
  public Guid UserId { get; init; }

  public string FullName { get; init; } = string.Empty;
  public string? About { get; init; }

  public List<string> Skills { get; init; } = new();

  public string? City { get; init; }
  public string? Address { get; init; }
  public string? AvatarUrl { get; init; }
}