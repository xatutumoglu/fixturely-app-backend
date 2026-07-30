using System.Security.Claims;
using Fixturely.Application.DTOs.Participants;
using Fixturely.Application.Tournaments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fixturely.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/tournaments/{tournamentId:guid}/participants")]
public sealed class ParticipantsController : ControllerBase
{
    private readonly ParticipantService _participantService;

    public ParticipantsController(ParticipantService participantService)
    {
        _participantService = participantService;
    }

    [HttpGet]
    public async Task<IActionResult> ListAsync(Guid tournamentId, CancellationToken cancellationToken)
    {
        var result = await _participantService.ListAsync(tournamentId, GetUserId(), cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<ParticipantResponse>> AddAsync(
        Guid tournamentId,
        CreateParticipantRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _participantService.AddAsync(tournamentId, GetUserId(), request, cancellationToken);
        return CreatedAtAction("List", new { tournamentId }, result);
    }

    [HttpPut("{participantId:guid}")]
    public async Task<ActionResult<ParticipantResponse>> UpdateAsync(
        Guid tournamentId,
        Guid participantId,
        UpdateParticipantRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _participantService.UpdateAsync(
            tournamentId, participantId, GetUserId(), request, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{participantId:guid}")]
    public async Task<IActionResult> RemoveAsync(
        Guid tournamentId,
        Guid participantId,
        CancellationToken cancellationToken)
    {
        await _participantService.RemoveAsync(tournamentId, participantId, GetUserId(), cancellationToken);
        return NoContent();
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
