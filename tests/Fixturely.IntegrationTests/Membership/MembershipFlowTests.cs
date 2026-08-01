using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Fixturely.Application.Common;
using Fixturely.Application.DTOs.Matches;
using Fixturely.Application.DTOs.Members;
using Fixturely.Application.DTOs.Participants;
using Fixturely.Application.DTOs.Tournaments;
using Fixturely.Domain.Enums;
using Fixturely.IntegrationTests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Fixturely.IntegrationTests.Membership;

[Collection(IntegrationTestCollection.Name)]
public sealed class MembershipFlowTests
{
    private readonly IntegrationTestWebAppFactory _factory;

    public MembershipFlowTests(IntegrationTestWebAppFactory factory)
    {
        _factory = factory;
    }

    private async Task<(TournamentDetailResponse Tournament, HttpClient OwnerClient)> CreateTournamentWithParticipantsAsync(
        string ownerPrefix)
    {
        var ownerClient = _factory.CreateClient();
        var owner = await TestUserFactory.RegisterAndConfirmAsync(ownerClient, _factory.EmailCapture, ownerPrefix);
        var (ownerToken, _) = await TestUserFactory.LoginAsync(ownerClient, owner);
        ownerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);

        var createResponse = await ownerClient.PostAsJsonAsync("/api/v1/tournaments", new CreateTournamentRequest(
            $"{ownerPrefix} Tournament", null, TournamentFormat.League, LegMode.SingleLeg, null, false));
        var tournament = await createResponse.ReadAsAsync<TournamentDetailResponse>();

        foreach (var teamName in new[] { "Red", "Blue", "Green", "Yellow" })
        {
            await ownerClient.PostAsJsonAsync(
                $"/api/v1/tournaments/{tournament!.Id}/participants", new CreateParticipantRequest(teamName, null));
        }

        await ownerClient.PostAsync($"/api/v1/tournaments/{tournament!.Id}/generate-fixture", null);

        return (tournament!, ownerClient);
    }

    private async Task<HttpClient> InviteAndAcceptAsync(
        HttpClient ownerClient,
        Guid tournamentId,
        TournamentMemberRole role,
        string memberPrefix)
    {
        var memberClient = _factory.CreateClient();
        var member = await TestUserFactory.RegisterAndConfirmAsync(memberClient, _factory.EmailCapture, memberPrefix);

        var inviteResponse = await ownerClient.PostAsJsonAsync(
            $"/api/v1/tournaments/{tournamentId}/invitations", new InviteMemberRequest(member.Email, role));
        inviteResponse.EnsureSuccessStatusCode();

        var invitationMessage = _factory.EmailCapture.Messages
            .Last(m => m.ToEmail.Equals(member.Email, StringComparison.OrdinalIgnoreCase)
                && m.TemplateType == EmailTemplateType.TournamentInvitation);
        var invitationToken = EmailLinkParser.ParseInvitationToken(invitationMessage);

        var (memberToken, _) = await TestUserFactory.LoginAsync(memberClient, member);
        memberClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", memberToken);

        var acceptResponse = await memberClient.PostAsync($"/api/v1/invitations/{invitationToken}/accept", null);
        acceptResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        return memberClient;
    }

    [Fact]
    public async Task Viewer_CannotUpdateScore()
    {
        var (tournament, ownerClient) = await CreateTournamentWithParticipantsAsync("viewertest");
        var viewerClient = await InviteAndAcceptAsync(ownerClient, tournament.Id, TournamentMemberRole.Viewer, "viewer");

        var matchesResponse = await ownerClient.GetAsync($"/api/v1/tournaments/{tournament.Id}/matches");
        var matches = await matchesResponse.ReadAsAsync<List<MatchResponse>>();
        var firstMatch = matches!.First();

        var scoreResponse = await viewerClient.PutAsJsonAsync(
            $"/api/v1/tournaments/{tournament.Id}/matches/{firstMatch.Id}/score",
            new UpdateMatchScoreRequest(1, 0, null, null, null, null, firstMatch.RowVersion, null));

        scoreResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ScoreManager_CanUpdateScoreButCannotChangeTournamentSettings()
    {
        var (tournament, ownerClient) = await CreateTournamentWithParticipantsAsync("scoremgrtest");
        var scoreManagerClient = await InviteAndAcceptAsync(
            ownerClient, tournament.Id, TournamentMemberRole.ScoreManager, "scoremgr");

        var matchesResponse = await ownerClient.GetAsync($"/api/v1/tournaments/{tournament.Id}/matches");
        var matches = await matchesResponse.ReadAsAsync<List<MatchResponse>>();
        var firstMatch = matches!.First();

        var scoreResponse = await scoreManagerClient.PutAsJsonAsync(
            $"/api/v1/tournaments/{tournament.Id}/matches/{firstMatch.Id}/score",
            new UpdateMatchScoreRequest(3, 0, null, null, null, null, firstMatch.RowVersion, null));
        scoreResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var updateSettingsResponse = await scoreManagerClient.PutAsJsonAsync(
            $"/api/v1/tournaments/{tournament.Id}",
            new UpdateTournamentRequest("Renamed", null, LegMode.SingleLeg, false, tournament.RowVersion));

        updateSettingsResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Owner_CanManageParticipantsAndInvitations()
    {
        var (tournament, ownerClient) = await CreateTournamentWithParticipantsAsync("ownermgmt");

        var participantsResponse = await ownerClient.GetAsync($"/api/v1/tournaments/{tournament.Id}/participants");
        participantsResponse.EnsureSuccessStatusCode();
        var participants = await participantsResponse.ReadAsAsync<List<ParticipantResponse>>();
        participants.Should().HaveCount(4);

        var membersResponse = await ownerClient.GetAsync($"/api/v1/tournaments/{tournament.Id}/members");
        membersResponse.EnsureSuccessStatusCode();
        var members = await membersResponse.ReadAsAsync<List<TournamentMemberResponse>>();
        members.Should().ContainSingle(m => m.Role == TournamentMemberRole.Owner);
    }

    [Fact]
    public async Task InviteAsync_WhenInvitedEmailHasNoFixturelyAccount_ReturnsNotFound()
    {
        var (tournament, ownerClient) = await CreateTournamentWithParticipantsAsync("invitenoacct");

        var inviteResponse = await ownerClient.PostAsJsonAsync(
            $"/api/v1/tournaments/{tournament.Id}/invitations",
            new InviteMemberRequest("nobody-registered-here@fixturely.test", TournamentMemberRole.Viewer));

        inviteResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AcceptingAnInvitation_MakesTheTournamentVisibleInTheInviteesOwnTournamentList()
    {
        var (tournament, ownerClient) = await CreateTournamentWithParticipantsAsync("visibilitytest");

        var inviteeClient = _factory.CreateClient();
        var invitee = await TestUserFactory.RegisterAndConfirmAsync(inviteeClient, _factory.EmailCapture, "invitee");
        var (inviteeToken, _) = await TestUserFactory.LoginAsync(inviteeClient, invitee);
        inviteeClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", inviteeToken);

        // Before any invitation exists, the tournament must not appear in the invitee's own list.
        var beforeResponse = await inviteeClient.GetAsync("/api/v1/tournaments?pageNumber=1&pageSize=50");
        var before = await beforeResponse.ReadAsAsync<PagedResult<TournamentSummaryResponse>>();
        before!.Items.Should().NotContain(t => t.Id == tournament.Id);

        var inviteResponse = await ownerClient.PostAsJsonAsync(
            $"/api/v1/tournaments/{tournament.Id}/invitations",
            new InviteMemberRequest(invitee.Email, TournamentMemberRole.Viewer));
        inviteResponse.EnsureSuccessStatusCode();

        // Discover the invitation the way the frontend's "Invitations" tab does - by the invitee's
        // own email, never a client-supplied tournament/invitation id.
        var mineResponse = await inviteeClient.GetAsync("/api/v1/invitations/mine");
        mineResponse.EnsureSuccessStatusCode();
        var mine = await mineResponse.ReadAsAsync<List<InvitationResponse>>();
        var myInvitation = mine.Should().ContainSingle(i => i.TournamentId == tournament.Id).Subject;
        myInvitation.TournamentName.Should().Be(tournament.Name);
        myInvitation.Role.Should().Be(TournamentMemberRole.Viewer);

        var acceptResponse = await inviteeClient.PostAsync($"/api/v1/invitations/mine/{myInvitation.Id}/accept", null);
        acceptResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // The accepted invitation must no longer be listed as pending.
        var afterAcceptMineResponse = await inviteeClient.GetAsync("/api/v1/invitations/mine");
        var afterAcceptMine = await afterAcceptMineResponse.ReadAsAsync<List<InvitationResponse>>();
        afterAcceptMine.Should().BeEmpty();

        // This is the actual assertion the user cares about: the tournament now shows up in the
        // invitee's own Dashboard/Tournaments list, with the invited role attached.
        var afterResponse = await inviteeClient.GetAsync("/api/v1/tournaments?pageNumber=1&pageSize=50");
        var after = await afterResponse.ReadAsAsync<PagedResult<TournamentSummaryResponse>>();
        var visibleTournament = after!.Items.Should().ContainSingle(t => t.Id == tournament.Id).Subject;
        visibleTournament.CurrentUserRole.Should().Be(TournamentMemberRole.Viewer);
    }
}
