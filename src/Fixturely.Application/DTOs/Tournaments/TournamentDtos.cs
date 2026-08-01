using Fixturely.Domain.Enums;

namespace Fixturely.Application.DTOs.Tournaments;

public sealed record CreateTournamentRequest(
    string Name,
    string? Description,
    TournamentFormat Format,
    LegMode LegMode,
    int? NumberOfGroups,
    bool HasThirdPlaceMatch);

public sealed record UpdateTournamentRequest(
    string Name,
    string? Description,
    LegMode LegMode,
    bool HasThirdPlaceMatch,
    byte[] RowVersion);

public sealed record TournamentSummaryResponse(
    Guid Id,
    string Name,
    TournamentFormat Format,
    LegMode LegMode,
    TournamentStatus Status,
    Guid OwnerUserId,
    TournamentMemberRole CurrentUserRole,
    int ParticipantCount,
    DateTime CreatedAtUtc);

public sealed record TournamentDetailResponse(
    Guid Id,
    string Name,
    string? Description,
    TournamentFormat Format,
    LegMode LegMode,
    TournamentStatus Status,
    Guid OwnerUserId,
    int? NumberOfGroups,
    bool HasThirdPlaceMatch,
    int CurrentFixtureGenerationNumber,
    TournamentMemberRole CurrentUserRole,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    byte[] RowVersion,
    int? MaxParticipants);
