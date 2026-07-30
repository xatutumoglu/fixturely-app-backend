using System.Net;
using Fixturely.IntegrationTests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Fixturely.IntegrationTests.Auth;

[Collection(IntegrationTestCollection.Name)]
public sealed class HealthCheckTests
{
    private readonly IntegrationTestWebAppFactory _factory;

    public HealthCheckTests(IntegrationTestWebAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task HealthLive_ReturnsHealthy()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HealthReady_VerifiesSqlServerAndRedisConnectivity()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
