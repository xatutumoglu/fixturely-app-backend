using Fixturely.Application.Abstractions.Persistence;
using Fixturely.Application.Abstractions.Security;
using Fixturely.Application.Common;
using Fixturely.Application.Tournaments.Formats;
using Fixturely.Domain.Entities;
using Fixturely.Domain.Enums;
using Fixturely.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Fixturely.Application.Tournaments.Fixtures;

public sealed class FixtureGenerationService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    private readonly ITournamentAuthorizationService _authorizationService;
    private readonly IReadOnlyDictionary<TournamentFormat, ITournamentFormatEngine> _engines;

    public FixtureGenerationService(
        IApplicationDbContext dbContext,
        TimeProvider timeProvider,
        ITournamentAuthorizationService authorizationService,
        IEnumerable<ITournamentFormatEngine> engines)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
        _authorizationService = authorizationService;
        _engines = engines.ToDictionary(e => e.Format);
    }

    public Task<int> GenerateAsync(Guid tournamentId, Guid userId, CancellationToken cancellationToken = default) =>
        GenerateInternalAsync(tournamentId, userId, isRegeneration: false, cancellationToken);

    public Task<int> RegenerateAsync(Guid tournamentId, Guid userId, CancellationToken cancellationToken = default) =>
        GenerateInternalAsync(tournamentId, userId, isRegeneration: true, cancellationToken);

    public async Task ConfirmAsync(Guid tournamentId, Guid userId, CancellationToken cancellationToken = default)
    {
        await _authorizationService.EnsureIsOwnerAsync(tournamentId, userId, cancellationToken);

        var tournament = await _dbContext.Tournaments
            .FirstOrDefaultAsync(t => t.Id == tournamentId && !t.IsDeleted, cancellationToken)
            ?? throw new TournamentNotFoundException(tournamentId);

        var history = await _dbContext.FixtureGenerationHistories
            .Where(h => h.TournamentId == tournamentId && h.GenerationNumber == tournament.CurrentFixtureGenerationNumber)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidFixtureGenerationException(
                ErrorCodes.NoPendingFixtureGeneration, "There is no pending fixture generation to confirm.");

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        history.Confirm(utcNow);

        _dbContext.AuditLogs.Add(AuditLog.Create(
            userId, tournamentId, "FixtureGeneration", "Confirmed", null, null, null, utcNow));

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<int> GenerateInternalAsync(
        Guid tournamentId,
        Guid userId,
        bool isRegeneration,
        CancellationToken cancellationToken)
    {
        await _authorizationService.EnsureIsOwnerAsync(tournamentId, userId, cancellationToken);

        var tournament = await _dbContext.Tournaments
            .Include(t => t.Participants)
            .Include(t => t.Matches)
            .FirstOrDefaultAsync(t => t.Id == tournamentId && !t.IsDeleted, cancellationToken)
            ?? throw new TournamentNotFoundException(tournamentId);

        if (isRegeneration)
        {
            if (!tournament.CanRegenerateFixture())
            {
                throw new InvalidTournamentStateException(
                    ErrorCodes.FixtureAlreadyHasScores,
                    "The fixture cannot be regenerated once a score has been entered.");
            }
        }
        else if (tournament.Status != TournamentStatus.Setup)
        {
            throw new InvalidTournamentStateException(
                ErrorCodes.FixtureOnlySetupStatus,
                "The fixture can only be generated for tournaments in the Setup status.");
        }

        if (!_engines.TryGetValue(tournament.Format, out var engine))
        {
            throw new InvalidFixtureGenerationException(
                ErrorCodes.NoEngineForFormat,
                $"No fixture generation engine is registered for format '{tournament.Format}'.",
                new Dictionary<string, object?> { ["format"] = tournament.Format.ToString() });
        }

        var participants = tournament.Participants.Where(p => !p.IsDeleted).ToList();
        var seed = SecureDraw.GenerateSeed();
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;

        var input = new FixtureGenerationInput
        {
            TournamentId = tournamentId,
            LegMode = tournament.LegMode,
            Participants = participants,
            NumberOfGroups = tournament.NumberOfGroups,
            HasThirdPlaceMatch = tournament.HasThirdPlaceMatch,
            RandomSeed = seed,
            UtcNow = utcNow
        };

        var output = engine.GenerateFixture(input);

        if (isRegeneration)
        {
            await RemoveExistingFixtureAsync(tournamentId, cancellationToken);

            var previousHistory = await _dbContext.FixtureGenerationHistories
                .Where(h => h.TournamentId == tournamentId
                    && h.GenerationNumber == tournament.CurrentFixtureGenerationNumber)
                .FirstOrDefaultAsync(cancellationToken);
            previousHistory?.Supersede(utcNow);
        }

        var generationNumber = tournament.CurrentFixtureGenerationNumber + 1;

        foreach (var group in output.Groups)
        {
            _dbContext.TournamentGroups.Add(group);

            foreach (var groupParticipant in group.GroupParticipants)
            {
                _dbContext.GroupParticipants.Add(groupParticipant);
            }
        }

        foreach (var round in output.Rounds)
        {
            _dbContext.TournamentRounds.Add(round);
        }

        foreach (var match in output.Matches)
        {
            _dbContext.Matches.Add(match);
        }

        tournament.MarkFixtureGenerated(generationNumber, utcNow);

        _dbContext.FixtureGenerationHistories.Add(FixtureGenerationHistory.Create(
            tournamentId,
            userId,
            generationNumber,
            tournament.Format,
            seed,
            output.DrawMetadataJson,
            utcNow));

        _dbContext.AuditLogs.Add(AuditLog.Create(
            userId,
            tournamentId,
            "FixtureGeneration",
            isRegeneration ? "Regenerated" : "Generated",
            null,
            null,
            null,
            utcNow));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return generationNumber;
    }

    private async Task RemoveExistingFixtureAsync(Guid tournamentId, CancellationToken cancellationToken)
    {
        var existingMatches = await _dbContext.Matches
            .Where(m => m.TournamentId == tournamentId)
            .ToListAsync(cancellationToken);
        _dbContext.Matches.RemoveRange(existingMatches);

        var existingRounds = await _dbContext.TournamentRounds
            .Where(r => r.TournamentId == tournamentId)
            .ToListAsync(cancellationToken);
        _dbContext.TournamentRounds.RemoveRange(existingRounds);

        var existingGroupIds = await _dbContext.TournamentGroups
            .Where(g => g.TournamentId == tournamentId)
            .Select(g => g.Id)
            .ToListAsync(cancellationToken);

        var existingGroupParticipants = await _dbContext.GroupParticipants
            .Where(gp => existingGroupIds.Contains(gp.TournamentGroupId))
            .ToListAsync(cancellationToken);
        _dbContext.GroupParticipants.RemoveRange(existingGroupParticipants);

        var existingGroups = await _dbContext.TournamentGroups
            .Where(g => g.TournamentId == tournamentId)
            .ToListAsync(cancellationToken);
        _dbContext.TournamentGroups.RemoveRange(existingGroups);
    }
}
