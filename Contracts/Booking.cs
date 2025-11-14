namespace BeTendyBE.Contracts;
public record SlotResponse(
    DateTime StartUtc,
    DateTime EndUtc,
    bool IsPast
);