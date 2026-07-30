using Fixturely.Application.Tournaments.Standings;
using Fixturely.UnitTests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Fixturely.UnitTests.Application.Standings;

public sealed class StandingsCalculationServiceTests
{
    private readonly StandingsCalculationService _service = new(new TieBreakerService());

    [Fact]
    public void Calculate_AwardsThreePointsForWinOnePointForDraw()
    {
        var tournamentId = Guid.NewGuid();
        var roundId = Guid.NewGuid();
        var participants = TestEntityFactory.CreateParticipants(tournamentId, 3);
        var utcNow = TestEntityFactory.UtcNow;

        var matches = new[]
        {
            MatchTestFactory.CreateCompletedMatch(tournamentId, roundId, participants[0].Id, participants[1].Id, 2, 0, utcNow),
            MatchTestFactory.CreateCompletedMatch(tournamentId, roundId, participants[1].Id, participants[2].Id, 1, 1, utcNow),
            MatchTestFactory.CreateCompletedMatch(tournamentId, roundId, participants[2].Id, participants[0].Id, 0, 3, utcNow)
        };

        var standings = _service.Calculate(participants, matches);

        var team1 = standings.Single(s => s.ParticipantId == participants[0].Id);
        var team2 = standings.Single(s => s.ParticipantId == participants[1].Id);
        var team3 = standings.Single(s => s.ParticipantId == participants[2].Id);

        team1.Points.Should().Be(6);
        team1.Won.Should().Be(2);
        team2.Points.Should().Be(1);
        team2.Drawn.Should().Be(1);
        team3.Points.Should().Be(1);
    }

    [Fact]
    public void Calculate_TwoParticipantTie_ResolvedByHeadToHeadResult()
    {
        var tournamentId = Guid.NewGuid();
        var roundId = Guid.NewGuid();
        var participants = TestEntityFactory.CreateParticipants(tournamentId, 3);
        var utcNow = TestEntityFactory.UtcNow;

        // Team1 and Team2 both finish with 4 points, but Team1 beat Team2 head-to-head.
        var matches = new[]
        {
            MatchTestFactory.CreateCompletedMatch(tournamentId, roundId, participants[0].Id, participants[1].Id, 1, 0, utcNow),
            MatchTestFactory.CreateCompletedMatch(tournamentId, roundId, participants[0].Id, participants[2].Id, 0, 1, utcNow),
            MatchTestFactory.CreateCompletedMatch(tournamentId, roundId, participants[1].Id, participants[2].Id, 3, 0, utcNow),
            MatchTestFactory.CreateCompletedMatch(tournamentId, roundId, participants[2].Id, participants[0].Id, 0, 2, utcNow),
            MatchTestFactory.CreateCompletedMatch(tournamentId, roundId, participants[1].Id, participants[0].Id, 0, 0, utcNow),
            MatchTestFactory.CreateCompletedMatch(tournamentId, roundId, participants[2].Id, participants[1].Id, 1, 2, utcNow)
        };

        var standings = _service.Calculate(participants, matches);

        var team1Position = standings.Single(s => s.ParticipantId == participants[0].Id).Position;
        var team2Position = standings.Single(s => s.ParticipantId == participants[1].Id).Position;

        var team1Points = standings.Single(s => s.ParticipantId == participants[0].Id).Points;
        var team2Points = standings.Single(s => s.ParticipantId == participants[1].Id).Points;

        team1Points.Should().Be(team2Points);
        team1Position.Should().BeLessThan(team2Position);
    }

    [Fact]
    public void Calculate_ThreeWayTie_UsesMiniTableAmongTiedParticipants()
    {
        var tournamentId = Guid.NewGuid();
        var roundId = Guid.NewGuid();
        var participants = TestEntityFactory.CreateParticipants(tournamentId, 4);
        var utcNow = TestEntityFactory.UtcNow;

        // Teams 0, 1, 2 all finish level on points; Team 0 wins the mini-league,
        // Team 1 has the better mini-league record than Team 2.
        var matches = new List<Fixturely.Domain.Entities.Match>
        {
            MatchTestFactory.CreateCompletedMatch(tournamentId, roundId, participants[0].Id, participants[1].Id, 2, 0, utcNow),
            MatchTestFactory.CreateCompletedMatch(tournamentId, roundId, participants[1].Id, participants[2].Id, 2, 0, utcNow),
            MatchTestFactory.CreateCompletedMatch(tournamentId, roundId, participants[2].Id, participants[0].Id, 0, 0, utcNow),
            MatchTestFactory.CreateCompletedMatch(tournamentId, roundId, participants[0].Id, participants[3].Id, 0, 0, utcNow),
            MatchTestFactory.CreateCompletedMatch(tournamentId, roundId, participants[1].Id, participants[3].Id, 0, 0, utcNow),
            MatchTestFactory.CreateCompletedMatch(tournamentId, roundId, participants[2].Id, participants[3].Id, 0, 0, utcNow)
        };

        var standings = _service.Calculate(participants, matches);

        var orderedTiedTeams = standings
            .Where(s => s.ParticipantId != participants[3].Id)
            .OrderBy(s => s.Position)
            .Select(s => s.ParticipantId)
            .ToList();

        orderedTiedTeams.Should().Equal(participants[0].Id, participants[1].Id, participants[2].Id);
    }

    [Fact]
    public void Calculate_FullyTiedParticipants_FlagsControlledTieBreakRequirement()
    {
        var tournamentId = Guid.NewGuid();
        var roundId = Guid.NewGuid();
        var participants = TestEntityFactory.CreateParticipants(tournamentId, 2);
        var utcNow = TestEntityFactory.UtcNow;

        var matches = new[]
        {
            MatchTestFactory.CreateCompletedMatch(tournamentId, roundId, participants[0].Id, participants[1].Id, 1, 1, utcNow),
            MatchTestFactory.CreateCompletedMatch(tournamentId, roundId, participants[1].Id, participants[0].Id, 1, 1, utcNow)
        };

        var standings = _service.Calculate(participants, matches);

        standings.Should().OnlyContain(s => s.TieBreakNote != null);
    }
}
