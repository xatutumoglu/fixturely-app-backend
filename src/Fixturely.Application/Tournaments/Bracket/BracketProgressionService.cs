using Fixturely.Domain.Entities;
using Fixturely.Domain.Enums;

namespace Fixturely.Application.Tournaments.Bracket;

/// <summary>
/// Determines knockout tie winners (single leg, double-leg aggregate with no away-goals
/// rule, extra time and penalties) and propagates the result forward through the bracket,
/// including safely invalidating and clearing dependent matches when a historical result
/// is corrected.
/// </summary>
public sealed class BracketProgressionService
{
    public sealed record TieDecision(bool IsDecided, Guid? WinnerParticipantId, bool RequiresExtraTime, bool RequiresPenalties);

    public TieDecision EvaluateSingleLegTie(Match match)
    {
        if (match.HomeParticipantId is null || match.AwayParticipantId is null)
        {
            return new TieDecision(false, null, false, false);
        }

        if (match.IsRegularTimeDecisive())
        {
            var winner = match.HomeRegularTimeScore > match.AwayRegularTimeScore
                ? match.HomeParticipantId
                : match.AwayParticipantId;
            return new TieDecision(true, winner, false, false);
        }

        if (match.HomeRegularTimeScore is null || match.AwayRegularTimeScore is null)
        {
            return new TieDecision(false, null, false, false);
        }

        if (match.HomeExtraTimeScore is null || match.AwayExtraTimeScore is null)
        {
            return new TieDecision(false, null, true, false);
        }

        if (match.IsExtraTimeDecisive())
        {
            var winner = match.HomeExtraTimeScore > match.AwayExtraTimeScore
                ? match.HomeParticipantId
                : match.AwayParticipantId;
            return new TieDecision(true, winner, false, false);
        }

        if (match.HomePenaltyScore is null || match.AwayPenaltyScore is null)
        {
            return new TieDecision(false, null, false, true);
        }

        var penaltyWinner = match.HomePenaltyScore > match.AwayPenaltyScore
            ? match.HomeParticipantId
            : match.AwayParticipantId;
        return new TieDecision(true, penaltyWinner, false, false);
    }

    /// <summary>
    /// Evaluates a double-leg tie using the aggregate score across both legs. The away-goals
    /// rule is intentionally never applied. Extra time and penalties, when required, are
    /// taken exclusively from the second leg.
    /// </summary>
    public TieDecision EvaluateDoubleLegTie(Match leg1, Match leg2)
    {
        if (leg1.HomeRegularTimeScore is null || leg1.AwayRegularTimeScore is null
            || leg2.HomeRegularTimeScore is null || leg2.AwayRegularTimeScore is null)
        {
            return new TieDecision(false, null, false, false);
        }

        var teamA = leg1.HomeParticipantId;
        var teamB = leg1.AwayParticipantId;

        if (teamA is null || teamB is null)
        {
            return new TieDecision(false, null, false, false);
        }

        var teamAAggregate = leg1.HomeRegularTimeScore.Value + leg2.AwayRegularTimeScore.Value;
        var teamBAggregate = leg1.AwayRegularTimeScore.Value + leg2.HomeRegularTimeScore.Value;

        if (teamAAggregate != teamBAggregate)
        {
            return new TieDecision(true, teamAAggregate > teamBAggregate ? teamA : teamB, false, false);
        }

        if (leg2.HomeExtraTimeScore is null || leg2.AwayExtraTimeScore is null)
        {
            return new TieDecision(false, null, true, false);
        }

        if (leg2.IsExtraTimeDecisive())
        {
            var winner = leg2.HomeExtraTimeScore > leg2.AwayExtraTimeScore ? leg2.HomeParticipantId : leg2.AwayParticipantId;
            return new TieDecision(true, winner, false, false);
        }

        if (leg2.HomePenaltyScore is null || leg2.AwayPenaltyScore is null)
        {
            return new TieDecision(false, null, false, true);
        }

        var penaltyWinner = leg2.HomePenaltyScore > leg2.AwayPenaltyScore ? leg2.HomeParticipantId : leg2.AwayParticipantId;
        return new TieDecision(true, penaltyWinner, false, false);
    }

    /// <summary>
    /// Walks forward from a decided match's next-match links and returns every downstream
    /// match (recursively) whose participant slots were populated as a consequence of this
    /// match's outcome. Used to preview/apply cascading invalidation when a historical result
    /// changes the winner of an already-completed tie.
    /// </summary>
    public List<Match> CollectDownstreamMatches(Match decisiveLeg, IReadOnlyCollection<Match> allTournamentMatches)
    {
        var matchesById = allTournamentMatches.ToDictionary(m => m.Id);
        var visited = new HashSet<Guid>();
        var result = new List<Match>();

        void Visit(Guid? matchId)
        {
            if (matchId is null || !matchesById.TryGetValue(matchId.Value, out var next) || !visited.Add(next.Id))
            {
                return;
            }

            result.Add(next);

            var siblingLeg = allTournamentMatches.FirstOrDefault(m =>
                m.TieIdentifier == next.TieIdentifier && m.Id != next.Id);

            if (siblingLeg is not null && visited.Add(siblingLeg.Id))
            {
                result.Add(siblingLeg);
            }

            if (next.Status == MatchStatus.Completed)
            {
                Visit(next.NextHomeMatchId);
                Visit(next.NextAwayMatchId);
            }
        }

        Visit(decisiveLeg.NextHomeMatchId);
        Visit(decisiveLeg.NextAwayMatchId);

        return result;
    }
}
