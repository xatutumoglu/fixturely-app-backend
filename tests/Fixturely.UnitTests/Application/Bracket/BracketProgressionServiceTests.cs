using Fixturely.Application.Tournaments.Bracket;
using Fixturely.Domain.Entities;
using Fixturely.UnitTests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Fixturely.UnitTests.Application.Bracket;

public sealed class BracketProgressionServiceTests
{
    private readonly BracketProgressionService _service = new();
    private readonly DateTime _utcNow = TestEntityFactory.UtcNow;

    [Fact]
    public void EvaluateSingleLegTie_DecisiveRegularTime_ReturnsWinnerImmediately()
    {
        var match = CreateKnockoutMatch();
        match.SetRegularTimeScore(2, 1, _utcNow);

        var decision = _service.EvaluateSingleLegTie(match);

        decision.IsDecided.Should().BeTrue();
        decision.WinnerParticipantId.Should().Be(match.HomeParticipantId);
    }

    [Fact]
    public void EvaluateSingleLegTie_TiedRegularTime_RequiresExtraTime()
    {
        var match = CreateKnockoutMatch();
        match.SetRegularTimeScore(1, 1, _utcNow);

        var decision = _service.EvaluateSingleLegTie(match);

        decision.IsDecided.Should().BeFalse();
        decision.RequiresExtraTime.Should().BeTrue();
    }

    [Fact]
    public void EvaluateSingleLegTie_TiedExtraTime_RequiresPenalties()
    {
        var match = CreateKnockoutMatch();
        match.SetRegularTimeScore(1, 1, _utcNow);
        match.SetExtraTimeScore(0, 0, _utcNow);

        var decision = _service.EvaluateSingleLegTie(match);

        decision.IsDecided.Should().BeFalse();
        decision.RequiresPenalties.Should().BeTrue();
    }

    [Fact]
    public void EvaluateSingleLegTie_PenaltiesDecide_ReturnsPenaltyWinner()
    {
        var match = CreateKnockoutMatch();
        match.SetRegularTimeScore(1, 1, _utcNow);
        match.SetExtraTimeScore(0, 0, _utcNow);
        match.SetPenaltyScore(3, 5, _utcNow);

        var decision = _service.EvaluateSingleLegTie(match);

        decision.IsDecided.Should().BeTrue();
        decision.WinnerParticipantId.Should().Be(match.AwayParticipantId);
    }

    [Fact]
    public void EvaluateDoubleLegTie_AggregateScoreDecides_NoAwayGoalsRuleApplied()
    {
        // Leg 1: TeamA 1-1 TeamB. Leg 2: TeamB 1-1 TeamA (TeamA away).
        // Aggregate is 2-2. Away goals must NOT break the tie -> extra time required.
        var (leg1, leg2) = CreateDoubleLegTie();
        leg1.SetRegularTimeScore(1, 1, _utcNow);
        leg2.SetRegularTimeScore(1, 1, _utcNow);

        var decision = _service.EvaluateDoubleLegTie(leg1, leg2);

        decision.IsDecided.Should().BeFalse();
        decision.RequiresExtraTime.Should().BeTrue();
    }

    [Fact]
    public void EvaluateDoubleLegTie_HigherAggregateWins()
    {
        var (leg1, leg2) = CreateDoubleLegTie();
        leg1.SetRegularTimeScore(2, 0, _utcNow); // TeamA 2 - TeamB 0
        leg2.SetRegularTimeScore(1, 0, _utcNow); // TeamB 1 - TeamA 0 (away leg for TeamA)

        // Aggregate: TeamA = 2 + 0 = 2, TeamB = 0 + 1 = 1 -> TeamA wins.
        var decision = _service.EvaluateDoubleLegTie(leg1, leg2);

        decision.IsDecided.Should().BeTrue();
        decision.WinnerParticipantId.Should().Be(leg1.HomeParticipantId);
    }

    [Fact]
    public void EvaluateDoubleLegTie_ExtraTimeAndPenaltiesAppliedOnlyToSecondLeg()
    {
        var (leg1, leg2) = CreateDoubleLegTie();
        leg1.SetRegularTimeScore(1, 1, _utcNow);
        leg2.SetRegularTimeScore(1, 1, _utcNow);
        leg2.SetExtraTimeScore(0, 0, _utcNow);
        leg2.SetPenaltyScore(4, 2, _utcNow);

        var decision = _service.EvaluateDoubleLegTie(leg1, leg2);

        decision.IsDecided.Should().BeTrue();
        decision.WinnerParticipantId.Should().Be(leg2.HomeParticipantId);
    }

    [Fact]
    public void CollectDownstreamMatches_WalksForwardThroughLinkedMatches()
    {
        var tournamentId = Guid.NewGuid();
        var roundId = Guid.NewGuid();

        var semifinal = Match.CreateKnockoutMatch(
            tournamentId, roundId, Guid.NewGuid(), Guid.NewGuid(), 1, Guid.NewGuid(), false, 0, false, _utcNow);

        var final = Match.CreateKnockoutMatch(
            tournamentId, roundId, null, null, 1, Guid.NewGuid(), false, 0, false, _utcNow);

        semifinal.LinkNextMatchSlots(final.Id, true, null, false, _utcNow);

        var downstream = _service.CollectDownstreamMatches(semifinal, new[] { semifinal, final });

        downstream.Should().Contain(final);
    }

    private Match CreateKnockoutMatch()
    {
        return Match.CreateKnockoutMatch(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, Guid.NewGuid(), false, 0, false, _utcNow);
    }

    private (Match Leg1, Match Leg2) CreateDoubleLegTie()
    {
        var tournamentId = Guid.NewGuid();
        var roundId = Guid.NewGuid();
        var tieId = Guid.NewGuid();
        var teamA = Guid.NewGuid();
        var teamB = Guid.NewGuid();

        var leg1 = Match.CreateKnockoutMatch(tournamentId, roundId, teamA, teamB, 1, tieId, false, 0, false, _utcNow);
        var leg2 = Match.CreateKnockoutMatch(tournamentId, roundId, teamB, teamA, 2, tieId, false, 0, false, _utcNow);

        return (leg1, leg2);
    }
}
