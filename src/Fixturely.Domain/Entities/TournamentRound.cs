using Fixturely.Domain.Common;

namespace Fixturely.Domain.Entities;

public enum RoundPhase
{
    League = 0,
    GroupStage = 1,
    KnockoutRound = 2,
    ThirdPlace = 3,
    Final = 4
}

public sealed class TournamentRound : Entity
{
    private TournamentRound()
    {
    }

    public Guid TournamentId { get; private set; }

    public int RoundNumber { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public RoundPhase Phase { get; private set; }

    public Guid? TournamentGroupId { get; private set; }

    public static TournamentRound Create(
        Guid tournamentId,
        int roundNumber,
        string name,
        RoundPhase phase,
        Guid? tournamentGroupId,
        DateTime utcNow)
    {
        var round = new TournamentRound
        {
            TournamentId = tournamentId,
            RoundNumber = roundNumber,
            Name = name,
            Phase = phase,
            TournamentGroupId = tournamentGroupId
        };
        round.Initialize(utcNow);
        return round;
    }
}
