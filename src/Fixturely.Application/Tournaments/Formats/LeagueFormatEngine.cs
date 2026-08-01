using Fixturely.Domain.Entities;
using Fixturely.Domain.Enums;
using Fixturely.Domain.Exceptions;

namespace Fixturely.Application.Tournaments.Formats;

public sealed class LeagueFormatEngine : ITournamentFormatEngine
{
    public TournamentFormat Format => TournamentFormat.League;

    public FixtureGenerationOutput GenerateFixture(FixtureGenerationInput input)
    {
        if (input.Participants.Count < 2)
        {
            throw new InvalidFixtureGenerationException(
                ErrorCodes.LeagueMinParticipants, "A league tournament requires at least two participants.");
        }

        var participantIds = input.Participants.Select(p => (Guid?)p.Id).ToList();

        var singleLegPairings = RoundRobinScheduler.GenerateSingleLeg(participantIds);
        var singleLegRoundsCount = singleLegPairings.Count == 0 ? 0 : singleLegPairings.Max(p => p.RoundIndex) + 1;

        var pairings = input.LegMode == LegMode.DoubleLeg
            ? RoundRobinScheduler.GenerateDoubleLeg(participantIds)
            : singleLegPairings;

        var rounds = new List<TournamentRound>();
        var matches = new List<Match>();
        var roundsByIndex = new Dictionary<int, TournamentRound>();

        foreach (var pairing in pairings)
        {
            if (!roundsByIndex.TryGetValue(pairing.RoundIndex, out var round))
            {
                round = TournamentRound.Create(
                    input.TournamentId,
                    pairing.RoundIndex + 1,
                    $"Round {pairing.RoundIndex + 1}",
                    RoundPhase.League,
                    tournamentGroupId: null,
                    input.UtcNow);

                roundsByIndex[pairing.RoundIndex] = round;
                rounds.Add(round);
            }

            var legNumber = pairing.RoundIndex >= singleLegRoundsCount ? 2 : 1;

            matches.Add(Match.CreateLeagueOrGroupMatch(
                input.TournamentId,
                round.Id,
                groupId: null,
                pairing.Home,
                pairing.Away,
                legNumber,
                input.UtcNow));
        }

        return new FixtureGenerationOutput
        {
            Groups = Array.Empty<TournamentGroup>(),
            Rounds = rounds,
            Matches = matches,
            DrawMetadataJson = "{}"
        };
    }
}
