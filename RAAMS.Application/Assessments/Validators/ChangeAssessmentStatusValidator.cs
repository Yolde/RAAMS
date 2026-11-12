using FluentValidation;

namespace RAAMS.Application.Assessments.Validators;

public class ChangeAssessmentStatusValidator : AbstractValidator<ChangeAssessmentStatusDto>
{
    public ChangeAssessmentStatusValidator()
    {
        RuleFor(x => x.TargetStatus).NotEmpty()
            .Must(status => status is "Started" or "DraftReport" or "Completed" or "Deferred" or "Cancelled")
            .WithMessage("TargetStatus is not an allowed assessment status.");
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}
