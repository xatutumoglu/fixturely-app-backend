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

public sealed record BulkInviteMembersRequest(IReadOnlyCollection<string> Emails, TournamentMemberRole Role);

public sealed record BulkInviteResultItem(
    string Email,
    bool Success,
    InvitationResponse? Invitation,
    string? Error);

public sealed record BulkRemoveMembersRequest(IReadOnlyCollection<Guid> MemberIds);

public sealed record BulkRemoveResultItem(Guid MemberId, bool Success, string? Error);
