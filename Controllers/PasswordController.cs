using System.Net;
using System.Security.Cryptography;
using BeTendlyBE.Data;
using BeTendlyBE.DTO;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BeTendlyBE.Controllers;

[ApiController]
[Route("auth/password")]
public class PasswordController : ControllerBase
{
  private readonly AppDbContext _db;
  private readonly PasswordHasher<Domain.User> _hasher = new PasswordHasher<Domain.User>();

  public PasswordController(AppDbContext db)
  {
    _db = db;
  }

  [HttpPost("forgot")]
  public async Task<IActionResult> Forgot([FromBody] ForgotPasswordRequest req, [FromServices] EmailService emailService)
  {
    var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == req.Email);

    if (user != null)
    {
      var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

      _db.PasswordResetTokens.Add(new PasswordResetToken
      {
        UserId = user.Id,
        Token = token,
        ExpiresUtc = DateTime.UtcNow.AddHours(1)
      });

      await _db.SaveChangesAsync();

      var resetLink = $"https://betendly-fe.vercel.app/reset-password?token={Uri.EscapeDataString(token)}";

      await emailService.SendResetPasswordEmailAsync(user.Email, resetLink);
    }

    return NoContent();
  }

  [HttpPost("reset")]
  public async Task<IActionResult> Reset([FromBody] ResetPasswordRequest req)
  {
    var rawToken = WebUtility.UrlDecode(req.Token ?? string.Empty).Trim();

    rawToken = rawToken.Replace(' ', '+');

    var token = await _db.PasswordResetTokens
        .Include(t => t.User)
        .FirstOrDefaultAsync(t => t.Token == rawToken);

    if (token == null)
      return Unauthorized(new { error = "token_not_found" });

    if (token.User == null)
      return Unauthorized(new { error = "user_not_found_for_token", tokenId = token.Id });

    if (token.Used)
      return Unauthorized(new { error = "token_already_used", tokenId = token.Id });

    if (token.ExpiresUtc < DateTime.UtcNow)
      return Unauthorized(new { error = "token_expired", expiresUtc = token.ExpiresUtc });

    token.Used = true;
    token.User.PasswordHash = _hasher.HashPassword(token.User, req.NewPassword);

    await _db.SaveChangesAsync();

    return NoContent();
  }

  public record ForgotPasswordRequest(string Email);
  public record ResetPasswordRequest(string Token, string NewPassword);
}
