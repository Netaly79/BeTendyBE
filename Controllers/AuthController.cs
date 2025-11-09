using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;

using BeTendlyBE.Services;
using BeTendyBE.Data;
using BeTendyBE.Domain;
using BeTendyBE.DTO;
using System.Security.Claims;

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
        Detail = "Invalid email or password.",
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
      IsMaster = user.IsMaster
    });
  }

  /// <summary>Оновлення токенів за refresh-токеном.</summary>
  /// <remarks>
  /// Приймає <c>refreshToken</c>. Повертає нову пару <c>access</c>/<c>refresh</c>.
  /// Порожній або недійсний refresh — 400/401 відповідно.
  /// </remarks>
  /// <response code="200">Оновлено. Повернено нові токени.</response>
  /// <response code="400">Відсутній або некоректний формат refresh-токена.</response>
  /// <response code="401">Недійсний/протермінований refresh-токен.</response>
  [HttpPost("refresh")]
  [AllowAnonymous]
  [SwaggerOperation(Summary = "Оновити токени", Description = "Обміняти дійсний refresh на нову пару токенів.")]
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

    // Валідація може кидати виключення — краще повертати керований 401
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
