using System.Security.Claims;
using Fixturely.Application.DTOs.Members;
using Fixturely.Application.Tournaments.Members;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Fixturely.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/tournaments/{tournamentId:guid}")]
public sealed class MembersController : ControllerBase
{
    private readonly MembershipService _membershipService;

    public MembersController(MembershipService membershipService)
    {
        _membershipService = membershipService;
    }

    [HttpGet("members")]
    public async Task<IActionResult> ListMembersAsync(Guid tournamentId, CancellationToken cancellationToken)
    {
        var result = await _membershipService.ListMembersAsync(tournamentId, GetUserId(), cancellationToken);
        return Ok(result);
    }

    [HttpPut("members/{memberId:guid}/role")]
    public async Task<IActionResult> ChangeRoleAsync(
        Guid tournamentId,
        Guid memberId,
        ChangeMemberRoleRequest request,
        CancellationToken cancellationToken)
    {
        await _membershipService.ChangeRoleAsync(tournamentId, memberId, GetUserId(), request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("members/{memberId:guid}")]
    public async Task<IActionResult> RemoveMemberAsync(
        Guid tournamentId,
        Guid memberId,
        CancellationToken cancellationToken)
    {
        await _membershipService.RemoveMemberAsync(tournamentId, memberId, GetUserId(), cancellationToken);
        return NoContent();
    }

    [HttpDelete("members/bulk")]
    public async Task<ActionResult<IReadOnlyCollection<BulkRemoveResultItem>>> RemoveMembersBulkAsync(
        Guid tournamentId,
        [FromBody] BulkRemoveMembersRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _membershipService.RemoveMembersBulkAsync(
            tournamentId, GetUserId(), request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("invitations")]
    [EnableRateLimiting("invitation-sensitive")]
    public async Task<ActionResult<InvitationResponse>> InviteAsync(
        Guid tournamentId,
        InviteMemberRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _membershipService.InviteAsync(tournamentId, GetUserId(), request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("invitations/bulk")]
    [EnableRateLimiting("invitation-sensitive")]
    public async Task<ActionResult<IReadOnlyCollection<BulkInviteResultItem>>> InviteBulkAsync(
        Guid tournamentId,
        BulkInviteMembersRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _membershipService.InviteBulkAsync(tournamentId, GetUserId(), request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("invitations/{invitationId:guid}/resend")]
    [EnableRateLimiting("invitation-sensitive")]
    public async Task<IActionResult> ResendInvitationAsync(
        Guid tournamentId,
        Guid invitationId,
        CancellationToken cancellationToken)
    {
        await _membershipService.ResendInvitationAsync(tournamentId, invitationId, GetUserId(), cancellationToken);
        return NoContent();
    }

    [HttpDelete("invitations/{invitationId:guid}")]
    public async Task<IActionResult> RevokeInvitationAsync(
        Guid tournamentId,
        Guid invitationId,
        CancellationToken cancellationToken)
    {
        await _membershipService.RevokeInvitationAsync(tournamentId, invitationId, GetUserId(), cancellationToken);
        return NoContent();
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
