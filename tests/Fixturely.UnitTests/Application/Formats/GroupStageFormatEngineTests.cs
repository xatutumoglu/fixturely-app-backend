using Fixturely.Application.Tournaments.Formats;
using Fixturely.Domain.Enums;
using Fixturely.UnitTests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Fixturely.UnitTests.Application.Formats;

public sealed class GroupStageFormatEngineTests
{
    [Fact]
    public void GenerateFixture_WithExactMultipleOfFour_CreatesGroupsOfFour()
    {
        var tournamentId = Guid.NewGuid();
        var participants = TestEntityFactory.CreateParticipants(tournamentId, 8);
        var engine = new GroupStageFormatEngine();

        var output = engine.GenerateFixture(new FixtureGenerationInput
        {
            TournamentId = tournamentId,
            LegMode = LegMode.SingleLeg,
            Participants = participants,
            NumberOfGroups = 2,
            RandomSeed = "seed-groups",
            UtcNow = TestEntityFactory.UtcNow
        });

        output.Groups.Should().HaveCount(2);

        foreach (var group in output.Groups)
        {
            group.GroupParticipants.Should().HaveCount(4);
        }

        var allAssignedParticipantIds = output.Groups
            .SelectMany(g => g.GroupParticipants.Select(gp => gp.ParticipantId))
            .ToList();

        allAssignedParticipantIds.Should().OnlyHaveUniqueItems();
        allAssignedParticipantIds.Should().BeEquivalentTo(participants.Select(p => p.Id));
    }

    [Fact]
    public void GenerateFixture_SingleLegGroupOfFour_ProducesSixMatches()
    {
        var tournamentId = Guid.NewGuid();
        var participants = TestEntityFactory.CreateParticipants(tournamentId, 4);
        var engine = new GroupStageFormatEngine();

        var output = engine.GenerateFixture(new FixtureGenerationInput
        {
            TournamentId = tournamentId,
            LegMode = LegMode.SingleLeg,
            Participants = participants,
            NumberOfGroups = 1,
            RandomSeed = "seed",
            UtcNow = TestEntityFactory.UtcNow
        });

        output.Matches.Should().HaveCount(6);
    }

    [Fact]
    public void GenerateFixture_DoubleLegGroupOfFour_ProducesTwelveMatches()
    {
        var tournamentId = Guid.NewGuid();
        var participants = TestEntityFactory.CreateParticipants(tournamentId, 4);
        var engine = new GroupStageFormatEngine();

        var output = engine.GenerateFixture(new FixtureGenerationInput
        {
            TournamentId = tournamentId,
            LegMode = LegMode.DoubleLeg,
            Participants = participants,
            NumberOfGroups = 1,
            RandomSeed = "seed",
            UtcNow = TestEntityFactory.UtcNow
        });

        output.Matches.Should().HaveCount(12);
    }

    [Fact]
    public void GenerateFixture_WithWrongParticipantCount_ThrowsCompositionException()
    {
        var tournamentId = Guid.NewGuid();
        var participants = TestEntityFactory.CreateParticipants(tournamentId, 7);
        var engine = new GroupStageFormatEngine();

        var act = () => engine.GenerateFixture(new FixtureGenerationInput
        {
            TournamentId = tournamentId,
            LegMode = LegMode.SingleLeg,
            Participants = participants,
            NumberOfGroups = 2,
            RandomSeed = "seed",
            UtcNow = TestEntityFactory.UtcNow
        });

        act.Should().Throw<Fixturely.Domain.Exceptions.TournamentGroupCompositionException>();
    }
}
