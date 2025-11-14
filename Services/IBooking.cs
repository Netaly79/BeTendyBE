namespace BeTendlyBE.Services;
public interface IBookingService
{
  Task<BookingResponse> CreateAsync(CreateBookingRequest req, CancellationToken ct);
  Task<bool> CancelAsync(Guid bookingId, CancellationToken ct);
}
