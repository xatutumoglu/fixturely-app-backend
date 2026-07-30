using Fixturely.Domain.Common;
using Fixturely.Domain.Enums;

namespace Fixturely.Domain.Entities;

public sealed class FixtureGenerationHistory : Entity
{
    private FixtureGenerationHistory()
    {
    }

    public Guid TournamentId { get; private set; }

    public Guid GeneratedByUserId { get; private set; }

    public DateTime GeneratedAtUtc { get; private set; }

    public int GenerationNumber { get; private set; }

    public TournamentFormat TournamentFormat { get; private set; }

    public bool WasConfirmed { get; private set; }

    public DateTime? SupersededAtUtc { get; private set; }

    public string RandomSeed { get; private set; } = string.Empty;

    public string? DrawMetadataJson { get; private set; }

    public static FixtureGenerationHistory Create(
        Guid tournamentId,
        Guid generatedByUserId,
        int generationNumber,
        TournamentFormat tournamentFormat,
        string randomSeed,
        string? drawMetadataJson,
        DateTime utcNow)
    {
        var history = new FixtureGenerationHistory
        {
            TournamentId = tournamentId,
            GeneratedByUserId = generatedByUserId,
            GeneratedAtUtc = utcNow,
            GenerationNumber = generationNumber,
            TournamentFormat = tournamentFormat,
            RandomSeed = randomSeed,
            DrawMetadataJson = drawMetadataJson,
            WasConfirmed = false
        };
        history.Initialize(utcNow);
        return history;
    }

    public void Confirm(DateTime utcNow)
    {
        WasConfirmed = true;
        Touch(utcNow);
    }

    public void Supersede(DateTime utcNow)
    {
        SupersededAtUtc = utcNow;
        Touch(utcNow);
    }
}
