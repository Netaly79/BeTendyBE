using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using BeTendyBE.Contracts;
using BeTendyBE.Data;
using BeTendyBE.Domain;
using BeTendyBE.Infrastructure.Identity;
using Swashbuckle.AspNetCore.Annotations;

namespace BeTendyBE.Controllers;

[ApiController]
[Route("member")]
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
  [HttpGet("{id:guid?}")]
  [SwaggerOperation(Summary = "Отримати профіль",
    Description = "Без параметра повертає мій профіль. З параметром id — профіль користувача за ідентифікатором.")]
  [ProducesResponseType(typeof(ProfileResponse), StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
  public async Task<ActionResult<ProfileResponse>> Me(Guid? id, CancellationToken ct)
  {
    var targetUserId = id ?? User.GetUserId();
    var baseDto = await _db.Users
    .AsNoTracking()
    .Where(u => u.Id == targetUserId /* && !u.IsDeleted && !u.IsBanned */)
    .Select(u => new
    {
      Dto = new ProfileResponse(
            u.Id,
            (u.Email ?? string.Empty).Trim(),
            (u.FirstName ?? string.Empty).Trim(),
            (u.LastName ?? string.Empty).Trim(),
            (u.Phone ?? string.Empty).Trim(),
            u.AvatarUrl,
            (u.Master != null) || u.IsMaster,
            u.Master == null
                ? null
                : new MasterProfileResponse(
                    u.Master.About,
                    u.Master.Skills,
                    u.Master.ExperienceYears,
                    u.Master.Address,
                    null
                )
        ),
      MasterId = u.Master != null ? (Guid?)u.Master.Id : null
    })
    .FirstOrDefaultAsync(ct);

    if (baseDto is null) return NotFound();

    var dto = baseDto.Dto;

    if (baseDto.Dto.IsMaster && baseDto.MasterId is Guid mid)
    {
      var services = await _db.Services
          .AsNoTracking()
          .Where(s => s.MasterId == mid)
          .OrderBy(s => s.Name)
          .Select(s => new ServiceListItemResponse
          {
            Id = s.Id,
            Name = s.Name,
            Price = s.Price,
            DurationMinutes = s.DurationMinutes,
            Description = s.Description,
            CreatedAtUtc = s.CreatedAtUtc,
            UpdatedAtUtc = s.UpdatedAtUtc
          })
          .Take(20)
          .ToListAsync(ct);

      if (dto.Master is not null)
        dto = dto with { Master = dto.Master with { Services = services } };
    }

    if (dto.Master is not null && dto.Master.Skills is null)
      dto = dto with { Master = dto.Master with { Skills = [] } };

    return Ok(dto);
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
  [Authorize]
  [SwaggerOperation(Summary = "Змінити пароль", Description = "Скидання пароля за умови правильного поточного пароля.")]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
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

  private static IQueryable<ProfileResponse> BuildProfileQuery(AppDbContext db, Guid userId)
        => db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId /* && !u.IsDeleted && !u.IsBanned */)
            .Select(u => new ProfileResponse(
                u.Id,
                (u.Email ?? string.Empty).Trim(),
                (u.FirstName ?? string.Empty).Trim(),
                (u.LastName ?? string.Empty).Trim(),
                (u.Phone ?? string.Empty).Trim(),
                u.AvatarUrl,
                (u.Master != null) || u.IsMaster,
                u.Master == null
                    ? null
                    : new MasterProfileResponse(
                        u.Master.About,
                        u.Master.Skills,
                        u.Master.ExperienceYears,
                        u.Master.Address,
                        null
                    )
            ));

  [HttpPut]
  [Authorize]
  [SwaggerOperation(
      Summary = "Редагувати мій профіль",
      Description = "Часткове оновлення полів профілю користувача і (за наявності) майстра. Сервіси не змінюються."
  )]
  [ProducesResponseType(typeof(ProfileResponse), StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
  public async Task<ActionResult<ProfileResponse>> UpdateMyProfile(
      [FromBody] MemberUpdateRequest body,
      CancellationToken ct)
  {
    // 1) Авторизация
    Guid currentUserId;
    try { currentUserId = User.GetUserId(); }
    catch { return Unauthorized(); }

    // 2) Быстрая валидация: есть ли вообще что обновлять?
    var hasUser = body?.User is not null;
    var hasMaster = body?.Master is not null;
    if (!hasUser && !hasMaster)
      return BadRequest("Nothing to update.");

    // 3) Загружаем пользователя (трекинг нужен для обновления)
    var user = await _db.Users
        .FirstOrDefaultAsync(u => u.Id == currentUserId, ct);

    if (user is null) return NotFound();

    // 4) Применяем частичные изменения к User
    if (hasUser)
    {
      var u = body!.User!;
      if (u.FirstName is not null) user.FirstName = u.FirstName.Trim();
      if (u.LastName is not null) user.LastName = u.LastName.Trim();
      if (u.Phone is not null) user.Phone = u.Phone.Trim();
      if (u.AvatarUrl is not null) user.AvatarUrl = string.IsNullOrWhiteSpace(u.AvatarUrl) ? null : u.AvatarUrl.Trim();

    }

    // 5) Применяем частичные изменения к Master (если присланы)
    if (hasMaster)
    {
      // Находим мастер-профиль этого пользователя по userId
      var master = await _db.Masters.FirstOrDefaultAsync(m => m.UserId == currentUserId, ct);
      if (master is null)
        return BadRequest("User is not a master.");

      var m = body!.Master!;
      if (m.About is not null) master.About = string.IsNullOrWhiteSpace(m.About) ? null : m.About.Trim();
      if (m.Address is not null) master.Address = string.IsNullOrWhiteSpace(m.Address) ? null : m.Address.Trim();
      if (m.ExperienceYears is not null) master.ExperienceYears = m.ExperienceYears;
      if (m.Skills is not null) master.Skills = m.Skills;
      master.UpdatedAtUtc = DateTime.UtcNow;
    }

    // 6) Сохраняем изменения
    await _db.SaveChangesAsync(ct);

    // 7) Возвращаем свежий профиль (тем же контрактом, что в GET /profile)
    var dto = await BuildProfileQuery(_db, currentUserId).FirstOrDefaultAsync(ct);
    if (dto is null) return NotFound(); // маловероятно

    // Нормализация коллекций: фронту удобнее [] вместо null
    if (dto.Master is not null && dto.Master.Skills is null)
      dto = dto with { Master = dto.Master with { Skills = [] } };

    return Ok(dto);
  }

  [HttpPost("me/avatar")]
  [Authorize]
  [RequestSizeLimit(2_000_000)]
  public async Task<IActionResult> UploadAvatar(
  IFormFile file,
  [FromServices] IWebHostEnvironment env)
  {
    if (file == null || file.Length == 0)
      return BadRequest("Файл не завантажено.");

    if (!file.ContentType.StartsWith("image/"))
      return BadRequest("Потрібне зображення.");

    var userId = User.GetUserId();

    // путь до wwwroot
    var webRoot = env.WebRootPath;
    if (string.IsNullOrEmpty(webRoot))
    {
      webRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
    }

    var avatarsRoot = Path.Combine(webRoot, "avatars");
    if (!Directory.Exists(avatarsRoot))
    {
      Directory.CreateDirectory(avatarsRoot);
    }

    var fileName = $"{userId:N}.jpg";
    var filePath = Path.Combine(avatarsRoot, fileName);

    using (var stream = System.IO.File.Create(filePath))
    {
      await file.CopyToAsync(stream);
    }

    var avatarUrl = $"/avatars/{fileName}";

    var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
    if (user == null)
      return Unauthorized();

    user.AvatarUrl = avatarUrl;
    await _db.SaveChangesAsync();

    return Ok(new { avatarUrl });
  }

}
