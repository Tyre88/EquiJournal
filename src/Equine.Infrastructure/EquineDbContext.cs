using System.Linq.Expressions;
using System.Reflection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Equine.Domain.Common;
using Equine.Domain.Entities;
using Equine.Infrastructure.Audit;
using Equine.Infrastructure.Tenancy;

namespace Equine.Infrastructure;

public class EquineDbContext : IdentityDbContext<
    ApplicationUser,
    IdentityRole<Guid>,
    Guid,
    IdentityUserClaim<Guid>,
    IdentityUserRole<Guid>,
    IdentityUserLogin<Guid>,
    IdentityRoleClaim<Guid>,
    IdentityUserToken<Guid>>
{
    private readonly ITenantContext _tenant;

    public EquineDbContext(DbContextOptions<EquineDbContext> options)
        : this(options, new TenantContext())
    {
    }

    public EquineDbContext(DbContextOptions<EquineDbContext> options, ITenantContext tenant)
        : base(options)
    {
        _tenant = tenant;
    }

    public DbSet<Tenant> Tenants { get; set; } = null!;
    public DbSet<AuditEntry> AuditLog { get; set; } = null!;
    public DbSet<Owner> Owners { get; set; } = null!;
    public DbSet<Horse> Horses { get; set; } = null!;
    public DbSet<TreatmentType> TreatmentTypes { get; set; } = null!;
    public DbSet<JournalEntry> JournalEntries { get; set; } = null!;
    public DbSet<JournalAmendment> JournalAmendments { get; set; } = null!;
    public DbSet<Attachment> Attachments { get; set; } = null!;
    public DbSet<Location> Locations { get; set; } = null!;
    public DbSet<Zone> Zones { get; set; } = null!;
    public DbSet<AvailabilityRule> AvailabilityRules { get; set; } = null!;
    public DbSet<TimeOff> TimeOff { get; set; } = null!;
    public DbSet<Visit> Visits { get; set; } = null!;
    public DbSet<BookingLine> BookingLines { get; set; } = null!;
    public DbSet<WidgetSettings> WidgetSettings { get; set; } = null!;
    public DbSet<PracticeSettings> PracticeSettings { get; set; } = null!;
    public DbSet<NotificationSettings> NotificationSettings { get; set; } = null!;
    public DbSet<NotificationTemplate> NotificationTemplates { get; set; } = null!;
    public DbSet<ScheduledNotification> ScheduledNotifications { get; set; } = null!;
    public DbSet<NotificationLog> NotificationLog { get; set; } = null!;
    public DbSet<InAppNotification> InAppNotifications { get; set; } = null!;
    public DbSet<WebPushEndpoint> WebPushEndpoints { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(EquineDbContext).Assembly);

        builder.Entity<ApplicationUser>(b =>
        {
            b.Property(u => u.TenantId).IsRequired();
            b.HasIndex(u => u.TenantId);
            b.HasOne<Tenant>().WithMany().HasForeignKey(u => u.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        ApplyTenantQueryFilters(builder);

        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTimeOffset) || property.ClrType == typeof(DateTimeOffset?))
                {
                    property.SetColumnType("timestamptz");
                }
            }
        }
        builder.HasDefaultSchema("public");
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampTenantIds();

        var auditWriter = new AuditWriter(this);
        var changes = ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        foreach (var change in changes)
        {
            if (change.Entity is AuditEntry) continue;
            if (change.Entity is SoftDeletableEntity soft && soft.IsDeleted && change.State == EntityState.Modified)
            {
                await auditWriter.WriteAsync(
                    actorId: null,
                    action: "SOFT_DELETE",
                    entityType: change.Entity.GetType().Name,
                    entityId: GetEntityId(change.Entity),
                    before: null,
                    after: null,
                    cancellationToken);
            }
        }
        return await base.SaveChangesAsync(cancellationToken);
    }

    private void StampTenantIds()
    {
        if (!_tenant.HasTenant) return;

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State != EntityState.Added) continue;
            if (entry.Entity is ITenantScoped scoped && scoped.TenantId == Guid.Empty)
                scoped.AssignTenant(_tenant.TenantId);
        }
    }

    private void ApplyTenantQueryFilters(ModelBuilder builder)
    {
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (!typeof(ITenantScoped).IsAssignableFrom(entityType.ClrType))
                continue;

            var method = typeof(SoftDeletableEntity).IsAssignableFrom(entityType.ClrType)
                ? nameof(SetSoftTenantFilter)
                : nameof(SetTenantFilter);
            typeof(EquineDbContext)
                .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!
                .MakeGenericMethod(entityType.ClrType)
                .Invoke(this, [builder]);
        }
    }

    private void SetTenantFilter<T>(ModelBuilder builder) where T : class, ITenantScoped
    {
        builder.Entity<T>().HasQueryFilter(Filter<T>());
        builder.Entity<T>().HasOne<Tenant>().WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
    }

    private void SetSoftTenantFilter<T>(ModelBuilder builder) where T : SoftDeletableEntity
    {
        builder.Entity<T>().HasQueryFilter(e =>
            e.DeletedAt == null && _tenant.HasTenant && e.TenantId == _tenant.TenantId);
        builder.Entity<T>().HasOne<Tenant>().WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
    }

    private Expression<Func<T, bool>> Filter<T>() where T : class, ITenantScoped =>
        e => _tenant.HasTenant && e.TenantId == _tenant.TenantId;

    private static string GetEntityId(object entity)
    {
        var idProp = entity.GetType().GetProperty("Id");
        return idProp?.GetValue(entity)?.ToString() ?? Guid.Empty.ToString();
    }
}
