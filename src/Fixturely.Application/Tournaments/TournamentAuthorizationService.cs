using Fixturely.Application.Abstractions.Persistence;
using Fixturely.Application.Abstractions.Security;
using Fixturely.Domain.Enums;
using Fixturely.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Fixturely.Application.Tournaments;

public sealed class TournamentAuthorizationService : ITournamentAuthorizationService
{
    private readonly IApplicationDbContext _dbContext;

    public TournamentAuthorizationService(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<TournamentMemberRole?> GetRoleAsync(
        Guid tournamentId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var member = await _dbContext.TournamentMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(
                m => m.TournamentId == tournamentId
                    && m.UserId == userId
                    && m.Status == TournamentMemberStatus.Active,
                cancellationToken);

        return member?.Role;
    }

    public async Task EnsureCanViewAsync(Guid tournamentId, Guid userId, CancellationToken cancellationToken = default)
    {
        var role = await GetRoleAsync(tournamentId, userId, cancellationToken);

        if (role is null)
        {
            throw new UnauthorizedTournamentAccessException(tournamentId, userId);
        }
    }

    public async Task EnsureCanManageScoresAsync(
        Guid tournamentId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var role = await GetRoleAsync(tournamentId, userId, cancellationToken);

        if (role is not (TournamentMemberRole.Owner or TournamentMemberRole.ScoreManager))
        {
            throw new UnauthorizedTournamentAccessException(tournamentId, userId);
        }
    }

    public async Task EnsureIsOwnerAsync(Guid tournamentId, Guid userId, CancellationToken cancellationToken = default)
    {
        var role = await GetRoleAsync(tournamentId, userId, cancellationToken);

        if (role != TournamentMemberRole.Owner)
        {
            throw new UnauthorizedTournamentAccessException(tournamentId, userId);
        }
    }
}
