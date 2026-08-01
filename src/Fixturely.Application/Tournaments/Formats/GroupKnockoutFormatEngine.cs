using Fixturely.Application.Common;
using Fixturely.Domain.Entities;
using Fixturely.Domain.Enums;
using Fixturely.Domain.Exceptions;

namespace Fixturely.Application.Tournaments.Formats;

public sealed class GroupKnockoutFormatEngine : ITournamentFormatEngine
{
    private static readonly int[] AllowedGroupCounts = { 2, 4, 8, 16 };

    public TournamentFormat Format => TournamentFormat.GroupKnockout;

    public FixtureGenerationOutput GenerateFixture(FixtureGenerationInput input)
    {
        if (input.NumberOfGroups is null || !AllowedGroupCounts.Contains(input.NumberOfGroups.Value))
        {
            throw new TournamentGroupCompositionException(
                ErrorCodes.GroupKnockoutInvalidGroupCount,
                "Group + knockout tournaments must use 2, 4, 8, or 16 groups of exactly four participants.");
        }

        var (groups, groupAssignments) = GroupDrawHelper.DrawGroups(input);

        var groupStageEngine = new GroupStageOnlyBuilder();
        var (groupRounds, groupMatches) = groupStageEngine.Build(input, groups, groupAssignments);

        var groupOrderShuffled = SecureDraw.Shuffle(groups.Select(g => g.OrderIndex), input.RandomSeed + ":knockout-pairing");
        var groupCount = groups.Count;

        var firstRoundRound = TournamentRound.Create(
            input.TournamentId,
            groupRounds.Count > 0 ? groupRounds.Max(r => r.RoundNumber) + 1 : 1,
            "Knockout Round 1",
            RoundPhase.KnockoutRound,
            tournamentGroupId: null,
            input.UtcNow);

        var knockoutMatches = new List<Match>();
        var firstRoundTieSlots = new List<KnockoutTieSlot>();

        for (var i = 0; i < groupCount; i++)
        {
            var winnerGroupOrderIndex = groupOrderShuffled[i];
            var runnerUpGroupOrderIndex = groupOrderShuffled[(i + 1) % groupCount];

            var tieSlot = new KnockoutTieSlot();
            var leg1 = Match.CreateKnockoutMatch(
                input.TournamentId,
                firstRoundRound.Id,
                homeParticipantId: null,
                awayParticipantId: null,
                legNumber: 1,
                tieIdentifier: tieSlot.TieId,
                isThirdPlaceMatch: false,
                bracketSlotIndex: i,
                isBye: false,
                utcNow: input.UtcNow,
                homeQualifierGroupOrderIndex: winnerGroupOrderIndex,
                homeQualifierPosition: 1,
                awayQualifierGroupOrderIndex: runnerUpGroupOrderIndex,
                awayQualifierPosition: 2);

            tieSlot.Legs.Add(leg1);
            knockoutMatches.Add(leg1);

            if (input.LegMode == LegMode.DoubleLeg)
            {
                var leg2 = Match.CreateKnockoutMatch(
                    input.TournamentId,
                    firstRoundRound.Id,
                    homeParticipantId: null,
                    awayParticipantId: null,
                    legNumber: 2,
                    tieIdentifier: tieSlot.TieId,
                    isThirdPlaceMatch: false,
                    bracketSlotIndex: i,
                    isBye: false,
                    utcNow: input.UtcNow,
                    homeQualifierGroupOrderIndex: runnerUpGroupOrderIndex,
                    homeQualifierPosition: 2,
                    awayQualifierGroupOrderIndex: winnerGroupOrderIndex,
                    awayQualifierPosition: 1);

                tieSlot.Legs.Add(leg2);
                knockoutMatches.Add(leg2);
            }

            firstRoundTieSlots.Add(tieSlot);
        }

        var (laterRounds, laterMatches) = KnockoutRoundBuilder.BuildSubsequentRounds(
            input.TournamentId,
            firstRoundTieSlots,
            input.LegMode,
            input.HasThirdPlaceMatch,
            startingRoundNumber: firstRoundRound.RoundNumber + 1,
            input.UtcNow);

        var rounds = new List<TournamentRound>(groupRounds) { firstRoundRound };
        rounds.AddRange(laterRounds);

        var matches = new List<Match>(groupMatches);
        matches.AddRange(knockoutMatches);
        matches.AddRange(laterMatches);

        var metadata = groups.ToDictionary(g => g.Name, g => groupAssignments[g.Id]);

        return new FixtureGenerationOutput
        {
            Groups = groups,
            Rounds = rounds,
            Matches = matches,
            DrawMetadataJson = System.Text.Json.JsonSerializer.Serialize(metadata)
        };
    }

    private sealed class GroupStageOnlyBuilder
    {
        public (List<TournamentRound> Rounds, List<Match> Matches) Build(
            FixtureGenerationInput input,
            List<TournamentGroup> groups,
            Dictionary<Guid, List<Guid>> groupAssignments)
        {
            var rounds = new List<TournamentRound>();
            var matches = new List<Match>();
            var roundIndexOffset = 0;

            foreach (var group in groups)
            {
                var participantIds = groupAssignments[group.Id].Select(p => (Guid?)p).ToList();

                var pairings = input.LegMode == LegMode.DoubleLeg
                    ? RoundRobinScheduler.GenerateDoubleLeg(participantIds)
                    : RoundRobinScheduler.GenerateSingleLeg(participantIds);

                var singleLegRoundCount = participantIds.Count - 1;
                var roundsByIndex = new Dictionary<int, TournamentRound>();

                foreach (var pairing in pairings)
                {
                    if (!roundsByIndex.TryGetValue(pairing.RoundIndex, out var round))
                    {
                        round = TournamentRound.Create(
                            input.TournamentId,
                            roundIndexOffset + pairing.RoundIndex + 1,
                            $"{group.Name} - Round {pairing.RoundIndex + 1}",
                            RoundPhase.GroupStage,
                            group.Id,
                            input.UtcNow);

                        roundsByIndex[pairing.RoundIndex] = round;
                        rounds.Add(round);
                    }

                    var legNumber = pairing.RoundIndex >= singleLegRoundCount ? 2 : 1;

                    matches.Add(Match.CreateLeagueOrGroupMatch(
                        input.TournamentId,
                        round.Id,
                        group.Id,
                        pairing.Home,
                        pairing.Away,
                        legNumber,
                        input.UtcNow));
                }

                roundIndexOffset += roundsByIndex.Count;
            }

            return (rounds, matches);
        }
    }
}
