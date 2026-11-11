using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Logging;
using RAAMS.Application.Auditing;
using RAAMS.Application.Services;
using RAAMS.Domain.Common;
using RAAMS.Domain.Entities.Actions;
using RAAMS.Domain.Entities.Assessments;
using RAAMS.Domain.Entities.Auditing;
using RAAMS.Domain.Entities.Evidence;
using RAAMS.Domain.Entities.Findings;
using RAAMS.Domain.Entities.Notifications;
using RAAMS.Domain.Entities.Reviews;
using RAAMS.Domain.Entities.Roles;
using RAAMS.Domain.Entities.Users;
using RAAMS.Infrastructure.Auditing;

namespace RAAMS.Infrastructure.Data;

public class RaamsDbContext : DbContext
{
    private readonly IAuditContext _auditContext;
    private readonly AuditSaveChangesInterceptor _auditCapture;

    public RaamsDbContext(
        DbContextOptions<RaamsDbContext> options,
        ICurrentUserService currentUserService,
        IAuditContext auditContext,
        ILogger<AuditSaveChangesInterceptor> logger)
        : base(options)
    {
        ArgumentNullException.ThrowIfNull(currentUserService);
        _auditContext = auditContext ?? throw new ArgumentNullException(nameof(auditContext));
        _auditCapture = new AuditSaveChangesInterceptor(_auditContext, logger);
        ChangeTracker.CascadeDeleteTiming = CascadeTiming.OnSaveChanges;
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<WorkflowPermission> WorkflowPermissions => Set<WorkflowPermission>();
    public DbSet<FieldPermission> FieldPermissions => Set<FieldPermission>();
    public DbSet<Delegation> Delegations => Set<Delegation>();
    public DbSet<Assessment> Assessments => Set<Assessment>();
    public DbSet<AssessmentStatusHistory> AssessmentStatusHistories => Set<AssessmentStatusHistory>();
    public DbSet<Finding> Findings => Set<Finding>();
    public DbSet<ActionEntity> Actions => Set<ActionEntity>();
    public DbSet<ActionStatusHistory> ActionStatusHistories => Set<ActionStatusHistory>();
    public DbSet<EvidenceItem> Evidence => Set<EvidenceItem>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditTrail> AuditTrails => Set<AuditTrail>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HavePrecision(3);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RaamsDbContext).Assembly);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        PrepareChanges();
        var changes = _auditCapture.GetChanges(this);
        var audits = changes.Count == 0 ? [] : _auditCapture.Capture(this, changes, ResolveAuditUser());
        try
        {
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }
        catch
        {
            DetachAudits(audits);
            throw;
        }
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        PrepareChanges();
        var changes = _auditCapture.GetChanges(this);
        var audits = changes.Count == 0 ? [] : _auditCapture.Capture(this, changes, await ResolveAuditUserAsync(cancellationToken));
        try
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        catch
        {
            DetachAudits(audits);
            throw;
        }
    }

    private Guid? TrackedAuditUser(Guid subject) =>
        ChangeTracker.Entries<User>()
            .Where(entry => entry.State != EntityState.Deleted && entry.Entity.EntraObjectId == subject)
            .Select(entry => (Guid?)entry.Entity.Id)
            .FirstOrDefault();

    private Guid? ResolveAuditUser()
    {
        if (_auditContext.UserId is not Guid subject)
            return null;
        return TrackedAuditUser(subject) ?? Users.IgnoreQueryFilters().AsNoTracking()
            .Where(user => user.EntraObjectId == subject)
            .Select(user => (Guid?)user.Id).SingleOrDefault();
    }

    private async Task<Guid?> ResolveAuditUserAsync(CancellationToken cancellationToken)
    {
        if (_auditContext.UserId is not Guid subject)
            return null;
        return TrackedAuditUser(subject) ?? await Users.IgnoreQueryFilters().AsNoTracking()
            .Where(user => user.EntraObjectId == subject)
            .Select(user => (Guid?)user.Id).SingleOrDefaultAsync(cancellationToken);
    }

    private void DetachAudits(IEnumerable<AuditTrail> audits)
    {
        foreach (var audit in audits)
            Entry(audit).State = EntityState.Detached;
    }

    private void PrepareChanges()
    {
        ChangeTracker.DetectChanges();
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Deleted && entry.Entity is ISoftDelete)
            {
                entry.State = EntityState.Unchanged;
                entry.Property(nameof(ISoftDelete.IsDeleted)).CurrentValue = true;
                entry.Property(nameof(ISoftDelete.IsDeleted)).IsModified = true;
            }

            if (entry.State == EntityState.Added && entry.Entity is ICreatedEntity created)
                created.CreatedDate = now;

            if ((entry.State == EntityState.Added || entry.State == EntityState.Modified) &&
                entry.Entity is IModifiedEntity modified)
            {
                modified.ModifiedDate = now;
                if (entry.State == EntityState.Modified)
                    entry.Property(nameof(IModifiedEntity.ModifiedDate)).IsModified = true;
            }
        }
    }
}
