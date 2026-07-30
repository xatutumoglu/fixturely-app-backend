using System.Text.Json;
using Fixturely.Application.Abstractions.Caching;
using StackExchange.Redis;

namespace Fixturely.Infrastructure.Caching;

/// <summary>
/// Stores session state in Redis using the "fixturely:session:{sessionId}" key pattern with
/// a sliding idle-timeout expiration. A secondary "fixturely:user-sessions:{userId}" set is
/// maintained so that logout-all can discover and remove every session for a user.
/// </summary>
public sealed class RedisSessionStore : ISessionStore
{
    private readonly IConnectionMultiplexer _connectionMultiplexer;

    public RedisSessionStore(IConnectionMultiplexer connectionMultiplexer)
    {
        _connectionMultiplexer = connectionMultiplexer;
    }

    private IDatabase Database => _connectionMultiplexer.GetDatabase();

    private static string SessionKey(string sessionId) => $"fixturely:session:{sessionId}";

    private static string UserSessionsKey(Guid userId) => $"fixturely:user-sessions:{userId}";

    public async Task CreateSessionAsync(
        SessionData session,
        TimeSpan idleTimeout,
        CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(session);
        await Database.StringSetAsync(SessionKey(session.SessionId), json, idleTimeout);
        await Database.SetAddAsync(UserSessionsKey(session.UserId), session.SessionId);
        await Database.KeyExpireAsync(UserSessionsKey(session.UserId), TimeSpan.FromDays(30));
    }

    public async Task<SessionData?> GetSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var value = await Database.StringGetAsync(SessionKey(sessionId));

        return value.HasValue
            ? JsonSerializer.Deserialize<SessionData>(value.ToString())
            : null;
    }

    public async Task<bool> TouchSessionAsync(
        string sessionId,
        DateTime utcNow,
        TimeSpan idleTimeout,
        CancellationToken cancellationToken = default)
    {
        var existing = await GetSessionAsync(sessionId, cancellationToken);

        if (existing is null)
        {
            return false;
        }

        var refreshed = new SessionData
        {
            UserId = existing.UserId,
            SessionId = existing.SessionId,
            IpAddress = existing.IpAddress,
            UserAgent = existing.UserAgent,
            CreatedAtUtc = existing.CreatedAtUtc,
            LastActivityAtUtc = utcNow
        };

        var json = JsonSerializer.Serialize(refreshed);
        await Database.StringSetAsync(SessionKey(sessionId), json, idleTimeout);
        return true;
    }

    public async Task RemoveSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var existing = await GetSessionAsync(sessionId, cancellationToken);
        await Database.KeyDeleteAsync(SessionKey(sessionId));

        if (existing is not null)
        {
            await Database.SetRemoveAsync(UserSessionsKey(existing.UserId), sessionId);
        }
    }

    public async Task RemoveAllSessionsForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var sessionIds = await Database.SetMembersAsync(UserSessionsKey(userId));

        if (sessionIds.Length > 0)
        {
            var keys = sessionIds.Select(s => (RedisKey)SessionKey(s.ToString())).ToArray();
            await Database.KeyDeleteAsync(keys);
        }

        await Database.KeyDeleteAsync(UserSessionsKey(userId));
    }
}
