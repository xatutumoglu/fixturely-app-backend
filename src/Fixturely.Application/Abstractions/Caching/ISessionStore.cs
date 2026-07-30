namespace Fixturely.Application.Abstractions.Caching;

public sealed class SessionData
{
    public required Guid UserId { get; init; }

    public required string SessionId { get; init; }

    public string? IpAddress { get; init; }

    public string? UserAgent { get; init; }

    public required DateTime CreatedAtUtc { get; init; }

    public required DateTime LastActivityAtUtc { get; init; }
}

public interface ISessionStore
{
    Task CreateSessionAsync(SessionData session, TimeSpan idleTimeout, CancellationToken cancellationToken = default);

    Task<SessionData?> GetSessionAsync(string sessionId, CancellationToken cancellationToken = default);

    Task<bool> TouchSessionAsync(
        string sessionId,
        DateTime utcNow,
        TimeSpan idleTimeout,
        CancellationToken cancellationToken = default);

    Task RemoveSessionAsync(string sessionId, CancellationToken cancellationToken = default);

    Task RemoveAllSessionsForUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
