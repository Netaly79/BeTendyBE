using BeTendyBE.Domain;

public record CreateBookingRequest(
    Guid MasterId,
    Guid ClientId,
    Guid ServiceId,
    DateTimeOffset StartUtc,
    string IdempotencyKey
);

public record BookingResponse(
    Guid Id,
    Guid MasterId,
    Guid ClientId,
    Guid ServiceId,
    BookingStatus Status,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? HoldExpiresUtc
);

public record CancelBookingRequest(Guid BookingId);