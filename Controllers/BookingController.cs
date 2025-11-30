using BeTendlyBE.Services;
using BeTendlyBE.Data;
using BeTendlyBE.Domain;
using BeTendlyBE.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BeTendlyBE.Controllers;

[ApiController]
[Route("bookings")]
public class BookingController : ControllerBase
{
    private readonly IBookingService _svc;

    public BookingController(IBookingService svc) => _svc = svc;

    /// <summary>
    /// Створення нового бронювання (ідемпотентно).
    /// </summary>
    /// <remarks>
    /// Створює нове бронювання для зазначених майстра, клієнта та послуги.  
    /// Якщо слот уже зайнятий або бронювання з тим самим <c>idempotencyKey</c> вже існує — повертається помилка або попередній результат.
    ///
    /// <br/><br/>
    /// **Правила:**
    /// - Бронювання можливе лише на вільний часовий слот.
    /// - Час початку буде автоматично нормалізовано до найближчого інтервалу (наприклад, 30 хв).
    /// - Тимчасове бронювання зберігається до закінчення <c>HoldExpiresUtc</c>.
    ///
    /// <br/><br/>
    /// </remarks>
    /// <response code="201">Бронювання успішно створено.</response>
    /// <response code="400">Помилка валідації або перетин із наявним бронюванням.</response>
    /// <response code="409">Бронювання з таким <c>idempotencyKey</c> уже існує.</response>
    /// <response code="500">Внутрішня помилка сервера.</response>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(BookingResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<BookingResponse>> Create([FromBody] CreateBookingRequest req, CancellationToken ct)
    {
        var result = await _svc.CreateAsync(req, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Отримати бронювання за ідентифікатором.
    /// </summary>
    /// <remarks>
    /// Повертає повну інформацію про конкретне бронювання за його унікальним <c>id</c>.
    ///
    /// <br/><br/>
    /// </remarks>
    /// <param name="id">Унікальний ідентифікатор бронювання.</param>
    /// <response code="200">Бронювання знайдено та успішно повернуто.</response>
    /// <response code="404">Бронювання з указаним <c>id</c> не знайдено.</response>
    /// <response code="500">Внутрішня помилка сервера.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(BookingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]

    public async Task<ActionResult<BookingResponse>> GetById([FromServices] AppDbContext db, Guid id, CancellationToken ct)
    {
        var entity = await db.Bookings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return NotFound();
        return Ok(new BookingResponse(entity.Id, entity.MasterId, entity.ClientId, entity.ServiceId, entity.Master.User.FirstName + " " + entity.Master.User.LastName, entity.Client.FirstName + " " + entity.Client.LastName, entity.Service.Name,
            entity.Status, entity.StartUtc, entity.EndUtc, entity.CreatedAtUtc, entity.HoldExpiresUtc));
    }

    /// <summary>
    /// Скасувати бронювання.
    /// </summary>
    /// <remarks>
    /// Доступно для користувача, який створив бронювання (Client),
    /// або для майстра, якому належить бронювання (Master).
    ///
    /// <para>Можна скасувати бронювання зі статусами <c>Pending</c>.</para>
    /// <para>Повторне скасування повертає <c>400 Bad Request</c>.</para>
    /// </remarks>
    /// <param name="db"></param>
    /// <param name="id">Ідентифікатор бронювання.</param>
    /// <response code="204">Бронювання успішно скасовано.</response>
    /// <response code="400">Бронювання вже скасоване або має недопустимий статус.</response>
    /// <response code="401">Неавторизовано.</response>
    /// <response code="403">Користувач не має доступу до цього бронювання.</response>
    /// <response code="404">Бронювання не знайдено.</response>
    [HttpPut("{id:guid}/reject")]
    [Authorize]
    public async Task<IActionResult> CancelBooking([FromServices] AppDbContext db, Guid id)
    {
        var userId = User.GetUserId();

        var booking = await db.Bookings
        .Include(b => b.Master)
        .FirstOrDefaultAsync(b => b.Id == id);

        if (booking == null)
            return NotFound("Booking not found");

        if (booking.Status == BookingStatus.Cancelled)
            return BadRequest("Booking already cancelled.");


        bool isClient = booking.ClientId == userId;
        bool isMaster = booking.Master != null && booking.Master.UserId == userId;

        if (!isClient && !isMaster)
            return Forbid(isClient.ToString(), isMaster.ToString());

        booking.Status = BookingStatus.Cancelled;
        booking.HoldExpiresUtc = null;

        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Підтвердити бронювання.
    /// </summary>
    /// <remarks>
    /// Доступно для майстра, якому належить бронювання (Master).
    ///
    /// <para>Можна підтвердити бронювання зі статусами <c>Pending</c>.</para>
    /// <para>Повторне підтвердження повертає <c>400 Bad Request</c>.</para>
    /// </remarks>
    /// <param name="db"></param>
    /// <param name="id">Ідентифікатор бронювання.</param>
    /// <response code="204">Бронювання успішно підтверджено.</response>
    /// <response code="400">Бронювання вже підтверджене або має недопустимий статус.</response>
    /// <response code="401">Неавторизовано.</response>
    /// <response code="403">Користувач не має доступу до цього бронювання.</response>
    /// <response code="404">Бронювання не знайдено.</response>
    [HttpPut("{id:guid}/confirm")]
    [Authorize(Roles = "Master")]
    public async Task<IActionResult> ConfirmBooking([FromServices] AppDbContext db, Guid id)
    {
        var userId = User.GetUserId();

        var booking = await db.Bookings
            .FirstOrDefaultAsync(b => b.Id == id);

        if (booking == null)
            return NotFound("Booking not found");

        if (booking.Master != null && booking.Master.UserId == userId)
            return Forbid();
        var masterId = booking.Master?.UserId;

        if (booking.Status != BookingStatus.Pending)
            return BadRequest("Only pending bookings can be confirmed.");

        var hasOverlap = await db.Bookings.AnyAsync(b =>
            b.MasterId == masterId &&
            b.Status == BookingStatus.Confirmed &&
            b.Id != booking.Id &&
            !(booking.EndUtc <= b.StartUtc || booking.StartUtc >= b.EndUtc));

        if (hasOverlap)
            return Conflict("Time slot overlaps another confirmed booking.");

        booking.Status = BookingStatus.Confirmed;
        booking.HoldExpiresUtc = null;

        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Список бронювань для майстра або клієнта.
    /// </summary>
    /// <remarks>
    /// Потрібно вказати рівно один параметр:
    /// - <c>master_id</c> — бронювання майстра;
    /// - <c>client_id</c> — бронювання клієнта.
    ///
    /// Якщо <c>status</c> не вказано — повертаються бронювання з усіма статусами.
    /// Якщо вказано — фільтрація тільки за цим статусом.
    ///
    /// Діапазон часу:
    /// - Якщо <c>from_utc</c> і <c>to_utc</c> **не вказані** — використовується тиждень від поточного часу (UTC).
    /// - Якщо вказані обидва — використовується заданий діапазон.
    /// - Якщо вказано лише один — <c>400 Bad Request</c>.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<BookingResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<BookingResponse>>> GetList(
        [FromServices] AppDbContext db,
        [FromQuery(Name = "masterId")] Guid? masterId,
        [FromQuery(Name = "clientId")] Guid? clientId,
        [FromQuery(Name = "fromUtc")] DateTime? fromUtc,
        [FromQuery(Name = "toUtc")] DateTime? toUtc,
        [FromQuery] BookingStatus? status,
        CancellationToken ct)
    {
        var hasMaster = masterId.HasValue && masterId.Value != Guid.Empty;
        var hasClient = clientId.HasValue && clientId.Value != Guid.Empty;

        if (!hasMaster && !hasClient)
            return BadRequest(new { message = "Specify exactly one of 'master_id' or 'client_id'." });

        if (hasMaster && hasClient)
            return BadRequest(new { message = "Only one of 'master_id' or 'client_id' can be specified at the same time." });

        var nowUtc = DateTime.UtcNow;

        var hasFrom = fromUtc.HasValue;
        var hasTo = toUtc.HasValue;

        if (!hasFrom && !hasTo)
        {
            fromUtc = nowUtc;
            toUtc = nowUtc.AddDays(7);
        }
        else if (hasFrom != hasTo)
        {
            return BadRequest(new { message = "Both 'from_utc' and 'to_utc' must be specified together." });
        }

        if (fromUtc >= toUtc)
        {
            return BadRequest(new { message = "'from_utc' must be earlier than 'to_utc'." });
        }

        var query = db.Bookings
            .AsNoTracking()
            .Where(b => b.StartUtc >= fromUtc && b.StartUtc < toUtc);

        if (hasMaster)
            query = query.Where(b => b.MasterId == masterId);
        else
            query = query.Where(b => b.ClientId == clientId);

        if (status.HasValue)
        {
            query = query.Where(b => b.Status == status.Value);
        }

        var items = await query
            .OrderBy(b => b.StartUtc)
            .Select(b => new BookingResponse(
                b.Id,
                b.MasterId,
                b.ClientId,
                b.ServiceId,
                b.Master.User.FirstName + " " + b.Master.User.LastName,
                b.Client.FirstName + " " + b.Client.LastName,
                b.Service.Name,
                b.Status,
                b.StartUtc,
                b.EndUtc,
                b.CreatedAtUtc,
                b.HoldExpiresUtc))
            .ToListAsync(ct);

        return Ok(items);
    }
}
