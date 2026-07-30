using Fixturely.Application.Abstractions.Caching;
using Fixturely.Application.Abstractions.Persistence;
using Fixturely.Application.Abstractions.Security;
using Fixturely.Application.DTOs.Common;
using Fixturely.Domain.Entities;
using Fixturely.Domain.Enums;
using Fixturely.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Fixturely.Application.Tournaments.Standings;

public sealed class TournamentQueryService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ITournamentAuthorizationService _authorizationService;
    private readonly StandingsCalculationService _standingsCalculationService;
    private readonly ICacheService _cacheService;

    public TournamentQueryService(
        IApplicationDbContext dbContext,
        ITournamentAuthorizationService authorizationService,
        StandingsCalculationService standingsCalculationService,
        ICacheService cacheService)
    {
        _dbContext = dbContext;
        _authorizationService = authorizationService;
        _standingsCalculationService = standingsCalculationService;
        _cacheService = cacheService;
    }

    public async Task<IReadOnlyCollection<GroupStandingsResponse>> GetStandingsAsync(
        Guid tournamentId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureCanViewAsync(tournamentId, userId, cancellationToken);

        var tournament = await _dbContext.Tournaments
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tournamentId && !t.IsDeleted, cancellationToken)
            ?? throw new TournamentNotFoundException(tournamentId);

        var participants = await _dbContext.Participants
            .AsNoTracking()
            .Where(p => p.TournamentId == tournamentId && !p.IsDeleted)
            .ToListAsync(cancellationToken);

        var matches = await _dbContext.Matches
            .AsNoTracking()
            .Where(m => m.TournamentId == tournamentId && m.Status == MatchStatus.Completed)
            .ToListAsync(cancellationToken);

        if (tournament.Format is TournamentFormat.League)
        {
            var standings = _standingsCalculationService.Calculate(participants, matches);
            return new[] { new GroupStandingsResponse(Guid.Empty, tournament.Name, standings) };
        }

        var groups = await _dbContext.TournamentGroups
            .AsNoTracking()
            .Include(g => g.GroupParticipants)
            .Where(g => g.TournamentId == tournamentId)
            .OrderBy(g => g.OrderIndex)
            .ToListAsync(cancellationToken);

        var result = new List<GroupStandingsResponse>();

        foreach (var group in groups)
        {
            var groupParticipantIds = group.GroupParticipants.Select(gp => gp.ParticipantId).ToHashSet();
            var groupParticipants = participants.Where(p => groupParticipantIds.Contains(p.Id)).ToList();
            var groupMatches = matches.Where(m => m.TournamentGroupId == group.Id).ToList();

            var standings = _standingsCalculationService.Calculate(groupParticipants, groupMatches);
            result.Add(new GroupStandingsResponse(group.Id, group.Name, standings));
        }

        return result;
    }

    public async Task<IReadOnlyCollection<RoundResponse>> GetRoundsAsync(
        Guid tournamentId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureCanViewAsync(tournamentId, userId, cancellationToken);

        var rounds = await _dbContext.TournamentRounds
            .AsNoTracking()
            .Where(r => r.TournamentId == tournamentId)
            .OrderBy(r => r.RoundNumber)
            .ToListAsync(cancellationToken);

        return rounds
            .Select(r => new RoundResponse(r.Id, r.RoundNumber, r.Name, r.Phase.ToString(), r.TournamentGroupId))
            .ToList();
    }

    public async Task<BracketResponse> GetBracketAsync(
        Guid tournamentId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureCanViewAsync(tournamentId, userId, cancellationToken);

        var rounds = await _dbContext.TournamentRounds
            .AsNoTracking()
            .Where(r => r.TournamentId == tournamentId
                && (r.Phase == RoundPhase.KnockoutRound || r.Phase == RoundPhase.Final || r.Phase == RoundPhase.ThirdPlace))
            .ToDictionaryAsync(r => r.Id, cancellationToken);

        var matches = await _dbContext.Matches
            .AsNoTracking()
            .Where(m => m.TournamentId == tournamentId && rounds.Keys.Contains(m.RoundId))
            .ToListAsync(cancellationToken);

        var participants = await _dbContext.Participants
            .AsNoTracking()
            .Where(p => p.TournamentId == tournamentId)
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var nodes = matches
            .OrderBy(m => rounds[m.RoundId].RoundNumber)
            .ThenBy(m => m.BracketSlotIndex)
            .ThenBy(m => m.LegNumber)
            .Select(m => new BracketMatchNode(
                m.Id,
                rounds[m.RoundId].RoundNumber,
                rounds[m.RoundId].Name,
                m.HomeParticipantId,
                m.HomeParticipantId is not null && participants.TryGetValue(m.HomeParticipantId.Value, out var home) ? home.Name : null,
                m.AwayParticipantId,
                m.AwayParticipantId is not null && participants.TryGetValue(m.AwayParticipantId.Value, out var away) ? away.Name : null,
                m.WinnerParticipantId,
                m.IsThirdPlaceMatch,
                m.NextHomeMatchId,
                m.NextAwayMatchId))
            .ToList();

        return new BracketResponse(nodes);
    }

    public async Task<IReadOnlyCollection<AuditLogResponse>> GetAuditLogsAsync(
        Guid tournamentId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureCanViewAsync(tournamentId, userId, cancellationToken);

        var logs = await _dbContext.AuditLogs
            .AsNoTracking()
            .Where(a => a.TournamentId == tournamentId)
            .OrderByDescending(a => a.OccurredAtUtc)
            .Take(200)
            .ToListAsync(cancellationToken);

        return logs
            .Select(l => new AuditLogResponse(l.Id, l.UserId, l.Category, l.Action, l.Reason, l.OccurredAtUtc))
            .ToList();
    }

    public async Task<IReadOnlyCollection<TournamentGroup>> GetGroupsAsync(
        Guid tournamentId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureCanViewAsync(tournamentId, userId, cancellationToken);

        return await _dbContext.TournamentGroups
            .AsNoTracking()
            .Include(g => g.GroupParticipants)
            .Where(g => g.TournamentId == tournamentId)
            .OrderBy(g => g.OrderIndex)
            .ToListAsync(cancellationToken);
    }
}
