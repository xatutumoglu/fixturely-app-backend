namespace Fixturely.Application.DTOs.Common;

public sealed record StandingsRowResponse(
    int Position,
    Guid ParticipantId,
    string ParticipantName,
    int Played,
    int Won,
    int Drawn,
    int Lost,
    int GoalsFor,
    int GoalsAgainst,
    int GoalDifference,
    int Points,
    string? TieBreakNote);

public sealed record GroupStandingsResponse(
    Guid TournamentGroupId,
    string GroupName,
    IReadOnlyCollection<StandingsRowResponse> Standings);

public sealed record RoundResponse(Guid Id, int RoundNumber, string Name, string Phase, Guid? TournamentGroupId);

public sealed record AuditLogResponse(
    Guid Id,
    Guid? UserId,
    string Category,
    string Action,
    string? Reason,
    DateTime OccurredAtUtc);

public sealed record BracketMatchNode(
    Guid MatchId,
    int RoundNumber,
    string RoundName,
    Guid? HomeParticipantId,
    string? HomeParticipantName,
    Guid? AwayParticipantId,
    string? AwayParticipantName,
    Guid? WinnerParticipantId,
    bool IsThirdPlaceMatch,
    Guid? NextHomeMatchId,
    Guid? NextAwayMatchId);

public sealed record BracketResponse(IReadOnlyCollection<BracketMatchNode> Matches);
