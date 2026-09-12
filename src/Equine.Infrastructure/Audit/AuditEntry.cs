using Equine.Domain.Common;

namespace Equine.Infrastructure.Audit;

public class AuditEntry : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string? ActorId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string? Before { get; set; }
    public string? After { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    public void AssignTenant(Guid tenantId) => TenantId = TenantAssignment.Apply(TenantId, tenantId);
}

public interface IAuditWriter
{
    Task WriteAsync(
        string? actorId,
        string action,
        string entityType,
        string entityId,
        string? before,
        string? after,
        CancellationToken cancellationToken = default);
}

internal sealed class AuditWriter : IAuditWriter
{
    private static string? AsJson(string? value)
    {
        if (value is null) return null;
        var trimmed = value.TrimStart();
        if (trimmed.StartsWith('{') || trimmed.StartsWith('[') || trimmed.StartsWith('"'))
            return value;
        return System.Text.Json.JsonSerializer.Serialize(value);
    }

    private readonly EquineDbContext _context;

    public AuditWriter(EquineDbContext context)
    {
        _context = context;
    }

    public Task WriteAsync(
        string? actorId,
        string action,
        string entityType,
        string entityId,
        string? before,
        string? after,
        CancellationToken cancellationToken = default)
    {
        var entry = new AuditEntry
        {
            ActorId = actorId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Before = AsJson(before),
            After = AsJson(after),
            Timestamp = DateTimeOffset.UtcNow
        };

        _context.AuditLog.Add(entry);

        return Task.CompletedTask;
    }
}
