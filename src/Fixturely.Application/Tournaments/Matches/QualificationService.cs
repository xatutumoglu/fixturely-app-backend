using Fixturely.Application.Abstractions.Persistence;
using Fixturely.Application.Tournaments.Standings;
using Fixturely.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Fixturely.Application.Tournaments.Matches;

/// <summary>
/// Once every match in a group has been completed, resolves the group's top two finishers
/// into the knockout matches that were generated with an unresolved qualifier reference
/// (see <see cref="Fixturely.Domain.Entities.Match.HomeQualifierGroupOrderIndex"/>).
/// </summary>
public sealed class QualificationService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly StandingsCalculationService _standingsCalculationService;

    public QualificationService(
        IApplicationDbContext dbContext,
        StandingsCalculationService standingsCalculationService)
    {
        _dbContext = dbContext;
        _standingsCalculationService = standingsCalculationService;
    }

    public async Task TryResolveGroupQualifiersAsync(
        Guid tournamentId,
        Guid tournamentGroupId,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var group = await _dbContext.TournamentGroups
            .Include(g => g.GroupParticipants)
            .FirstOrDefaultAsync(g => g.Id == tournamentGroupId, cancellationToken);

        if (group is null)
        {
            return;
        }

        var groupMatches = await _dbContext.Matches
            .Where(m => m.TournamentGroupId == tournamentGroupId)
            .ToListAsync(cancellationToken);

        var allDecided = groupMatches.All(m => m.Status is MatchStatus.Completed or MatchStatus.Invalidated);

        if (!allDecided || groupMatches.Count == 0)
        {
            return;
        }

        var participantIds = group.GroupParticipants.Select(gp => gp.ParticipantId).ToList();
        var participants = await _dbContext.Participants
            .Where(p => participantIds.Contains(p.Id))
            .ToListAsync(cancellationToken);

        var completedMatches = groupMatches.Where(m => m.Status == MatchStatus.Completed).ToList();
        var standings = _standingsCalculationService.Calculate(participants, completedMatches);

        if (standings.Count < 2)
        {
            return;
        }

        var winnerId = standings[0].ParticipantId;
        var runnerUpId = standings[1].ParticipantId;

        var pendingSlots = await _dbContext.Matches
            .Where(m => m.TournamentId == tournamentId
                && (m.HomeQualifierGroupOrderIndex == group.OrderIndex
                    || m.AwayQualifierGroupOrderIndex == group.OrderIndex))
            .ToListAsync(cancellationToken);

        foreach (var match in pendingSlots)
        {
            if (match.HomeQualifierGroupOrderIndex == group.OrderIndex)
            {
                var participantId = match.HomeQualifierPosition == 1 ? winnerId : runnerUpId;
                match.ResolveQualifierSlot(true, participantId, utcNow);
            }

            if (match.AwayQualifierGroupOrderIndex == group.OrderIndex)
            {
                var participantId = match.AwayQualifierPosition == 1 ? winnerId : runnerUpId;
                match.ResolveQualifierSlot(false, participantId, utcNow);
            }
        }
    }
}
