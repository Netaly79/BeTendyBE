using BeTendlyBE.Contracts;
using BeTendlyBE.Data;
using BeTendlyBE.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BeTendlyBE.Controllers;

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
  /// <param name="serviceId">Ідентифікатор послуги (query: <c>service_id</c>).</param>
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
    [FromQuery(Name = "service_id")] Guid? serviceId,
    [FromQuery] DateTimeOffset? date,
    CancellationToken ct)
  {
    if (!masterId.HasValue || masterId.Value == Guid.Empty)
      return BadRequest(new { message = "Parameter 'master_id' is required." });

    if (!serviceId.HasValue || serviceId.Value == Guid.Empty)
      return BadRequest(new { message = "Parameter 'service_id' is required." });

    if (date is null)
      return BadRequest(new { message = "Parameter 'date' is required." });

    var serviceInfo = await db.Services
        .AsNoTracking()
        .Where(s => s.Id == serviceId.Value)
        .Select(s => new { s.DurationMinutes })
        .SingleOrDefaultAsync(ct);

    if (serviceInfo is null)
      return BadRequest(new { message = "Service not found for given 'service_id'." });

    if (serviceInfo.DurationMinutes <= 0)
      return BadRequest(new { message = "Service duration must be greater than zero." });

    var serviceDuration = TimeSpan.FromMinutes(serviceInfo.DurationMinutes);

    var kyiv = TimeZoneInfo.FindSystemTimeZoneById("Europe/Kyiv");
    var utcInstant = date.Value.UtcDateTime;
    var localKyiv = TimeZoneInfo.ConvertTimeFromUtc(utcInstant, kyiv);

    var day = DateOnly.FromDateTime(localKyiv);

    var localStart = day.ToDateTime(new TimeOnly(9, 0), DateTimeKind.Unspecified);
    var localEnd = day.ToDateTime(new TimeOnly(19, 0), DateTimeKind.Unspecified);

    var dayStartUtc = TimeZoneInfo.ConvertTimeToUtc(localStart, kyiv);
    var dayEndUtc = TimeZoneInfo.ConvertTimeToUtc(localEnd, kyiv);

    var nowUtc = DateTime.UtcNow;

    var busyBookings = await db.Bookings
        .AsNoTracking()
        .Where(b => b.MasterId == masterId.Value
                    && (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed)
                    && (b.HoldExpiresUtc == null || b.HoldExpiresUtc > nowUtc)
                    && !(b.EndUtc <= dayStartUtc || b.StartUtc >= dayEndUtc))
        .Select(b => new { b.StartUtc, b.EndUtc })
        .ToListAsync(ct);

    var freeSlots = new List<SlotResponse>();
    var current = dayStartUtc;

    while (current < dayEndUtc)
    {
      var slotStart = current;
      var slotEnd = slotStart + serviceDuration;

      if (slotEnd > dayEndUtc)
        break;

      var overlaps = busyBookings.Any(b =>
          !(slotEnd <= b.StartUtc || slotStart >= b.EndUtc));

      if (!overlaps)
      {
        var isPast = slotStart <= nowUtc;
        freeSlots.Add(new SlotResponse(slotStart, slotEnd, isPast));
      }

      current = current.AddMinutes(30);
    }

    return Ok(freeSlots);
  }
}
