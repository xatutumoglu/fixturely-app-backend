using Fixturely.Infrastructure.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Fixturely.Api.HealthChecks;

public sealed class RedisHealthCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer _connectionMultiplexer;

    public RedisHealthCheck(IConnectionMultiplexer connectionMultiplexer)
    {
        _connectionMultiplexer = connectionMultiplexer;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Task.FromResult(_connectionMultiplexer.IsConnected
                ? HealthCheckResult.Healthy("Redis is connected.")
                : HealthCheckResult.Unhealthy("Redis is not connected."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Redis health check failed.", ex));
        }
    }
}

public sealed class SmtpConfigurationHealthCheck : IHealthCheck
{
    private readonly SmtpOptions _options;

    public SmtpConfigurationHealthCheck(IOptions<SmtpOptions> options)
    {
        _options = options.Value;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var isValid = !string.IsNullOrWhiteSpace(_options.Host)
            && _options.Port > 0
            && !string.IsNullOrWhiteSpace(_options.FromEmail);

        return Task.FromResult(isValid
            ? HealthCheckResult.Healthy("SMTP configuration is valid.")
            : HealthCheckResult.Unhealthy("SMTP configuration is missing required values."));
    }
}
