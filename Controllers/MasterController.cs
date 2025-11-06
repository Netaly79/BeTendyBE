using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;

using BeTendyBE.Data;
using BeTendyBE.Domain;
using BeTendyBE.Infrastructure.Identity;

namespace BeTendyBE.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class MasterController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IMasterService _masterSvc;

    public MasterController(AppDbContext db, IMasterService masterSvc)
    {
        _db = db;
        _masterSvc = masterSvc;
    }


    /// <summary>
    /// Підвищити роль поточного користувача до Master (ідемпотентно).
    /// </summary>
    /// <remarks>
    /// Якщо користувач уже має роль Master — повертається <c>204 No Content</c>.
    /// Ендпоінт не приймає тіла запиту.
    /// </remarks>
    /// <response code="204">Роль успішно встановлено (або вже була Master).</response>
    /// <response code="401">Неавторизовано: відсутній або недійсний токен.</response>
    /// <response code="404">Користувача не знайдено.</response>
    [HttpPost("upgrade")]
    [Authorize]
    [SwaggerOperation(
        Summary = "Стати Master (для поточного користувача)",
        Description = "Підвищує роль авторизованого користувача до Master. Ідемпотентно.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Upgrade(CancellationToken ct)
    {
        var userId = ClaimsPrincipalExt.GetUserId(User);
        await _masterSvc.EnsureMasterAsync(userId, ct);
        return NoContent();
    }

    /// <summary>
    /// Адмін: затвердити користувача як Master за його ідентифікатором (ідемпотентно).
    /// </summary>
    /// <remarks>
    /// Доступ лише для ролі <c>Admin</c>. Якщо користувач уже Master — повертається <c>204 No Content</c>.
    /// </remarks>
    /// <param name="userId">Ідентифікатор користувача.</param>
    /// <response code="204">Користувача затверджено як Master (або вже був Master).</response>
    /// <response code="401">Неавторизовано.</response>
    /// <response code="403">Заборонено: потрібна роль Admin.</response>
    /// <response code="404">Користувача не знайдено.</response>
    [HttpPost("{userId:guid}/approve")]
    [Authorize(Roles = "Admin")]
    [SwaggerOperation(
        Summary = "Адмін: затвердити Master за userId",
        Description = "Встановлює роль Master для вказаного користувача. Ідемпотентно.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Approve(Guid userId, CancellationToken ct)
    {
        var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId, ct);
        if (u is null) return NotFound();

        if (!u.IsMaster)
        {
            u.IsMaster = true;

            if (!await _db.Masters.AnyAsync(m => m.UserId == u.Id, ct))
                _db.Masters.Add(new Master { UserId = u.Id });

            await _db.SaveChangesAsync(ct);
        }

        return NoContent();
    }
}
