using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;

using BeTendyBE.Data;
using BeTendyBE.Domain;
using BeTendyBE.Infrastructure.Identity;
using Swashbuckle.AspNetCore.Filters;
using BeTendyBE.DTO;
using BeTendyBE.Contracts;

namespace BeTendyBE.Controllers;

[ApiController]
[Route("masters")]
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
    /// Next release feature:Підвищити роль поточного користувача до Master (ідемпотентно).
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
    /// Next release feature: Адмін: затвердити користувача як Master за його ідентифікатором (ідемпотентно).
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


    /// <summary>Отримати всіх майстрів з фільтрами та пагінацією.</summary>
    /// <remarks>
    /// Параметри:
    /// - <c>?skill=haircut</c> — фільтр по навику (точное совпадение).
    /// - <c>?address=kyiv</c> — подстрочный поиск по адресу (ILike).
    /// - <c>?page=1&amp;pageSize=20</c> — пагінація.
    /// </remarks>
    /// <response code="200">Список майстрів з метаданими пагінації.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<MasterResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllMasters([FromQuery] MastersQuery query)
    {
        var pageSize = Math.Clamp(query.PageSize <= 0 ? 20 : query.PageSize, 1, 100);
        var page = query.Page <= 0 ? 1 : query.Page;

        IQueryable<Master> mastersQ = _db.Masters
            .AsNoTracking()
            .Include(m => m.User);

        if (!string.IsNullOrEmpty(query.Skill))
        {
            mastersQ = mastersQ.Where(m => m.Skills != null && m.Skills.Contains(query.Skill));
        }

        if (!string.IsNullOrEmpty(query.Address))
        {
            mastersQ = mastersQ.Where(m => m.Address != null &&
                                           EF.Functions.ILike(m.Address, $"%{query.Address}%"));
        }

        var total = await mastersQ.CountAsync();

        var pageItemsRaw = await mastersQ
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new
            {
                m.Id,
                m.UserId,
                m.User.FirstName,
                m.User.LastName,
                m.About,
                m.Skills,
                m.Address,
                m.City,
                m.User.AvatarUrl
            })
            .ToListAsync();

        var items = pageItemsRaw.Select(m => new MasterResponse
        {
            Id = m.Id,
            UserId = m.UserId,
            FullName = string.Join(' ', new[]
            {
                m.FirstName ?? string.Empty,
                m.LastName  ?? string.Empty
            }.Where(s => !string.IsNullOrWhiteSpace(s))),

            About = m.About,
            Skills = (m.Skills ?? new List<string>()).Where(s => !string.IsNullOrWhiteSpace(s)).ToList(),
            Address = string.IsNullOrWhiteSpace(m.Address) ? null : m.Address,
            City = string.IsNullOrWhiteSpace(m.City) ? null : m.City,
            AvatarUrl = m.AvatarUrl
        })
        .ToList();

        var response = new PagedResponse<MasterResponse>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            Total = total
        };

        return Ok(response);
    }


    /// <summary>
    /// Отримати список сервісів для конкретного майстра за його ідентифікатором.
    /// </summary>
    /// <remarks>
    /// Повертає всі послуги, що належать обраному майстру.  
    /// Якщо майстра з таким <c>id</c> не існує — повертається <c>404 Not Found</c>.
    /// </remarks>
    /// <param name="id">Ідентифікатор майстра.</param>
    /// <response code="200">Успішно. Повертає перелік сервісів майстра.</response>
    /// <response code="404">Майстра не знайдено.</response>
    [HttpGet("{id:guid}/services")]
    [SwaggerOperation(
        Summary = "Отримати сервіси майстра",
        Description = "Повертає перелік послуг, що належать майстру за його ідентифікатором.")]
    [SwaggerResponse(200, "Успішно. Повертає перелік сервісів майстра.")]
    [SwaggerResponseExample(StatusCodes.Status200OK, typeof(ServiceListExample))]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<ServiceListItemResponse>>> GetMasterServices([FromRoute] Guid id)
    {
        // 404, если мастер не существует
        var exists = await _db.Masters.AsNoTracking().AnyAsync(m => m.Id == id);
        if (!exists) return NotFound();

        var services = await _db.Services
            .AsNoTracking()
            .Where(s => s.MasterId == id)
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
            .ToListAsync();

        return Ok(services);
    }

}