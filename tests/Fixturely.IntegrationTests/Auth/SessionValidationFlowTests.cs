using System.Net;
using System.Net.Http.Headers;
using Fixturely.Application.Abstractions.Caching;
using Fixturely.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Fixturely.IntegrationTests.Auth;

[Collection(IntegrationTestCollection.Name)]
public sealed class SessionValidationFlowTests
{
    private readonly IntegrationTestWebAppFactory _factory;

    public SessionValidationFlowTests(IntegrationTestWebAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RemovingRedisSession_RejectsSubsequentRequestsEvenWithValidJwt()
    {
        var client = _factory.CreateClient();
        var user = await TestUserFactory.RegisterAndConfirmAsync(client, _factory.EmailCapture, "sessionflow");
        var (accessToken, _) = await TestUserFactory.LoginAsync(client, user);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var beforeResponse = await client.GetAsync("/api/v1/auth/me");
        beforeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var sessionStore = scope.ServiceProvider.GetRequiredService<ISessionStore>();

        var sessionIdClaim = ExtractSessionIdFromJwt(accessToken);
        await sessionStore.RemoveSessionAsync(sessionIdClaim, CancellationToken.None);

        var afterResponse = await client.GetAsync("/api/v1/auth/me");
        afterResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static string ExtractSessionIdFromJwt(string jwt)
    {
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(jwt);
        return token.Claims.First(c => c.Type == "sid").Value;
    }
}
