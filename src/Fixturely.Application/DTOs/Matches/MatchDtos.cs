using Fixturely.Domain.Enums;

namespace Fixturely.Application.DTOs.Matches;

public sealed record MatchResponse(
    Guid Id,
    Guid TournamentId,
    Guid RoundId,
    string RoundName,
    Guid? TournamentGroupId,
    Guid? HomeParticipantId,
    string? HomeParticipantName,
    Guid? AwayParticipantId,
    string? AwayParticipantName,
    MatchStatus Status,
    DateTime? ScheduledAtUtc,
    string? Venue,
    int? HomeRegularTimeScore,
    int? AwayRegularTimeScore,
    int? HomeExtraTimeScore,
    int? AwayExtraTimeScore,
    int? HomePenaltyScore,
    int? AwayPenaltyScore,
    Guid? WinnerParticipantId,
    int LegNumber,
    Guid? TieIdentifier,
    bool IsBye,
    bool IsThirdPlaceMatch,
    Guid? NextHomeMatchId,
    Guid? NextAwayMatchId,
    byte[] RowVersion);

public sealed record UpdateMatchScoreRequest(
    int HomeRegularTimeScore,
    int AwayRegularTimeScore,
    int? HomeExtraTimeScore,
    int? AwayExtraTimeScore,
    int? HomePenaltyScore,
    int? AwayPenaltyScore,
    byte[] RowVersion,
    string? Reason,
    bool ConfirmDependentInvalidation = false);

public sealed record ScheduleMatchRequest(DateTime? ScheduledAtUtc, string? Venue, byte[] RowVersion);

public sealed record InvalidateMatchRequest(string Reason, byte[] RowVersion);
