using Fixturely.Application.Tournaments.Formats;
using Fixturely.Domain.Enums;
using Fixturely.UnitTests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Fixturely.UnitTests.Application.Formats;

public sealed class LeagueFormatEngineTests
{
    [Fact]
    public void GenerateFixture_SingleLeg_ProducesExpectedMatchCount()
    {
        var tournamentId = Guid.NewGuid();
        var participants = TestEntityFactory.CreateParticipants(tournamentId, 6);
        var engine = new LeagueFormatEngine();

        var output = engine.GenerateFixture(new FixtureGenerationInput
        {
            TournamentId = tournamentId,
            LegMode = LegMode.SingleLeg,
            Participants = participants,
            RandomSeed = "seed",
            UtcNow = TestEntityFactory.UtcNow
        });

        output.Matches.Should().HaveCount(15);
        output.Matches.Should().OnlyContain(m => !m.IsBye);
    }

    [Fact]
    public void GenerateFixture_DoubleLeg_ProducesDoubleMatchCount()
    {
        var tournamentId = Guid.NewGuid();
        var participants = TestEntityFactory.CreateParticipants(tournamentId, 4);
        var engine = new LeagueFormatEngine();

        var output = engine.GenerateFixture(new FixtureGenerationInput
        {
            TournamentId = tournamentId,
            LegMode = LegMode.DoubleLeg,
            Participants = participants,
            RandomSeed = "seed",
            UtcNow = TestEntityFactory.UtcNow
        });

        output.Matches.Should().HaveCount(12);

        var legOneCount = output.Matches.Count(m => m.LegNumber == 1);
        var legTwoCount = output.Matches.Count(m => m.LegNumber == 2);

        legOneCount.Should().Be(6);
        legTwoCount.Should().Be(6);
    }

    [Fact]
    public void GenerateFixture_OddParticipantCount_MarksByeMatchesAsCompleted()
    {
        var tournamentId = Guid.NewGuid();
        var participants = TestEntityFactory.CreateParticipants(tournamentId, 5);
        var engine = new LeagueFormatEngine();

        var output = engine.GenerateFixture(new FixtureGenerationInput
        {
            TournamentId = tournamentId,
            LegMode = LegMode.SingleLeg,
            Participants = participants,
            RandomSeed = "seed",
            UtcNow = TestEntityFactory.UtcNow
        });

        var byeMatches = output.Matches.Where(m => m.IsBye).ToList();
        byeMatches.Should().HaveCount(5);
        byeMatches.Should().OnlyContain(m => m.Status == Fixturely.Domain.Enums.MatchStatus.Completed);
        byeMatches.Should().OnlyContain(m => m.WinnerParticipantId != null);
    }

    [Fact]
    public void GenerateFixture_WithFewerThanTwoParticipants_Throws()
    {
        var tournamentId = Guid.NewGuid();
        var participants = TestEntityFactory.CreateParticipants(tournamentId, 1);
        var engine = new LeagueFormatEngine();

        var act = () => engine.GenerateFixture(new FixtureGenerationInput
        {
            TournamentId = tournamentId,
            LegMode = LegMode.SingleLeg,
            Participants = participants,
            RandomSeed = "seed",
            UtcNow = TestEntityFactory.UtcNow
        });

        act.Should().Throw<Fixturely.Domain.Exceptions.InvalidFixtureGenerationException>();
    }
}
