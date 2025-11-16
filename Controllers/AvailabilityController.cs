using BeTendyBE.Contracts;
using BeTendyBE.Data;
using BeTendyBE.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BeTendyBE.Controllers;

[ApiController]
[Route("availability")]
public class AvailabilityController : ControllerBase
{
  /// <summary>
  /// Вільні часові слоти майстра на день (09:00–17:00, крок 30 хв).
  /// </summary>
  /// <remarks>
  /// Повертає тільки вільні слоти для вказаного майстра на конкретну дату.
  ///
  /// - Інтервал робочого дня: 09:00–17:00 (UTC).
  /// - Крок: 30 хвилин.
  /// - Слот вважається зайнятим, якщо він перетинається з бронюванням
  ///   у статусі <c>Pending</c> або <c>Confirmed</c>.
  /// - Поле <c>isPast</c> показує, чи слот уже в минулому відносно поточного часу (UTC).
  /// </remarks>
  /// <param name="db">Контекст бази даних.</param>
  /// <param name="masterId">Ідентифікатор майстра (query: <c>master_id</c>).</param>
  /// <param name="date">Дата у форматі <c>YYYY-MM-DD</c> (UTC).</param>
  /// <param name="ct"></param>
  /// <response code="200">Повернуто список вільних слотів.</response>
  /// <response code="400">Некоректні або відсутні параметри.</response>
  [HttpGet("slots")]
  [ProducesResponseType(typeof(IEnumerable<SlotResponse>), StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  public async Task<ActionResult<IEnumerable<SlotResponse>>> GetFreeSlotsForDay(
      [FromServices] AppDbContext db,
      [FromQuery(Name = "master_id")] Guid? masterId,
      [FromQuery] DateOnly? date,
      CancellationToken ct)
  {
    if (!masterId.HasValue || masterId.Value == Guid.Empty)
      return BadRequest(new { message = "Parameter 'master_id' is required." });

    if (date is null)
      return BadRequest(new { message = "Parameter 'date' is required (format: YYYY-MM-DD)." });

    var day = date.Value;

    var kyiv = TimeZoneInfo.FindSystemTimeZoneById("Europe/Kyiv");

    var localStart = day.ToDateTime(new TimeOnly(9, 0), DateTimeKind.Unspecified);
    var localEnd = day.ToDateTime(new TimeOnly(17, 0), DateTimeKind.Unspecified);

    var dayStartUtc = TimeZoneInfo.ConvertTimeToUtc(localStart, kyiv);
    var dayEndUtc = TimeZoneInfo.ConvertTimeToUtc(localEnd, kyiv);

    var nowUtc = DateTime.UtcNow;

    var busyBookings = await db.Bookings
        .AsNoTracking()
        .Where(b => b.MasterId == masterId.Value
                    && (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed)
                    && !(b.EndUtc <= dayStartUtc || b.StartUtc >= dayEndUtc))
        .Select(b => new { b.StartUtc, b.EndUtc })
        .ToListAsync(ct);

    var freeSlots = new List<SlotResponse>();
    var current = dayStartUtc;

    while (current < dayEndUtc)
    {
      var slotStart = current;
      var slotEnd = current.AddMinutes(30);

      var overlaps = busyBookings.Any(b =>
          !(slotEnd <= b.StartUtc || slotStart >= b.EndUtc));

      if (!overlaps)
      {
        var isPast = slotEnd <= nowUtc;
        freeSlots.Add(new SlotResponse(slotStart, slotEnd, isPast));
      }

      current = slotEnd;
    }

    return Ok(freeSlots);
  }
}
