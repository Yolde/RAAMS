using FluentValidation;

namespace RAAMS.Application.Assessments.Validators;

public class UploadEscalationEmailValidator : AbstractValidator<UploadEscalationEmailDto>
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "message/rfc822",
        "application/vnd.ms-outlook",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/msword"
    };

    public UploadEscalationEmailValidator()
    {
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(255);
        RuleFor(x => x.ContentType).NotEmpty().MaximumLength(100)
            .Must(type => type is not null && AllowedContentTypes.Contains(type))
            .WithMessage("Content type must be PDF, EML, MSG, DOCX, or DOC.");
        RuleFor(x => x.SizeBytes).InclusiveBetween(1, 20L * 1024 * 1024);
    }
}
