using Fixturely.Application.Abstractions.Persistence;
using Fixturely.Application.Abstractions.Security;
using Fixturely.Application.Common;
using Fixturely.Application.DTOs.Tournaments;
using Fixturely.Domain.Entities;
using Fixturely.Domain.Enums;
using Fixturely.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Fixturely.Application.Tournaments;

public sealed class TournamentService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    private readonly ITournamentAuthorizationService _authorizationService;

    public TournamentService(
        IApplicationDbContext dbContext,
        TimeProvider timeProvider,
        ITournamentAuthorizationService authorizationService)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
        _authorizationService = authorizationService;
    }

    public async Task<TournamentDetailResponse> CreateAsync(
        Guid ownerUserId,
        CreateTournamentRequest request,
        CancellationToken cancellationToken = default)
    {
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;

        var tournament = Tournament.Create(
            request.Name,
            request.Description,
            ownerUserId,
            request.Format,
            request.LegMode,
            request.NumberOfGroups,
            request.HasThirdPlaceMatch,
            utcNow);

        _dbContext.Tournaments.Add(tournament);
        await _dbContext.SaveChangesAsync(cancellationToken);

        tournament.MoveToSetup(utcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToDetail(tournament, TournamentMemberRole.Owner);
    }

    public async Task<PagedResult<TournamentSummaryResponse>> ListForUserAsync(
        Guid userId,
        PaginationRequest pagination,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Tournaments
            .AsNoTracking()
            .Where(t => !t.IsDeleted && t.Members.Any(m =>
                m.UserId == userId && m.Status == TournamentMemberStatus.Active))
            .OrderByDescending(t => t.CreatedAtUtc);

        var totalCount = await query.CountAsync(cancellationToken);

        var tournaments = await query
            .Skip((pagination.PageNumber - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .Select(t => new
            {
                Tournament = t,
                Role = t.Members.First(m => m.UserId == userId).Role,
                ParticipantCount = t.Participants.Count(p => !p.IsDeleted)
            })
            .ToListAsync(cancellationToken);

        var items = tournaments
            .Select(x => new TournamentSummaryResponse(
                x.Tournament.Id,
                x.Tournament.Name,
                x.Tournament.Format,
                x.Tournament.LegMode,
                x.Tournament.Status,
                x.Tournament.OwnerUserId,
                x.Role,
                x.ParticipantCount,
                x.Tournament.CreatedAtUtc))
            .ToList();

        return new PagedResult<TournamentSummaryResponse>
        {
            Items = items,
            PageNumber = pagination.PageNumber,
            PageSize = pagination.PageSize,
            TotalCount = totalCount
        };
    }

    public async Task<TournamentDetailResponse> GetByIdAsync(
        Guid tournamentId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var role = await _authorizationService.GetRoleAsync(tournamentId, userId, cancellationToken)
            ?? throw new UnauthorizedTournamentAccessException(tournamentId, userId);

        var tournament = await GetTournamentOrThrowAsync(tournamentId, cancellationToken);
        return MapToDetail(tournament, role);
    }

    public async Task<TournamentDetailResponse> UpdateAsync(
        Guid tournamentId,
        Guid userId,
        UpdateTournamentRequest request,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureIsOwnerAsync(tournamentId, userId, cancellationToken);

        var tournament = await GetTournamentOrThrowAsync(tournamentId, cancellationToken);
        _dbContext.SetOriginalRowVersion(tournament, request.RowVersion);

        tournament.UpdateSettings(
            request.Name,
            request.Description,
            request.LegMode,
            request.HasThirdPlaceMatch,
            _timeProvider.GetUtcNow().UtcDateTime);

        await SaveWithConcurrencyCheckAsync(cancellationToken);

        return MapToDetail(tournament, TournamentMemberRole.Owner);
    }

    public async Task DeleteAsync(Guid tournamentId, Guid userId, CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureIsOwnerAsync(tournamentId, userId, cancellationToken);

        var tournament = await GetTournamentOrThrowAsync(tournamentId, cancellationToken);
        tournament.MarkAsDeleted(_timeProvider.GetUtcNow().UtcDateTime);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<TournamentDetailResponse> ArchiveAsync(
        Guid tournamentId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureIsOwnerAsync(tournamentId, userId, cancellationToken);

        var tournament = await GetTournamentOrThrowAsync(tournamentId, cancellationToken);
        tournament.Archive(_timeProvider.GetUtcNow().UtcDateTime);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return MapToDetail(tournament, TournamentMemberRole.Owner);
    }

    public async Task<TournamentDetailResponse> ReopenAsync(
        Guid tournamentId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureIsOwnerAsync(tournamentId, userId, cancellationToken);

        var tournament = await GetTournamentOrThrowAsync(tournamentId, cancellationToken);
        tournament.Reopen(_timeProvider.GetUtcNow().UtcDateTime);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return MapToDetail(tournament, TournamentMemberRole.Owner);
    }

    internal async Task<Tournament> GetTournamentOrThrowAsync(Guid tournamentId, CancellationToken cancellationToken)
    {
        var tournament = await _dbContext.Tournaments
            .FirstOrDefaultAsync(t => t.Id == tournamentId && !t.IsDeleted, cancellationToken);

        return tournament ?? throw new TournamentNotFoundException(tournamentId);
    }

    private async Task SaveWithConcurrencyCheckAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException(
                "The record was modified by another user. Please reload and try again.");
        }
    }

    private static TournamentDetailResponse MapToDetail(Tournament tournament, TournamentMemberRole role)
    {
        return new TournamentDetailResponse(
            tournament.Id,
            tournament.Name,
            tournament.Description,
            tournament.Format,
            tournament.LegMode,
            tournament.Status,
            tournament.OwnerUserId,
            tournament.NumberOfGroups,
            tournament.HasThirdPlaceMatch,
            tournament.CurrentFixtureGenerationNumber,
            role,
            tournament.CreatedAtUtc,
            tournament.UpdatedAtUtc,
            tournament.RowVersion,
            ParticipantCapacity.GetMaxParticipants(tournament.Format, tournament.NumberOfGroups));
    }
}
