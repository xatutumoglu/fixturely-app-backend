using Fixturely.Application.DTOs.Tournaments;
using Fixturely.Application.Tournaments;
using Fixturely.Domain.Enums;
using Fixturely.UnitTests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fixturely.UnitTests.Application.Tournaments;

public sealed class TournamentServiceTests
{
    private readonly FixedTimeProvider _timeProvider = new(TestEntityFactory.UtcNow);

    private TournamentService CreateService(Fixturely.Infrastructure.Persistence.ApplicationDbContext dbContext)
    {
        return new TournamentService(dbContext, _timeProvider, new TournamentAuthorizationService(dbContext));
    }

    private static async Task<Fixturely.Domain.Entities.Tournament> SeedTournamentAsync(
        Fixturely.Infrastructure.Persistence.ApplicationDbContext dbContext,
        Guid ownerId)
    {
        var tournament = TestEntityFactory.CreateLeagueTournament(ownerId, 4, LegMode.SingleLeg);
        dbContext.Tournaments.Add(tournament);
        foreach (var member in tournament.Members)
        {
            dbContext.TournamentMembers.Add(member);
        }
        await dbContext.SaveChangesAsync();
        return tournament;
    }

    [Fact]
    public async Task DeleteBulkAsync_WhenCallerOwnsAllTournaments_MarksAllAsDeleted()
    {
        using var dbContext = InMemoryDbContextFactory.Create();
        var ownerId = Guid.NewGuid();
        var tournamentA = await SeedTournamentAsync(dbContext, ownerId);
        var tournamentB = await SeedTournamentAsync(dbContext, ownerId);

        var service = CreateService(dbContext);

        await service.DeleteBulkAsync(
            ownerId, new BulkDeleteTournamentsRequest(new[] { tournamentA.Id, tournamentB.Id }));

        var remaining = await dbContext.Tournaments
            .IgnoreQueryFilters()
            .Where(t => t.Id == tournamentA.Id || t.Id == tournamentB.Id)
            .ToListAsync();

        remaining.Should().HaveCount(2);
        remaining.Should().OnlyContain(t => t.IsDeleted);
    }

    [Fact]
    public async Task DeleteBulkAsync_SkipsTournamentsNotOwnedByCaller()
    {
        using var dbContext = InMemoryDbContextFactory.Create();
        var ownerId = Guid.NewGuid();
        var otherOwnerId = Guid.NewGuid();
        var ownedTournament = await SeedTournamentAsync(dbContext, ownerId);
        var otherTournament = await SeedTournamentAsync(dbContext, otherOwnerId);

        var service = CreateService(dbContext);

        await service.DeleteBulkAsync(
            ownerId, new BulkDeleteTournamentsRequest(new[] { ownedTournament.Id, otherTournament.Id }));

        var owned = await dbContext.Tournaments.IgnoreQueryFilters().FirstAsync(t => t.Id == ownedTournament.Id);
        var other = await dbContext.Tournaments.IgnoreQueryFilters().FirstAsync(t => t.Id == otherTournament.Id);

        owned.IsDeleted.Should().BeTrue();
        other.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteBulkAsync_SkipsUnknownTournamentIds_WithoutThrowing()
    {
        using var dbContext = InMemoryDbContextFactory.Create();
        var ownerId = Guid.NewGuid();
        var tournament = await SeedTournamentAsync(dbContext, ownerId);
        var unknownId = Guid.NewGuid();

        var service = CreateService(dbContext);

        var act = () => service.DeleteBulkAsync(
            ownerId, new BulkDeleteTournamentsRequest(new[] { tournament.Id, unknownId }));

        await act.Should().NotThrowAsync();

        var owned = await dbContext.Tournaments.IgnoreQueryFilters().FirstAsync(t => t.Id == tournament.Id);
        owned.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteBulkAsync_WhenTournamentAlreadyDeleted_LeavesItUntouched()
    {
        using var dbContext = InMemoryDbContextFactory.Create();
        var ownerId = Guid.NewGuid();
        var tournament = await SeedTournamentAsync(dbContext, ownerId);
        tournament.MarkAsDeleted(TestEntityFactory.UtcNow);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var act = () => service.DeleteBulkAsync(ownerId, new BulkDeleteTournamentsRequest(new[] { tournament.Id }));

        await act.Should().NotThrowAsync();
    }
}
