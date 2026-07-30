using Fixturely.Domain.Common;
using Fixturely.Domain.Enums;

namespace Fixturely.Domain.Entities;

public sealed class TournamentMember : Entity
{
    private TournamentMember()
    {
    }

    public Guid TournamentId { get; private set; }

    public Guid UserId { get; private set; }

    public TournamentMemberRole Role { get; private set; }

    public TournamentMemberStatus Status { get; private set; }

    public static TournamentMember CreateOwner(Guid tournamentId, Guid ownerUserId, DateTime utcNow)
    {
        var member = new TournamentMember
        {
            TournamentId = tournamentId,
            UserId = ownerUserId,
            Role = TournamentMemberRole.Owner,
            Status = TournamentMemberStatus.Active
        };
        member.Initialize(utcNow);
        return member;
    }

    public static TournamentMember CreateFromInvitation(
        Guid tournamentId,
        Guid userId,
        TournamentMemberRole role,
        DateTime utcNow)
    {
        var member = new TournamentMember
        {
            TournamentId = tournamentId,
            UserId = userId,
            Role = role,
            Status = TournamentMemberStatus.Active
        };
        member.Initialize(utcNow);
        return member;
    }

    public void ChangeRole(TournamentMemberRole role, DateTime utcNow)
    {
        Role = role;
        Touch(utcNow);
    }

    public void Remove(DateTime utcNow)
    {
        Status = TournamentMemberStatus.Removed;
        Touch(utcNow);
    }
}
