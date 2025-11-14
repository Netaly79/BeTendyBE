

namespace BeTendyBE.Domain;

public enum BookingStatus { Pending = 0, Confirmed = 1, Cancelled = 2 }

public class Booking
{
    public Guid Id { get; set; }
    public Guid MasterId { get; set; }
    public Guid ClientId { get; set; }
    public Guid ServiceId { get; set; }
    public BookingStatus Status { get; set; }
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? HoldExpiresUtc { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;

    public Master Master { get; set; } = default!;
    public User? Client { get; set; }
}
