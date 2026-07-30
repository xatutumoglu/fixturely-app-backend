using Fixturely.Domain.Enums;

namespace Fixturely.Application.DTOs.Members;

public sealed record TournamentMemberResponse(
    Guid Id,
    Guid TournamentId,
    Guid UserId,
    string UserName,
    string Email,
    TournamentMemberRole Role,
    TournamentMemberStatus Status,
    DateTime CreatedAtUtc);

public sealed record InviteMemberRequest(string Email, TournamentMemberRole Role);

public sealed record ChangeMemberRoleRequest(TournamentMemberRole Role);

public sealed record InvitationResponse(
    Guid Id,
    Guid TournamentId,
    string TournamentName,
    string InvitedEmail,
    TournamentMemberRole Role,
    InvitationStatus Status,
    DateTime ExpiresAtUtc,
    DateTime CreatedAtUtc);

public sealed record AcceptInvitationRequest(string Token);
