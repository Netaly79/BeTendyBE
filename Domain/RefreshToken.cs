using BeTendlyBE.Domain;

public class RefreshToken
{
  public int Id { get; set; }
  public Guid UserId { get; set; }

  public string TokenHash { get; set; } = string.Empty;

  public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
  public DateTime ExpiresAtUtc { get; set; }
  public DateTime? RevokedAtUtc { get; set; }

  public string? ReplacedByTokenHash { get; set; }

  public string? DeviceInfo { get; set; }

  public User User { get; set; } = default!;
}
