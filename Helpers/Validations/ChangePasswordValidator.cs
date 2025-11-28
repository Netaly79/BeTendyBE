using FluentValidation;
using BeTendlyBE.Contracts;

namespace BeTendlyBE.Helpers.Validation;

public sealed class ChangePasswordValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("CurrentPassword is required");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("NewPassword is required")
            .MinimumLength(8).WithMessage("NewPassword must be at least 8 characters");
        //.Matches("[A-Z]").WithMessage("NewPassword must contain an uppercase letter")
        //.Matches("[a-z]").WithMessage("NewPassword must contain a lowercase letter")
        //.Matches(@"\d").WithMessage("NewPassword must contain a digit")
        //.Matches(@"[^\w\s]").WithMessage("NewPassword must contain a symbol");
    }
}
