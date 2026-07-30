using Fixturely.Domain.Entities;

namespace Fixturely.UnitTests.TestHelpers;

public static class TestEntityFactory
{
    public static readonly DateTime UtcNow = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    public static Tournament CreateLeagueTournament(Guid ownerUserId, int participantCount, Fixturely.Domain.Enums.LegMode legMode)
    {
        var tournament = Tournament.Create(
            "Test League",
            "Description",
            ownerUserId,
            Fixturely.Domain.Enums.TournamentFormat.League,
            legMode,
            numberOfGroups: null,
            hasThirdPlaceMatch: false,
            UtcNow);

        tournament.MoveToSetup(UtcNow);

        for (var i = 0; i < participantCount; i++)
        {
            tournament.AddParticipant(Participant.Create(tournament.Id, $"Team {i + 1}", null, UtcNow));
        }

        return tournament;
    }

    public static List<Participant> CreateParticipants(Guid tournamentId, int count)
    {
        var participants = new List<Participant>();

        for (var i = 0; i < count; i++)
        {
            participants.Add(Participant.Create(tournamentId, $"Team {i + 1}", null, UtcNow));
        }

        return participants;
    }
}
