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
public sealed class GroupKnockoutFlowTests
{
    private readonly IntegrationTestWebAppFactory _factory;

    public GroupKnockoutFlowTests(IntegrationTestWebAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GroupKnockoutTournament_GeneratesGroupsAndKnockoutBracket()
    {
        var client = _factory.CreateClient();
        var owner = await TestUserFactory.RegisterAndConfirmAsync(client, _factory.EmailCapture, "gkowner");
        var (accessToken, _) = await TestUserFactory.LoginAsync(client, owner);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var createResponse = await client.PostAsJsonAsync("/api/v1/tournaments", new CreateTournamentRequest(
            "Integration GroupKnockout", null, TournamentFormat.GroupKnockout, LegMode.SingleLeg, 2, true));
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var tournament = await createResponse.ReadAsAsync<TournamentDetailResponse>();

        for (var i = 1; i <= 8; i++)
        {
            var response = await client.PostAsJsonAsync(
                $"/api/v1/tournaments/{tournament!.Id}/participants", new CreateParticipantRequest($"Team {i}", null));
            response.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        var generateResponse = await client.PostAsync($"/api/v1/tournaments/{tournament!.Id}/generate-fixture", null);
        generateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var groupsResponse = await client.GetAsync($"/api/v1/tournaments/{tournament.Id}/groups");
        groupsResponse.EnsureSuccessStatusCode();
        var groupsJson = await groupsResponse.Content.ReadAsStringAsync();
        groupsJson.Should().Contain("Group A");
        groupsJson.Should().Contain("Group B");

        var bracketResponse = await client.GetAsync($"/api/v1/tournaments/{tournament.Id}/bracket");
        bracketResponse.EnsureSuccessStatusCode();
        var bracket = await bracketResponse.ReadAsAsync<BracketResponse>();
        bracket!.Matches.Should().Contain(m => m.IsThirdPlaceMatch);

        var matchesResponse = await client.GetAsync($"/api/v1/tournaments/{tournament.Id}/matches");
        var matches = await matchesResponse.ReadAsAsync<List<MatchResponse>>();

        var groupMatches = matches!.Where(m => m.TournamentGroupId != null).ToList();
        groupMatches.Should().HaveCount(12);
    }
}
