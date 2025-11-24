using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;
using System.Security.Claims;

using BeTendlyBE.Services;
using BeTendyBE.Data;
using BeTendyBE.Domain;
using BeTendyBE.DTO;
using BeTendyBE.Infrastructure.Identity;

namespace BeTendyBE.Controllers;

[ApiController]
[Route("auth")]
[Produces("application/json")]
public sealed class AuthController : ControllerBase
{
  private readonly AppDbContext _db;
  private readonly IPasswordHasher<User> _hasher;
  private readonly IJwtProvider _jwt;
  private readonly IRefreshTokenService _refreshSvc;
  private readonly IMasterService _masterSvc;

  public AuthController(AppDbContext db, IPasswordHasher<User> hasher, IJwtProvider jwt, IRefreshTokenService refreshSvc, IMasterService masterSvc)
  {
    _db = db;
    _hasher = hasher;
    _jwt = jwt;
    _refreshSvc = refreshSvc;
    _masterSvc = masterSvc;
  }

  /// <summary>Реєстрація нового користувача.</summary>
  /// <remarks>
  /// Створює користувача та повертає пару токенів: <c>access</c> і <c>refresh</c>.
  /// Якщо email вже використовується — повертає 400 з деталями помилки.
  /// </remarks>
  /// <response code="200">Успішна реєстрація. Повернено токени доступу.</response>
  /// <response code="400">Помилка валідації або email вже зареєстрований.</response>
  [HttpPost("register")]
  [AllowAnonymous]
  [SwaggerOperation(Summary = "Реєстрація", Description = "Створює обліковий запис і повертає access/refresh токени.")]
  [ProducesResponseType(typeof(AuthWithRefreshResponse), StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
  public async Task<IActionResult> Register([FromBody] RegisterRequest req, CancellationToken ct)
  {
    // Базова нормалізація
    req.Email = (req.Email ?? string.Empty).Trim().ToLowerInvariant();

    if (await _db.Users.AnyAsync(u => u.Email == req.Email))
    {
      return BadRequest(new ProblemDetails
      {
        Type = "https://httpstatuses.io/400",
        Title = "Email already registered",
        Status = StatusCodes.Status400BadRequest,
        Detail = "A user with the provided email already exists.",
        Instance = HttpContext?.Request?.Path.Value
      });
    }

    using var tx = await _db.Database.BeginTransactionAsync(ct);
    var user = new User
    {
      Email = req.Email,
      FirstName = (req.FirstName ?? string.Empty).Trim(),
      LastName = (req.LastName ?? string.Empty).Trim(),
      IsMaster = false
    };

    user.PasswordHash = _hasher.HashPassword(user, req.Password);

    _db.Users.Add(user);
    await _db.SaveChangesAsync(ct);

    if (req.IsMaster)
    {
      await _masterSvc.EnsureMasterAsync(user.Id, ct);
    }
    await tx.CommitAsync(ct);

    var access = _jwt.Generate(user);
    var refresh = await _refreshSvc.IssueAsync(user, deviceInfo: Request.Headers["User-Agent"]);

    return Ok(new AuthWithRefreshResponse
    {
      AccessToken = access.AccessToken,
      ExpiresAtUtc = access.ExpiresAtUtc,
      RefreshToken = refresh
    });
  }

  /// <summary>Логін користувача.</summary>
  /// <remarks>
  /// Повертає нову пару <c>access</c>/<c>refresh</c>. Невірні облікові дані — 401.
  /// </remarks>
  /// <response code="200">Успішний вхід. Повернено токени доступу.</response>
  /// <response code="401">Невірний email або пароль.</response>
  [HttpPost("login")]
  [AllowAnonymous]
  [SwaggerOperation(Summary = "Логін", Description = "Аутентифікація користувача та видача токенів.")]
  [ProducesResponseType(typeof(AuthWithRefreshResponse), StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
  public async Task<IActionResult> Login([FromBody] LoginRequest req)
  {
    var email = (req.Email ?? string.Empty).Trim().ToLowerInvariant();

    var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
    if (user is null)
    {
      return Unauthorized(new ProblemDetails
      {
        Type = "https://httpstatuses.io/401",
        Title = "Invalid credentials",
        Status = StatusCodes.Status401Unauthorized,
        Detail = "Invalid email or password.",
        Instance = HttpContext?.Request?.Path.Value
      });
    }

    var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, req.Password);
    if (result == PasswordVerificationResult.Failed)
    {
      return Unauthorized(new ProblemDetails
      {
        Type = "https://httpstatuses.io/401",
        Title = "Invalid credentials",
        Status = StatusCodes.Status401Unauthorized,
        Detail = "Невірний логін або пароль.",
        Instance = HttpContext?.Request?.Path.Value
      });
    }

    var access = _jwt.Generate(user);
    var refresh = await _refreshSvc.IssueAsync(user, deviceInfo: Request.Headers["User-Agent"]);

    return Ok(new AuthWithRefreshResponse
    {
      AccessToken = access.AccessToken,
      ExpiresAtUtc = access.ExpiresAtUtc,
      RefreshToken = refresh
    });
  }

  /// <summary>Отримати дані поточного користувача.</summary>
  /// <remarks>
  /// Повертає основну інформацію з JWT токена та бази (якщо треба — оновлену).
  /// </remarks>
  /// <response code="200">Повернуто дані користувача.</response>
  /// <response code="401">Користувач не авторизований.</response>
  [HttpGet("me")]
  [Authorize]
  [SwaggerOperation(Summary = "Поточний користувач", Description = "Повертає дані поточного користувача за JWT.")]
  [ProducesResponseType(typeof(UserInfoResponse), StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
  public async Task<IActionResult> Me()
  {
    var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (!Guid.TryParse(userIdStr, out var userId))
    {
      return Unauthorized(new ProblemDetails
      {
        Type = "https://httpstatuses.io/401",
        Title = "Invalid token",
        Status = StatusCodes.Status401Unauthorized,
        Detail = "Token does not contain a valid user ID."
      });
    }

    var user = await _db.Users.FindAsync(userId);
    if (user is null)
    {
      return Unauthorized(new ProblemDetails
      {
        Type = "https://httpstatuses.io/401",
        Title = "User not found",
        Status = StatusCodes.Status401Unauthorized,
        Detail = "User no longer exists."
      });
    }

    return Ok(new UserInfoResponse
    {
      Id = user.Id,
      Email = user.Email,
      FirstName = user.FirstName,
      LastName = user.LastName,
      AvatarUrl = user.AvatarUrl ?? string.Empty,
      IsMaster = user.IsMaster
    });
  }

  /// <summary>
  /// Оновити токен (refresh) — отримати новий access-токен і новий refresh-токен.
  /// </summary>
  /// <remarks>
  /// Використовується для продовження сесії без повторного логіну.
  ///
  /// <para>
  /// Потрібно передати дійсний <c>refreshToken</c>. Сервіс виконує:
  /// <list type="bullet">
  ///   <item><description>валідацію refresh-токена;</description></item>
  ///   <item><description>перевірку, що токен не відкликаний (<c>revoked</c>) і не прострочений;</description></item>
  ///   <item><description>ротацію refresh-токена (старий — відкликається, видається новий);</description></item>
  ///   <item><description>генерацію нового access-токена.</description></item>
  /// </list>
  /// </para>
  /// </remarks>
  /// <param name="req">Запит із refresh-токеном, який потрібно оновити.</param>
  /// <response code="200">
  /// Успішне оновлення. Повертає новий access-токен, час його дії та новий refresh-токен.
  /// </response>
  /// <response code="400">
  /// Refresh-токен не передано у запиті.
  /// </response>
  /// <response code="401">
  /// Refresh-токен некоректний, прострочений, відкликаний або не належить користувачу.
  /// </response>
  [HttpPost("refresh")]
  [AllowAnonymous]
  [ProducesResponseType(typeof(AuthWithRefreshResponse), StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
  public async Task<IActionResult> Refresh([FromBody] RefreshRequest req)
  {
    if (string.IsNullOrWhiteSpace(req.RefreshToken))
    {
      return BadRequest(new ProblemDetails
      {
        Type = "https://httpstatuses.io/400",
        Title = "Refresh token required",
        Status = StatusCodes.Status400BadRequest,
        Detail = "The refresh token must be provided.",
        Instance = HttpContext?.Request?.Path.Value
      });
    }
    try
    {
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
    catch (Exception)
    {
      return Unauthorized(new ProblemDetails
      {
        Type = "https://httpstatuses.io/401",
        Title = "Invalid refresh token",
        Status = StatusCodes.Status401Unauthorized,
        Detail = "The provided refresh token is invalid or expired.",
        Instance = HttpContext?.Request?.Path.Value
      });
    }
  }

  /// <summary>
  /// Вийти з системи (logout) для поточного пристрою.
  /// </summary>
  /// <remarks>
  /// Logout реалізовано через відкликання (revoke) refresh-токена.
  ///
  /// </remarks>
  /// <param name="req">Запит із refresh-токеном, який потрібно відкликати.</param>
  /// <response code="204">
  /// Logout виконано успішно, refresh-токен відкликано (або вже був недійсним).
  /// </response>
  /// <response code="400">
  /// Refresh-токен не передано у запиті.
  /// </response>
  /// <response code="401">
  /// Refresh-токен некоректний, не належить поточному користувачу
  /// або вже прострочений / відкликаний.
  /// </response>
  [HttpPost("logout")]
  [Authorize]
  [Produces("application/json")]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  public async Task<IActionResult> Logout([FromBody] RefreshRequest req)
  {
    if (string.IsNullOrWhiteSpace(req.RefreshToken))
    {
      return BadRequest(new ProblemDetails
      {
        Type = "https://httpstatuses.io/400",
        Title = "Refresh token required",
        Status = StatusCodes.Status400BadRequest,
        Detail = "The refresh token must be provided.",
        Instance = HttpContext?.Request?.Path.Value
      });
    }

    var currentUserId = User.GetUserId();

    try
    {
      var (user, stored) = await _refreshSvc.ValidateAsync(req.RefreshToken);

      if (user.Id != currentUserId)
      {
        return Unauthorized(new ProblemDetails
        {
          Type = "https://httpstatuses.io/401",
          Title = "Invalid refresh token",
          Status = StatusCodes.Status401Unauthorized,
          Detail = "The provided refresh token does not belong to the current user.",
          Instance = HttpContext?.Request?.Path.Value
        });
      }

      await _refreshSvc.RevokeAsync(stored, HttpContext.RequestAborted);

      return NoContent();
    }
    catch (Exception)
    {
      return Unauthorized(new ProblemDetails
      {
        Type = "https://httpstatuses.io/401",
        Title = "Invalid refresh token",
        Status = StatusCodes.Status401Unauthorized,
        Detail = "The provided refresh token is invalid, expired, or already revoked.",
        Instance = HttpContext?.Request?.Path.Value
      });
    }
  }
}

public sealed class RefreshRequest
{
  public string RefreshToken { get; set; } = string.Empty;
}

public sealed class AuthWithRefreshResponse
{
  public string AccessToken { get; set; } = string.Empty;
  public DateTime ExpiresAtUtc { get; set; }
  public string RefreshToken { get; set; } = string.Empty;
}
