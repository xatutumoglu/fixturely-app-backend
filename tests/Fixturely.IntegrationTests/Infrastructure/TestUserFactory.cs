using System.Net.Http.Json;
using Fixturely.Application.DTOs.Auth;
using Fixturely.Domain.Enums;
using FluentAssertions;

namespace Fixturely.IntegrationTests.Infrastructure;

public sealed record RegisteredTestUser(Guid UserId, string UserName, string Email, string Password);

public static class TestUserFactory
{
    public static async Task<RegisteredTestUser> RegisterAndConfirmAsync(
        HttpClient client,
        TestEmailCapture emailCapture,
        string? userNamePrefix = null)
    {
        var uniqueSuffix = Guid.NewGuid().ToString("N")[..10];
        var userName = $"{userNamePrefix ?? "user"}{uniqueSuffix}";
        var email = $"{userName}@fixturely.test";
        const string password = "Password123!";

        var registerResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/register", new RegisterRequest(userName, email, password));
        registerResponse.EnsureSuccessStatusCode();

        var confirmationMessage = emailCapture.Messages
            .Last(m => m.ToEmail.Equals(email, StringComparison.OrdinalIgnoreCase)
                && m.TemplateType == EmailTemplateType.EmailConfirmation);

        var (userId, token) = EmailLinkParser.ParseUserIdAndToken(confirmationMessage);

        var confirmResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/confirm-email", new ConfirmEmailRequest(userId, token));
        confirmResponse.EnsureSuccessStatusCode();

        return new RegisteredTestUser(userId, userName, email, password);
    }

    public static async Task<(string AccessToken, string RefreshTokenCookie)> LoginAsync(
        HttpClient client,
        RegisteredTestUser user)
    {
        var loginResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/login", new LoginRequest(user.Email, user.Password));
        loginResponse.EnsureSuccessStatusCode();

        var body = await loginResponse.ReadAsAsync<LoginResponse>();
        body.Should().NotBeNull();

        var refreshCookie = loginResponse.GetSetCookieValue("fixturely_refresh_token");
        refreshCookie.Should().NotBeNullOrEmpty();

        return (body!.AccessToken, refreshCookie!);
    }
}
