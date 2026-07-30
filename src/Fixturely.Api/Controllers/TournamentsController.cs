using System.Security.Claims;
using Fixturely.Application.Common;
using Fixturely.Application.DTOs.Tournaments;
using Fixturely.Application.Tournaments;
using Fixturely.Application.Tournaments.Fixtures;
using Fixturely.Application.Tournaments.Standings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fixturely.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/tournaments")]
public sealed class TournamentsController : ControllerBase
{
    private readonly TournamentService _tournamentService;
    private readonly FixtureGenerationService _fixtureGenerationService;
    private readonly TournamentQueryService _tournamentQueryService;

    public TournamentsController(
        TournamentService tournamentService,
        FixtureGenerationService fixtureGenerationService,
        TournamentQueryService tournamentQueryService)
    {
        _tournamentService = tournamentService;
        _fixtureGenerationService = fixtureGenerationService;
        _tournamentQueryService = tournamentQueryService;
    }

    [HttpGet]
    public async Task<IActionResult> ListAsync(
        [FromQuery] int pageNumber,
        [FromQuery] int pageSize,
        CancellationToken cancellationToken)
    {
        var pagination = new PaginationRequest
        {
            PageNumber = pageNumber <= 0 ? 1 : pageNumber,
            PageSize = pageSize
        };

        var result = await _tournamentService.ListForUserAsync(GetUserId(), pagination, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<TournamentDetailResponse>> CreateAsync(
        CreateTournamentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _tournamentService.CreateAsync(GetUserId(), request, cancellationToken);
        return CreatedAtAction("GetById", new { tournamentId = result.Id }, result);
    }

    [HttpGet("{tournamentId:guid}")]
    public async Task<ActionResult<TournamentDetailResponse>> GetByIdAsync(
        Guid tournamentId,
        CancellationToken cancellationToken)
    {
        var result = await _tournamentService.GetByIdAsync(tournamentId, GetUserId(), cancellationToken);
        return Ok(result);
    }

    [HttpPut("{tournamentId:guid}")]
    public async Task<ActionResult<TournamentDetailResponse>> UpdateAsync(
        Guid tournamentId,
        UpdateTournamentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _tournamentService.UpdateAsync(tournamentId, GetUserId(), request, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{tournamentId:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid tournamentId, CancellationToken cancellationToken)
    {
        await _tournamentService.DeleteAsync(tournamentId, GetUserId(), cancellationToken);
        return NoContent();
    }

    [HttpPost("{tournamentId:guid}/archive")]
    public async Task<ActionResult<TournamentDetailResponse>> ArchiveAsync(
        Guid tournamentId,
        CancellationToken cancellationToken)
    {
        var result = await _tournamentService.ArchiveAsync(tournamentId, GetUserId(), cancellationToken);
        return Ok(result);
    }

    [HttpPost("{tournamentId:guid}/reopen")]
    public async Task<ActionResult<TournamentDetailResponse>> ReopenAsync(
        Guid tournamentId,
        CancellationToken cancellationToken)
    {
        var result = await _tournamentService.ReopenAsync(tournamentId, GetUserId(), cancellationToken);
        return Ok(result);
    }

    [HttpPost("{tournamentId:guid}/generate-fixture")]
    public async Task<IActionResult> GenerateFixtureAsync(Guid tournamentId, CancellationToken cancellationToken)
    {
        var generationNumber = await _fixtureGenerationService.GenerateAsync(tournamentId, GetUserId(), cancellationToken);
        return Ok(new { generationNumber });
    }

    [HttpPost("{tournamentId:guid}/regenerate-fixture")]
    public async Task<IActionResult> RegenerateFixtureAsync(Guid tournamentId, CancellationToken cancellationToken)
    {
        var generationNumber = await _fixtureGenerationService.RegenerateAsync(tournamentId, GetUserId(), cancellationToken);
        return Ok(new { generationNumber });
    }

    [HttpPost("{tournamentId:guid}/confirm-fixture")]
    public async Task<IActionResult> ConfirmFixtureAsync(Guid tournamentId, CancellationToken cancellationToken)
    {
        await _fixtureGenerationService.ConfirmAsync(tournamentId, GetUserId(), cancellationToken);
        return NoContent();
    }

    [HttpGet("{tournamentId:guid}/standings")]
    public async Task<IActionResult> GetStandingsAsync(Guid tournamentId, CancellationToken cancellationToken)
    {
        var result = await _tournamentQueryService.GetStandingsAsync(tournamentId, GetUserId(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{tournamentId:guid}/groups")]
    public async Task<IActionResult> GetGroupsAsync(Guid tournamentId, CancellationToken cancellationToken)
    {
        var groups = await _tournamentQueryService.GetGroupsAsync(tournamentId, GetUserId(), cancellationToken);

        var response = groups.Select(g => new
        {
            g.Id,
            g.Name,
            g.OrderIndex,
            ParticipantIds = g.GroupParticipants.Select(gp => gp.ParticipantId)
        });

        return Ok(response);
    }

    [HttpGet("{tournamentId:guid}/bracket")]
    public async Task<IActionResult> GetBracketAsync(Guid tournamentId, CancellationToken cancellationToken)
    {
        var result = await _tournamentQueryService.GetBracketAsync(tournamentId, GetUserId(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{tournamentId:guid}/rounds")]
    public async Task<IActionResult> GetRoundsAsync(Guid tournamentId, CancellationToken cancellationToken)
    {
        var result = await _tournamentQueryService.GetRoundsAsync(tournamentId, GetUserId(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{tournamentId:guid}/audit-logs")]
    public async Task<IActionResult> GetAuditLogsAsync(Guid tournamentId, CancellationToken cancellationToken)
    {
        var result = await _tournamentQueryService.GetAuditLogsAsync(tournamentId, GetUserId(), cancellationToken);
        return Ok(result);
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
