using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RAAMS.API.Services;
using RAAMS.Application.Auditing;
using RAAMS.Application.Common.Exceptions;
using RAAMS.Application.Evidence;
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

public class EvidenceServiceTests
{
    [Fact]
    public async Task ListsActiveEvidenceWithFiltersAndIncludesDeletedUploaders()
    {
        await using var f = new Fixture();
        await f.SeedAsync();
        var old = f.Item("old", 1, DateTime.UtcNow.AddDays(-1));
        var latest = f.Item("latest", 2, DateTime.UtcNow);
        var deleted = f.Item("deleted", 3, DateTime.UtcNow);
        deleted.IsDeleted = true;
        f.Db.Evidence.AddRange(old, latest, deleted);
        await f.Db.SaveChangesAsync();
        f.User.IsDeleted = true;
        await f.Db.SaveChangesAsync();
        var page = await f.Service.ListAsync(f.Action.Id, 1, 1);
        Assert.Equal(2, page.TotalCount);
        Assert.Equal(latest.Id, Assert.Single(page.Items).Id);
        Assert.Equal("Uploader", page.Items[0].UploadedByName);
        Assert.Equal(old.Id, Assert.Single((await f.Service.ListAsync(null, 2, 1)).Items).Id);
        Assert.Empty((await f.Service.ListAsync(Guid.NewGuid(), 1, 20)).Items);
        Assert.Null(await f.Service.GetByIdAsync(deleted.Id));
        Assert.NotNull(await f.Service.GetByIdAsync(old.Id));
        await Assert.ThrowsAsync<ValidationException>(() => f.Service.ListAsync(null, 0, 20));
        await Assert.ThrowsAsync<ValidationException>(() => f.Service.ListAsync(null, int.MaxValue, 200));
    }

    [Fact]
    public async Task DeleteRequiresUploaderOrGraAndPreservesRow()
    {
        await using var f = new Fixture();
        await f.SeedAsync();
        var evidence = f.Item("file", 1, DateTime.UtcNow);
        f.Db.Evidence.Add(evidence);
        await f.Db.SaveChangesAsync();
        f.As(f.Other, "Auditee");
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Service.DeleteAsync(evidence.Id));
        Assert.False(evidence.IsDeleted);
        f.As(f.User, "ActionParty");
        await f.Service.DeleteAsync(evidence.Id);
        Assert.True((await f.Db.Evidence.IgnoreQueryFilters().SingleAsync()).IsDeleted);
        Assert.Null(await f.Service.GetByIdAsync(evidence.Id));
        f.Audit.Verify(a => a.SetReason("Evidence deleted: file"), Times.Once);
        var other = f.Item("other", 2, DateTime.UtcNow);
        f.Db.Evidence.Add(other);
        await f.Db.SaveChangesAsync();
        f.As(f.Other, "GRA");
        await f.Service.DeleteAsync(other.Id);
        Assert.True(other.IsDeleted);
    }

    [Fact]
    public async Task CreateRejectsWrongRoleUnknownSubjectAndInvalidMetadataBeforeDatabaseWrite()
    {
        await using var f = new Fixture();
        await f.SeedAsync();
        f.As(f.User, "SuperAdmin");
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Service.CreateAsync(f.Create()));
        f.As(f.User, "ActionParty");
        f.Current.SetupGet(u => u.UserId).Returns(f.User.Id);
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Service.CreateAsync(f.Create()));
        f.As(f.User, "ActionParty");
        var bad = f.Create();
        bad.SizeBytes = 50L * 1024 * 1024 + 1;
        await Assert.ThrowsAsync<ValidationException>(() => f.Service.CreateAsync(bad));
        Assert.Empty(await f.Db.Evidence.ToListAsync());
    }

    [Fact]
    public async Task AssessmentParentRequiresGraAndNullParentsAreRejected()
    {
        await using var f = new Fixture();
        await f.SeedAsync();
        var dto = f.Create();
        dto.ActionId = null;
        var error = await Assert.ThrowsAsync<ArgumentException>(() => f.Service.CreateAsync(dto));
        Assert.Equal("Evidence must reference either an Action or an Assessment", error.Message);
        dto.AssessmentId = f.Action.Finding.Assessment.Id;
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Service.CreateAsync(dto));
        dto.ActionId = f.Action.Id;
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Service.CreateAsync(dto));
        f.Db.Evidence.Add(new EvidenceItem
        {
            Id = Guid.NewGuid(), AssessmentId = dto.AssessmentId, UploadedById = f.User.Id,
            FileName = "assessment", ContentType = "text/plain", SizeBytes = 1, Version = 1, UploadedAt = DateTime.UtcNow
        });
        await f.Db.SaveChangesAsync();
        var result = Assert.Single((await f.Service.ListAsync(null, 1, 20)).Items);
        Assert.Null(result.ActionId);
        Assert.Equal(dto.AssessmentId, result.AssessmentId);
    }

    [Fact]
    public async Task ModelUsesCorrectDefaultSentinelsAndOptionalEvidenceParents()
    {
        await using var f = new Fixture();
        var action = f.Db.Model.FindEntityType(typeof(ActionEntity))!;
        var assessment = f.Db.Model.FindEntityType(typeof(Assessment))!;
        var evidence = f.Db.Model.FindEntityType(typeof(EvidenceItem))!;
        Assert.Equal(Reportable.NonCEO, action.FindProperty(nameof(ActionEntity.Reportable))!.Sentinel);
        Assert.Equal(AssessmentOpinion.NotRated, assessment.FindProperty(nameof(Assessment.Opinion))!.Sentinel);
        Assert.True(evidence.FindProperty(nameof(EvidenceItem.ActionId))!.IsNullable);
        Assert.True(evidence.FindProperty(nameof(EvidenceItem.AssessmentId))!.IsNullable);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public User User { get; } = MakeUser("Uploader");
        public User Other { get; } = MakeUser("Other");
        public Mock<ICurrentUserService> Current { get; } = new();
        public Mock<IAuditContext> Audit { get; } = new();
        public RaamsDbContext Db { get; }
        public EvidenceService Service { get; }
        public ActionEntity Action { get; }

        public Fixture()
        {
            As(User, "ActionParty");
            var options = new DbContextOptionsBuilder<RaamsDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString(), o => o.EnableNullChecks(false)).Options;
            Db = new RaamsDbContext(options, Current.Object, Audit.Object, NullLogger<AuditSaveChangesInterceptor>.Instance);
            Service = new EvidenceService(Db, Current.Object, Audit.Object, NullLogger<EvidenceService>.Instance);
            Action = new ActionEntity
            {
                Id = Guid.NewGuid(), ReferenceNumber = "ACTION", ActionDescription = "D", ActionRequired = "R",
                ActionPartyId = User.Id, AuditeeId = Other.Id, TargetDate = DateOnly.FromDateTime(DateTime.UtcNow),
                Status = ActionStatus.Open, Finding = new Finding
                {
                    Id = Guid.NewGuid(), ReferenceNumber = "F", Description = "D", RootCause = "R", Recommendation = "R",
                    Assessment = new Assessment { Id = Guid.NewGuid(), ReferenceNumber = "A", Name = "A", Scope = "S",
                        Function = "F", Quarter = "Q1", Year = 2026, LeadAuditorId = User.Id, CoordinatorId = Other.Id }
                }
            };
        }

        public async Task SeedAsync()
        {
            Db.Users.AddRange(User, Other);
            Db.Actions.Add(Action);
            await Db.SaveChangesAsync();
        }
        public void As(User user, params string[] roles)
        {
            Current.SetupGet(u => u.UserId).Returns(user.EntraObjectId);
            Current.SetupGet(u => u.Roles).Returns(roles);
        }
        public EvidenceItem Item(string name, int version, DateTime uploadedAt) => new()
        {
            Id = Guid.NewGuid(), ActionId = Action.Id, UploadedById = User.Id, FileName = name,
            ContentType = "text/plain", SizeBytes = 1, Version = version, UploadedAt = uploadedAt
        };
        public CreateEvidenceDto Create() => new()
        {
            ActionId = Action.Id, FileName = "file", ContentType = "text/plain", SizeBytes = 1
        };
        public ValueTask DisposeAsync() => Db.DisposeAsync();
        private static User MakeUser(string name) => new()
        {
            Id = Guid.NewGuid(), EntraObjectId = Guid.NewGuid(), DisplayName = name,
            Email = $"{name}@example.invalid", Function = "F", Department = "D"
        };
    }
}
