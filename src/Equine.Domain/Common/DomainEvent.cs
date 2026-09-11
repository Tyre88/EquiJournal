namespace Equine.Domain.Common;

public record DomainEvent(Guid Id, string Type, DateTimeOffset OccurredAt);
