using FluentValidation;
using BeTendyBE.Contracts;

namespace BeTendyBE.Helpers.Validation;
public sealed class UpdateClientProfileValidator : AbstractValidator<UpdateClientProfileRequest>
{
    public UpdateClientProfileValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("FirstName is required")
            .MaximumLength(100);

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("LastName is required")
            .MaximumLength(100);

        RuleFor(x => x.Phone)
            .NotNull().WithMessage("Phone is required")
            .MaximumLength(32)
            // при желании — мягкая проверка телефона:
            .Matches(@"^[\d\s+\-()]{5,32}$")
                .WithMessage("Phone has invalid format");
    }
}
