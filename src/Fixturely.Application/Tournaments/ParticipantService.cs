using Fixturely.Application.Abstractions.Persistence;
using Fixturely.Application.Abstractions.Security;
using Fixturely.Application.DTOs.Participants;
using Fixturely.Domain.Entities;
using Fixturely.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Fixturely.Application.Tournaments;

public sealed class ParticipantService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    private readonly ITournamentAuthorizationService _authorizationService;

    public ParticipantService(
        IApplicationDbContext dbContext,
        TimeProvider timeProvider,
        ITournamentAuthorizationService authorizationService)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
        _authorizationService = authorizationService;
    }

    public async Task<IReadOnlyCollection<ParticipantResponse>> ListAsync(
        Guid tournamentId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureCanViewAsync(tournamentId, userId, cancellationToken);

        var participants = await _dbContext.Participants
            .AsNoTracking()
            .Where(p => p.TournamentId == tournamentId && !p.IsDeleted)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);

        return participants.Select(Map).ToList();
    }

    public async Task<ParticipantResponse> AddAsync(
        Guid tournamentId,
        Guid userId,
        CreateParticipantRequest request,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureIsOwnerAsync(tournamentId, userId, cancellationToken);

        var tournament = await _dbContext.Tournaments
            .Include(t => t.Participants)
            .FirstOrDefaultAsync(t => t.Id == tournamentId && !t.IsDeleted, cancellationToken)
            ?? throw new TournamentNotFoundException(tournamentId);

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        var participant = Participant.Create(tournamentId, request.Name, request.ShortCode, utcNow);

        tournament.AddParticipant(participant);
        _dbContext.Participants.Add(participant);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Map(participant);
    }

    public async Task<ParticipantResponse> UpdateAsync(
        Guid tournamentId,
        Guid participantId,
        Guid userId,
        UpdateParticipantRequest request,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureIsOwnerAsync(tournamentId, userId, cancellationToken);

        var participant = await _dbContext.Participants
            .FirstOrDefaultAsync(
                p => p.Id == participantId && p.TournamentId == tournamentId && !p.IsDeleted,
                cancellationToken)
            ?? throw new InvalidTournamentStateException("Participant not found in this tournament.");

        participant.Update(request.Name, request.ShortCode, _timeProvider.GetUtcNow().UtcDateTime);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Map(participant);
    }

    public async Task RemoveAsync(
        Guid tournamentId,
        Guid participantId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureIsOwnerAsync(tournamentId, userId, cancellationToken);

        var tournament = await _dbContext.Tournaments
            .Include(t => t.Participants)
            .FirstOrDefaultAsync(t => t.Id == tournamentId && !t.IsDeleted, cancellationToken)
            ?? throw new TournamentNotFoundException(tournamentId);

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        tournament.RemoveParticipant(participantId, utcNow);

        var participant = await _dbContext.Participants
            .FirstOrDefaultAsync(p => p.Id == participantId, cancellationToken);
        participant?.MarkDeleted(utcNow);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static ParticipantResponse Map(Participant participant) =>
        new(participant.Id, participant.TournamentId, participant.Name, participant.ShortCode);
}
