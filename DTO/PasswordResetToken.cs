using BeTendlyBE.Domain;

namespace BeTendlyBE.DTO;

public class PasswordResetToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }
    public User? User { get; set; }

    public string Token { get; set; } = default!;
    public DateTime ExpiresUtc { get; set; }
    public bool Used { get; set; }
}
