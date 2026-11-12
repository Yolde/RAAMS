using FluentValidation;

namespace RAAMS.Application.Actions.Validators;

public class SubmitActionValidator : AbstractValidator<SubmitActionDto>
{
    public SubmitActionValidator()
    {
        RuleFor(x => x.ActionTaken).NotEmpty().MaximumLength(4000);
    }
}
