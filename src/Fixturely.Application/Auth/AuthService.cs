using Fixturely.Application.Abstractions.Caching;
using Fixturely.Application.Abstractions.Email;
using Fixturely.Application.Abstractions.Identity;
using Fixturely.Application.Abstractions.Persistence;
using Fixturely.Application.Abstractions.Security;
using Fixturely.Application.Common;
using Fixturely.Application.DTOs.Auth;
using Fixturely.Domain.Entities;
using Fixturely.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Fixturely.Application.Auth;

public sealed class AuthService
{
    private readonly IIdentityService _identityService;
    private readonly ITokenService _tokenService;
    private readonly ISessionStore _sessionStore;
    private readonly IApplicationDbContext _dbContext;
    private readonly IEmailNotificationService _emailNotificationService;
    private readonly TimeProvider _timeProvider;
    private readonly SessionOptions _sessionOptions;
    private readonly RefreshTokenOptions _refreshTokenOptions;

    public AuthService(
        IIdentityService identityService,
        ITokenService tokenService,
        ISessionStore sessionStore,
        IApplicationDbContext dbContext,
        IEmailNotificationService emailNotificationService,
        TimeProvider timeProvider,
        IOptions<SessionOptions> sessionOptions,
        IOptions<RefreshTokenOptions> refreshTokenOptions)
    {
        _identityService = identityService;
        _tokenService = tokenService;
        _sessionStore = sessionStore;
        _dbContext = dbContext;
        _emailNotificationService = emailNotificationService;
        _timeProvider = timeProvider;
        _sessionOptions = sessionOptions.Value;
        _refreshTokenOptions = refreshTokenOptions.Value;
    }

    public async Task RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var (result, userId) = await _identityService.CreateUserAsync(
            request.UserName, request.Email, request.Password, cancellationToken);

        if (!result.Succeeded || userId is null)
        {
            throw new IdentityValidationException(result.Errors, result.Codes);
        }

        var token = await _identityService.GenerateEmailConfirmationTokenAsync(userId.Value, cancellationToken);

        await _emailNotificationService.SendEmailConfirmationAsync(
            userId.Value, request.Email, request.UserName, token, cancellationToken);
    }

    public async Task ConfirmEmailAsync(ConfirmEmailRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _identityService.ConfirmEmailAsync(request.UserId, request.Token, cancellationToken);

        if (!result.Succeeded)
        {
            throw new IdentityValidationException(result.Errors, result.Codes);
        }
    }

    public async Task ResendConfirmationAsync(
        ResendConfirmationRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await _identityService.FindByEmailAsync(request.Email, cancellationToken);

        if (user is null || user.EmailConfirmed)
        {
            return;
        }

        var token = await _identityService.GenerateEmailConfirmationTokenAsync(user.Id, cancellationToken);

        await _emailNotificationService.SendEmailConfirmationAsync(
            user.Id, user.Email, user.UserName, token, cancellationToken);
    }

    public async Task<AuthTokensResult> LoginAsync(
        LoginRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        var user = request.EmailOrUserName.Contains('@')
            ? await _identityService.FindByEmailAsync(request.EmailOrUserName, cancellationToken)
            : await _identityService.FindByUserNameAsync(request.EmailOrUserName, cancellationToken);

        if (user is null)
        {
            throw new InvalidCredentialsException();
        }

        if (!user.IsActive)
        {
            throw new AccountDisabledException();
        }

        if (!user.EmailConfirmed)
        {
            throw new EmailNotConfirmedException();
        }

        var passwordValid = await _identityService.CheckPasswordAsync(user.Id, request.Password, cancellationToken);

        if (!passwordValid)
        {
            throw new InvalidCredentialsException();
        }

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        await _identityService.SetLastLoginAsync(user.Id, utcNow, cancellationToken);

        var sessionId = Guid.NewGuid().ToString("N");

        await _sessionStore.CreateSessionAsync(
            new SessionData
            {
                UserId = user.Id,
                SessionId = sessionId,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                CreatedAtUtc = utcNow,
                LastActivityAtUtc = utcNow
            },
            TimeSpan.FromMinutes(_sessionOptions.IdleTimeoutMinutes),
            cancellationToken);

        _dbContext.UserSessions.Add(UserSession.Create(user.Id, sessionId, ipAddress, userAgent, utcNow));

        return await IssueTokensAsync(user, sessionId, ipAddress, utcNow, cancellationToken);
    }

    public async Task<AuthTokensResult> RefreshAsync(
        string rawRefreshToken,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var tokenHash = TokenHasher.Hash(rawRefreshToken);
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;

        var existingToken = await _dbContext.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (existingToken is null || !existingToken.IsActive(utcNow))
        {
            throw new InvalidRefreshTokenException();
        }

        var user = await _identityService.FindByIdAsync(existingToken.UserId, cancellationToken);

        if (user is null || !user.IsActive)
        {
            throw new InvalidRefreshTokenException();
        }

        var sessionTouched = await _sessionStore.TouchSessionAsync(
            existingToken.SessionId, utcNow, TimeSpan.FromMinutes(_sessionOptions.IdleTimeoutMinutes), cancellationToken);

        if (!sessionTouched)
        {
            throw new InvalidRefreshTokenException();
        }

        var newTokenResult = await IssueTokensAsync(user, existingToken.SessionId, ipAddress, utcNow, cancellationToken);

        existingToken.MarkUsed(TokenHasher.Hash(newTokenResult.RawRefreshToken), utcNow);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Two concurrent refresh requests raced to rotate the same refresh token (e.g. a
            // client sending duplicate silent-refresh calls on load). The loser's token row was
            // already rotated by the winner by the time this write ran, so it is now stale -
            // treat it exactly like any other already-used/invalid refresh token (401) instead
            // of surfacing a raw 500.
            throw new InvalidRefreshTokenException();
        }

        return newTokenResult;
    }

    public async Task LogoutAsync(
        Guid userId,
        string? sessionId,
        string? rawRefreshToken,
        CancellationToken cancellationToken = default)
    {
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;

        if (!string.IsNullOrEmpty(rawRefreshToken))
        {
            var tokenHash = TokenHasher.Hash(rawRefreshToken);
            var token = await _dbContext.RefreshTokens
                .FirstOrDefaultAsync(t => t.TokenHash == tokenHash && t.UserId == userId, cancellationToken);
            token?.Revoke(utcNow);
        }

        if (!string.IsNullOrEmpty(sessionId))
        {
            await _sessionStore.RemoveSessionAsync(sessionId, cancellationToken);

            var userSession = await _dbContext.UserSessions
                .FirstOrDefaultAsync(s => s.SessionId == sessionId && s.UserId == userId, cancellationToken);
            userSession?.End(utcNow);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task LogoutAllAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;

        var activeTokens = await _dbContext.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.Revoke(utcNow);
        }

        var activeSessions = await _dbContext.UserSessions
            .Where(s => s.UserId == userId && s.EndedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var session in activeSessions)
        {
            session.End(utcNow);
        }

        await _sessionStore.RemoveAllSessionsForUserAsync(userId, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _identityService.FindByEmailAsync(request.Email, cancellationToken);

        if (user is null || !user.EmailConfirmed || !user.IsActive)
        {
            return;
        }

        var token = await _identityService.GeneratePasswordResetTokenAsync(user.Id, cancellationToken);

        await _emailNotificationService.SendPasswordResetAsync(
            user.Id, user.Email, user.UserName, token, cancellationToken);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _identityService.ResetPasswordAsync(
            request.UserId, request.Token, request.NewPassword, cancellationToken);

        if (!result.Succeeded)
        {
            throw new IdentityValidationException(result.Errors, result.Codes);
        }

        await LogoutAllAsync(request.UserId, cancellationToken);
    }

    public async Task<UserProfileResponse> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _identityService.FindByIdAsync(userId, cancellationToken)
            ?? throw new InvalidCredentialsException();

        return Map(user);
    }

    private async Task<AuthTokensResult> IssueTokensAsync(
        UserRecord user,
        string sessionId,
        string? ipAddress,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var accessToken = _tokenService.GenerateAccessToken(user.Id, user.UserName, user.Email, sessionId);
        var rawRefreshToken = _tokenService.GenerateRefreshToken();
        var refreshTokenExpiresAtUtc = utcNow.AddDays(_refreshTokenOptions.RefreshTokenDays);

        _dbContext.RefreshTokens.Add(RefreshToken.Create(
            user.Id, TokenHasher.Hash(rawRefreshToken), sessionId, refreshTokenExpiresAtUtc, ipAddress, utcNow));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new AuthTokensResult(
            accessToken.Token,
            accessToken.ExpiresAtUtc,
            rawRefreshToken,
            refreshTokenExpiresAtUtc,
            Map(user));
    }

    private static UserProfileResponse Map(UserRecord user) =>
        new(user.Id, user.UserName, user.Email, user.EmailConfirmed, user.CreatedAtUtc, user.LastLoginAtUtc);
}
