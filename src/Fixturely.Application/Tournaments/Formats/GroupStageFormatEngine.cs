using System.Text.Json;
using Fixturely.Application.Common;
using Fixturely.Domain.Entities;
using Fixturely.Domain.Enums;
using Fixturely.Domain.Exceptions;

namespace Fixturely.Application.Tournaments.Formats;

public sealed class GroupStageFormatEngine : ITournamentFormatEngine
{
    public TournamentFormat Format => TournamentFormat.GroupStage;

    public FixtureGenerationOutput GenerateFixture(FixtureGenerationInput input)
    {
        var (groups, groupAssignments) = GroupDrawHelper.DrawGroups(input);

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

        var metadata = groups.ToDictionary(
            g => g.Name,
            g => groupAssignments[g.Id]);

        return new FixtureGenerationOutput
        {
            Groups = groups,
            Rounds = rounds,
            Matches = matches,
            DrawMetadataJson = JsonSerializer.Serialize(metadata)
        };
    }
}

internal static class GroupDrawHelper
{
    public const int ParticipantsPerGroup = Fixturely.Application.Common.ParticipantCapacity.ParticipantsPerGroup;

    public static (List<TournamentGroup> Groups, Dictionary<Guid, List<Guid>> Assignments) DrawGroups(
        FixtureGenerationInput input)
    {
        if (input.NumberOfGroups is null || input.NumberOfGroups <= 0)
        {
            throw new InvalidFixtureGenerationException("Number of groups must be provided.");
        }

        var expectedParticipantCount = input.NumberOfGroups.Value * ParticipantsPerGroup;

        if (input.Participants.Count != expectedParticipantCount)
        {
            throw new TournamentGroupCompositionException(
                $"This tournament requires exactly {expectedParticipantCount} participants " +
                $"({input.NumberOfGroups} groups x {ParticipantsPerGroup} participants per group). " +
                $"Currently there are {input.Participants.Count}.");
        }

        var shuffledParticipantIds = SecureDraw.Shuffle(input.Participants.Select(p => p.Id), input.RandomSeed);

        var groups = new List<TournamentGroup>();
        var assignments = new Dictionary<Guid, List<Guid>>();

        for (var groupIndex = 0; groupIndex < input.NumberOfGroups.Value; groupIndex++)
        {
            var groupName = $"Group {(char)('A' + groupIndex)}";
            var group = TournamentGroup.Create(input.TournamentId, groupName, groupIndex, input.UtcNow);
            groups.Add(group);

            var members = shuffledParticipantIds
                .Skip(groupIndex * ParticipantsPerGroup)
                .Take(ParticipantsPerGroup)
                .ToList();

            foreach (var participantId in members)
            {
                group.AddParticipant(participantId, input.UtcNow);
            }

            assignments[group.Id] = members;
        }

        return (groups, assignments);
    }
}
