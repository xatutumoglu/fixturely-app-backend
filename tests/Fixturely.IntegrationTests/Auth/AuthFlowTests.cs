using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Fixturely.Application.DTOs.Auth;
using Fixturely.IntegrationTests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Fixturely.IntegrationTests.Auth;

[Collection(IntegrationTestCollection.Name)]
public sealed class AuthFlowTests
{
    private readonly IntegrationTestWebAppFactory _factory;

    public AuthFlowTests(IntegrationTestWebAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RegisterConfirmLogin_FullFlow_Succeeds()
    {
        var client = _factory.CreateClient();

        var user = await TestUserFactory.RegisterAndConfirmAsync(client, _factory.EmailCapture, "regflow");
        var (accessToken, refreshCookie) = await TestUserFactory.LoginAsync(client, user);

        accessToken.Should().NotBeNullOrEmpty();
        refreshCookie.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Login_BeforeEmailConfirmed_IsRejected()
    {
        var client = _factory.CreateClient();
        var uniqueSuffix = Guid.NewGuid().ToString("N")[..10];
        var userName = $"unconfirmed{uniqueSuffix}";
        var email = $"{userName}@fixturely.test";

        var registerResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/register", new RegisterRequest(userName, email, "Password123!"));
        registerResponse.EnsureSuccessStatusCode();

        var loginResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/login", new LoginRequest(email, "Password123!"));

        loginResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LoginThenAccessProtectedEndpointThenLogout_Succeeds()
    {
        var client = _factory.CreateClient();
        var user = await TestUserFactory.RegisterAndConfirmAsync(client, _factory.EmailCapture, "protectedflow");
        var (accessToken, refreshCookie) = await TestUserFactory.LoginAsync(client, user);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var meResponse = await client.GetAsync("/api/v1/auth/me");
        meResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var profile = await meResponse.ReadAsAsync<UserProfileResponse>();
        profile!.Email.Should().Be(user.Email);

        var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        logoutRequest.Headers.Add("Cookie", $"fixturely_refresh_token={refreshCookie}");
        var logoutResponse = await client.SendAsync(logoutRequest);
        logoutResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var meAfterLogoutResponse = await client.GetAsync("/api/v1/auth/me");
        meAfterLogoutResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ForgotPasswordThenResetPassword_InvalidatesOldSessions()
    {
        var client = _factory.CreateClient();
        var user = await TestUserFactory.RegisterAndConfirmAsync(client, _factory.EmailCapture, "resetflow");
        var (accessToken, _) = await TestUserFactory.LoginAsync(client, user);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var meBeforeReset = await client.GetAsync("/api/v1/auth/me");
        meBeforeReset.StatusCode.Should().Be(HttpStatusCode.OK);

        var forgotResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/forgot-password", new ForgotPasswordRequest(user.Email));
        forgotResponse.EnsureSuccessStatusCode();

        var resetMessage = _factory.EmailCapture.Messages
            .Last(m => m.ToEmail.Equals(user.Email, StringComparison.OrdinalIgnoreCase)
                && m.TemplateType == Fixturely.Domain.Enums.EmailTemplateType.PasswordReset);

        var (userId, token) = EmailLinkParser.ParseUserIdAndToken(resetMessage);

        var resetResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/reset-password", new ResetPasswordRequest(userId, token, "NewPassword456!"));
        resetResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var meAfterResetResponse = await client.GetAsync("/api/v1/auth/me");
        meAfterResetResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var loginWithNewPassword = await client.PostAsJsonAsync(
            "/api/v1/auth/login", new LoginRequest(user.Email, "NewPassword456!"));
        loginWithNewPassword.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ForgotPassword_ForUnknownEmail_DoesNotRevealAccountExistence()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/forgot-password", new ForgotPasswordRequest("no-such-user@fixturely.test"));

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }
}
