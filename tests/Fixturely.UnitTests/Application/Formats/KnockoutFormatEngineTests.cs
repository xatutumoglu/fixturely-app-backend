using Fixturely.Application.Tournaments.Formats;
using Fixturely.Domain.Enums;
using Fixturely.UnitTests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Fixturely.UnitTests.Application.Formats;

public sealed class KnockoutFormatEngineTests
{
    [Fact]
    public void GenerateFixture_WithPowerOfTwoParticipants_CreatesFullBracketWithNoByes()
    {
        var tournamentId = Guid.NewGuid();
        var participants = TestEntityFactory.CreateParticipants(tournamentId, 8);
        var engine = new KnockoutFormatEngine();

        var output = engine.GenerateFixture(new FixtureGenerationInput
        {
            TournamentId = tournamentId,
            LegMode = LegMode.SingleLeg,
            Participants = participants,
            HasThirdPlaceMatch = false,
            RandomSeed = "seed",
            UtcNow = TestEntityFactory.UtcNow
        });

        output.Matches.Should().HaveCount(7);
        output.Matches.Should().OnlyContain(m => !m.IsBye);
        output.Rounds.Should().HaveCount(3);
    }

    [Fact]
    public void GenerateFixture_WithNonPowerOfTwoParticipants_AssignsByesAndAutoAdvancesWinners()
    {
        var tournamentId = Guid.NewGuid();
        var participants = TestEntityFactory.CreateParticipants(tournamentId, 5);
        var engine = new KnockoutFormatEngine();

        var output = engine.GenerateFixture(new FixtureGenerationInput
        {
            TournamentId = tournamentId,
            LegMode = LegMode.SingleLeg,
            Participants = participants,
            HasThirdPlaceMatch = false,
            RandomSeed = "seed",
            UtcNow = TestEntityFactory.UtcNow
        });

        var round1Matches = output.Matches.Where(m => m.LegNumber == 1 && m.TieIdentifier != null)
            .GroupBy(m => m.TieIdentifier)
            .Select(g => g.OrderByDescending(m => m.LegNumber).First())
            .ToList();

        var byeMatches = output.Matches.Where(m => m.IsBye).ToList();
        byeMatches.Should().HaveCount(3);
        byeMatches.Should().OnlyContain(m => m.WinnerParticipantId != null);
        byeMatches.Should().OnlyContain(m => m.Status == Fixturely.Domain.Enums.MatchStatus.Completed);

        foreach (var bye in byeMatches)
        {
            bye.NextHomeMatchId.Should().NotBeNull();
        }
    }

    [Fact]
    public void GenerateFixture_ThirdPlaceMatchRequested_CreatesThirdPlaceMatch()
    {
        var tournamentId = Guid.NewGuid();
        var participants = TestEntityFactory.CreateParticipants(tournamentId, 4);
        var engine = new KnockoutFormatEngine();

        var output = engine.GenerateFixture(new FixtureGenerationInput
        {
            TournamentId = tournamentId,
            LegMode = LegMode.SingleLeg,
            Participants = participants,
            HasThirdPlaceMatch = true,
            RandomSeed = "seed",
            UtcNow = TestEntityFactory.UtcNow
        });

        output.Matches.Should().ContainSingle(m => m.IsThirdPlaceMatch);
    }

    [Fact]
    public void GenerateFixture_DoubleLeg_CreatesTwoLegsPerTie()
    {
        var tournamentId = Guid.NewGuid();
        var participants = TestEntityFactory.CreateParticipants(tournamentId, 4);
        var engine = new KnockoutFormatEngine();

        var output = engine.GenerateFixture(new FixtureGenerationInput
        {
            TournamentId = tournamentId,
            LegMode = LegMode.DoubleLeg,
            Participants = participants,
            HasThirdPlaceMatch = false,
            RandomSeed = "seed",
            UtcNow = TestEntityFactory.UtcNow
        });

        var ties = output.Matches.GroupBy(m => m.TieIdentifier).ToList();
        ties.Should().OnlyContain(g => g.Count() == 2);
    }

    [Fact]
    public void GenerateFixture_WithFewerThanTwoParticipants_Throws()
    {
        var tournamentId = Guid.NewGuid();
        var participants = TestEntityFactory.CreateParticipants(tournamentId, 1);
        var engine = new KnockoutFormatEngine();

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
