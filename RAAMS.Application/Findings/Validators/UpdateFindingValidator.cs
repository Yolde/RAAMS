using FluentValidation;

namespace RAAMS.Application.Findings.Validators;

public class UpdateFindingValidator : AbstractValidator<UpdateFindingDto>
{
    public UpdateFindingValidator()
    {
        RuleFor(x => x.Description).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.RootCause).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.Recommendation).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.Severity).NotEmpty().Must(CreateFindingValidator.ValidSeverity)
            .WithMessage("Severity must be High, Medium, or Low.");
    }
}
