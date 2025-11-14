using BeTendyBE.Data;
using BeTendyBE.Domain;
using Microsoft.EntityFrameworkCore;

namespace BeTendlyBE.Services;

public class BookingService : IBookingService
{
  private readonly AppDbContext _db;
  private const int SlotStepMinutes = 30;

  public BookingService(AppDbContext db) => _db = db;

  public async Task<BookingResponse> CreateAsync(CreateBookingRequest req, CancellationToken ct)
  {
    // 0) Идемпотентность: вернуть существующую бронь
    var existing = await _db.Bookings.AsNoTracking()
        .FirstOrDefaultAsync(b => b.ClientId == req.ClientId && b.IdempotencyKey == req.IdempotencyKey, ct);
    if (existing is not null)
      return Map(existing);

    // 1) Получаем сервис и проверяем целостность
    var service = await _db.Set<Service>()
        .AsNoTracking()
        .FirstOrDefaultAsync(s => s.Id == req.ServiceId, ct)
        ?? throw new InvalidOperationException("Service not found.");

    if (service.MasterId != req.MasterId)
      throw new InvalidOperationException("Service does not belong to the specified master.");

    if (service.DurationMinutes <= 0)
      throw new InvalidOperationException("Service duration must be positive.");

    // 2) Нормализуем время старта (интерпретируем Unspecified как UTC)
    const int SlotStepMinutes = 30;
    var startUtc = req.StartUtc.ToUniversalTime().UtcDateTime;

    // сброс секунд/миллисекунд
    startUtc = new DateTime(
                startUtc.Year, startUtc.Month, startUtc.Day,
                startUtc.Hour, startUtc.Minute, 0, 0, DateTimeKind.Utc);

    // выравниваем по сетке (вниз) — возьми 30 или duration услуги

    var alignedMinutes = startUtc.Minute / SlotStepMinutes * SlotStepMinutes;
    startUtc = new DateTime(
        startUtc.Year, startUtc.Month, startUtc.Day,
        startUtc.Hour, alignedMinutes, 0, DateTimeKind.Utc);

    var endUtc = startUtc.AddMinutes(service.DurationMinutes);

    // 3) Ранняя проверка пересечений (база всё равно проверит триггером)
    var overlaps = await _db.Bookings.AnyAsync(b =>
        b.MasterId == req.MasterId &&
        (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed) &&
        (b.HoldExpiresUtc == null || b.HoldExpiresUtc > DateTime.UtcNow) &&
        !(endUtc <= b.StartUtc || startUtc >= b.EndUtc), ct);

    if (overlaps)
      throw new InvalidOperationException("Slot overlaps an existing booking.");

    // 4) Создаём сущность
    var entity = new Booking
    {
      Id = Guid.NewGuid(),
      MasterId = req.MasterId,
      ClientId = req.ClientId,
      ServiceId = req.ServiceId,
      Status = BookingStatus.Pending,
      StartUtc = startUtc,
      EndUtc = endUtc,
      HoldExpiresUtc = DateTime.UtcNow.AddDays(1),
      IdempotencyKey = req.IdempotencyKey,
      CreatedAtUtc = DateTime.UtcNow
    };

    _db.Bookings.Add(entity);

    try
    {
      await _db.SaveChangesAsync(ct);
    }
    catch (DbUpdateException ex) when (IsUniqueViolation(ex))
    {
      // Повтор с тем же ключом — вернуть старую запись
      var again = await _db.Bookings.AsNoTracking().FirstAsync(b =>
          b.ClientId == req.ClientId && b.IdempotencyKey == req.IdempotencyKey, ct);
      return Map(again);
    }

    return Map(entity);
  }

  public async Task<bool> CancelAsync(Guid bookingId, CancellationToken ct)
  {
    var entity = await _db.Bookings.FirstOrDefaultAsync(x => x.Id == bookingId, ct);
    if (entity is null) return false;

    if (entity.Status == BookingStatus.Cancelled) return true;

    entity.Status = BookingStatus.Cancelled;
    await _db.SaveChangesAsync(ct);
    return true;
  }

  private static bool IsUniqueViolation(DbUpdateException ex)
      => ex.InnerException?.Message.Contains("ux_booking_client_idempotency", StringComparison.OrdinalIgnoreCase) == true;

  private static BookingResponse Map(Booking b) =>
      new(b.Id, b.MasterId, b.ClientId, b.ServiceId, b.Status, b.StartUtc, b.EndUtc, b.CreatedAtUtc, b.HoldExpiresUtc);
}
