namespace BeTendlyBE.Contracts;

public record SlotResponse(
    DateTime StartUtc,
    DateTime EndUtc,
    bool IsPast
);