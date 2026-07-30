using Fixturely.Application.Abstractions.Caching;
using StackExchange.Redis;

namespace Fixturely.Infrastructure.Caching;

public sealed class RedisCacheService : ICacheService
{
    private readonly IConnectionMultiplexer _connectionMultiplexer;

    public RedisCacheService(IConnectionMultiplexer connectionMultiplexer)
    {
        _connectionMultiplexer = connectionMultiplexer;
    }

    private IDatabase Database => _connectionMultiplexer.GetDatabase();

    public async Task<string?> GetStringAsync(string key, CancellationToken cancellationToken = default)
    {
        var value = await Database.StringGetAsync(key);
        return value.HasValue ? value.ToString() : null;
    }

    public Task SetStringAsync(
        string key,
        string value,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default)
    {
        return Database.StringSetAsync(key, value, expiration);
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        return Database.KeyDeleteAsync(key);
    }

    public async Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        foreach (var endpoint in _connectionMultiplexer.GetEndPoints())
        {
            var server = _connectionMultiplexer.GetServer(endpoint);

            if (!server.IsConnected)
            {
                continue;
            }

            var keys = server.Keys(pattern: $"{prefix}*").ToArray();

            if (keys.Length > 0)
            {
                await Database.KeyDeleteAsync(keys);
            }
        }
    }
}
