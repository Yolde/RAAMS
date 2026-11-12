using FluentValidation;

namespace RAAMS.Application.Assessments.Validators;

public class UpdateAssessmentValidator : AbstractValidator<UpdateAssessmentDto>
{
    public UpdateAssessmentValidator()
    {
        Include(new CreateAssessmentValidator());
        RuleFor(x => x.ReferenceNumber).NotEmpty().MaximumLength(50);
    }
}
