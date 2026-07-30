using Fixturely.Domain.Common;

namespace Fixturely.Domain.Entities;

public sealed class UserSession : Entity
{
    private UserSession()
    {
    }

    public Guid UserId { get; private set; }

    public string SessionId { get; private set; } = string.Empty;

    public string? IpAddress { get; private set; }

    public string? UserAgent { get; private set; }

    public DateTime LastActivityAtUtc { get; private set; }

    public DateTime? EndedAtUtc { get; private set; }

    public static UserSession Create(
        Guid userId,
        string sessionId,
        string? ipAddress,
        string? userAgent,
        DateTime utcNow)
    {
        var session = new UserSession
        {
            UserId = userId,
            SessionId = sessionId,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            LastActivityAtUtc = utcNow
        };
        session.Initialize(utcNow);
        return session;
    }

    public void Touch(DateTime utcNow, bool updateAuditTimestamp = true)
    {
        LastActivityAtUtc = utcNow;
        if (updateAuditTimestamp)
        {
            base.Touch(utcNow);
        }
    }

    public void End(DateTime utcNow)
    {
        EndedAtUtc = utcNow;
        base.Touch(utcNow);
    }
}
