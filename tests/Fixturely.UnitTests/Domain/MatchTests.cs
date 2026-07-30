using Fixturely.Domain.Entities;
using Fixturely.Domain.Exceptions;
using Fixturely.UnitTests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Fixturely.UnitTests.Domain;

public sealed class MatchTests
{
    private readonly DateTime _utcNow = TestEntityFactory.UtcNow;

    [Fact]
    public void CompleteWithWinner_KnockoutMatchWithoutWinner_Throws()
    {
        var match = Match.CreateKnockoutMatch(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, Guid.NewGuid(), false, 0, false, _utcNow);

        var act = () => match.CompleteWithWinner(null, _utcNow);

        act.Should().Throw<InvalidScoreException>();
    }

    [Fact]
    public void SetPenaltyScore_EqualScores_Throws()
    {
        var match = Match.CreateKnockoutMatch(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, Guid.NewGuid(), false, 0, false, _utcNow);

        var act = () => match.SetPenaltyScore(3, 3, _utcNow);

        act.Should().Throw<InvalidScoreException>();
    }

    [Fact]
    public void SetRegularTimeScore_NegativeScore_Throws()
    {
        var match = Match.CreateLeagueOrGroupMatch(
            Guid.NewGuid(), Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), 1, _utcNow);

        var act = () => match.SetRegularTimeScore(-1, 2, _utcNow);

        act.Should().Throw<InvalidScoreException>();
    }

    [Fact]
    public void CreateLeagueOrGroupMatch_WithMissingParticipant_IsMarkedAsByeAndCompleted()
    {
        var homeId = Guid.NewGuid();

        var match = Match.CreateLeagueOrGroupMatch(
            Guid.NewGuid(), Guid.NewGuid(), null, homeId, null, 1, _utcNow);

        match.IsBye.Should().BeTrue();
        match.Status.Should().Be(Fixturely.Domain.Enums.MatchStatus.Completed);
        match.WinnerParticipantId.Should().Be(homeId);
    }

    [Fact]
    public void Invalidate_ClearsScoresAndWinner()
    {
        var homeId = Guid.NewGuid();
        var awayId = Guid.NewGuid();
        var match = Match.CreateLeagueOrGroupMatch(Guid.NewGuid(), Guid.NewGuid(), null, homeId, awayId, 1, _utcNow);

        match.SetRegularTimeScore(2, 1, _utcNow);
        match.CompleteWithWinner(homeId, _utcNow);

        match.Invalidate(_utcNow);

        match.Status.Should().Be(Fixturely.Domain.Enums.MatchStatus.Invalidated);
        match.WinnerParticipantId.Should().BeNull();
        match.HomeRegularTimeScore.Should().BeNull();
    }
}
