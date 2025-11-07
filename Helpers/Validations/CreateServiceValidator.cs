using BeTendyBE.DTO;
using FluentValidation;

namespace BeTendyBE.Helpers.Validation;

public sealed class CreateServiceRequestValidator : AbstractValidator<CreateServiceRequest>
{
    public CreateServiceRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Price)
            .GreaterThan(0).WithMessage("Price must be > 0")
            .LessThanOrEqualTo(10000);

        RuleFor(x => x.DurationMinutes)
            .InclusiveBetween(30, 300);
    }
}