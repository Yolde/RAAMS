using FluentValidation;

namespace RAAMS.Application.Assessments.Validators;

public class CreateAssessmentValidator : AbstractValidator<CreateAssessmentDto>
{
    public CreateAssessmentValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Scope).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.Function).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Quarter).NotEmpty().Must(value => value is "Q1" or "Q2" or "Q3" or "Q4")
            .WithMessage("Quarter must be Q1, Q2, Q3, or Q4.");
        RuleFor(x => x.Year).InclusiveBetween(2000, 2100);
        RuleFor(x => x.LeadAuditorId).NotEmpty();
        RuleFor(x => x.CoordinatorId).NotEmpty();
    }
}
