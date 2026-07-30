namespace Fixturely.Application.DTOs.Participants;

public sealed record CreateParticipantRequest(string Name, string? ShortCode);

public sealed record UpdateParticipantRequest(string Name, string? ShortCode);

public sealed record ParticipantResponse(Guid Id, Guid TournamentId, string Name, string? ShortCode);
