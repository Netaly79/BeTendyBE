using FluentValidation;
using BeTendyBE.Contracts;

namespace BeTendyBE.Helpers.Validation;
public sealed class UpsertMasterProfileValidator : AbstractValidator<UpsertMasterProfileRequest>
{
    public UpsertMasterProfileValidator()
    {
        RuleFor(x => x.About).MaximumLength(500);
        RuleFor(x => x.Skills).MaximumLength(300);
        RuleFor(x => x.YearsExperience)
            .InclusiveBetween(0, 60)
            .When(x => x.YearsExperience.HasValue);
    }
}