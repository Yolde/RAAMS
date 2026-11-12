using FluentValidation;

namespace RAAMS.Application.Actions.Validators;

public class UpdateActionValidator : AbstractValidator<UpdateActionDto>
{
    public UpdateActionValidator()
    {
        RuleFor(x => x.ActionDescription).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.ActionRequired).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.ActionPartyId).NotEmpty().NotEqual(x => x.AuditeeId);
        RuleFor(x => x.AuditeeId).NotEmpty();
        RuleFor(x => x.TargetDate).NotEmpty();
    }
}
