using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RAAMS.API.Services;
using RAAMS.Application.Assessments;
using RAAMS.Application.Auditing;
using RAAMS.Application.Authorization;
using RAAMS.Application.Evidence;
using RAAMS.Application.Services;
using RAAMS.Domain.Entities.Actions;
using RAAMS.Domain.Entities.Assessments;
using RAAMS.Domain.Entities.Findings;
using RAAMS.Domain.Entities.Users;
using RAAMS.Domain.Enums;
using RAAMS.Infrastructure.Auditing;
using RAAMS.Infrastructure.Data;

namespace RAAMS.Tests;

public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RAAMS_TEST_SQL_CONNECTION")))
            Skip = "Set RAAMS_TEST_SQL_CONNECTION to run isolated SQL Server integration tests.";
    }
}

public class EscalationSqlIntegrationTests
{
    [SqlServerFact]
    public async Task UploadUsesSharedVersionsAndPersistsAuditAndMetadata()
    {
        var dbName = $"RaamsEscalationTest_{Guid.NewGuid():N}";
        var connection = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("RAAMS_TEST_SQL_CONNECTION"))
        {
            InitialCatalog = dbName
        };
        var options = new DbContextOptionsBuilder<RaamsDbContext>()
            .UseSqlServer(connection.ConnectionString).Options;
        var user = new User { Id = Guid.NewGuid(), EntraObjectId = Guid.NewGuid(), DisplayName = "Uploader",
            Email = "u@example.invalid", Function = "F", Department = "D" };
        var current = new Mock<ICurrentUserService>();
        current.SetupGet(u => u.UserId).Returns(user.EntraObjectId);
        current.SetupGet(u => u.Roles).Returns(["GRA"]);
        var audit = new AuditContext(null);
        await using var db = new RaamsDbContext(options, current.Object, audit, NullLogger<AuditSaveChangesInterceptor>.Instance);
        var created = false;
        try
        {
            await db.GetService<IRelationalDatabaseCreator>().CreateAsync();
            created = true;
            await db.Database.MigrateAsync();
            var assessment = new Assessment { Id = Guid.NewGuid(), ReferenceNumber = "RA-TEST", Name = "A", Scope = "S",
                Function = "F", Quarter = "Q1", Year = 2026, LeadAuditorId = user.Id, CoordinatorId = user.Id,
                Opinion = AssessmentOpinion.Unacceptable };
            var finding = new Finding { Id = Guid.NewGuid(), Assessment = assessment, ReferenceNumber = "F",
                Description = "D", RootCause = "R", Recommendation = "R", Severity = FindingSeverity.Low };
            var action = new ActionEntity { Id = Guid.NewGuid(), Finding = finding, ReferenceNumber = "A",
                ActionDescription = "D", ActionRequired = "R", ActionPartyId = user.Id, AuditeeId = user.Id,
                Status = ActionStatus.Closed, TargetDate = new DateOnly(2026, 1, 1) };
            db.Users.Add(user);
            db.Actions.Add(action);
            await db.SaveChangesAsync();
            var evidence = new EvidenceService(db, current.Object, audit, NullLogger<EvidenceService>.Instance);
            await evidence.CreateAsync(new CreateEvidenceDto { AssessmentId = assessment.Id,
                FileName = "earlier", ContentType = "text/plain", SizeBytes = 1 });
            current.SetupGet(u => u.Roles).Returns(["Auditee"]);
            var service = new AssessmentService(db, new RecordSecurityService(), Mock.Of<IWorkflowAuthorizationService>(),
                current.Object, audit, NullLogger<AssessmentService>.Instance);
            byte[] contents = [1, 2, 3, 4];
            var result = await service.UploadEscalationEmailAsync(assessment.Id,
                new UploadEscalationEmailDto { FileName = "mail.eml", ContentType = "message/rfc822", SizeBytes = contents.Length, Notes = "Confirmed" },
                new MemoryStream(contents));
            Assert.Equal(assessment.Id, result.AssessmentId);
            Assert.Equal("RA-TEST", result.AssessmentReference);
            Assert.Equal("Uploader", result.UploadedByName);
            var stored = await db.Evidence.SingleAsync(e => e.Id == result.Id);
            Assert.Null(stored.ActionId);
            Assert.Equal(2, stored.Version);
            Assert.Equal(user.Id, stored.UploadedById);
            Assert.Null(stored.SharePointUrl);
            Assert.Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(contents)).ToLowerInvariant(), stored.FileHash);
            Assert.Equal(contents, (await service.GetEscalationEmailFileAsync(assessment.Id, result.Id))!.Data);
            Assert.Null(await service.GetEscalationEmailFileAsync(Guid.NewGuid(), result.Id));
            Assert.Null(await service.GetEscalationEmailFileAsync(assessment.Id,
                (await db.Evidence.SingleAsync(e => e.FileName == "earlier")).Id));
            Assert.Equal("Escalation email uploaded for RA-TEST: mail.eml; Notes: Confirmed",
                (await db.AuditTrails.SingleAsync(a => a.EntityId == result.Id)).Reason);
            Assert.DoesNotContain(await db.AuditTrails.ToListAsync(), a => a.EntityType == "EvidenceContent");
            Assert.Equal(2, (await service.ListEscalationEmailsAsync(assessment.Id)).Count);

            foreach (var (name, type) in new[]
            {
                ("mail.pdf", "application/pdf"),
                ("mail.msg", "application/vnd.ms-outlook"),
                ("mail.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document"),
                ("mail.doc", "application/msword")
            })
            {
                var uploaded = await service.UploadEscalationEmailAsync(assessment.Id,
                    new UploadEscalationEmailDto { FileName = name, ContentType = type, SizeBytes = contents.Length },
                    new MemoryStream(contents));
                var downloaded = await service.GetEscalationEmailFileAsync(assessment.Id, uploaded.Id);
                Assert.NotNull(downloaded);
                Assert.Equal(type, downloaded.ContentType);
                Assert.Equal(contents, downloaded.Data);
            }

            async Task UploadConcurrently(string fileName)
            {
                var uploadAudit = new AuditContext(null);
                await using var uploadDb = new RaamsDbContext(options, current.Object, uploadAudit,
                    NullLogger<AuditSaveChangesInterceptor>.Instance);
                var uploadService = new AssessmentService(uploadDb, new RecordSecurityService(),
                    Mock.Of<IWorkflowAuthorizationService>(), current.Object, uploadAudit,
                    NullLogger<AssessmentService>.Instance);
                await uploadService.UploadEscalationEmailAsync(assessment.Id,
                    new UploadEscalationEmailDto { FileName = fileName, ContentType = "message/rfc822", SizeBytes = 1 },
                    new MemoryStream([1]));
            }

            await Task.WhenAll(UploadConcurrently("a.eml"), UploadConcurrently("b.eml"));
            Assert.Equal(Enumerable.Range(1, 8),
                await db.Evidence.AsNoTracking().Where(e => e.AssessmentId == assessment.Id)
                    .OrderBy(e => e.Version).Select(e => e.Version).ToArrayAsync());
            stored.IsDeleted = true;
            await db.SaveChangesAsync();
            Assert.Null(await service.GetEscalationEmailFileAsync(assessment.Id, result.Id));
            await Assert.ThrowsAsync<SqlException>(() => db.GetService<IMigrator>()
                .MigrateAsync("20261112130533_AddLodReportableAndAssessmentEvidence"));
        }
        finally
        {
            if (created)
                await db.Database.EnsureDeletedAsync();
        }
    }
}
