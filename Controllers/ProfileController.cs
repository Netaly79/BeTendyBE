using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Authentication;

using BeTendyBE.Contracts;
using BeTendyBE.Data;
using BeTendyBE.Domain;
using BeTendyBE.Infrastructure.Identity;

namespace BeTendyBE.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class ProfileController : ControllerBase
{
  private readonly AppDbContext _db;
  private readonly IPasswordHasher<User> _hasher;

  public ProfileController(AppDbContext db, IPasswordHasher<User> hasher)
  {
    _db = db;
    _hasher = hasher;
  }

  // GET /api/profile/me
  [HttpGet("me")]
  public async Task<ActionResult<ProfileResponse>> Me(CancellationToken ct)
  {
    Guid userId;
    try { userId = User.GetUserId(); }
    catch { return Unauthorized(); }

    var user = await _db.Users
        .Include(u => u.Master)
        .AsNoTracking()
        .FirstOrDefaultAsync(u => u.Id == userId, ct);

    if (user is null) return NotFound();

    return Ok(ClientProfileMapping.ToDto(user));
  }


  [HttpPut]
  public async Task<ActionResult<ProfileResponse>> Update([FromBody] UpdateClientProfileRequest req, CancellationToken ct)
  {
    var userId = User.GetUserId();

    var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId, ct);
    if (u is null) return NotFound();

    u.FirstName = (req.FirstName ?? string.Empty).Trim();
    u.LastName = (req.LastName ?? string.Empty).Trim();
    u.Phone = (req.Phone ?? string.Empty).Trim();

    await _db.SaveChangesAsync(ct);

    await _db.Entry(u).Reference(x => x.Master).LoadAsync(ct);

    return Ok(ClientProfileMapping.ToDto(u));
  }


  // PATCH /api/profile/password  — смена пароля
  [HttpPatch("password")]
  public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req, CancellationToken ct)
  {
    var userId = User.GetUserId();

    var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId, ct);
    if (u is null) return NotFound();

    var res = _hasher.VerifyHashedPassword(u, u.PasswordHash, req.CurrentPassword);
    if (res == PasswordVerificationResult.Failed)
      throw new AuthenticationException("Current password is invalid.");

    u.PasswordHash = _hasher.HashPassword(u, req.NewPassword);
    await _db.SaveChangesAsync(ct);

    return NoContent();
  }
}
