using System.Security.Claims;
using Fixturely.Application.DTOs.Matches;
using Fixturely.Application.Tournaments.Matches;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fixturely.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/tournaments/{tournamentId:guid}/matches")]
public sealed class MatchesController : ControllerBase
{
    private readonly MatchService _matchService;

    public MatchesController(MatchService matchService)
    {
        _matchService = matchService;
    }

    [HttpGet]
    public async Task<IActionResult> ListAsync(Guid tournamentId, CancellationToken cancellationToken)
    {
        var result = await _matchService.ListAsync(tournamentId, GetUserId(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{matchId:guid}")]
    public async Task<ActionResult<MatchResponse>> GetByIdAsync(
        Guid tournamentId,
        Guid matchId,
        CancellationToken cancellationToken)
    {
        var matches = await _matchService.ListAsync(tournamentId, GetUserId(), cancellationToken);
        var match = matches.FirstOrDefault(m => m.Id == matchId);
        return match is null ? NotFound() : Ok(match);
    }

    [HttpPut("{matchId:guid}/score")]
    public async Task<ActionResult<MatchResponse>> UpdateScoreAsync(
        Guid tournamentId,
        Guid matchId,
        UpdateMatchScoreRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _matchService.UpdateScoreAsync(tournamentId, matchId, GetUserId(), request, cancellationToken);
        return Ok(result);
    }

    [HttpPut("{matchId:guid}/schedule")]
    public async Task<ActionResult<MatchResponse>> ScheduleAsync(
        Guid tournamentId,
        Guid matchId,
        ScheduleMatchRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _matchService.ScheduleAsync(tournamentId, matchId, GetUserId(), request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{matchId:guid}/invalidate")]
    public async Task<IActionResult> InvalidateAsync(
        Guid tournamentId,
        Guid matchId,
        InvalidateMatchRequest request,
        CancellationToken cancellationToken)
    {
        await _matchService.InvalidateAsync(tournamentId, matchId, GetUserId(), request, cancellationToken);
        return NoContent();
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
