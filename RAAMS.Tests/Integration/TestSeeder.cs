using RAAMS.Domain.Entities.Actions;
using RAAMS.Domain.Entities.Assessments;
using RAAMS.Domain.Entities.Findings;
using RAAMS.Domain.Entities.Roles;
using RAAMS.Domain.Entities.Users;
using RAAMS.Domain.Enums;
using RAAMS.Infrastructure.Data;

namespace RAAMS.Tests.Integration;

public sealed record TestData(Guid HarunId, Guid ApId, Guid AuId, Guid AssessmentId, Guid FindingId, Guid ActionId);

public static class TestSeeder
{
    public static async Task<TestData> SeedAsync(RaamsDbContext db)
    {
        var roleNames = new[] { "GRA", "LeadAuditor", "AssuranceCoordinator", "Auditee",
            "ActionParty", "Reviewer", "Sponsor", "SuperAdmin" };
        var roles = roleNames.ToDictionary(name => name, name => new Role { Id = Guid.NewGuid(), Name = name });
        db.Roles.AddRange(roles.Values);

        var names = new[]
        {
            "Assessment.View", "Assessment.Create", "Assessment.Edit", "Assessment.Submit",
            "Assessment.Approve", "Assessment.Cancel", "Assessment.Reopen",
            "Finding.View", "Finding.Create", "Finding.Edit", "Finding.Delete",
            "Action.View", "Action.Create", "Action.Edit", "Action.Submit", "Action.Approve",
            "Action.Reopen", "Action.Close", "Evidence.View", "Evidence.Upload", "Evidence.Delete",
            "Review.Create", "Reporting.View", "Administration.Users", "Administration.Security"
        };
        var modules = new Dictionary<string, string>
        {
            ["Assessment"] = "Assessments", ["Finding"] = "Findings", ["Action"] = "Actions",
            ["Evidence"] = "Evidence", ["Review"] = "Reviews", ["Reporting"] = "Reporting",
            ["Administration"] = "Administration"
        };
        var permissions = names.ToDictionary(name => name, name => new Permission
        {
            Id = Guid.NewGuid(), Name = name, Module = modules[name.Split('.')[0]]
        });
        db.Permissions.AddRange(permissions.Values);

        void Grant(string role, IEnumerable<string> grants)
        {
            foreach (var name in grants)
                db.RolePermissions.Add(new RolePermission { RoleId = roles[role].Id, PermissionId = permissions[name].Id });
        }
        Grant("GRA", names);
        Grant("SuperAdmin", names);
        Grant("ActionParty", ["Action.View", "Action.Edit", "Action.Submit", "Evidence.Upload", "Evidence.View", "Finding.View"]);
        Grant("Auditee", ["Assessment.View", "Finding.View", "Action.Approve", "Action.View", "Evidence.View", "Evidence.Upload"]);
        Grant("Reviewer", ["Assessment.View", "Finding.View", "Action.View", "Action.Reopen", "Evidence.View", "Review.Create"]);
        Grant("LeadAuditor", ["Assessment.View", "Assessment.Create", "Assessment.Edit", "Assessment.Submit",
            "Finding.View", "Finding.Create", "Finding.Edit", "Action.View", "Action.Create", "Evidence.View", "Reporting.View"]);
        Grant("AssuranceCoordinator", ["Assessment.View", "Assessment.Create", "Assessment.Edit", "Finding.View",
            "Finding.Create", "Finding.Edit", "Action.View", "Action.Create", "Evidence.View", "Reporting.View"]);
        Grant("Sponsor", ["Assessment.View", "Finding.View", "Action.View", "Reporting.View"]);

        void Field(string role, string entity, string field, bool view, bool edit) =>
            db.FieldPermissions.Add(new FieldPermission
            {
                Id = Guid.NewGuid(), RoleId = roles[role].Id, EntityType = entity, FieldName = field,
                CanView = view, CanEdit = edit
            });
        foreach (var (role, view, edit) in new[]
        {
            ("GRA", true, true), ("LeadAuditor", true, false), ("AssuranceCoordinator", true, false),
            ("ActionParty", false, false), ("Auditee", true, false), ("Reviewer", true, false)
        })
            Field(role, "Action", "TargetDate", view, edit);
        foreach (var (role, view, edit) in new[]
        {
            ("GRA", true, true), ("ActionParty", true, true), ("Auditee", true, true),
            ("AssuranceCoordinator", true, false), ("Reviewer", true, false), ("Sponsor", false, false)
        })
            Field(role, "Action", "ActionTaken", view, edit);
        foreach (var role in new[] { "GRA", "ActionParty", "Auditee", "Reviewer" })
            Field(role, "Action", "Status", true, role == "GRA");
        Field("GRA", "User", "Role", true, true);
        Field("SuperAdmin", "User", "Role", true, true);
        foreach (var role in roleNames)
        {
            foreach (var field in new[] { "ActionPartyId", "AuditeeId", "ReferenceNumber" })
                Field(role, "Action", field, true, role == "GRA");
            var effectiveness = role is "GRA" or "Reviewer";
            Field(role, "Action", "EffectivenessRating", effectiveness, effectiveness);
            foreach (var field in new[] { "Function", "Department" })
                Field(role, "User", field, true, role == "GRA");
        }

        void Workflow(string role, string entity, string from, string to, bool edit = false, bool approve = false, bool reopen = false) =>
            db.WorkflowPermissions.Add(new WorkflowPermission
            {
                Id = Guid.NewGuid(), RoleId = roles[role].Id, EntityType = entity,
                CurrentStatus = from, TargetStatus = to, CanView = true, CanEdit = edit,
                CanApprove = approve, CanReopen = reopen
            });
        Workflow("ActionParty", "Action", "Open", "Submitted", edit: true);
        Workflow("Auditee", "Action", "Open", "Submitted");
        Workflow("Auditee", "Action", "Submitted", "Approved", approve: true);
        Workflow("Auditee", "Action", "Submitted", "Rejected", approve: true);
        Workflow("GRA", "Action", "Approved", "Closed", edit: true);
        Workflow("Reviewer", "Action", "Closed", "Reopened", reopen: true);
        Workflow("GRA", "Action", "Closed", "Reopened", reopen: true);
        Workflow("ActionParty", "Action", "Reopened", "Submitted", edit: true);
        foreach (var (from, to) in new[] { ("Planned", "Started"), ("Started", "DraftReport"), ("DraftReport", "Completed") })
        {
            Workflow("LeadAuditor", "Assessment", from, to, edit: true);
            Workflow("GRA", "Assessment", from, to, edit: true);
        }
        Workflow("GRA", "Assessment", "Planned", "Deferred", edit: true);
        Workflow("GRA", "Assessment", "Planned", "Cancelled", edit: true);
        Workflow("LeadAuditor", "Assessment", "Planned", "Cancelled", edit: true);
        Workflow("GRA", "Assessment", "Completed", "Reopened", reopen: true);

        User User(string name, string email)
        {
            var id = Guid.NewGuid();
            return new User
            {
                Id = id, EntraObjectId = id, DisplayName = name, Email = email,
                Function = "Assurance", Department = "Test", IsActive = true
            };
        }
        var harun = User("Harun", "harun@example.invalid");
        var ap = User("AP User", "ap@example.invalid");
        var au = User("AU User", "au@example.invalid");
        db.Users.AddRange(harun, ap, au);
        foreach (var (user, role) in new[] { (harun, "GRA"), (harun, "SuperAdmin"), (ap, "ActionParty"), (au, "Auditee") })
            db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = roles[role].Id, EffectiveFrom = DateTime.UtcNow.AddDays(-1) });

        var assessment = new Assessment
        {
            Id = Guid.NewGuid(), ReferenceNumber = "RA-2026-Q1-0001", Name = "Test assessment",
            Scope = "Integration testing", Function = "Assurance", Quarter = "Q1", Year = 2026,
            LeadAuditorId = harun.Id, CoordinatorId = harun.Id, LineOfDefence = LineOfDefence.LOD1,
            Opinion = AssessmentOpinion.NotRated, Status = AssessmentStatus.Planned
        };
        var finding = new Finding
        {
            Id = Guid.NewGuid(), AssessmentId = assessment.Id, ReferenceNumber = "F-0001",
            Description = "Test finding", RootCause = "Test root cause", Recommendation = "Test recommendation",
            Severity = FindingSeverity.High, Status = FindingStatus.Draft
        };
        var action = new ActionEntity
        {
            Id = Guid.NewGuid(), FindingId = finding.Id, ReferenceNumber = "A-0001",
            ActionDescription = "Test action", ActionRequired = "Complete test action",
            ActionPartyId = ap.Id, AuditeeId = au.Id, TargetDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            Status = ActionStatus.Open
        };
        db.Assessments.Add(assessment);
        db.Findings.Add(finding);
        db.Actions.Add(action);
        await db.SaveChangesAsync();
        return new TestData(harun.Id, ap.Id, au.Id, assessment.Id, finding.Id, action.Id);
    }
}
