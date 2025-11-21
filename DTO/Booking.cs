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
    string MasterName,
    string ClientName,
    string ServiceName,
    BookingStatus Status,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? HoldExpiresUtc
);

public record CancelBookingRequest(Guid BookingId);