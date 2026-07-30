using Fixturely.Domain.Entities;
using Fixturely.Domain.Enums;

namespace Fixturely.UnitTests.TestHelpers;

public static class MatchTestFactory
{
    public static Match CreateCompletedMatch(
        Guid tournamentId,
        Guid roundId,
        Guid homeParticipantId,
        Guid awayParticipantId,
        int homeScore,
        int awayScore,
        DateTime utcNow,
        Guid? groupId = null)
    {
        var match = Match.CreateLeagueOrGroupMatch(
            tournamentId, roundId, groupId, homeParticipantId, awayParticipantId, legNumber: 1, utcNow);

        match.SetRegularTimeScore(homeScore, awayScore, utcNow);

        var winnerId = homeScore == awayScore
            ? (Guid?)null
            : homeScore > awayScore ? homeParticipantId : awayParticipantId;

        match.CompleteWithWinner(winnerId, utcNow);
        return match;
    }
}
