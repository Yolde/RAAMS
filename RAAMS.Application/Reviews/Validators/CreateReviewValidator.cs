using FluentValidation;

namespace RAAMS.Application.Reviews.Validators;

public class CreateReviewValidator : AbstractValidator<CreateReviewDto>
{
    public CreateReviewValidator()
    {
        RuleFor(x => x.ActionId).NotEmpty();
        RuleFor(x => x.Outcome).NotEmpty().Must(value => value is "Effective" or "Ineffective")
            .WithMessage("Outcome must be Effective or Ineffective.");
        RuleFor(x => x.ReasonCode).NotEmpty().When(x => x.Outcome == "Ineffective");
        RuleFor(x => x.ReasonCode)
            .Must(value => value is "OverdueCompletion" or "IncompleteAction" or "IrrelevantAction" or
                "MissingEvidence" or "UnauthorizedChange" or "Other")
            .When(x => x.ReasonCode is not null)
            .WithMessage("ReasonCode is not valid.");
        RuleFor(x => x.Comments).MaximumLength(2000);
    }
}
