using Fixturely.Domain.Enums;

namespace Fixturely.Application.Abstractions.Security;

public sealed class AccessTokenResult
{
    public required string Token { get; init; }

    public required DateTime ExpiresAtUtc { get; init; }
}

public interface ITokenService
{
    AccessTokenResult GenerateAccessToken(Guid userId, string userName, string email, string sessionId);

    string GenerateRefreshToken();
}

public interface ICurrentUserService
{
    Guid? UserId { get; }

    bool IsAuthenticated { get; }

    string? SessionId { get; }

    string? IpAddress { get; }

    string? UserAgent { get; }
}

public interface ITournamentAuthorizationService
{
    Task<TournamentMemberRole?> GetRoleAsync(
        Guid tournamentId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task EnsureCanViewAsync(Guid tournamentId, Guid userId, CancellationToken cancellationToken = default);

    Task EnsureCanManageScoresAsync(Guid tournamentId, Guid userId, CancellationToken cancellationToken = default);

    Task EnsureIsOwnerAsync(Guid tournamentId, Guid userId, CancellationToken cancellationToken = default);
}
