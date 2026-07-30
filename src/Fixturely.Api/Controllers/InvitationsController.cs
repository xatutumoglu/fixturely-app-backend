using System.Security.Claims;
using Fixturely.Application.DTOs.Members;
using Fixturely.Application.Tournaments.Members;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Fixturely.Api.Controllers;

[ApiController]
[Route("api/v1/invitations")]
public sealed class InvitationsController : ControllerBase
{
    private readonly MembershipService _membershipService;

    public InvitationsController(MembershipService membershipService)
    {
        _membershipService = membershipService;
    }

    [HttpGet("{token}")]
    [EnableRateLimiting("invitation-sensitive")]
    public async Task<ActionResult<InvitationResponse>> GetByTokenAsync(string token, CancellationToken cancellationToken)
    {
        var result = await _membershipService.GetByTokenAsync(token, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{token}/accept")]
    [Authorize]
    [EnableRateLimiting("invitation-sensitive")]
    public async Task<IActionResult> AcceptAsync(string token, CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await _membershipService.AcceptInvitationAsync(token, userId, cancellationToken);
        return NoContent();
    }
}
