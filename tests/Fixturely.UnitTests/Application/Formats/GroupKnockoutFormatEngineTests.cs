using Fixturely.Application.Tournaments.Formats;
using Fixturely.Domain.Enums;
using Fixturely.UnitTests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Fixturely.UnitTests.Application.Formats;

public sealed class GroupKnockoutFormatEngineTests
{
    [Fact]
    public void GenerateFixture_WithFourGroups_CreatesGroupStagePlusKnockoutRounds()
    {
        var tournamentId = Guid.NewGuid();
        var participants = TestEntityFactory.CreateParticipants(tournamentId, 16);
        var engine = new GroupKnockoutFormatEngine();

        var output = engine.GenerateFixture(new FixtureGenerationInput
        {
            TournamentId = tournamentId,
            LegMode = LegMode.SingleLeg,
            Participants = participants,
            NumberOfGroups = 4,
            HasThirdPlaceMatch = true,
            RandomSeed = "seed",
            UtcNow = TestEntityFactory.UtcNow
        });

        output.Groups.Should().HaveCount(4);
        output.Groups.Should().OnlyContain(g => g.GroupParticipants.Count == 4);

        var groupMatches = output.Matches.Where(m => m.TournamentGroupId != null).ToList();
        groupMatches.Should().HaveCount(4 * 6);

        var knockoutMatches = output.Matches.Where(m => m.TournamentGroupId == null).ToList();
        knockoutMatches.Should().NotBeEmpty();

        knockoutMatches.Should().Contain(m => m.IsThirdPlaceMatch);
    }

    [Fact]
    public void GenerateFixture_FirstKnockoutRound_NeverPairsSameGroupOrderIndexAsWinnerAndRunnerUp()
    {
        var tournamentId = Guid.NewGuid();
        var participants = TestEntityFactory.CreateParticipants(tournamentId, 8);
        var engine = new GroupKnockoutFormatEngine();

        var output = engine.GenerateFixture(new FixtureGenerationInput
        {
            TournamentId = tournamentId,
            LegMode = LegMode.SingleLeg,
            Participants = participants,
            NumberOfGroups = 2,
            HasThirdPlaceMatch = false,
            RandomSeed = "seed",
            UtcNow = TestEntityFactory.UtcNow
        });

        var firstRoundKnockoutMatches = output.Matches
            .Where(m => m.TournamentGroupId == null && m.LegNumber == 1 && m.HomeQualifierGroupOrderIndex != null)
            .ToList();

        firstRoundKnockoutMatches.Should().OnlyContain(
            m => m.HomeQualifierGroupOrderIndex != m.AwayQualifierGroupOrderIndex);
    }

    [Fact]
    public void GenerateFixture_RequiresGroupCountOfTwoFourEightOrSixteen()
    {
        var tournamentId = Guid.NewGuid();
        var participants = TestEntityFactory.CreateParticipants(tournamentId, 12);
        var engine = new GroupKnockoutFormatEngine();

        var act = () => engine.GenerateFixture(new FixtureGenerationInput
        {
            TournamentId = tournamentId,
            LegMode = LegMode.SingleLeg,
            Participants = participants,
            NumberOfGroups = 3,
            RandomSeed = "seed",
            UtcNow = TestEntityFactory.UtcNow
        });

        act.Should().Throw<Fixturely.Domain.Exceptions.TournamentGroupCompositionException>();
    }
}
