using System.Security.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using BeTendyBE.Contracts;
using BeTendyBE.Data;
using BeTendyBE.Domain;

namespace BeTendlyBE.Services
{
  public interface IProfileService
  {
    Task<User> GetMeAsync(Guid userId, CancellationToken ct);
    Task<User> UpdateAsync(Guid userId, UpdateClientProfileRequest req, CancellationToken ct);
    Task ChangePasswordAsync(Guid userId, string current, string next, CancellationToken ct);
  }

  public sealed class ProfileService : IProfileService
  {
    private readonly AppDbContext _db;
    private readonly IPasswordHasher<User> _hasher;

    public ProfileService(AppDbContext db, IPasswordHasher<User> hasher)
    {
      _db = db; _hasher = hasher;
    }

    public async Task<User> GetMeAsync(Guid userId, CancellationToken ct)
    {
      return await _db.Users
          .Include(u => u.Master)
          .AsNoTracking()
          .FirstOrDefaultAsync(u => u.Id == userId, ct)
          ?? throw new KeyNotFoundException("User not found");
    }

    public async Task<User> UpdateAsync(Guid userId, UpdateClientProfileRequest req, CancellationToken ct)
    {
      var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId, ct)
              ?? throw new KeyNotFoundException("User not found");

      u.FirstName = (req.FirstName ?? string.Empty).Trim();
      u.LastName = (req.LastName ?? string.Empty).Trim();
      u.Phone = (req.Phone ?? string.Empty).Trim();

      await _db.SaveChangesAsync(ct);
      return u;
    }

    public async Task ChangePasswordAsync(Guid userId, string current, string next, CancellationToken ct)
    {
      var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId, ct)
              ?? throw new KeyNotFoundException("User not found");

      var res = _hasher.VerifyHashedPassword(u, u.PasswordHash, current);
      if (res == PasswordVerificationResult.Failed)
        throw new AuthenticationException("Current password is invalid.");

      u.PasswordHash = _hasher.HashPassword(u, next);
      await _db.SaveChangesAsync(ct);
    }
  }
}
