using Fixturely.Application.Abstractions.Caching;
using Fixturely.Application.Abstractions.Persistence;
using Fixturely.Application.Abstractions.Security;
using Fixturely.Application.DTOs.Matches;
using Fixturely.Application.Tournaments.Bracket;
using Fixturely.Domain.Entities;
using Fixturely.Domain.Enums;
using Fixturely.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Fixturely.Application.Tournaments.Matches;

public sealed class MatchService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    private readonly ITournamentAuthorizationService _authorizationService;
    private readonly BracketProgressionService _bracketProgressionService;
    private readonly QualificationService _qualificationService;
    private readonly ICacheService _cacheService;

    public MatchService(
        IApplicationDbContext dbContext,
        TimeProvider timeProvider,
        ITournamentAuthorizationService authorizationService,
        BracketProgressionService bracketProgressionService,
        QualificationService qualificationService,
        ICacheService cacheService)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
        _authorizationService = authorizationService;
        _bracketProgressionService = bracketProgressionService;
        _qualificationService = qualificationService;
        _cacheService = cacheService;
    }

    public async Task<IReadOnlyCollection<MatchResponse>> ListAsync(
        Guid tournamentId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureCanViewAsync(tournamentId, userId, cancellationToken);

        var matches = await _dbContext.Matches
            .AsNoTracking()
            .Where(m => m.TournamentId == tournamentId)
            .ToListAsync(cancellationToken);

        var rounds = await _dbContext.TournamentRounds
            .AsNoTracking()
            .Where(r => r.TournamentId == tournamentId)
            .ToDictionaryAsync(r => r.Id, cancellationToken);

        var participants = await _dbContext.Participants
            .AsNoTracking()
            .Where(p => p.TournamentId == tournamentId)
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        return matches.Select(m => Map(m, rounds, participants)).ToList();
    }

    public async Task<MatchResponse> UpdateScoreAsync(
        Guid tournamentId,
        Guid matchId,
        Guid userId,
        UpdateMatchScoreRequest request,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureCanManageScoresAsync(tournamentId, userId, cancellationToken);

        var tournament = await _dbContext.Tournaments
            .FirstOrDefaultAsync(t => t.Id == tournamentId && !t.IsDeleted, cancellationToken)
            ?? throw new TournamentNotFoundException(tournamentId);

        if (tournament.Status is TournamentStatus.Archived or TournamentStatus.Deleted)
        {
            throw new InvalidTournamentStateException("This tournament is read-only and cannot be modified.");
        }

        var match = await _dbContext.Matches
            .FirstOrDefaultAsync(m => m.Id == matchId && m.TournamentId == tournamentId, cancellationToken)
            ?? throw new InvalidTournamentStateException("Match not found in this tournament.");

        if (match.IsBye)
        {
            throw new InvalidScoreException("BYE matches do not require score entry.");
        }

        _dbContext.SetOriginalRowVersion(match, request.RowVersion);

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        var oldWinnerId = match.WinnerParticipantId;
        var wasCompleted = match.Status == MatchStatus.Completed;

        var oldValues = $"H:{match.HomeRegularTimeScore}-A:{match.AwayRegularTimeScore}";

        match.SetRegularTimeScore(request.HomeRegularTimeScore, request.AwayRegularTimeScore, utcNow);

        if (request.HomeExtraTimeScore is not null && request.AwayExtraTimeScore is not null)
        {
            match.SetExtraTimeScore(request.HomeExtraTimeScore.Value, request.AwayExtraTimeScore.Value, utcNow);
        }

        if (request.HomePenaltyScore is not null && request.AwayPenaltyScore is not null)
        {
            match.SetPenaltyScore(request.HomePenaltyScore.Value, request.AwayPenaltyScore.Value, utcNow);
        }

        Guid? newWinnerId;

        if (match.RequiresDecisiveWinner)
        {
            newWinnerId = await ResolveKnockoutWinnerAsync(match, utcNow, cancellationToken);

            if (newWinnerId is not null)
            {
                if (wasCompleted && oldWinnerId is not null && oldWinnerId != newWinnerId
                    && !request.ConfirmDependentInvalidation)
                {
                    var allMatches = await _dbContext.Matches
                        .Where(m => m.TournamentId == tournamentId)
                        .ToListAsync(cancellationToken);

                    var decisiveLeg = allMatches
                        .Where(m => m.TieIdentifier == match.TieIdentifier)
                        .OrderByDescending(m => m.LegNumber)
                        .First();

                    var downstream = _bracketProgressionService.CollectDownstreamMatches(decisiveLeg, allMatches);

                    if (downstream.Count > 0)
                    {
                        var ids = string.Join(", ", downstream.Select(m => m.Id));
                        throw new InvalidScoreException(
                            "Correcting this result changes the winner of an already-decided tie and " +
                            $"invalidates {downstream.Count} dependent match(es): {ids}. Resubmit with " +
                            "confirmDependentInvalidation=true to apply this correction.");
                    }
                }

                match.CompleteWithWinner(newWinnerId, utcNow);

                if (wasCompleted && oldWinnerId is not null && oldWinnerId != newWinnerId)
                {
                    await InvalidateDownstreamAsync(tournamentId, match, utcNow, cancellationToken);
                }

                await PropagateWinnerAsync(match, newWinnerId.Value, utcNow, cancellationToken);
            }
        }
        else
        {
            newWinnerId = request.HomeRegularTimeScore == request.AwayRegularTimeScore
                ? null
                : request.HomeRegularTimeScore > request.AwayRegularTimeScore
                    ? match.HomeParticipantId
                    : match.AwayParticipantId;

            match.CompleteWithWinner(newWinnerId, utcNow);
        }

        _dbContext.AuditLogs.Add(AuditLog.Create(
            userId,
            tournamentId,
            "MatchScore",
            wasCompleted ? "ScoreCorrected" : "ScoreEntered",
            oldValues,
            $"H:{request.HomeRegularTimeScore}-A:{request.AwayRegularTimeScore}",
            request.Reason,
            utcNow));

        tournament.MarkInProgress(utcNow);

        if (tournament.Format == TournamentFormat.GroupKnockout && match.TournamentGroupId is not null)
        {
            await _qualificationService.TryResolveGroupQualifiersAsync(
                tournamentId, match.TournamentGroupId.Value, utcNow, cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _cacheService.RemoveAsync($"fixturely:standings:{tournamentId}", cancellationToken);
        await _cacheService.RemoveAsync($"fixturely:bracket:{tournamentId}", cancellationToken);

        var rounds = await _dbContext.TournamentRounds
            .AsNoTracking()
            .Where(r => r.TournamentId == tournamentId)
            .ToDictionaryAsync(r => r.Id, cancellationToken);

        var participants = await _dbContext.Participants
            .AsNoTracking()
            .Where(p => p.TournamentId == tournamentId)
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        return Map(match, rounds, participants);
    }

    public async Task<MatchResponse> ScheduleAsync(
        Guid tournamentId,
        Guid matchId,
        Guid userId,
        ScheduleMatchRequest request,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureIsOwnerAsync(tournamentId, userId, cancellationToken);

        var match = await _dbContext.Matches
            .FirstOrDefaultAsync(m => m.Id == matchId && m.TournamentId == tournamentId, cancellationToken)
            ?? throw new InvalidTournamentStateException("Match not found in this tournament.");

        _dbContext.SetOriginalRowVersion(match, request.RowVersion);

        match.Schedule(request.ScheduledAtUtc, request.Venue, _timeProvider.GetUtcNow().UtcDateTime);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var rounds = await _dbContext.TournamentRounds
            .AsNoTracking()
            .Where(r => r.TournamentId == tournamentId)
            .ToDictionaryAsync(r => r.Id, cancellationToken);

        var participants = await _dbContext.Participants
            .AsNoTracking()
            .Where(p => p.TournamentId == tournamentId)
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        return Map(match, rounds, participants);
    }

    public async Task InvalidateAsync(
        Guid tournamentId,
        Guid matchId,
        Guid userId,
        InvalidateMatchRequest request,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureIsOwnerAsync(tournamentId, userId, cancellationToken);

        var match = await _dbContext.Matches
            .FirstOrDefaultAsync(m => m.Id == matchId && m.TournamentId == tournamentId, cancellationToken)
            ?? throw new InvalidTournamentStateException("Match not found in this tournament.");

        _dbContext.SetOriginalRowVersion(match, request.RowVersion);

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        match.Invalidate(utcNow);

        _dbContext.AuditLogs.Add(AuditLog.Create(
            userId, tournamentId, "MatchScore", "Invalidated", null, null, request.Reason, utcNow));

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<Guid?> ResolveKnockoutWinnerAsync(Match match, DateTime utcNow, CancellationToken cancellationToken)
    {
        if (match.LegNumber == 1)
        {
            var siblingLeg2 = await _dbContext.Matches
                .FirstOrDefaultAsync(m => m.TieIdentifier == match.TieIdentifier && m.LegNumber == 2, cancellationToken);

            if (siblingLeg2 is null)
            {
                return _bracketProgressionService.EvaluateSingleLegTie(match).WinnerParticipantId;
            }

            return null;
        }

        var leg1 = await _dbContext.Matches
            .FirstOrDefaultAsync(m => m.TieIdentifier == match.TieIdentifier && m.LegNumber == 1, cancellationToken);

        return leg1 is null
            ? _bracketProgressionService.EvaluateSingleLegTie(match).WinnerParticipantId
            : _bracketProgressionService.EvaluateDoubleLegTie(leg1, match).WinnerParticipantId;
    }

    private async Task PropagateWinnerAsync(Match decisiveLeg, Guid winnerId, DateTime utcNow, CancellationToken cancellationToken)
    {
        if (decisiveLeg.NextHomeMatchId is null)
        {
            return;
        }

        var nextMatch = await _dbContext.Matches
            .FirstOrDefaultAsync(m => m.Id == decisiveLeg.NextHomeMatchId, cancellationToken);

        nextMatch?.AssignParticipant(decisiveLeg.NextHomeMatchSlotIsHome, winnerId, utcNow);
    }

    private async Task InvalidateDownstreamAsync(
        Guid tournamentId,
        Match match,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var allMatches = await _dbContext.Matches
            .Where(m => m.TournamentId == tournamentId)
            .ToListAsync(cancellationToken);

        var decisiveLeg = allMatches
            .Where(m => m.TieIdentifier == match.TieIdentifier)
            .OrderByDescending(m => m.LegNumber)
            .First();

        var downstream = _bracketProgressionService.CollectDownstreamMatches(decisiveLeg, allMatches);

        foreach (var downstreamMatch in downstream)
        {
            downstreamMatch.ClearParticipantSlot(true, utcNow);
            downstreamMatch.ClearParticipantSlot(false, utcNow);
            downstreamMatch.Invalidate(utcNow);

            _dbContext.AuditLogs.Add(AuditLog.Create(
                null,
                tournamentId,
                "MatchScore",
                "InvalidatedByDependency",
                null,
                null,
                $"Invalidated as a downstream consequence of correcting match {match.Id}.",
                utcNow));
        }
    }

    private static MatchResponse Map(
        Match match,
        Dictionary<Guid, TournamentRound> rounds,
        Dictionary<Guid, Participant> participants)
    {
        rounds.TryGetValue(match.RoundId, out var round);
        var homeName = match.HomeParticipantId is not null && participants.TryGetValue(match.HomeParticipantId.Value, out var home)
            ? home.Name
            : null;
        var awayName = match.AwayParticipantId is not null && participants.TryGetValue(match.AwayParticipantId.Value, out var away)
            ? away.Name
            : null;

        return new MatchResponse(
            match.Id,
            match.TournamentId,
            match.RoundId,
            round?.Name ?? string.Empty,
            match.TournamentGroupId,
            match.HomeParticipantId,
            homeName,
            match.AwayParticipantId,
            awayName,
            match.Status,
            match.ScheduledAtUtc,
            match.Venue,
            match.HomeRegularTimeScore,
            match.AwayRegularTimeScore,
            match.HomeExtraTimeScore,
            match.AwayExtraTimeScore,
            match.HomePenaltyScore,
            match.AwayPenaltyScore,
            match.WinnerParticipantId,
            match.LegNumber,
            match.TieIdentifier,
            match.IsBye,
            match.IsThirdPlaceMatch,
            match.NextHomeMatchId,
            match.NextAwayMatchId,
            match.RowVersion);
    }
}
