using Fixturely.Domain.Common;
using Fixturely.Domain.Enums;
using Fixturely.Domain.Exceptions;

namespace Fixturely.Domain.Entities;

public sealed class TournamentInvitation : Entity
{
    private TournamentInvitation()
    {
    }

    public Guid TournamentId { get; private set; }

    public string InvitedEmail { get; private set; } = string.Empty;

    public TournamentMemberRole Role { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public InvitationStatus Status { get; private set; }

    public Guid InvitedByUserId { get; private set; }

    public DateTime ExpiresAtUtc { get; private set; }

    public DateTime? AcceptedAtUtc { get; private set; }

    public Guid? AcceptedByUserId { get; private set; }

    public static TournamentInvitation Create(
        Guid tournamentId,
        string invitedEmail,
        TournamentMemberRole role,
        string tokenHash,
        Guid invitedByUserId,
        DateTime utcNow,
        int expiryDays = 7)
    {
        var invitation = new TournamentInvitation
        {
            TournamentId = tournamentId,
            InvitedEmail = invitedEmail.Trim().ToLowerInvariant(),
            Role = role,
            TokenHash = tokenHash,
            InvitedByUserId = invitedByUserId,
            Status = InvitationStatus.Pending,
            ExpiresAtUtc = utcNow.AddDays(expiryDays)
        };
        invitation.Initialize(utcNow);
        return invitation;
    }

    public void Resend(string newTokenHash, DateTime utcNow, int expiryDays = 7)
    {
        if (Status != InvitationStatus.Pending)
        {
            throw new InvitationException(
                ErrorCodes.InvitationOnlyPendingCanResend, "Only pending invitations can be resent.");
        }

        TokenHash = newTokenHash;
        ExpiresAtUtc = utcNow.AddDays(expiryDays);
        Touch(utcNow);
    }

    public void Revoke(DateTime utcNow)
    {
        if (Status != InvitationStatus.Pending)
        {
            throw new InvitationException(
                ErrorCodes.InvitationOnlyPendingCanRevoke, "Only pending invitations can be revoked.");
        }

        Status = InvitationStatus.Revoked;
        Touch(utcNow);
    }

    public void Accept(Guid acceptedByUserId, DateTime utcNow)
    {
        if (Status != InvitationStatus.Pending)
        {
            throw new InvitationException(
                ErrorCodes.InvitationNoLongerValid, "This invitation is no longer valid.");
        }

        if (utcNow > ExpiresAtUtc)
        {
            Status = InvitationStatus.Expired;
            Touch(utcNow);
            throw new InvitationException(ErrorCodes.InvitationExpired, "This invitation has expired.");
        }

        Status = InvitationStatus.Accepted;
        AcceptedAtUtc = utcNow;
        AcceptedByUserId = acceptedByUserId;
        Touch(utcNow);
    }

    public bool IsActive(DateTime utcNow)
    {
        return Status == InvitationStatus.Pending && utcNow <= ExpiresAtUtc;
    }
}
