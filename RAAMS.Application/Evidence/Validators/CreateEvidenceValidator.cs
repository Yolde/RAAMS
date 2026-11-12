using FluentValidation;

namespace RAAMS.Application.Evidence.Validators;

public class CreateEvidenceValidator : AbstractValidator<CreateEvidenceDto>
{
    public CreateEvidenceValidator()
    {
        RuleFor(x => x.ActionId).NotEmpty().When(x => x.ActionId.HasValue);
        RuleFor(x => x.AssessmentId).NotEmpty().When(x => x.AssessmentId.HasValue);
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(255);
        RuleFor(x => x.ContentType).NotEmpty().MaximumLength(100);
        RuleFor(x => x.SizeBytes).InclusiveBetween(1, 50L * 1024 * 1024);
    }
}
