using Fixturely.Application.DTOs.Common;
using Fixturely.Domain.Entities;
using Fixturely.Domain.Enums;

namespace Fixturely.Application.Tournaments.Standings;

public sealed class ParticipantStandingsAccumulator
{
    public Guid ParticipantId { get; init; }

    public string ParticipantName { get; init; } = string.Empty;

    public int Played { get; set; }

    public int Won { get; set; }

    public int Drawn { get; set; }

    public int Lost { get; set; }

    public int GoalsFor { get; set; }

    public int GoalsAgainst { get; set; }

    public int GoalDifference => GoalsFor - GoalsAgainst;

    public int Points => (Won * 3) + Drawn;
}

/// <summary>
/// Calculates league/group standings purely from completed matches. Redis may cache the
/// resulting table for a short time, but SQL Server (via the completed match rows) always
/// remains the source of truth; standings are recomputed whenever the cache is invalidated.
/// </summary>
public sealed class StandingsCalculationService
{
    private readonly TieBreakerService _tieBreakerService;

    public StandingsCalculationService(TieBreakerService tieBreakerService)
    {
        _tieBreakerService = tieBreakerService;
    }

    public IReadOnlyList<StandingsRowResponse> Calculate(
        IReadOnlyCollection<Participant> participants,
        IReadOnlyCollection<Match> completedMatches)
    {
        var accumulators = participants.ToDictionary(
            p => p.Id,
            p => new ParticipantStandingsAccumulator { ParticipantId = p.Id, ParticipantName = p.Name });

        foreach (var match in completedMatches.Where(m => !m.IsBye
            && m.HomeParticipantId is not null
            && m.AwayParticipantId is not null
            && m.HomeRegularTimeScore is not null
            && m.AwayRegularTimeScore is not null))
        {
            if (!accumulators.TryGetValue(match.HomeParticipantId!.Value, out var home)
                || !accumulators.TryGetValue(match.AwayParticipantId!.Value, out var away))
            {
                continue;
            }

            var homeGoals = match.HomeRegularTimeScore!.Value;
            var awayGoals = match.AwayRegularTimeScore!.Value;

            home.Played++;
            away.Played++;
            home.GoalsFor += homeGoals;
            home.GoalsAgainst += awayGoals;
            away.GoalsFor += awayGoals;
            away.GoalsAgainst += homeGoals;

            if (homeGoals > awayGoals)
            {
                home.Won++;
                away.Lost++;
            }
            else if (homeGoals < awayGoals)
            {
                away.Won++;
                home.Lost++;
            }
            else
            {
                home.Drawn++;
                away.Drawn++;
            }
        }

        var ordered = _tieBreakerService.OrderByRanking(accumulators.Values.ToList(), completedMatches);

        return ordered
            .Select((row, index) => new StandingsRowResponse(
                index + 1,
                row.Accumulator.ParticipantId,
                row.Accumulator.ParticipantName,
                row.Accumulator.Played,
                row.Accumulator.Won,
                row.Accumulator.Drawn,
                row.Accumulator.Lost,
                row.Accumulator.GoalsFor,
                row.Accumulator.GoalsAgainst,
                row.Accumulator.GoalDifference,
                row.Accumulator.Points,
                row.TieBreakNote))
            .ToList();
    }
}
