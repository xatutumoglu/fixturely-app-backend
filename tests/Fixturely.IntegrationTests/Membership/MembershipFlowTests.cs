using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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
}
