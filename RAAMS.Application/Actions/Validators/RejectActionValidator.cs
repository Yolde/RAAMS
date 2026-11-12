using FluentValidation;

namespace RAAMS.Application.Actions.Validators;

public class RejectActionValidator : AbstractValidator<RejectActionDto>
{
    public RejectActionValidator()
    {
        RuleFor(x => x.Comments).MaximumLength(500);
    }
}
