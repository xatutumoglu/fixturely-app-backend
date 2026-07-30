using Fixturely.Domain.Entities;
using Fixturely.Domain.Enums;

namespace Fixturely.Application.Tournaments.Formats;

public sealed class KnockoutTieSlot
{
    public Guid TieId { get; init; } = Guid.NewGuid();

    public List<Match> Legs { get; } = new();

    public Guid? KnownWinnerId { get; set; }
}

/// <summary>
/// Builds knockout rounds (round 2 onward, including the optional third-place match)
/// from an already-resolved list of first round tie slots. Shared between the pure
/// knockout format engine and the knockout phase of the group+knockout format engine.
/// </summary>
public static class KnockoutRoundBuilder
{
    public static (List<TournamentRound> Rounds, List<Match> Matches) BuildSubsequentRounds(
        Guid tournamentId,
        List<KnockoutTieSlot> firstRoundTieSlots,
        LegMode legMode,
        bool hasThirdPlaceMatch,
        int startingRoundNumber,
        DateTime utcNow)
    {
        var rounds = new List<TournamentRound>();
        var matches = new List<Match>();
        var currentTieSlots = firstRoundTieSlots;
        var roundNumber = startingRoundNumber;
        List<KnockoutTieSlot>? semifinalTieSlots = null;

        while (currentTieSlots.Count > 1)
        {
            if (currentTieSlots.Count == 2)
            {
                semifinalTieSlots = currentTieSlots;
            }

            var round = TournamentRound.Create(
                tournamentId,
                roundNumber,
                currentTieSlots.Count == 2 ? "Final" : $"Round {roundNumber}",
                currentTieSlots.Count == 2 ? RoundPhase.Final : RoundPhase.KnockoutRound,
                tournamentGroupId: null,
                utcNow);
            rounds.Add(round);

            var nextTieSlots = new List<KnockoutTieSlot>();

            for (var i = 0; i < currentTieSlots.Count / 2; i++)
            {
                var tieA = currentTieSlots[2 * i];
                var tieB = currentTieSlots[2 * i + 1];

                var homeId = tieA.KnownWinnerId;
                var awayId = tieB.KnownWinnerId;

                var tieSlot = new KnockoutTieSlot();
                var leg1 = Match.CreateKnockoutMatch(
                    tournamentId,
                    round.Id,
                    homeId,
                    awayId,
                    legNumber: 1,
                    tieIdentifier: tieSlot.TieId,
                    isThirdPlaceMatch: false,
                    bracketSlotIndex: i,
                    isBye: false,
                    utcNow: utcNow);

                tieSlot.Legs.Add(leg1);
                matches.Add(leg1);

                var decisiveLegA = tieA.Legs[^1];
                var decisiveLegB = tieB.Legs[^1];
                decisiveLegA.LinkNextMatchSlots(
                    leg1.Id, true, decisiveLegA.NextAwayMatchId, decisiveLegA.NextAwayMatchSlotIsHome, utcNow);
                decisiveLegB.LinkNextMatchSlots(
                    leg1.Id, false, decisiveLegB.NextAwayMatchId, decisiveLegB.NextAwayMatchSlotIsHome, utcNow);

                if (legMode == LegMode.DoubleLeg)
                {
                    var leg2 = Match.CreateKnockoutMatch(
                        tournamentId,
                        round.Id,
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

                nextTieSlots.Add(tieSlot);
            }

            currentTieSlots = nextTieSlots;
            roundNumber++;
        }

        if (hasThirdPlaceMatch && semifinalTieSlots is { Count: 2 })
        {
            var thirdPlaceRound = TournamentRound.Create(
                tournamentId,
                roundNumber,
                "Third Place",
                RoundPhase.ThirdPlace,
                tournamentGroupId: null,
                utcNow);
            rounds.Add(thirdPlaceRound);

            var tieId = Guid.NewGuid();
            var thirdPlaceLeg1 = Match.CreateKnockoutMatch(
                tournamentId,
                thirdPlaceRound.Id,
                homeParticipantId: null,
                awayParticipantId: null,
                legNumber: 1,
                tieIdentifier: tieId,
                isThirdPlaceMatch: true,
                bracketSlotIndex: 0,
                isBye: false,
                utcNow: utcNow);
            matches.Add(thirdPlaceLeg1);

            for (var i = 0; i < semifinalTieSlots.Count; i++)
            {
                var decisiveLeg = semifinalTieSlots[i].Legs[^1];
                decisiveLeg.LinkNextMatchSlots(
                    decisiveLeg.NextHomeMatchId,
                    decisiveLeg.NextHomeMatchSlotIsHome,
                    thirdPlaceLeg1.Id,
                    i == 0,
                    utcNow);
            }

            if (legMode == LegMode.DoubleLeg)
            {
                var thirdPlaceLeg2 = Match.CreateKnockoutMatch(
                    tournamentId,
                    thirdPlaceRound.Id,
                    homeParticipantId: null,
                    awayParticipantId: null,
                    legNumber: 2,
                    tieIdentifier: tieId,
                    isThirdPlaceMatch: true,
                    bracketSlotIndex: 0,
                    isBye: false,
                    utcNow: utcNow);
                matches.Add(thirdPlaceLeg2);
            }
        }

        return (rounds, matches);
    }
}
