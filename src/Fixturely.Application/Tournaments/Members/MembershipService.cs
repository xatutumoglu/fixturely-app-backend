using Fixturely.Application.Abstractions.Email;
using Fixturely.Application.Abstractions.Identity;
using Fixturely.Application.Abstractions.Persistence;
using Fixturely.Application.Abstractions.Security;
using Fixturely.Application.Common;
using Fixturely.Application.DTOs.Members;
using Fixturely.Domain.Entities;
using Fixturely.Domain.Enums;
using Fixturely.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Fixturely.Application.Tournaments.Members;

public sealed class MembershipService
{
    private const int InvitationExpiryDays = 7;

    private readonly IApplicationDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    private readonly ITournamentAuthorizationService _authorizationService;
    private readonly IIdentityService _identityService;
    private readonly IEmailNotificationService _emailNotificationService;

    public MembershipService(
        IApplicationDbContext dbContext,
        TimeProvider timeProvider,
        ITournamentAuthorizationService authorizationService,
        IIdentityService identityService,
        IEmailNotificationService emailNotificationService)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
        _authorizationService = authorizationService;
        _identityService = identityService;
        _emailNotificationService = emailNotificationService;
    }

    public async Task<IReadOnlyCollection<TournamentMemberResponse>> ListMembersAsync(
        Guid tournamentId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureCanViewAsync(tournamentId, userId, cancellationToken);

        var members = await _dbContext.TournamentMembers
            .AsNoTracking()
            .Where(m => m.TournamentId == tournamentId && m.Status == TournamentMemberStatus.Active)
            .ToListAsync(cancellationToken);

        var responses = new List<TournamentMemberResponse>();

        foreach (var member in members)
        {
            var user = await _identityService.FindByIdAsync(member.UserId, cancellationToken);
            responses.Add(new TournamentMemberResponse(
                member.Id,
                member.TournamentId,
                member.UserId,
                user?.UserName ?? "(unknown)",
                user?.Email ?? "(unknown)",
                member.Role,
                member.Status,
                member.CreatedAtUtc));
        }

        return responses;
    }

    public async Task ChangeRoleAsync(
        Guid tournamentId,
        Guid memberId,
        Guid actingUserId,
        ChangeMemberRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureIsOwnerAsync(tournamentId, actingUserId, cancellationToken);

        var member = await _dbContext.TournamentMembers
            .FirstOrDefaultAsync(m => m.Id == memberId && m.TournamentId == tournamentId, cancellationToken)
            ?? throw new InvalidTournamentStateException("Tournament member not found.");

        if (member.Role == TournamentMemberRole.Owner)
        {
            throw new InvalidTournamentStateException("The tournament owner's role cannot be changed.");
        }

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        member.ChangeRole(request.Role, utcNow);

        _dbContext.AuditLogs.Add(AuditLog.Create(
            actingUserId, tournamentId, "Membership", "RoleChanged", null, request.Role.ToString(), null, utcNow));

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveMemberAsync(
        Guid tournamentId,
        Guid memberId,
        Guid actingUserId,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureIsOwnerAsync(tournamentId, actingUserId, cancellationToken);

        var member = await _dbContext.TournamentMembers
            .FirstOrDefaultAsync(m => m.Id == memberId && m.TournamentId == tournamentId, cancellationToken)
            ?? throw new InvalidTournamentStateException("Tournament member not found.");

        if (member.Role == TournamentMemberRole.Owner)
        {
            throw new InvalidTournamentStateException("The tournament owner cannot be removed.");
        }

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        member.Remove(utcNow);

        _dbContext.AuditLogs.Add(AuditLog.Create(
            actingUserId, tournamentId, "Membership", "MemberRemoved", null, null, null, utcNow));

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<InvitationResponse> InviteAsync(
        Guid tournamentId,
        Guid ownerUserId,
        InviteMemberRequest request,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureIsOwnerAsync(tournamentId, ownerUserId, cancellationToken);

        var tournament = await _dbContext.Tournaments
            .FirstOrDefaultAsync(t => t.Id == tournamentId && !t.IsDeleted, cancellationToken)
            ?? throw new TournamentNotFoundException(tournamentId);

        if (request.Role == TournamentMemberRole.Owner)
        {
            throw new InvitationException("A tournament cannot have more than one owner.");
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var owner = await _identityService.FindByIdAsync(ownerUserId, cancellationToken);

        if (owner is not null && string.Equals(owner.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvitationException("The tournament owner cannot be invited as a member.");
        }

        var hasActiveInvitation = await _dbContext.TournamentInvitations
            .AnyAsync(
                i => i.TournamentId == tournamentId
                    && i.InvitedEmail == normalizedEmail
                    && i.Status == InvitationStatus.Pending,
                cancellationToken);

        if (hasActiveInvitation)
        {
            throw new InvitationException("There is already an active invitation for this email address.");
        }

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        var rawToken = TokenHasher.GenerateUrlSafeToken();
        var tokenHash = TokenHasher.Hash(rawToken);

        var invitation = TournamentInvitation.Create(
            tournamentId, normalizedEmail, request.Role, tokenHash, ownerUserId, utcNow, InvitationExpiryDays);

        _dbContext.TournamentInvitations.Add(invitation);

        _dbContext.AuditLogs.Add(AuditLog.Create(
            ownerUserId, tournamentId, "Invitation", "Created", null, normalizedEmail, null, utcNow));

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _emailNotificationService.SendTournamentInvitationAsync(
            normalizedEmail,
            tournament.Name,
            owner?.UserName ?? "A Fixturely tournament owner",
            request.Role.ToString(),
            rawToken,
            cancellationToken);

        return MapInvitation(invitation, tournament.Name);
    }

    public async Task ResendInvitationAsync(
        Guid tournamentId,
        Guid invitationId,
        Guid ownerUserId,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureIsOwnerAsync(tournamentId, ownerUserId, cancellationToken);

        var invitation = await _dbContext.TournamentInvitations
            .FirstOrDefaultAsync(i => i.Id == invitationId && i.TournamentId == tournamentId, cancellationToken)
            ?? throw new InvitationException("Invitation not found.");

        var tournament = await _dbContext.Tournaments
            .FirstOrDefaultAsync(t => t.Id == tournamentId, cancellationToken)
            ?? throw new TournamentNotFoundException(tournamentId);

        var owner = await _identityService.FindByIdAsync(ownerUserId, cancellationToken);

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        var rawToken = TokenHasher.GenerateUrlSafeToken();
        var tokenHash = TokenHasher.Hash(rawToken);

        invitation.Resend(tokenHash, utcNow, InvitationExpiryDays);

        _dbContext.AuditLogs.Add(AuditLog.Create(
            ownerUserId, tournamentId, "Invitation", "Resent", null, invitation.InvitedEmail, null, utcNow));

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _emailNotificationService.SendTournamentInvitationAsync(
            invitation.InvitedEmail,
            tournament.Name,
            owner?.UserName ?? "A Fixturely tournament owner",
            invitation.Role.ToString(),
            rawToken,
            cancellationToken);
    }

    public async Task RevokeInvitationAsync(
        Guid tournamentId,
        Guid invitationId,
        Guid ownerUserId,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureIsOwnerAsync(tournamentId, ownerUserId, cancellationToken);

        var invitation = await _dbContext.TournamentInvitations
            .FirstOrDefaultAsync(i => i.Id == invitationId && i.TournamentId == tournamentId, cancellationToken)
            ?? throw new InvitationException("Invitation not found.");

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        invitation.Revoke(utcNow);

        _dbContext.AuditLogs.Add(AuditLog.Create(
            ownerUserId, tournamentId, "Invitation", "Revoked", null, invitation.InvitedEmail, null, utcNow));

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<InvitationResponse> GetByTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        var tokenHash = TokenHasher.Hash(token);

        var invitation = await _dbContext.TournamentInvitations
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.TokenHash == tokenHash, cancellationToken)
            ?? throw new InvitationException("Invitation not found or already used.");

        var tournament = await _dbContext.Tournaments
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == invitation.TournamentId, cancellationToken);

        return MapInvitation(invitation, tournament?.Name ?? string.Empty);
    }

    public async Task AcceptInvitationAsync(
        string token,
        Guid acceptingUserId,
        CancellationToken cancellationToken = default)
    {
        var tokenHash = TokenHasher.Hash(token);

        var invitation = await _dbContext.TournamentInvitations
            .FirstOrDefaultAsync(i => i.TokenHash == tokenHash, cancellationToken)
            ?? throw new InvitationException("Invitation not found or already used.");

        var user = await _identityService.FindByIdAsync(acceptingUserId, cancellationToken)
            ?? throw new InvitationException("User not found.");

        if (!string.Equals(user.Email, invitation.InvitedEmail, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvitationException("This invitation was issued for a different email address.");
        }

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        invitation.Accept(acceptingUserId, utcNow);

        var existingMember = await _dbContext.TournamentMembers
            .FirstOrDefaultAsync(
                m => m.TournamentId == invitation.TournamentId && m.UserId == acceptingUserId,
                cancellationToken);

        if (existingMember is not null)
        {
            existingMember.ChangeRole(invitation.Role, utcNow);
        }
        else
        {
            _dbContext.TournamentMembers.Add(
                TournamentMember.CreateFromInvitation(invitation.TournamentId, acceptingUserId, invitation.Role, utcNow));
        }

        _dbContext.AuditLogs.Add(AuditLog.Create(
            acceptingUserId, invitation.TournamentId, "Invitation", "Accepted", null, null, null, utcNow));

        await _dbContext.SaveChangesAsync(cancellationToken);

        var tournament = await _dbContext.Tournaments
            .FirstOrDefaultAsync(t => t.Id == invitation.TournamentId, cancellationToken);

        var owner = await _identityService.FindByIdAsync(invitation.InvitedByUserId, cancellationToken);

        if (tournament is not null && owner is not null)
        {
            await _emailNotificationService.SendInvitationAcceptedAsync(
                owner.Email, tournament.Name, user.UserName, cancellationToken);
        }
    }

    private static InvitationResponse MapInvitation(TournamentInvitation invitation, string tournamentName) =>
        new(
            invitation.Id,
            invitation.TournamentId,
            tournamentName,
            invitation.InvitedEmail,
            invitation.Role,
            invitation.Status,
            invitation.ExpiresAtUtc,
            invitation.CreatedAtUtc);
}
