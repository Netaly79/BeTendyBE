using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Authentication;

using BeTendyBE.Contracts;
using BeTendyBE.Data;
using BeTendyBE.Domain;
using BeTendyBE.Infrastructure.Identity;
using Swashbuckle.AspNetCore.Annotations;

namespace BeTendyBE.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public sealed class ProfileController : ControllerBase
{
  private readonly AppDbContext _db;
  private readonly IPasswordHasher<User> _hasher;

  public ProfileController(AppDbContext db, IPasswordHasher<User> hasher)
  {
    _db = db;
    _hasher = hasher;
  }

  /// <summary>Отримати поточний профіль користувача.</summary>
  /// <response code="200">Успішно. Повернуто актуальні дані профілю.</response>
  /// <response code="401">Неавторизовано: відсутній або недійсний токен.</response>
  /// <response code="404">Користувача не знайдено.</response>
  [HttpGet("me")]
  [SwaggerOperation(Summary = "Отримати мій профіль", Description = "Повертає профіль поточного користувача.")]
  [ProducesResponseType(typeof(ProfileResponse), StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
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

  /// <summary>Оновити профіль користувача.</summary>
  /// <remarks>Оновлює ім'я, прізвище та телефон. Повертає оновлені дані профілю.</remarks>
  /// <response code="200">Профіль оновлено. Повернуто актуальні дані.</response>
  /// <response code="400">Помилка валідації тіла запиту.</response>
  /// <response code="401">Неавторизовано.</response>
  /// <response code="404">Користувача не знайдено.</response>
  [HttpPut]
  [SwaggerOperation(Summary = "Оновити профіль", Description = "Оновлює поля профілю поточного користувача.")]
  [ProducesResponseType(typeof(ProfileResponse), StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
  public async Task<ActionResult<ProfileResponse>> Update([FromBody] UpdateClientProfileRequest req, CancellationToken ct)
  {
    var userId = User.GetUserId();

    var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId, ct);
    if (u is null) return NotFound();

    u.FirstName = (req.FirstName ?? string.Empty).Trim();
    u.LastName  = (req.LastName  ?? string.Empty).Trim();
    u.Phone     = (req.Phone     ?? string.Empty).Trim();

    await _db.SaveChangesAsync(ct);
    await _db.Entry(u).Reference(x => x.Master).LoadAsync(ct);

    return Ok(ClientProfileMapping.ToDto(u));
  }

  /// <summary>Змінити пароль поточного користувача.</summary>
  /// <remarks>
  /// Вимагає поточний пароль і новий пароль.
  /// Повертає 204 No Content у разі успіху.
  /// </remarks>
  /// <response code="204">Пароль успішно змінено.</response>
  /// <response code="400">Помилка валідації тіла запиту.</response>
  /// <response code="401">Неавторизовано.</response>
  /// <response code="404">Користувача не знайдено.</response>
  /// <response code="422">Невірний поточний пароль.</response>
  [HttpPatch("password")]
  [SwaggerOperation(Summary = "Змінити пароль", Description = "Скидання пароля за умови правильного поточного пароля.")]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
  // опціонально віддаємо 422 як ProblemDetails, якщо хочеш явно відрізняти від 400
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
  public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req, CancellationToken ct)
  {
    var userId = User.GetUserId();

    var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId, ct);
    if (u is null) return NotFound();

    var res = _hasher.VerifyHashedPassword(u, u.PasswordHash, req.CurrentPassword);
    if (res == PasswordVerificationResult.Failed)
    {
      // Варіант A: кинуть виняток і перехопити глобальним middleware => 422
      // Варіант B: повернути ProblemDetails тут же (більш явний Swagger-приклад):
      return UnprocessableEntity(new ProblemDetails
      {
        Type = "https://httpstatuses.io/422",
        Title = "Current password is invalid",
        Status = StatusCodes.Status422UnprocessableEntity,
        Detail = "The provided current password does not match.",
        Instance = HttpContext?.Request?.Path.Value
      });
      // або, якщо волієш виключення:
      // throw new AuthenticationException("Current password is invalid.");
    }

    u.PasswordHash = _hasher.HashPassword(u, req.NewPassword);
    await _db.SaveChangesAsync(ct);

    return NoContent();
  }
}
