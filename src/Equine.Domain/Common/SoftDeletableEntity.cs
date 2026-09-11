using Equine.Domain.Common;

namespace Equine.Domain.Common;

public abstract class SoftDeletableEntity : Entity
{
    public DateTimeOffset? DeletedAt { get; protected set; }

    public void SoftDelete()
    {
        DeletedAt = DateTimeOffset.UtcNow;
    }

    public void Restore()
    {
        DeletedAt = null;
    }

    public bool IsDeleted => DeletedAt.HasValue;
}
