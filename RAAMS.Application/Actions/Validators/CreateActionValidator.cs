using FluentValidation;

namespace RAAMS.Application.Actions.Validators;

public class CreateActionValidator : AbstractValidator<CreateActionDto>
{
    public CreateActionValidator()
    {
        RuleFor(x => x.FindingId).NotEmpty();
        RuleFor(x => x.ReferenceNumber).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Reportable).NotEmpty().Must(value => value is "CEO" or "NonCEO");
        RuleFor(x => x.ActionDescription).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.ActionRequired).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.ActionPartyId).NotEmpty().NotEqual(x => x.AuditeeId);
        RuleFor(x => x.AuditeeId).NotEmpty();
        RuleFor(x => x.TargetDate).Must(date => date > DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("TargetDate must be a future UTC date.");
    }
}
