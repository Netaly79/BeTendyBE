using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using BeTendlyBE.Contracts;
using BeTendlyBE.Data;
using BeTendlyBE.Domain;
using BeTendlyBE.Infrastructure.Identity;
using Swashbuckle.AspNetCore.Annotations;

namespace BeTendlyBE.Controllers;

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
  /// 
  /// <remarks>
  /// Без параметра повертає мій профіль. З параметром id — профіль користувача за ідентифікатором.
  /// <para>
  /// Щоб відобразити аватарку на фронті, необхідно поєднати
  /// <c>baseUrl</c> API з <c>avatarUrl</c>, наприклад:
  /// <br/>
  /// <c>https://betendly-api.azurewebsites.net/avatars/ddd41e45.jpg</c>
  /// </para>
  /// </remarks>
  /// <response code="200">Успішно. Повернуто актуальні дані профілю.</response>
  /// <response code="401">Неавторизовано: відсутній або недійсний токен.</response>
  /// <response code="404">Користувача не знайдено.</response>
  [HttpGet("{id:guid?}")]
  [ProducesResponseType(typeof(ProfileResponse), StatusCodes.Status200OK)]
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
                    u.Master.City,
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
      return UnprocessableEntity(new ProblemDetails
      {
        Type = "https://httpstatuses.io/422",
        Title = "Поточний пароль недійсний",
        Status = StatusCodes.Status422UnprocessableEntity,
        Detail = "Вказаний поточний пароль не збігається зі збереженим в системі.",
        Instance = HttpContext?.Request?.Path.Value
      });
    }

    u.PasswordHash = _hasher.HashPassword(u, req.NewPassword);
    await _db.SaveChangesAsync(ct);

    return NoContent();
  }

  private static IQueryable<ProfileResponse> BuildProfileQuery(AppDbContext db, Guid userId)
        => db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
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
                        u.Master.City,
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
    Guid currentUserId;
    try { currentUserId = User.GetUserId(); }
    catch { return Unauthorized(); }

    var hasUser = body?.User is not null;
    var hasMaster = body?.Master is not null;
    if (!hasUser && !hasMaster)
      return BadRequest("Nothing to update.");

    var user = await _db.Users
        .FirstOrDefaultAsync(u => u.Id == currentUserId, ct);

    if (user is null) return NotFound();

    if (hasUser)
    {
      var u = body!.User!;
      if (u.FirstName is not null) user.FirstName = u.FirstName.Trim();
      if (u.LastName is not null) user.LastName = u.LastName.Trim();
      if (u.Phone is not null) user.Phone = u.Phone.Trim();
    }

    if (hasMaster)
    {
      var master = await _db.Masters.FirstOrDefaultAsync(m => m.UserId == currentUserId, ct);
      if (master is null)
        return BadRequest("User is not a master.");

      var m = body!.Master!;
      if (m.About is not null) master.About = string.IsNullOrWhiteSpace(m.About) ? null : m.About.Trim();
      if (m.Address is not null) master.Address = string.IsNullOrWhiteSpace(m.Address) ? null : m.Address.Trim();
      if (m.City is not null) master.City = string.IsNullOrWhiteSpace(m.City) ? null : m.City.Trim();
      if (m.ExperienceYears is not null) master.ExperienceYears = m.ExperienceYears;
      if (m.Skills is not null) master.Skills = m.Skills;
      master.UpdatedAtUtc = DateTime.UtcNow;
    }

    await _db.SaveChangesAsync(ct);

    var dto = await BuildProfileQuery(_db, currentUserId).FirstOrDefaultAsync(ct);
    if (dto is null) return NotFound(); // маловероятно

    if (dto.Master is not null && dto.Master.Skills is null)
      dto = dto with { Master = dto.Master with { Skills = [] } };

    return Ok(dto);
  }

  /// <summary>
  /// Завантажити або оновити аватарку користувача.
  /// </summary>
  /// <remarks>
  /// Завантажує аватарку у <c>wwwroot/avatars</c> і повертає шлях до неї.
  ///
  /// <para>
  /// Після завантаження аватарка буде доступна за URL:
  /// <c>/avatars/{userId}.jpg</c>.
  /// </para>
  ///
  /// <para>
  /// Наприклад:
  /// <c>https://betendly-api.azurewebsites.net/avatars/ddd41e45.jpg</c>
  /// </para>
  ///
  /// <para>
  /// Підтримується тільки <c>image/*</c>. Розмір до 2 МБ.
  /// </para>
  /// </remarks>
  /// <param name="file">Файл зображення.</param>
  /// <response code="200">Аватарку успішно завантажено. Повертає шлях до файлу.</response>
  /// <response code="400">Некоректний файл або формат.</response>
  /// <response code="401">Неавторизовано.</response>
  [HttpPost("me/avatar")]
  [Authorize]
  [Produces("application/json")]
  [Consumes("multipart/form-data")]
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
