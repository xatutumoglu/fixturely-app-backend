using Fixturely.Domain.Entities;

namespace Fixturely.Application.Tournaments.Standings;

public sealed record RankedRow(ParticipantStandingsAccumulator Accumulator, string? TieBreakNote);

/// <summary>
/// Implements the League/Group ranking rules described in docs/tournament-rules.md:
/// total points, then head-to-head points/goal-difference/goals-scored computed on a
/// mini-table restricted to the tied participants (re-applied recursively to any
/// remaining tied subset), then overall goal difference, overall goals scored, total
/// wins, and finally a flagged controlled tie-break requirement. Alphabetic order is
/// never used as a sporting tie-breaker.
/// </summary>
public sealed class TieBreakerService
{
    private sealed record HeadToHeadStats(int Points, int GoalDifference, int GoalsFor);

    public IReadOnlyList<RankedRow> OrderByRanking(
        List<ParticipantStandingsAccumulator> accumulators,
        IReadOnlyCollection<Match> allMatches)
    {
        var result = new List<RankedRow>();

        var pointsGroups = accumulators
            .GroupBy(a => a.Points)
            .OrderByDescending(g => g.Key)
            .Select(g => g.ToList());

        foreach (var pointsGroup in pointsGroups)
        {
            result.AddRange(ResolveCluster(pointsGroup, allMatches));
        }

        return result;
    }

    private List<RankedRow> ResolveCluster(
        List<ParticipantStandingsAccumulator> cluster,
        IReadOnlyCollection<Match> allMatches)
    {
        if (cluster.Count <= 1)
        {
            return cluster.Select(a => new RankedRow(a, null)).ToList();
        }

        var h2hStats = ComputeHeadToHead(cluster, allMatches);
        var output = new List<RankedRow>();

        var afterPoints = SplitDescending(cluster, a => h2hStats[a.ParticipantId].Points);

        foreach (var pointsSubGroup in afterPoints)
        {
            if (pointsSubGroup.Count == 1)
            {
                output.Add(new RankedRow(pointsSubGroup[0], null));
                continue;
            }

            var afterGoalDifference = SplitDescending(pointsSubGroup, a => h2hStats[a.ParticipantId].GoalDifference);

            foreach (var goalDifferenceSubGroup in afterGoalDifference)
            {
                if (goalDifferenceSubGroup.Count == 1)
                {
                    output.Add(new RankedRow(goalDifferenceSubGroup[0], null));
                    continue;
                }

                var afterGoalsScored = SplitDescending(
                    goalDifferenceSubGroup,
                    a => h2hStats[a.ParticipantId].GoalsFor);

                foreach (var goalsScoredSubGroup in afterGoalsScored)
                {
                    if (goalsScoredSubGroup.Count == 1)
                    {
                        output.Add(new RankedRow(goalsScoredSubGroup[0], null));
                        continue;
                    }

                    output.AddRange(goalsScoredSubGroup.Count == cluster.Count
                        ? ResolveByOverallStats(goalsScoredSubGroup)
                        : ResolveCluster(goalsScoredSubGroup, allMatches));
                }
            }
        }

        return output;
    }

    private static List<RankedRow> ResolveByOverallStats(List<ParticipantStandingsAccumulator> cluster)
    {
        var output = new List<RankedRow>();

        var afterGoalDifference = SplitDescending(cluster, a => a.GoalDifference);

        foreach (var goalDifferenceGroup in afterGoalDifference)
        {
            if (goalDifferenceGroup.Count == 1)
            {
                output.Add(new RankedRow(goalDifferenceGroup[0], null));
                continue;
            }

            var afterGoalsFor = SplitDescending(goalDifferenceGroup, a => a.GoalsFor);

            foreach (var goalsForGroup in afterGoalsFor)
            {
                if (goalsForGroup.Count == 1)
                {
                    output.Add(new RankedRow(goalsForGroup[0], null));
                    continue;
                }

                var afterWins = SplitDescending(goalsForGroup, a => a.Won);

                foreach (var winsGroup in afterWins)
                {
                    if (winsGroup.Count == 1)
                    {
                        output.Add(new RankedRow(winsGroup[0], null));
                        continue;
                    }

                    const string note =
                        "Tied after all deterministic criteria; requires a controlled tie-break " +
                        "resolution (tie-break match, mini-league, or audited random draw).";

                    output.AddRange(winsGroup
                        .OrderBy(a => a.ParticipantId)
                        .Select(a => new RankedRow(a, note)));
                }
            }
        }

        return output;
    }

    private static Dictionary<Guid, HeadToHeadStats> ComputeHeadToHead(
        List<ParticipantStandingsAccumulator> cluster,
        IReadOnlyCollection<Match> allMatches)
    {
        var clusterIds = cluster.Select(a => a.ParticipantId).ToHashSet();

        var stats = cluster.ToDictionary(
            a => a.ParticipantId,
            _ => new MutableStats());

        var h2hMatches = allMatches.Where(m =>
            !m.IsBye
            && m.HomeParticipantId is not null
            && m.AwayParticipantId is not null
            && clusterIds.Contains(m.HomeParticipantId.Value)
            && clusterIds.Contains(m.AwayParticipantId.Value)
            && m.HomeRegularTimeScore is not null
            && m.AwayRegularTimeScore is not null);

        foreach (var match in h2hMatches)
        {
            var home = stats[match.HomeParticipantId!.Value];
            var away = stats[match.AwayParticipantId!.Value];
            var homeGoals = match.HomeRegularTimeScore!.Value;
            var awayGoals = match.AwayRegularTimeScore!.Value;

            home.GoalsFor += homeGoals;
            home.GoalsAgainst += awayGoals;
            away.GoalsFor += awayGoals;
            away.GoalsAgainst += homeGoals;

            if (homeGoals > awayGoals)
            {
                home.Points += 3;
            }
            else if (homeGoals < awayGoals)
            {
                away.Points += 3;
            }
            else
            {
                home.Points += 1;
                away.Points += 1;
            }
        }

        return stats.ToDictionary(
            kvp => kvp.Key,
            kvp => new HeadToHeadStats(
                kvp.Value.Points,
                kvp.Value.GoalsFor - kvp.Value.GoalsAgainst,
                kvp.Value.GoalsFor));
    }

    private static List<List<ParticipantStandingsAccumulator>> SplitDescending(
        List<ParticipantStandingsAccumulator> items,
        Func<ParticipantStandingsAccumulator, int> keySelector)
    {
        return items
            .GroupBy(keySelector)
            .OrderByDescending(g => g.Key)
            .Select(g => g.ToList())
            .ToList();
    }

    private sealed class MutableStats
    {
        public int Points { get; set; }

        public int GoalsFor { get; set; }

        public int GoalsAgainst { get; set; }
    }
}
