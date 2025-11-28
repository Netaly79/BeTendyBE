namespace BeTendlyBE.DTO;
public class AuthResponse
{
  public string AccessToken { get; set; } = string.Empty;
  public DateTime ExpiresAtUtc { get; set; }
}