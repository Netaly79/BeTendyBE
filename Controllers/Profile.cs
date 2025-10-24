using System.Security.Claims;
using BeTendyBE.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/[controller]")]
public sealed class ProfileController : ControllerBase
{
  private readonly AppDbContext _db;

  public ProfileController(AppDbContext db) => _db = db;

  [HttpGet("me")]
  [Authorize]
  public async Task<IActionResult> Me()
  {
    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

    if (!Guid.TryParse(userId, out var id)) return Unauthorized();

    var user = await _db.Users
        .Where(u => u.Id == id)
        .Select(u => new { u.Id, u.Email, u.FirstName, u.LastName, u.Phone, u.Role })
        .FirstOrDefaultAsync();

    return user is null ? NotFound() : Ok(user);
  }
}