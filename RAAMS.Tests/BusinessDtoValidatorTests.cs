using System.Reflection;
using RAAMS.Application.Actions;
using RAAMS.Application.Actions.Validators;
using RAAMS.Application.Assessments;
using RAAMS.Application.Assessments.Validators;
using RAAMS.Application.Authorization.Attributes;
using RAAMS.Application.Common;
using RAAMS.Application.Evidence;
using RAAMS.Application.Evidence.Validators;
using RAAMS.Application.Findings;
using RAAMS.Application.Findings.Validators;
using RAAMS.Application.Reviews;
using RAAMS.Application.Reviews.Validators;

namespace RAAMS.Tests;

public class BusinessDtoValidatorTests
{
    [Fact]
    public void AssessmentValidationCoversBoundsAndStatus()
    {
        var dto = new CreateAssessmentDto
        {
            Name = "Assessment", Scope = "Scope", Function = "F", Quarter = "Q1", Year = 2000,
            LeadAuditorId = Guid.NewGuid(), CoordinatorId = Guid.NewGuid(), LineOfDefence = "LOD1"
        };
        var validator = new CreateAssessmentValidator();
        Assert.True(validator.Validate(dto).IsValid);
        dto.LineOfDefence = "3";
        Assert.False(validator.Validate(dto).IsValid);
        dto.LineOfDefence = "LOD1";
        dto.Opinion = "invalid";
        Assert.False(validator.Validate(dto).IsValid);
        dto.Opinion = null;
        dto.LineOfDefence = null!;
        Assert.False(validator.Validate(dto).IsValid);
        dto.LineOfDefence = "LOD1";
        Assert.False(new UpdateAssessmentValidator().Validate(new UpdateAssessmentDto
        {
            Name = dto.Name, Scope = dto.Scope, Function = dto.Function, Quarter = dto.Quarter, Year = dto.Year,
            LeadAuditorId = dto.LeadAuditorId, CoordinatorId = dto.CoordinatorId, LineOfDefence = dto.LineOfDefence
        }).IsValid);
        dto.Year = 2101;
        dto.Quarter = "Q5";
        Assert.False(validator.Validate(dto).IsValid);
        Assert.False(new UpdateAssessmentValidator().Validate(new UpdateAssessmentDto()).IsValid);
        var status = new ChangeAssessmentStatusValidator();
        Assert.True(status.Validate(new ChangeAssessmentStatusDto { TargetStatus = "DraftReport" }).IsValid);
        Assert.False(status.Validate(new ChangeAssessmentStatusDto { TargetStatus = "Planned" }).IsValid);
        Assert.False(status.Validate(new ChangeAssessmentStatusDto { TargetStatus = "Started", Reason = new string('a', 501) }).IsValid);
    }

    [Fact]
    public void ActionValidationRequiresFutureUtcDateAndSegregatedParties()
    {
        var user = Guid.NewGuid();
        var dto = new CreateActionDto
        {
            FindingId = Guid.NewGuid(), ReferenceNumber = "A1", ActionDescription = "Description",
            ActionRequired = "Required", ActionPartyId = user, AuditeeId = Guid.NewGuid(),
            TargetDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1)
        };
        var validator = new CreateActionValidator();
        Assert.True(validator.Validate(dto).IsValid);
        dto.Reportable = "CEO";
        Assert.True(validator.Validate(dto).IsValid);
        dto.Reportable = "1";
        Assert.False(validator.Validate(dto).IsValid);
        dto.Reportable = "NonCEO";
        var update = new UpdateActionDto { ActionDescription = "D", ActionRequired = "R", Reportable = "bogus" };
        Assert.False(new UpdateActionValidator().Validate(update).IsValid);
        update.Reportable = null;
        Assert.True(new UpdateActionValidator().Validate(update).IsValid);
        dto.TargetDate = DateOnly.FromDateTime(DateTime.UtcNow);
        Assert.False(validator.Validate(dto).IsValid);
        dto.TargetDate = dto.TargetDate.AddDays(1);
        dto.AuditeeId = user;
        Assert.False(validator.Validate(dto).IsValid);
        Assert.False(new UpdateActionValidator().Validate(new UpdateActionDto()).IsValid);
        Assert.False(new SubmitActionValidator().Validate(new SubmitActionDto { ActionTaken = new string('a', 4001) }).IsValid);
        Assert.False(new ApproveActionValidator().Validate(new ApproveActionDto { Comments = new string('a', 501) }).IsValid);
        Assert.False(new RejectActionValidator().Validate(new RejectActionDto { Comments = new string('a', 501) }).IsValid);
    }

    [Fact]
    public void ReviewEvidenceAndFindingBoundsAreEnforced()
    {
        var review = new CreateReviewDto { ActionId = Guid.NewGuid(), Outcome = "Ineffective" };
        var reviewValidator = new CreateReviewValidator();
        Assert.False(reviewValidator.Validate(review).IsValid);
        review.ReasonCode = "MissingEvidence";
        Assert.True(reviewValidator.Validate(review).IsValid);
        review.ReasonCode = "Invalid";
        Assert.False(reviewValidator.Validate(review).IsValid);
        var evidence = new CreateEvidenceDto { ActionId = Guid.NewGuid(), FileName = "x", ContentType = "text/plain", SizeBytes = 50L * 1024 * 1024 };
        var evidenceValidator = new CreateEvidenceValidator();
        Assert.True(evidenceValidator.Validate(evidence).IsValid);
        evidence.SizeBytes++;
        Assert.False(evidenceValidator.Validate(evidence).IsValid);
        evidence.SizeBytes = 0;
        Assert.False(evidenceValidator.Validate(evidence).IsValid);
        Assert.False(new CreateFindingValidator().Validate(new CreateFindingDto()).IsValid);
        Assert.False(new UpdateFindingValidator().Validate(new UpdateFindingDto
        {
            Description = "D", RootCause = "R", Recommendation = "R", Severity = "Invalid"
        }).IsValid);
    }

    [Fact]
    public void PaginationDefaultsAndMaskablePropertiesMatchContract()
    {
        var request = new ActionFilterDto();
        Assert.Equal(1, request.Page);
        Assert.Equal(20, request.PageSize);
        Assert.False(request.OverdueOnly);
        Assert.Equal(3, new PaginatedResult<int>([], 41, 1, 20).TotalPages);
        foreach (var name in new[] { "TargetDate", "ActionTaken", "Status" })
            Assert.Equal(name, typeof(ActionDto).GetProperty(name)!.GetCustomAttribute<MaskablePropertyAttribute>()!.FieldName);
    }
}
