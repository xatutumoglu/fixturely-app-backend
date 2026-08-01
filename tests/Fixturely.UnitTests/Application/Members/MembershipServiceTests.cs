using Fixturely.Application.Abstractions.Email;
using Fixturely.Application.Abstractions.Identity;
using Fixturely.Application.DTOs.Members;
using Fixturely.Application.Tournaments;
using Fixturely.Application.Tournaments.Members;
using Fixturely.Domain.Entities;
using Fixturely.Domain.Enums;
using Fixturely.Domain.Exceptions;
using Fixturely.UnitTests.TestHelpers;
using FluentAssertions;
using Moq;
using Xunit;

namespace Fixturely.UnitTests.Application.Members;

public sealed class MembershipServiceTests
{
    private readonly Mock<IIdentityService> _identityService = new();
    private readonly Mock<IEmailNotificationService> _emailNotificationService = new();
    private readonly FixedTimeProvider _timeProvider = new(TestEntityFactory.UtcNow);

    private static UserRecord CreateUser(Guid id, string email, string userName) => new()
    {
        Id = id,
        UserName = userName,
        Email = email,
        EmailConfirmed = true,
        IsActive = true,
        CreatedAtUtc = TestEntityFactory.UtcNow
    };

    private MembershipService CreateService(Fixturely.Infrastructure.Persistence.ApplicationDbContext dbContext)
    {
        return new MembershipService(
            dbContext,
            _timeProvider,
            new TournamentAuthorizationService(dbContext),
            _identityService.Object,
            _emailNotificationService.Object);
    }

    [Fact]
    public async Task InviteAsync_WhenRecipientHasNoAccount_ThrowsUserNotRegisteredException()
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

        _identityService.Setup(s => s.FindByEmailAsync("notregistered@fixturely.test", It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserRecord?)null);
        _identityService.Setup(s => s.FindByIdAsync(ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateUser(ownerId, "owner@fixturely.test", "owner"));

        var service = CreateService(dbContext);

        var act = () => service.InviteAsync(
            tournament.Id, ownerId, new InviteMemberRequest("notregistered@fixturely.test", TournamentMemberRole.Viewer));

        await act.Should().ThrowAsync<UserNotRegisteredException>();

        _emailNotificationService.Verify(
            e => e.SendTournamentInvitationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task InviteAsync_WhenRecipientIsRegistered_CreatesInvitationAndSendsEmail()
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

        var recipientId = Guid.NewGuid();
        _identityService.Setup(s => s.FindByEmailAsync("viewer@fixturely.test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateUser(recipientId, "viewer@fixturely.test", "viewer"));
        _identityService.Setup(s => s.FindByIdAsync(ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateUser(ownerId, "owner@fixturely.test", "owner"));

        var service = CreateService(dbContext);

        var result = await service.InviteAsync(
            tournament.Id, ownerId, new InviteMemberRequest("viewer@fixturely.test", TournamentMemberRole.Viewer));

        result.InvitedEmail.Should().Be("viewer@fixturely.test");
        dbContext.TournamentInvitations.Should().ContainSingle(i => i.InvitedEmail == "viewer@fixturely.test");

        _emailNotificationService.Verify(
            e => e.SendTournamentInvitationAsync(
                "viewer@fixturely.test", tournament.Name, "owner", "Viewer", It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ListMyInvitationsAsync_ReturnsOnlyPendingNonExpiredInvitationsForCallerEmail()
    {
        using var dbContext = InMemoryDbContextFactory.Create();
        var ownerId = Guid.NewGuid();
        var tournament = TestEntityFactory.CreateLeagueTournament(ownerId, 4, LegMode.SingleLeg);
        dbContext.Tournaments.Add(tournament);
        await dbContext.SaveChangesAsync();

        var callerId = Guid.NewGuid();
        const string callerEmail = "invitee@fixturely.test";

        var pendingInvitation = TournamentInvitation.Create(
            tournament.Id, callerEmail, TournamentMemberRole.Viewer, "hash-pending", ownerId, TestEntityFactory.UtcNow);
        var expiredInvitation = TournamentInvitation.Create(
            tournament.Id, callerEmail, TournamentMemberRole.ScoreManager, "hash-expired", ownerId,
            TestEntityFactory.UtcNow.AddDays(-30));
        var someoneElsesInvitation = TournamentInvitation.Create(
            tournament.Id, "someone-else@fixturely.test", TournamentMemberRole.Viewer, "hash-other", ownerId,
            TestEntityFactory.UtcNow);

        dbContext.TournamentInvitations.AddRange(pendingInvitation, expiredInvitation, someoneElsesInvitation);
        await dbContext.SaveChangesAsync();

        _identityService.Setup(s => s.FindByIdAsync(callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateUser(callerId, callerEmail, "invitee"));

        var service = CreateService(dbContext);

        var result = await service.ListMyInvitationsAsync(callerId);

        result.Should().ContainSingle();
        result.Single().Id.Should().Be(pendingInvitation.Id);
        result.Single().TournamentName.Should().Be(tournament.Name);
    }

    [Fact]
    public async Task AcceptMyInvitationAsync_CreatesMembership_SoTheTournamentBecomesVisibleToAcceptingUser()
    {
        using var dbContext = InMemoryDbContextFactory.Create();
        var ownerId = Guid.NewGuid();
        var tournament = TestEntityFactory.CreateLeagueTournament(ownerId, 4, LegMode.SingleLeg);
        dbContext.Tournaments.Add(tournament);
        await dbContext.SaveChangesAsync();

        var acceptingUserId = Guid.NewGuid();
        const string acceptingUserEmail = "newmember@fixturely.test";

        var invitation = TournamentInvitation.Create(
            tournament.Id, acceptingUserEmail, TournamentMemberRole.Viewer, "hash-1", ownerId, TestEntityFactory.UtcNow);
        dbContext.TournamentInvitations.Add(invitation);
        await dbContext.SaveChangesAsync();

        _identityService.Setup(s => s.FindByIdAsync(acceptingUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateUser(acceptingUserId, acceptingUserEmail, "newmember"));
        _identityService.Setup(s => s.FindByIdAsync(ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateUser(ownerId, "owner@fixturely.test", "owner"));

        var service = CreateService(dbContext);

        await service.AcceptMyInvitationAsync(invitation.Id, acceptingUserId);

        var member = dbContext.TournamentMembers.Single(m => m.UserId == acceptingUserId);
        member.TournamentId.Should().Be(tournament.Id);
        member.Role.Should().Be(TournamentMemberRole.Viewer);
        member.Status.Should().Be(TournamentMemberStatus.Active);

        // This is precisely the query TournamentService.ListForUserAsync runs to decide whether a
        // tournament appears in a user's own tournament list/dashboard - asserting against it
        // directly proves the accepted invitation makes the tournament visible to that user.
        dbContext.Tournaments
            .Where(t => !t.IsDeleted && t.Members.Any(m => m.UserId == acceptingUserId && m.Status == TournamentMemberStatus.Active))
            .Should().ContainSingle(t => t.Id == tournament.Id);
    }

    [Fact]
    public async Task AcceptMyInvitationAsync_WhenEmailDoesNotMatch_ThrowsInvitationException()
    {
        using var dbContext = InMemoryDbContextFactory.Create();
        var ownerId = Guid.NewGuid();
        var tournament = TestEntityFactory.CreateLeagueTournament(ownerId, 4, LegMode.SingleLeg);
        dbContext.Tournaments.Add(tournament);
        await dbContext.SaveChangesAsync();

        var invitation = TournamentInvitation.Create(
            tournament.Id, "invitee@fixturely.test", TournamentMemberRole.Viewer, "hash-1", ownerId, TestEntityFactory.UtcNow);
        dbContext.TournamentInvitations.Add(invitation);
        await dbContext.SaveChangesAsync();

        var wrongUserId = Guid.NewGuid();
        _identityService.Setup(s => s.FindByIdAsync(wrongUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateUser(wrongUserId, "someoneelse@fixturely.test", "someoneelse"));

        var service = CreateService(dbContext);

        var act = () => service.AcceptMyInvitationAsync(invitation.Id, wrongUserId);

        await act.Should().ThrowAsync<InvitationException>();
    }
}
