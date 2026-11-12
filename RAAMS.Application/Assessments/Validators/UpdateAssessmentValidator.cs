using FluentValidation;

namespace RAAMS.Application.Assessments.Validators;

public class UpdateAssessmentValidator : AbstractValidator<UpdateAssessmentDto>
{
    public UpdateAssessmentValidator()
    {
        Include(new CreateAssessmentValidator());
    }
}
