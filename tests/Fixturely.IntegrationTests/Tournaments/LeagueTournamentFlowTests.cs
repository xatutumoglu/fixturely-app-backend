using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Fixturely.Application.DTOs.Common;
using Fixturely.Application.DTOs.Matches;
using Fixturely.Application.DTOs.Participants;
using Fixturely.Application.DTOs.Tournaments;
using Fixturely.Domain.Enums;
using Fixturely.IntegrationTests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Fixturely.IntegrationTests.Tournaments;

[Collection(IntegrationTestCollection.Name)]
public sealed class LeagueTournamentFlowTests
{
    private readonly IntegrationTestWebAppFactory _factory;

    public LeagueTournamentFlowTests(IntegrationTestWebAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task OwnerCreatesTournament_AddsParticipants_GeneratesFixture_EntersScores_RetrievesStandings()
    {
        var client = _factory.CreateClient();
        var owner = await TestUserFactory.RegisterAndConfirmAsync(client, _factory.EmailCapture, "leagueowner");
        var (accessToken, _) = await TestUserFactory.LoginAsync(client, owner);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var createResponse = await client.PostAsJsonAsync("/api/v1/tournaments", new CreateTournamentRequest(
            "Integration League", "Test league", TournamentFormat.League, LegMode.SingleLeg, null, false));
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var tournament = await createResponse.ReadAsAsync<TournamentDetailResponse>();
        tournament.Should().NotBeNull();

        foreach (var teamName in new[] { "Alpha FC", "Beta FC", "Gamma FC", "Delta FC" })
        {
            var participantResponse = await client.PostAsJsonAsync(
                $"/api/v1/tournaments/{tournament!.Id}/participants", new CreateParticipantRequest(teamName, null));
            participantResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        var generateResponse = await client.PostAsync($"/api/v1/tournaments/{tournament!.Id}/generate-fixture", null);
        generateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var matchesResponse = await client.GetAsync($"/api/v1/tournaments/{tournament.Id}/matches");
        matchesResponse.EnsureSuccessStatusCode();
        var matches = await matchesResponse.ReadAsAsync<List<MatchResponse>>();
        matches.Should().HaveCount(6);

        var firstMatch = matches!.First();
        var scoreResponse = await client.PutAsJsonAsync(
            $"/api/v1/tournaments/{tournament.Id}/matches/{firstMatch.Id}/score",
            new UpdateMatchScoreRequest(2, 1, null, null, null, null, firstMatch.RowVersion, null));
        scoreResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var standingsResponse = await client.GetAsync($"/api/v1/tournaments/{tournament.Id}/standings");
        standingsResponse.EnsureSuccessStatusCode();
        var standings = await standingsResponse.ReadAsAsync<List<GroupStandingsResponse>>();

        standings.Should().ContainSingle();
        var winnerRow = standings![0].Standings.First(s => s.ParticipantId == firstMatch.HomeParticipantId);
        winnerRow.Points.Should().Be(3);
        winnerRow.Won.Should().Be(1);
    }

    [Fact]
    public async Task UnauthorizedUser_CannotAccessAnotherUsersPrivateTournament()
    {
        var client = _factory.CreateClient();
        var owner = await TestUserFactory.RegisterAndConfirmAsync(client, _factory.EmailCapture, "privateowner");
        var (ownerToken, _) = await TestUserFactory.LoginAsync(client, owner);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);

        var createResponse = await client.PostAsJsonAsync("/api/v1/tournaments", new CreateTournamentRequest(
            "Private League", null, TournamentFormat.League, LegMode.SingleLeg, null, false));
        var tournament = await createResponse.ReadAsAsync<TournamentDetailResponse>();

        var stranger = await TestUserFactory.RegisterAndConfirmAsync(client, _factory.EmailCapture, "stranger");
        var (strangerToken, _) = await TestUserFactory.LoginAsync(client, stranger);

        var strangerClient = _factory.CreateClient();
        strangerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", strangerToken);

        var response = await strangerClient.GetAsync($"/api/v1/tournaments/{tournament!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
