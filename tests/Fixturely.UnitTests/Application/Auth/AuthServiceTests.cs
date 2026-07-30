using Fixturely.Application.Abstractions.Caching;
using Fixturely.Application.Abstractions.Email;
using Fixturely.Application.Abstractions.Identity;
using Fixturely.Application.Abstractions.Security;
using Fixturely.Application.Auth;
using Fixturely.Application.Common;
using Fixturely.Application.DTOs.Auth;
using Fixturely.Domain.Exceptions;
using Fixturely.UnitTests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Fixturely.UnitTests.Application.Auth;

public sealed class AuthServiceTests
{
    private readonly Mock<IIdentityService> _identityService = new();
    private readonly Mock<ITokenService> _tokenService = new();
    private readonly Mock<ISessionStore> _sessionStore = new();
    private readonly Mock<IEmailNotificationService> _emailNotificationService = new();
    private readonly FixedTimeProvider _timeProvider = new(TestEntityFactory.UtcNow);

    private AuthService CreateService(Fixturely.Infrastructure.Persistence.ApplicationDbContext dbContext)
    {
        return new AuthService(
            _identityService.Object,
            _tokenService.Object,
            _sessionStore.Object,
            dbContext,
            _emailNotificationService.Object,
            _timeProvider,
            Options.Create(new SessionOptions { IdleTimeoutMinutes = 15 }),
            Options.Create(new RefreshTokenOptions { RefreshTokenDays = 7 }));
    }

    private UserRecord CreateConfirmedUser(Guid userId) => new()
    {
        Id = userId,
        UserName = "player1",
        Email = "player1@fixturely.test",
        EmailConfirmed = true,
        IsActive = true,
        CreatedAtUtc = TestEntityFactory.UtcNow,
        LastLoginAtUtc = null
    };

    [Fact]
    public async Task LoginAsync_WhenEmailNotConfirmed_ThrowsEmailNotConfirmedException()
    {
        using var dbContext = InMemoryDbContextFactory.Create();
        var service = CreateService(dbContext);
        var userId = Guid.NewGuid();

        _identityService.Setup(s => s.FindByEmailAsync("player1@fixturely.test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserRecord
            {
                Id = userId,
                UserName = "player1",
                Email = "player1@fixturely.test",
                EmailConfirmed = false,
                IsActive = true,
                CreatedAtUtc = TestEntityFactory.UtcNow
            });

        var act = () => service.LoginAsync(
            new LoginRequest("player1@fixturely.test", "Password123!"), "127.0.0.1", "test-agent", CancellationToken.None);

        await act.Should().ThrowAsync<EmailNotConfirmedException>();
    }

    [Fact]
    public async Task LoginAsync_WhenAccountDisabled_ThrowsAccountDisabledException()
    {
        using var dbContext = InMemoryDbContextFactory.Create();
        var service = CreateService(dbContext);
        var userId = Guid.NewGuid();

        _identityService.Setup(s => s.FindByEmailAsync("player1@fixturely.test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserRecord
            {
                Id = userId,
                UserName = "player1",
                Email = "player1@fixturely.test",
                EmailConfirmed = true,
                IsActive = false,
                CreatedAtUtc = TestEntityFactory.UtcNow
            });

        var act = () => service.LoginAsync(
            new LoginRequest("player1@fixturely.test", "Password123!"), "127.0.0.1", "test-agent", CancellationToken.None);

        await act.Should().ThrowAsync<AccountDisabledException>();
    }

    [Fact]
    public async Task LoginAsync_WhenPasswordInvalid_ThrowsInvalidCredentialsException()
    {
        using var dbContext = InMemoryDbContextFactory.Create();
        var service = CreateService(dbContext);
        var userId = Guid.NewGuid();

        _identityService.Setup(s => s.FindByEmailAsync("player1@fixturely.test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateConfirmedUser(userId));
        _identityService.Setup(s => s.CheckPasswordAsync(userId, "WrongPassword", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var act = () => service.LoginAsync(
            new LoginRequest("player1@fixturely.test", "WrongPassword"), "127.0.0.1", "test-agent", CancellationToken.None);

        await act.Should().ThrowAsync<InvalidCredentialsException>();
    }

    [Fact]
    public async Task LoginAsync_WithValidCredentials_IssuesAccessAndRefreshToken()
    {
        using var dbContext = InMemoryDbContextFactory.Create();
        var service = CreateService(dbContext);
        var userId = Guid.NewGuid();

        _identityService.Setup(s => s.FindByEmailAsync("player1@fixturely.test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateConfirmedUser(userId));
        _identityService.Setup(s => s.CheckPasswordAsync(userId, "Password123!", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _tokenService.Setup(t => t.GenerateAccessToken(userId, "player1", "player1@fixturely.test", It.IsAny<string>()))
            .Returns(new AccessTokenResult { Token = "access-token", ExpiresAtUtc = TestEntityFactory.UtcNow.AddMinutes(15) });
        _tokenService.Setup(t => t.GenerateRefreshToken()).Returns("raw-refresh-token");

        var result = await service.LoginAsync(
            new LoginRequest("player1@fixturely.test", "Password123!"), "127.0.0.1", "test-agent", CancellationToken.None);

        result.AccessToken.Should().Be("access-token");
        result.RawRefreshToken.Should().Be("raw-refresh-token");

        var storedToken = dbContext.RefreshTokens.Single();
        storedToken.UserId.Should().Be(userId);
        storedToken.TokenHash.Should().Be(TokenHasher.Hash("raw-refresh-token"));

        _sessionStore.Verify(
            s => s.CreateSessionAsync(It.IsAny<SessionData>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RefreshAsync_RotatesToken_MarkingOldTokenUsedAndCreatingNewOne()
    {
        using var dbContext = InMemoryDbContextFactory.Create();
        var service = CreateService(dbContext);
        var userId = Guid.NewGuid();

        _identityService.Setup(s => s.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateConfirmedUser(userId));
        _identityService.Setup(s => s.CheckPasswordAsync(userId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _identityService.Setup(s => s.FindByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateConfirmedUser(userId));
        _tokenService.SetupSequence(t => t.GenerateRefreshToken())
            .Returns("first-refresh-token")
            .Returns("second-refresh-token");
        _tokenService.Setup(t => t.GenerateAccessToken(userId, "player1", "player1@fixturely.test", It.IsAny<string>()))
            .Returns(new AccessTokenResult { Token = "access-token", ExpiresAtUtc = TestEntityFactory.UtcNow.AddMinutes(15) });
        _sessionStore.Setup(s => s.TouchSessionAsync(
                It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var loginResult = await service.LoginAsync(
            new LoginRequest("player1@fixturely.test", "Password123!"), "127.0.0.1", "test-agent", CancellationToken.None);

        var refreshResult = await service.RefreshAsync(loginResult.RawRefreshToken, "127.0.0.1", CancellationToken.None);

        refreshResult.RawRefreshToken.Should().Be("second-refresh-token");
        refreshResult.RawRefreshToken.Should().NotBe(loginResult.RawRefreshToken);

        var originalToken = dbContext.RefreshTokens
            .Single(t => t.TokenHash == TokenHasher.Hash(loginResult.RawRefreshToken));
        originalToken.IsActive(_timeProvider.GetUtcNow().UtcDateTime).Should().BeFalse();

        dbContext.RefreshTokens.Count().Should().Be(2);
    }

    [Fact]
    public async Task RefreshAsync_WhenSessionExpired_ThrowsInvalidRefreshTokenException()
    {
        using var dbContext = InMemoryDbContextFactory.Create();
        var service = CreateService(dbContext);
        var userId = Guid.NewGuid();

        _identityService.Setup(s => s.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateConfirmedUser(userId));
        _identityService.Setup(s => s.CheckPasswordAsync(userId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _identityService.Setup(s => s.FindByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateConfirmedUser(userId));
        _tokenService.Setup(t => t.GenerateRefreshToken()).Returns("first-refresh-token");
        _tokenService.Setup(t => t.GenerateAccessToken(userId, "player1", "player1@fixturely.test", It.IsAny<string>()))
            .Returns(new AccessTokenResult { Token = "access-token", ExpiresAtUtc = TestEntityFactory.UtcNow.AddMinutes(15) });

        var loginResult = await service.LoginAsync(
            new LoginRequest("player1@fixturely.test", "Password123!"), "127.0.0.1", "test-agent", CancellationToken.None);

        // Simulate the Redis session having expired due to the sliding idle timeout.
        _sessionStore.Setup(s => s.TouchSessionAsync(
                It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var act = () => service.RefreshAsync(loginResult.RawRefreshToken, "127.0.0.1", CancellationToken.None);

        await act.Should().ThrowAsync<InvalidRefreshTokenException>();
    }

    [Fact]
    public async Task ResetPasswordAsync_InvalidatesAllSessionsAndRevokesAllRefreshTokens()
    {
        using var dbContext = InMemoryDbContextFactory.Create();
        var service = CreateService(dbContext);
        var userId = Guid.NewGuid();

        _identityService.Setup(s => s.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateConfirmedUser(userId));
        _identityService.Setup(s => s.CheckPasswordAsync(userId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _tokenService.Setup(t => t.GenerateRefreshToken()).Returns("refresh-token");
        _tokenService.Setup(t => t.GenerateAccessToken(userId, "player1", "player1@fixturely.test", It.IsAny<string>()))
            .Returns(new AccessTokenResult { Token = "access-token", ExpiresAtUtc = TestEntityFactory.UtcNow.AddMinutes(15) });

        await service.LoginAsync(
            new LoginRequest("player1@fixturely.test", "Password123!"), "127.0.0.1", "test-agent", CancellationToken.None);

        _identityService.Setup(s => s.ResetPasswordAsync(userId, "reset-token", "NewPassword123!", It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityOperationResult.Success());

        await service.ResetPasswordAsync(
            new ResetPasswordRequest(userId, "reset-token", "NewPassword123!"), CancellationToken.None);

        dbContext.RefreshTokens.Should().OnlyContain(t => t.RevokedAtUtc != null);
        dbContext.UserSessions.Should().OnlyContain(s => s.EndedAtUtc != null);

        _sessionStore.Verify(s => s.RemoveAllSessionsForUserAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
