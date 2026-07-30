namespace Fixturely.Application.Abstractions.Caching;

public interface ICacheService
{
    Task<string?> GetStringAsync(string key, CancellationToken cancellationToken = default);

    Task SetStringAsync(
        string key,
        string value,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default);

    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default);
}
