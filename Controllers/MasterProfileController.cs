using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;

using BeTendyBE.Contracts;
using BeTendyBE.Data;
using BeTendyBE.Domain;
using BeTendyBE.Infrastructure.Identity;

namespace BeTendyBE.Controllers;

/// <summary>
///Операції з профілем майстра (створення/оновлення та отримання).
/// Потребує ролі <c>Master</c>.
/// </summary>
[ApiController]
[Route("api/master/profile")]
[Authorize]
[Produces("application/json")]
public sealed class MasterProfileController : ControllerBase
{
    private readonly AppDbContext _db;
    public MasterProfileController(AppDbContext db) => _db = db;

    /// <summary>Отримати профіль поточного майстра.</summary>
    /// <remarks>
    /// Повертає пусті поля, якщо профіль ще не створено.
    /// </remarks>
    /// <response code="200">Профіль знайдено або повернено порожній шаблон.</response>
    /// <response code="401">Неавторизовано (відсутній або прострочений токен).</response>
    /// <response code="403">Доступ заборонено (роль не Master).</response>
    [HttpGet]
    [SwaggerOperation(Summary = "Отримати профіль майстра", Description = "Потребує ролі Master.")]
    [ProducesResponseType(typeof(MasterProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<MasterProfileResponse>> Get(CancellationToken ct)
    {
        var userId = User.GetUserId();

        var user = await _db.Users.Where(u => u.Id == userId).FirstOrDefaultAsync(ct);
        if (user is null || !user.IsMaster) return Forbid();

        var mp = await _db.Masters.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId, ct);
        var resp = mp is null
            ? new MasterProfileResponse(null, null, null, null)
            : new MasterProfileResponse(mp.About, mp.Skills, mp.ExperienceYears, mp.Address);

        return Ok(resp);
    }

    /// <summary>Створити або оновити профіль майстра (upsert).</summary>
    /// <response code="200">Профіль збережено і повернуто актуальні дані.</response>
    /// <response code="400">Помилка валідації тіла запиту.</response>
    /// <response code="401">Неавторизовано.</response>
    /// <response code="403">Роль не Master.</response>
    /// <response code="404">Користувача не знайдено.</response>
    [HttpPut]
    [SwaggerOperation(Summary = "Оновити профіль майстра", Description = "Upsert. Потребує ролі Master.")]
    [ProducesResponseType(typeof(MasterProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MasterProfileResponse>> Upsert([FromBody] UpsertMasterProfileRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return NotFound();
        if (!user.IsMaster) return Forbid();

        var about = string.IsNullOrWhiteSpace(req.About) ? null : req.About.Trim();
        var skills = string.IsNullOrWhiteSpace(req.Skills) ? null : req.Skills.Trim();

        var mp = await _db.Masters.FirstOrDefaultAsync(x => x.UserId == userId, ct);
        if (mp is null)
        {
            mp = new Master { UserId = userId };
            _db.Masters.Add(mp);
        }

        mp.About = about;
        mp.Skills = skills;
        mp.ExperienceYears = req.ExperienceYears;
        mp.Address = string.IsNullOrWhiteSpace(req.Address) ? null : req.Address.Trim();

        await _db.SaveChangesAsync(ct);

        return Ok(new MasterProfileResponse(mp.About, mp.Skills, mp.ExperienceYears, mp.Address));
    }
}
