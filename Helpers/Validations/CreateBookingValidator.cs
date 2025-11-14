using FluentValidation;

public class CreateBookingRequestValidator : AbstractValidator<CreateBookingRequest>
{
  private const int SlotStepMinutes = 30;

  public CreateBookingRequestValidator()
  {
    RuleFor(x => x.MasterId).NotEmpty();
    RuleFor(x => x.ClientId).NotEmpty();
    RuleFor(x => x.ServiceId).NotEmpty();
    RuleFor(x => x.StartUtc)
        .NotEmpty()
        .Must(dt => dt.ToUniversalTime() > DateTimeOffset.UtcNow.AddMinutes(-1))
        .WithMessage("StartUtc must be in the future.");
    RuleFor(x => x.IdempotencyKey)
        .NotEmpty()
        .MaximumLength(120);
  }
  
  private static bool BeAlignedToSlot(DateTimeOffset startUtc)
    {
        var minutes = startUtc.Minute;
        var seconds = startUtc.Second;
        var milliseconds = startUtc.Millisecond;

        return (minutes % SlotStepMinutes == 0) && seconds == 0 && milliseconds == 0;
    }
}
