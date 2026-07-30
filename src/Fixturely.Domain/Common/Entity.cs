namespace Fixturely.Domain.Common;

public abstract class Entity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();

    public DateTime CreatedAtUtc { get; protected set; }

    public DateTime UpdatedAtUtc { get; protected set; }

    public byte[] RowVersion { get; protected set; } = Array.Empty<byte>();

    protected void Touch(DateTime utcNow)
    {
        UpdatedAtUtc = utcNow;
    }

    protected void Initialize(DateTime utcNow)
    {
        CreatedAtUtc = utcNow;
        UpdatedAtUtc = utcNow;
    }
}

public abstract class SoftDeletableEntity : Entity
{
    public bool IsDeleted { get; protected set; }

    public DateTime? DeletedAtUtc { get; protected set; }

    public virtual void MarkAsDeleted(DateTime utcNow)
    {
        IsDeleted = true;
        DeletedAtUtc = utcNow;
        Touch(utcNow);
    }
}
