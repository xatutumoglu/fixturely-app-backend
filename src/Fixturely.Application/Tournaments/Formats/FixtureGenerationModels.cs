using Fixturely.Domain.Entities;
using Fixturely.Domain.Enums;

namespace Fixturely.Application.Tournaments.Formats;

public sealed class FixtureGenerationInput
{
    public required Guid TournamentId { get; init; }

    public required LegMode LegMode { get; init; }

    public required IReadOnlyList<Participant> Participants { get; init; }

    public int? NumberOfGroups { get; init; }

    public bool HasThirdPlaceMatch { get; init; }

    public required string RandomSeed { get; init; }

    public required DateTime UtcNow { get; init; }
}

public sealed class FixtureGenerationOutput
{
    public required IReadOnlyList<TournamentGroup> Groups { get; init; }

    public required IReadOnlyList<TournamentRound> Rounds { get; init; }

    public required IReadOnlyList<Match> Matches { get; init; }

    public required string DrawMetadataJson { get; init; }
}

public interface ITournamentFormatEngine
{
    TournamentFormat Format { get; }

    FixtureGenerationOutput GenerateFixture(FixtureGenerationInput input);
}
