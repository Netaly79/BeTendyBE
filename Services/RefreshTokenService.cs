
using System.Security;
using System.Security.Cryptography;
using System.Text;
using BeTendlyBE.Auth;
using BeTendyBE.Data;
using BeTendyBE.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

public interface IRefreshTokenService
{
  Task<string> IssueAsync(User user, string? deviceInfo = null, CancellationToken ct = default);
  Task<(User user, RefreshToken stored)> ValidateAsync(string rawRefreshToken, CancellationToken ct = default);
  Task<string> RotateAsync(RefreshToken oldToken, string? deviceInfo = null, CancellationToken ct = default);
  Task RevokeAsync(RefreshToken token, CancellationToken ct = default);
}

public sealed class RefreshTokenService : IRefreshTokenService
{
  private readonly AppDbContext _db;
  private readonly JwtOptions _jwt;

  public RefreshTokenService(AppDbContext db, IOptions<JwtOptions> jwt)
  {
    _db = db;
    _jwt = jwt.Value;

    if (string.IsNullOrWhiteSpace(_jwt.RefreshPepper))
      throw new InvalidOperationException("Jwt:RefreshPepper is not configured.");
  }

  public async Task<string> IssueAsync(User user, string? deviceInfo = null, CancellationToken ct = default)
  {
    var raw = GenerateSecureToken();
    var hash = Hash(raw, _jwt.RefreshPepper);

    var entity = new RefreshToken
    {
      UserId = user.Id,
      TokenHash = hash,
      CreatedAtUtc = DateTime.UtcNow,
      ExpiresAtUtc = DateTime.UtcNow.AddDays(_jwt.RefreshExpiresDays),
      DeviceInfo = deviceInfo
    };

    _db.RefreshTokens.Add(entity);
    await _db.SaveChangesAsync(ct);
    return raw;
  }

  public async Task<(User user, RefreshToken stored)> ValidateAsync(string rawRefreshToken, CancellationToken ct = default)
  {
    var hash = Hash(rawRefreshToken, _jwt.RefreshPepper);
    var token = await _db.RefreshTokens
        .Include(t => t.User)
        .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

    if (token is null || token.RevokedAtUtc != null || token.ExpiresAtUtc <= DateTime.UtcNow)
      throw new SecurityException("Invalid or expired refresh token.");

    return (token.User, token);
  }

  public async Task<string> RotateAsync(RefreshToken oldToken, string? deviceInfo = null, CancellationToken ct = default)
  {
    var raw = GenerateSecureToken();
    var hash = Hash(raw, _jwt.RefreshPepper);

    oldToken.RevokedAtUtc = DateTime.UtcNow;

    var newEntity = new RefreshToken
    {
      UserId = oldToken.UserId,
      TokenHash = hash,
      CreatedAtUtc = DateTime.UtcNow,
      ExpiresAtUtc = DateTime.UtcNow.AddDays(_jwt.RefreshExpiresDays),
      DeviceInfo = deviceInfo
    };
    oldToken.ReplacedByTokenHash = newEntity.TokenHash;

    _db.RefreshTokens.Add(newEntity);
    await _db.SaveChangesAsync(ct);
    return raw;
  }

  public async Task RevokeAsync(RefreshToken token, CancellationToken ct = default)
  {
    token.RevokedAtUtc = DateTime.UtcNow;
    await _db.SaveChangesAsync(ct);
  }

  private static string GenerateSecureToken()
  {
    Span<byte> bytes = stackalloc byte[64];
    RandomNumberGenerator.Fill(bytes);
    return Base64UrlEncode(bytes);
  }

  private static string Hash(string raw, string pepper)
  {
    using var sha = SHA256.Create();
    var data = Encoding.UTF8.GetBytes(raw + pepper);
    var hash = sha.ComputeHash(data);
    return Convert.ToHexString(hash);
  }

  private static string Base64UrlEncode(ReadOnlySpan<byte> data)
  {
    var s = Convert.ToBase64String(data);
    return s.Replace("+", "-").Replace("/", "_").TrimEnd('=');
  }
}
