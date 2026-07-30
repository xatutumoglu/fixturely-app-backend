using Fixturely.Domain.Common;

namespace Fixturely.Domain.Entities;

public enum TieBreakResolutionMethod
{
    HeadToHeadPoints = 0,
    HeadToHeadGoalDifference = 1,
    HeadToHeadGoalsScored = 2,
    OverallGoalDifference = 3,
    OverallGoalsScored = 4,
    TotalWins = 5,
    TieBreakMatch = 6,
    MiniLeague = 7,
    RandomDraw = 8
}

public sealed class TieBreakResolution : Entity
{
    private TieBreakResolution()
    {
    }

    public Guid TournamentId { get; private set; }

    public Guid? TournamentGroupId { get; private set; }

    public string TiedParticipantIdsJson { get; private set; } = string.Empty;

    public TieBreakResolutionMethod Method { get; private set; }

    public string? ResolutionDetailsJson { get; private set; }

    public Guid? ResultingOrderParticipantId { get; private set; }

    public string? RandomSeed { get; private set; }

    public static TieBreakResolution Create(
        Guid tournamentId,
        Guid? tournamentGroupId,
        string tiedParticipantIdsJson,
        TieBreakResolutionMethod method,
        string? resolutionDetailsJson,
        Guid? resultingOrderParticipantId,
        string? randomSeed,
        DateTime utcNow)
    {
        var resolution = new TieBreakResolution
        {
            TournamentId = tournamentId,
            TournamentGroupId = tournamentGroupId,
            TiedParticipantIdsJson = tiedParticipantIdsJson,
            Method = method,
            ResolutionDetailsJson = resolutionDetailsJson,
            ResultingOrderParticipantId = resultingOrderParticipantId,
            RandomSeed = randomSeed
        };
        resolution.Initialize(utcNow);
        return resolution;
    }
}
