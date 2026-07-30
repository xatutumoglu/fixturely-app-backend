using Fixturely.Domain.Entities;
using Fixturely.Domain.Enums;
using Fixturely.Domain.Exceptions;
using Fixturely.UnitTests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Fixturely.UnitTests.Domain;

public sealed class TournamentTests
{
    private readonly DateTime _utcNow = TestEntityFactory.UtcNow;

    [Fact]
    public void Create_GroupFormatWithoutGroupCount_Throws()
    {
        var act = () => Tournament.Create(
            "Test", null, Guid.NewGuid(), TournamentFormat.GroupStage, LegMode.SingleLeg,
            numberOfGroups: null, hasThirdPlaceMatch: false, _utcNow);

        act.Should().Throw<InvalidTournamentStateException>();
    }

    [Fact]
    public void Create_SetsOwnerAsActiveOwnerMember()
    {
        var ownerId = Guid.NewGuid();
        var tournament = Tournament.Create(
            "Test", null, ownerId, TournamentFormat.League, LegMode.SingleLeg,
            numberOfGroups: null, hasThirdPlaceMatch: false, _utcNow);

        tournament.Members.Should().ContainSingle(m => m.UserId == ownerId && m.Role == TournamentMemberRole.Owner);
    }

    [Fact]
    public void AddParticipant_DuplicateName_Throws()
    {
        var tournament = TestEntityFactory.CreateLeagueTournament(Guid.NewGuid(), 2, LegMode.SingleLeg);

        var act = () => tournament.AddParticipant(Participant.Create(tournament.Id, "Team 1", null, _utcNow));

        act.Should().Throw<ParticipantAlreadyExistsException>();
    }

    [Fact]
    public void CanRegenerateFixture_WithNoScoresEntered_ReturnsTrueAfterFixtureGenerated()
    {
        var tournament = TestEntityFactory.CreateLeagueTournament(Guid.NewGuid(), 4, LegMode.SingleLeg);
        tournament.MarkFixtureGenerated(1, _utcNow);

        tournament.CanRegenerateFixture().Should().BeTrue();
    }

    [Fact]
    public void Archive_MakesTournamentReadOnly()
    {
        var tournament = TestEntityFactory.CreateLeagueTournament(Guid.NewGuid(), 4, LegMode.SingleLeg);
        tournament.Archive(_utcNow);

        var act = () => tournament.UpdateSettings("New Name", null, LegMode.SingleLeg, false, _utcNow);

        act.Should().Throw<InvalidTournamentStateException>();
    }

    [Fact]
    public void Reopen_OnlyAllowedFromCompletedStatus()
    {
        var tournament = TestEntityFactory.CreateLeagueTournament(Guid.NewGuid(), 4, LegMode.SingleLeg);

        var act = () => tournament.Reopen(_utcNow);

        act.Should().Throw<InvalidTournamentStateException>();
    }
}
