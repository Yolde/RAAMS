using FluentValidation;

namespace RAAMS.Application.Findings.Validators;

public class CreateFindingValidator : AbstractValidator<CreateFindingDto>
{
    public CreateFindingValidator()
    {
        RuleFor(x => x.AssessmentId).NotEmpty();
        RuleFor(x => x.ReferenceNumber).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.RootCause).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.Recommendation).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.Severity).NotEmpty().Must(ValidSeverity).WithMessage("Severity must be High, Medium, or Low.");
    }

    internal static bool ValidSeverity(string? value) => value is "High" or "Medium" or "Low";
}
