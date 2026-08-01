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
            ?? throw new InvalidTournamentStateException(
                ErrorCodes.TournamentMemberNotFound, "Tournament member not found.");

        if (member.Role == TournamentMemberRole.Owner)
        {
            throw new InvalidTournamentStateException(
                ErrorCodes.OwnerRoleCannotChange, "The tournament owner's role cannot be changed.");
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
            ?? throw new InvalidTournamentStateException(
                ErrorCodes.TournamentMemberNotFound, "Tournament member not found.");

        if (member.Role == TournamentMemberRole.Owner)
        {
            throw new InvalidTournamentStateException(
                ErrorCodes.OwnerCannotBeRemoved, "The tournament owner cannot be removed.");
        }

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        member.Remove(utcNow);

        _dbContext.AuditLogs.Add(AuditLog.Create(
            actingUserId, tournamentId, "Membership", "MemberRemoved", null, null, null, utcNow));

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<BulkRemoveResultItem>> RemoveMembersBulkAsync(
        Guid tournamentId,
        Guid actingUserId,
        BulkRemoveMembersRequest request,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureIsOwnerAsync(tournamentId, actingUserId, cancellationToken);

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        var results = new List<BulkRemoveResultItem>();

        foreach (var memberId in request.MemberIds.Distinct())
        {
            var member = await _dbContext.TournamentMembers
                .FirstOrDefaultAsync(m => m.Id == memberId && m.TournamentId == tournamentId, cancellationToken);

            if (member is null)
            {
                continue;
            }

            if (member.Role == TournamentMemberRole.Owner)
            {
                results.Add(new BulkRemoveResultItem(
                    memberId, false, "The tournament owner cannot be removed.", ErrorCodes.OwnerCannotBeRemoved));
                continue;
            }

            member.Remove(utcNow);

            _dbContext.AuditLogs.Add(AuditLog.Create(
                actingUserId, tournamentId, "Membership", "MemberRemoved", null, null, null, utcNow));

            results.Add(new BulkRemoveResultItem(memberId, true, null));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return results;
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
            throw new InvitationException(
                ErrorCodes.InvitationOnlyOneOwner, "A tournament cannot have more than one owner.");
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var recipient = await _identityService.FindByEmailAsync(normalizedEmail, cancellationToken)
            ?? throw new UserNotRegisteredException(normalizedEmail);

        var owner = await _identityService.FindByIdAsync(ownerUserId, cancellationToken);

        if (owner is not null && string.Equals(owner.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvitationException(
                ErrorCodes.InvitationOwnerCannotBeInvited, "The tournament owner cannot be invited as a member.");
        }

        var hasActiveInvitation = await _dbContext.TournamentInvitations
            .AnyAsync(
                i => i.TournamentId == tournamentId
                    && i.InvitedEmail == normalizedEmail
                    && i.Status == InvitationStatus.Pending,
                cancellationToken);

        if (hasActiveInvitation)
        {
            throw new InvitationException(
                ErrorCodes.InvitationAlreadyActive, "There is already an active invitation for this email address.");
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

    /// <summary>
    /// Invites many recipients in one call, processing each email independently through
    /// <see cref="InviteAsync"/> so a bad email (unregistered account, duplicate active
    /// invitation, etc.) never aborts the rest of the batch - the caller gets a per-email
    /// success/failure breakdown instead.
    /// </summary>
    public async Task<IReadOnlyCollection<BulkInviteResultItem>> InviteBulkAsync(
        Guid tournamentId,
        Guid ownerUserId,
        BulkInviteMembersRequest request,
        CancellationToken cancellationToken = default)
    {
        var results = new List<BulkInviteResultItem>();
        var seenEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawEmail in request.Emails)
        {
            var email = rawEmail.Trim();

            if (email.Length == 0)
            {
                continue;
            }

            if (!seenEmails.Add(email))
            {
                results.Add(new BulkInviteResultItem(
                    email, false, null, "Duplicate email in this batch.", ErrorCodes.DuplicateEmailInBatch));
                continue;
            }

            try
            {
                var invitation = await InviteAsync(
                    tournamentId, ownerUserId, new InviteMemberRequest(email, request.Role), cancellationToken);

                results.Add(new BulkInviteResultItem(email, true, invitation, null));
            }
            catch (DomainException exception)
            {
                results.Add(new BulkInviteResultItem(email, false, null, exception.Message, exception.ErrorCode));
            }
        }

        return results;
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
            ?? throw new InvitationException(ErrorCodes.InvitationNotFound, "Invitation not found.");

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
            ?? throw new InvitationException(ErrorCodes.InvitationNotFound, "Invitation not found.");

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
            ?? throw new InvitationException(ErrorCodes.InvitationNotFoundOrUsed, "Invitation not found or already used.");

        var tournament = await _dbContext.Tournaments
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == invitation.TournamentId, cancellationToken);

        return MapInvitation(invitation, tournament?.Name ?? string.Empty);
    }

    public async Task<IReadOnlyCollection<InvitationResponse>> ListMyInvitationsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _identityService.FindByIdAsync(userId, cancellationToken)
            ?? throw new InvitationException(ErrorCodes.UserNotFound, "User not found.");

        var normalizedEmail = user.Email.Trim().ToLowerInvariant();
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;

        var invitations = await _dbContext.TournamentInvitations
            .AsNoTracking()
            .Where(i => i.InvitedEmail == normalizedEmail
                && i.Status == InvitationStatus.Pending
                && i.ExpiresAtUtc >= utcNow)
            .OrderByDescending(i => i.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        if (invitations.Count == 0)
        {
            return Array.Empty<InvitationResponse>();
        }

        var tournamentIds = invitations.Select(i => i.TournamentId).Distinct().ToList();
        var tournamentNames = await _dbContext.Tournaments
            .AsNoTracking()
            .Where(t => tournamentIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.Name, cancellationToken);

        return invitations
            .Select(i => MapInvitation(i, tournamentNames.GetValueOrDefault(i.TournamentId, string.Empty)))
            .ToList();
    }

    public async Task AcceptInvitationAsync(
        string token,
        Guid acceptingUserId,
        CancellationToken cancellationToken = default)
    {
        var tokenHash = TokenHasher.Hash(token);

        var invitation = await _dbContext.TournamentInvitations
            .FirstOrDefaultAsync(i => i.TokenHash == tokenHash, cancellationToken)
            ?? throw new InvitationException(ErrorCodes.InvitationNotFoundOrUsed, "Invitation not found or already used.");

        await AcceptInvitationCoreAsync(invitation, acceptingUserId, cancellationToken);
    }

    /// <summary>
    /// Accepts an invitation the caller already knows the id of (surfaced via
    /// <see cref="ListMyInvitationsAsync"/>), without requiring the raw email-link token. This is
    /// safe precisely because the invitation is only ever discoverable through
    /// <see cref="ListMyInvitationsAsync"/>, which itself is scoped to the authenticated caller's
    /// own verified account email (never a client-supplied email) - so no new enumeration surface
    /// is introduced versus the token-based flow.
    /// </summary>
    public async Task AcceptMyInvitationAsync(
        Guid invitationId,
        Guid acceptingUserId,
        CancellationToken cancellationToken = default)
    {
        var invitation = await _dbContext.TournamentInvitations
            .FirstOrDefaultAsync(i => i.Id == invitationId, cancellationToken)
            ?? throw new InvitationException(ErrorCodes.InvitationNotFoundOrUsed, "Invitation not found or already used.");

        await AcceptInvitationCoreAsync(invitation, acceptingUserId, cancellationToken);
    }

    private async Task AcceptInvitationCoreAsync(
        TournamentInvitation invitation,
        Guid acceptingUserId,
        CancellationToken cancellationToken)
    {
        var user = await _identityService.FindByIdAsync(acceptingUserId, cancellationToken)
            ?? throw new InvitationException(ErrorCodes.UserNotFound, "User not found.");

        if (!string.Equals(user.Email, invitation.InvitedEmail, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvitationException(
                ErrorCodes.InvitationEmailMismatch, "This invitation was issued for a different email address.");
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
