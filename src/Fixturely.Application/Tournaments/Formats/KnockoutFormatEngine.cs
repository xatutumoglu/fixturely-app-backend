using Fixturely.Application.Common;
using Fixturely.Domain.Entities;
using Fixturely.Domain.Enums;
using Fixturely.Domain.Exceptions;

namespace Fixturely.Application.Tournaments.Formats;

public sealed class KnockoutFormatEngine : ITournamentFormatEngine
{
    public TournamentFormat Format => TournamentFormat.Knockout;

    public FixtureGenerationOutput GenerateFixture(FixtureGenerationInput input)
    {
        var (rounds, matches) = BuildBracket(
            input.TournamentId,
            input.Participants.Select(p => p.Id).ToList(),
            input.LegMode,
            input.HasThirdPlaceMatch,
            input.RandomSeed,
            input.UtcNow);

        return new FixtureGenerationOutput
        {
            Groups = Array.Empty<TournamentGroup>(),
            Rounds = rounds,
            Matches = matches,
            DrawMetadataJson = "{}"
        };
    }

    public static (List<TournamentRound> Rounds, List<Match> Matches) BuildBracket(
        Guid tournamentId,
        IReadOnlyList<Guid> participantIds,
        LegMode legMode,
        bool hasThirdPlaceMatch,
        string randomSeed,
        DateTime utcNow)
    {
        if (participantIds.Count < 2)
        {
            throw new InvalidFixtureGenerationException(
                ErrorCodes.KnockoutMinParticipants, "A knockout tournament requires at least two participants.");
        }

        var bracketSize = BracketSeedOrderCalculator.NextPowerOfTwo(participantIds.Count);
        var seedOrder = BracketSeedOrderCalculator.ComputeSeedOrder(bracketSize);
        var shuffled = SecureDraw.Shuffle(participantIds, randomSeed);

        Guid? ParticipantForSeed(int seed) => seed <= shuffled.Count ? shuffled[seed - 1] : (Guid?)null;

        var round1 = TournamentRound.Create(
            tournamentId,
            1,
            "Round 1",
            RoundPhase.KnockoutRound,
            tournamentGroupId: null,
            utcNow);

        var matches = new List<Match>();
        var firstRoundTieSlots = new List<KnockoutTieSlot>();

        for (var i = 0; i < bracketSize / 2; i++)
        {
            var seedA = seedOrder[2 * i];
            var seedB = seedOrder[2 * i + 1];
            var homeId = ParticipantForSeed(seedA);
            var awayId = ParticipantForSeed(seedB);
            var isBye = homeId is null || awayId is null;

            var tieSlot = new KnockoutTieSlot();
            var leg1 = Match.CreateKnockoutMatch(
                tournamentId,
                round1.Id,
                homeId,
                awayId,
                legNumber: 1,
                tieIdentifier: tieSlot.TieId,
                isThirdPlaceMatch: false,
                bracketSlotIndex: i,
                isBye: isBye,
                utcNow: utcNow);

            tieSlot.Legs.Add(leg1);
            matches.Add(leg1);

            if (!isBye && legMode == LegMode.DoubleLeg)
            {
                var leg2 = Match.CreateKnockoutMatch(
                    tournamentId,
                    round1.Id,
                    awayId,
                    homeId,
                    legNumber: 2,
                    tieIdentifier: tieSlot.TieId,
                    isThirdPlaceMatch: false,
                    bracketSlotIndex: i,
                    isBye: false,
                    utcNow: utcNow);

                tieSlot.Legs.Add(leg2);
                matches.Add(leg2);
            }

            tieSlot.KnownWinnerId = isBye ? homeId ?? awayId : null;
            firstRoundTieSlots.Add(tieSlot);
        }

        var (laterRounds, laterMatches) = KnockoutRoundBuilder.BuildSubsequentRounds(
            tournamentId,
            firstRoundTieSlots,
            legMode,
            hasThirdPlaceMatch,
            startingRoundNumber: 2,
            utcNow);

        var rounds = new List<TournamentRound> { round1 };
        rounds.AddRange(laterRounds);
        matches.AddRange(laterMatches);

        return (rounds, matches);
    }
}
