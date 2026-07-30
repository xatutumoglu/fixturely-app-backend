using Fixturely.Application.Tournaments;
using Fixturely.Domain.Entities;
using Fixturely.Domain.Enums;
using Fixturely.Domain.Exceptions;
using Fixturely.UnitTests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Fixturely.UnitTests.Application.Authorization;

public sealed class TournamentAuthorizationServiceTests
{
    [Fact]
    public async Task EnsureCanViewAsync_OwnerCanView()
    {
        using var dbContext = InMemoryDbContextFactory.Create();
        var ownerId = Guid.NewGuid();
        var tournament = TestEntityFactory.CreateLeagueTournament(ownerId, 4, LegMode.SingleLeg);

        dbContext.Tournaments.Add(tournament);
        foreach (var member in tournament.Members)
        {
            dbContext.TournamentMembers.Add(member);
        }
        await dbContext.SaveChangesAsync();

        var service = new TournamentAuthorizationService(dbContext);

        var act = () => service.EnsureCanViewAsync(tournament.Id, ownerId, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnsureCanViewAsync_UnrelatedUser_ThrowsUnauthorized()
    {
        using var dbContext = InMemoryDbContextFactory.Create();
        var ownerId = Guid.NewGuid();
        var strangerId = Guid.NewGuid();
        var tournament = TestEntityFactory.CreateLeagueTournament(ownerId, 4, LegMode.SingleLeg);

        dbContext.Tournaments.Add(tournament);
        foreach (var member in tournament.Members)
        {
            dbContext.TournamentMembers.Add(member);
        }
        await dbContext.SaveChangesAsync();

        var service = new TournamentAuthorizationService(dbContext);

        var act = () => service.EnsureCanViewAsync(tournament.Id, strangerId, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedTournamentAccessException>();
    }

    [Fact]
    public async Task EnsureCanManageScoresAsync_ViewerCannotManageScores()
    {
        using var dbContext = InMemoryDbContextFactory.Create();
        var ownerId = Guid.NewGuid();
        var viewerId = Guid.NewGuid();
        var tournament = TestEntityFactory.CreateLeagueTournament(ownerId, 4, LegMode.SingleLeg);

        dbContext.Tournaments.Add(tournament);
        foreach (var member in tournament.Members)
        {
            dbContext.TournamentMembers.Add(member);
        }
        dbContext.TournamentMembers.Add(
            TournamentMember.CreateFromInvitation(tournament.Id, viewerId, TournamentMemberRole.Viewer, TestEntityFactory.UtcNow));
        await dbContext.SaveChangesAsync();

        var service = new TournamentAuthorizationService(dbContext);

        var act = () => service.EnsureCanManageScoresAsync(tournament.Id, viewerId, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedTournamentAccessException>();
    }

    [Fact]
    public async Task EnsureCanManageScoresAsync_ScoreManagerCanManageScores()
    {
        using var dbContext = InMemoryDbContextFactory.Create();
        var ownerId = Guid.NewGuid();
        var scoreManagerId = Guid.NewGuid();
        var tournament = TestEntityFactory.CreateLeagueTournament(ownerId, 4, LegMode.SingleLeg);

        dbContext.Tournaments.Add(tournament);
        foreach (var member in tournament.Members)
        {
            dbContext.TournamentMembers.Add(member);
        }
        dbContext.TournamentMembers.Add(
            TournamentMember.CreateFromInvitation(tournament.Id, scoreManagerId, TournamentMemberRole.ScoreManager, TestEntityFactory.UtcNow));
        await dbContext.SaveChangesAsync();

        var service = new TournamentAuthorizationService(dbContext);

        var act = () => service.EnsureCanManageScoresAsync(tournament.Id, scoreManagerId, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnsureIsOwnerAsync_ScoreManagerCannotManageTournamentSettings()
    {
        using var dbContext = InMemoryDbContextFactory.Create();
        var ownerId = Guid.NewGuid();
        var scoreManagerId = Guid.NewGuid();
        var tournament = TestEntityFactory.CreateLeagueTournament(ownerId, 4, LegMode.SingleLeg);

        dbContext.Tournaments.Add(tournament);
        foreach (var member in tournament.Members)
        {
            dbContext.TournamentMembers.Add(member);
        }
        dbContext.TournamentMembers.Add(
            TournamentMember.CreateFromInvitation(tournament.Id, scoreManagerId, TournamentMemberRole.ScoreManager, TestEntityFactory.UtcNow));
        await dbContext.SaveChangesAsync();

        var service = new TournamentAuthorizationService(dbContext);

        var act = () => service.EnsureIsOwnerAsync(tournament.Id, scoreManagerId, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedTournamentAccessException>();
    }
}
