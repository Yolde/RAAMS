using FluentValidation;

namespace RAAMS.Application.Actions.Validators;

public class UpdateActionValidator : AbstractValidator<UpdateActionDto>
{
    public UpdateActionValidator()
    {
        RuleFor(x => x.ActionDescription).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.ActionRequired).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.ActionPartyId).NotEmpty().When(x => x.ActionPartyId.HasValue);
        RuleFor(x => x.AuditeeId).NotEmpty().When(x => x.AuditeeId.HasValue);
        RuleFor(x => x.TargetDate).NotEmpty().When(x => x.TargetDate.HasValue);
        RuleFor(x => x.ActionPartyId).NotEqual(x => x.AuditeeId)
            .When(x => x.ActionPartyId.HasValue && x.AuditeeId.HasValue);
    }
}
