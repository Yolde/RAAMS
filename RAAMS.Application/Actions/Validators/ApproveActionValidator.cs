using FluentValidation;

namespace RAAMS.Application.Actions.Validators;

public class ApproveActionValidator : AbstractValidator<ApproveActionDto>
{
    public ApproveActionValidator()
    {
        RuleFor(x => x.Comments).MaximumLength(500);
    }
}
