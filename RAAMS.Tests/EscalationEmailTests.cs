using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RAAMS.API.Controllers;
using RAAMS.API.Services;
using RAAMS.Application.Assessments;
using RAAMS.Application.Assessments.Validators;
using RAAMS.Application.Auditing;
using RAAMS.Application.Authorization;
using RAAMS.Application.Common.Exceptions;
using RAAMS.Application.Services;
using RAAMS.Domain.Entities.Actions;
using RAAMS.Domain.Entities.Assessments;
using RAAMS.Domain.Entities.Evidence;
using RAAMS.Domain.Entities.Findings;
using RAAMS.Domain.Entities.Users;
using RAAMS.Domain.Enums;
using RAAMS.Infrastructure.Auditing;
using RAAMS.Infrastructure.Data;

namespace RAAMS.Tests;

public class EscalationEmailTests
{
    [Theory]
    [InlineData("message/rfc822", 1, true)]
    [InlineData("APPLICATION/VND.MS-OUTLOOK", 20971520, true)]
    [InlineData("application/pdf", 1, true)]
    [InlineData("application/vnd.openxmlformats-officedocument.wordprocessingml.document", 1, true)]
    [InlineData("application/msword", 1, true)]
    [InlineData("text/plain", 1, false)]
    [InlineData("", 1, false)]
    [InlineData("message/rfc822", 0, false)]
    [InlineData("message/rfc822", 20971521, false)]
    public void MetadataValidationEnforcesAllowedTypesAndSize(string contentType, long size, bool valid)
    {
        var dto = new UploadEscalationEmailDto { FileName = "mail.eml", ContentType = contentType, SizeBytes = size };
        Assert.Equal(valid, new UploadEscalationEmailValidator().Validate(dto).IsValid);
        dto.FileName = new string('x', 256);
        Assert.False(new UploadEscalationEmailValidator().Validate(dto).IsValid);
    }

    [Fact]
    public async Task UploadChecksAuthenticationRoleWorkflowAndMetadata()
    {
        await using var f = new Fixture();
        await f.SeedAsync();
        var dto = Fixture.Dto();
        f.Current.SetupGet(u => u.UserId).Returns((Guid?)null);
        Assert.Equal("Authentication required", (await Assert.ThrowsAsync<ForbiddenException>(
            () => f.Service.UploadEscalationEmailAsync(f.Assessment.Id, dto, new MemoryStream([1])))).Message);
        f.As("Reviewer");
        Assert.Equal("Only Auditee or GRA can upload escalation emails", (await Assert.ThrowsAsync<ForbiddenException>(
            () => f.Service.UploadEscalationEmailAsync(f.Assessment.Id, dto, new MemoryStream([1])))).Message);
        f.As("Auditee");
        Assert.Equal("Assessment not found", (await Assert.ThrowsAsync<ArgumentException>(
            () => f.Service.UploadEscalationEmailAsync(Guid.NewGuid(), dto, new MemoryStream([1])))).Message);

        Assert.Contains("only required", (await Assert.ThrowsAsync<WorkflowViolationException>(
            () => f.Service.UploadEscalationEmailAsync(f.Assessment.Id, dto, new MemoryStream([1])))).Message);
        f.Assessment.Opinion = AssessmentOpinion.Unsatisfactory;
        await f.Db.SaveChangesAsync();
        Assert.Equal("1 action(s) must be closed first", (await Assert.ThrowsAsync<WorkflowViolationException>(
            () => f.Service.UploadEscalationEmailAsync(f.Assessment.Id, dto, new MemoryStream([1])))).Message);
        f.Action.Status = ActionStatus.Closed;
        await f.Db.SaveChangesAsync();
        dto.ContentType = "text/plain";
        await Assert.ThrowsAsync<ValidationException>(() => f.Service.UploadEscalationEmailAsync(f.Assessment.Id, dto, new MemoryStream([1])));
        dto.ContentType = "message/rfc822";
        await Assert.ThrowsAsync<ValidationException>(() => f.Service.UploadEscalationEmailAsync(f.Assessment.Id, dto, new MemoryStream()));
        await Assert.ThrowsAsync<ValidationException>(() => f.Service.UploadEscalationEmailAsync(f.Assessment.Id, dto, new MemoryStream([1, 2])));
        await Assert.ThrowsAsync<ValidationException>(() => f.Service.UploadEscalationEmailAsync(f.Assessment.Id, dto, new MemoryStream(new byte[20 * 1024 * 1024 + 1])));
        dto.Notes = new string('n', 500);
        await Assert.ThrowsAsync<ValidationException>(() => f.Service.UploadEscalationEmailAsync(f.Assessment.Id, dto, new MemoryStream([1])));
        Assert.Empty(await f.Db.Evidence.ToListAsync());
    }

    [Fact]
    public async Task HighSeverityAlsoQualifiesAndDeletedFindingsDoNot()
    {
        await using var f = new Fixture();
        await f.SeedAsync();
        f.Finding.Severity = FindingSeverity.High;
        await f.Db.SaveChangesAsync();
        Assert.Equal("1 action(s) must be closed first", (await Assert.ThrowsAsync<WorkflowViolationException>(
            () => f.Service.UploadEscalationEmailAsync(f.Assessment.Id, Fixture.Dto(), new MemoryStream([1])))).Message);
        f.Finding.IsDeleted = true;
        await f.Db.SaveChangesAsync();
        Assert.Contains("only required", (await Assert.ThrowsAsync<WorkflowViolationException>(
            () => f.Service.UploadEscalationEmailAsync(f.Assessment.Id, Fixture.Dto(), new MemoryStream([1])))).Message);
    }

    [Fact]
    public async Task ListFiltersParentsAndDeletedItemsAndReturnsUploaderAndReference()
    {
        await using var f = new Fixture();
        await f.SeedAsync();
        var older = f.Email("old", DateTime.UtcNow.AddDays(-1));
        var newer = f.Email("new", DateTime.UtcNow);
        var deleted = f.Email("deleted", DateTime.UtcNow); deleted.IsDeleted = true;
        var actionEvidence = f.Email("action", DateTime.UtcNow); actionEvidence.ActionId = f.Action.Id;
        f.Db.Evidence.AddRange(older, newer, deleted, actionEvidence);
        await f.Db.SaveChangesAsync();
        f.User.IsDeleted = true;
        await f.Db.SaveChangesAsync();
        var emails = await f.Service.ListEscalationEmailsAsync(f.Assessment.Id);
        Assert.Equal(new[] { "new", "old" }, emails.Select(e => e.FileName));
        Assert.All(emails, email =>
        {
            Assert.Equal(f.Assessment.Id, email.AssessmentId);
            Assert.Equal("RA-TEST", email.AssessmentReference);
            Assert.Equal("Uploader", email.UploadedByName);
        });
        Assert.Empty(await f.Service.ListEscalationEmailsAsync(Guid.NewGuid()));
        f.Assessment.IsDeleted = true;
        await f.Db.SaveChangesAsync();
        Assert.Equal(2, (await f.Service.ListEscalationEmailsAsync(f.Assessment.Id)).Count);
    }

    [Fact]
    public async Task ControllerReturnsCreatedAndListAndMapsMissingAssessment()
    {
        var service = new Mock<IAssessmentService>();
        var id = Guid.NewGuid();
        var stream = new MemoryStream([1]);
        var formFile = new FormFile(stream, 0, 1, "file", @"C:\fakepath\mail.eml")
        {
            Headers = new HeaderDictionary(),
            ContentType = "message/rfc822"
        };
        var request = new UploadEscalationEmailRequest { File = formFile };
        var email = new EscalationEmailDto { Id = Guid.NewGuid(), AssessmentId = id };
        service.Setup(s => s.UploadEscalationEmailAsync(id, It.Is<UploadEscalationEmailDto>(
            dto => dto.FileName == "mail.eml" && dto.SizeBytes == 1), It.IsAny<Stream>(), It.IsAny<CancellationToken>())).ReturnsAsync(email);
        service.Setup(s => s.ListEscalationEmailsAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync([email]);
        service.Setup(s => s.GetEscalationEmailFileAsync(id, email.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EscalationEmailFile("mail.eml", "message/rfc822", [1]));
        var controller = new AssessmentsController(service.Object, NullLogger<AssessmentsController>.Instance);
        var created = Assert.IsType<CreatedAtActionResult>((await controller.UploadEscalationEmail(id, request, default)).Result);
        Assert.Equal(nameof(AssessmentsController.ListEscalationEmails), created.ActionName);
        Assert.Equal(id, created.RouteValues!["id"]);
        Assert.IsType<OkObjectResult>((await controller.ListEscalationEmails(id, default)).Result);
        var download = Assert.IsType<FileContentResult>(await controller.DownloadEscalationEmail(id, email.Id, default));
        Assert.Equal("mail.eml", download.FileDownloadName);
        Assert.Equal(new byte[] { 1 }, download.FileContents);
        Assert.IsType<NotFoundResult>(await controller.DownloadEscalationEmail(id, Guid.NewGuid(), default));
        service.Setup(s => s.UploadEscalationEmailAsync(id, It.IsAny<UploadEscalationEmailDto>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Assessment not found"));
        Assert.IsType<BadRequestObjectResult>((await controller.UploadEscalationEmail(id, request, default)).Result);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public User User { get; } = new()
        {
            Id = Guid.NewGuid(), EntraObjectId = Guid.NewGuid(), DisplayName = "Uploader",
            Email = "uploader@example.invalid", Function = "F", Department = "D"
        };
        public Mock<ICurrentUserService> Current { get; } = new();
        public RaamsDbContext Db { get; }
        public AssessmentService Service { get; }
        public Assessment Assessment { get; }
        public Finding Finding { get; }
        public ActionEntity Action { get; }

        public Fixture()
        {
            As("Auditee");
            var options = new DbContextOptionsBuilder<RaamsDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString(), o => o.EnableNullChecks(false)).Options;
            Db = new RaamsDbContext(options, Current.Object, Mock.Of<IAuditContext>(), NullLogger<AuditSaveChangesInterceptor>.Instance);
            Service = new AssessmentService(Db, new RecordSecurityService(), Mock.Of<IWorkflowAuthorizationService>(),
                Current.Object, Mock.Of<IAuditContext>(), NullLogger<AssessmentService>.Instance);
            Assessment = new Assessment
            {
                Id = Guid.NewGuid(), ReferenceNumber = "RA-TEST", Name = "A", Scope = "S", Function = "F",
                Quarter = "Q1", Year = 2026, LeadAuditorId = User.Id, CoordinatorId = User.Id,
                Opinion = AssessmentOpinion.Satisfactory
            };
            Finding = new Finding
            {
                Id = Guid.NewGuid(), Assessment = Assessment, ReferenceNumber = "F", Description = "D",
                RootCause = "R", Recommendation = "R", Severity = FindingSeverity.Low
            };
            Action = new ActionEntity
            {
                Id = Guid.NewGuid(), Finding = Finding, ReferenceNumber = "A", ActionDescription = "D",
                ActionRequired = "R", ActionPartyId = User.Id, AuditeeId = User.Id, Status = ActionStatus.Open
            };
        }

        public async Task SeedAsync()
        {
            Db.Users.Add(User);
            Db.Actions.Add(Action);
            await Db.SaveChangesAsync();
        }
        public void As(string role)
        {
            Current.SetupGet(u => u.UserId).Returns(User.EntraObjectId);
            Current.SetupGet(u => u.Roles).Returns([role]);
        }
        public static UploadEscalationEmailDto Dto() => new()
        {
            FileName = "mail.eml", ContentType = "message/rfc822", SizeBytes = 1
        };
        public EvidenceItem Email(string name, DateTime uploadedAt) => new()
        {
            Id = Guid.NewGuid(), AssessmentId = Assessment.Id, FileName = name,
            ContentType = "message/rfc822", SizeBytes = 1, Version = 1, UploadedById = User.Id, UploadedAt = uploadedAt
        };
        public async ValueTask DisposeAsync() => await Db.DisposeAsync();
    }
}
