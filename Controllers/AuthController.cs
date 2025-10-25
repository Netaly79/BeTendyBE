using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using BeTendlyBE.Services;
using BeTendyBE.Data;
using BeTendyBE.Domain;
using BeTendyBE.DTO;

namespace BeTendyBE.Controllers;
[ApiController]
[Route("api/[controller]")]
public sealed class AuthController : ControllerBase
{
  private readonly AppDbContext _db;
  private readonly IPasswordHasher<User> _hasher;
  private readonly IJwtProvider _jwt;
  private readonly IRefreshTokenService _refreshSvc;

  public AuthController(AppDbContext db, IPasswordHasher<User> hasher, IJwtProvider jwt, IRefreshTokenService refreshSvc)
  {
    _db = db;
    _hasher = hasher;
    _jwt = jwt;
    _refreshSvc = refreshSvc;
  }

  [HttpPost("register")]
  public async Task<IActionResult> Register([FromBody] RegisterRequest req)
  {
    req.Email = req.Email.Trim().ToLowerInvariant();

    if (await _db.Users.AnyAsync(u => u.Email == req.Email))
      return BadRequest(new { message = "Email already registered" });

    var user = new User
    {
      Email = req.Email,
      FirstName = req.FirstName.Trim(),
      LastName = req.LastName.Trim()
    };

    user.PasswordHash = _hasher.HashPassword(user, req.Password);

    _db.Users.Add(user);
    await _db.SaveChangesAsync();

    var access = _jwt.Generate(user);
    var refresh = await _refreshSvc.IssueAsync(user, deviceInfo: Request.Headers["User-Agent"]);

    return Ok(new AuthWithRefreshResponse
    {
      AccessToken = access.AccessToken,
      ExpiresAtUtc = access.ExpiresAtUtc,
      RefreshToken = refresh
    });
  }

  [HttpPost("login")]
  public async Task<IActionResult> Login([FromBody] LoginRequest req)
  {
    var email = req.Email.Trim().ToLowerInvariant();

    var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
    if (user is null)
      return Unauthorized(new { message = "Invalid email or password" });

    var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, req.Password);
    if (result == PasswordVerificationResult.Failed)
      return Unauthorized(new { message = "Invalid email or password" });

    var access = _jwt.Generate(user);
    var refresh = await _refreshSvc.IssueAsync(user, deviceInfo: Request.Headers["User-Agent"]);

    return Ok(new AuthWithRefreshResponse
    {
      AccessToken = access.AccessToken,
      ExpiresAtUtc = access.ExpiresAtUtc,
      RefreshToken = refresh
    });
  }

  [HttpPost("refresh")]
  public async Task<IActionResult> Refresh([FromBody] RefreshRequest req)
  {
    if (string.IsNullOrWhiteSpace(req.RefreshToken))
      return BadRequest(new { message = "Refresh token required" });

    var (user, stored) = await _refreshSvc.ValidateAsync(req.RefreshToken);

    var access = _jwt.Generate(user);
    var newRefresh = await _refreshSvc.RotateAsync(stored, deviceInfo: Request.Headers["User-Agent"]);

    return Ok(new AuthWithRefreshResponse
    {
      AccessToken = access.AccessToken,
      ExpiresAtUtc = access.ExpiresAtUtc,
      RefreshToken = newRefresh
    });
  }
}

public sealed class RefreshRequest { public string RefreshToken { get; set; } = string.Empty; }

public sealed class AuthWithRefreshResponse
{
  public string AccessToken { get; set; } = string.Empty;
  public DateTime ExpiresAtUtc { get; set; }
  public string RefreshToken { get; set; } = string.Empty;
}