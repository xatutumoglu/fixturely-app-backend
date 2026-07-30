namespace Fixturely.Application.Tournaments.Formats;

/// <summary>
/// Generates round-robin pairings using the circle method. A <c>null</c> participant id
/// represents a BYE slot for odd-sized participant sets.
/// </summary>
public static class RoundRobinScheduler
{
    public readonly record struct Pairing(int RoundIndex, Guid? Home, Guid? Away);

    public static IReadOnlyList<Pairing> GenerateSingleLeg(IReadOnlyList<Guid?> participantIds)
    {
        var teams = participantIds.ToList();

        if (teams.Count < 2)
        {
            return Array.Empty<Pairing>();
        }

        if (teams.Count % 2 != 0)
        {
            teams.Add(null);
        }

        var n = teams.Count;
        var roundsCount = n - 1;
        var matchesPerRound = n / 2;
        var pairings = new List<Pairing>();
        var current = teams.ToList();

        for (var round = 0; round < roundsCount; round++)
        {
            for (var i = 0; i < matchesPerRound; i++)
            {
                var home = current[i];
                var away = current[n - 1 - i];

                if (i == 0 && round % 2 == 1)
                {
                    (home, away) = (away, home);
                }

                pairings.Add(new Pairing(round, home, away));
            }

            var rotated = new List<Guid?> { current[0], current[^1] };
            rotated.AddRange(current.Skip(1).Take(n - 2));
            current = rotated;
        }

        return pairings;
    }

    public static IReadOnlyList<Pairing> GenerateDoubleLeg(IReadOnlyList<Guid?> participantIds)
    {
        var firstLeg = GenerateSingleLeg(participantIds).ToList();
        var roundsInFirstLeg = firstLeg.Count == 0 ? 0 : firstLeg.Max(p => p.RoundIndex) + 1;

        var secondLeg = firstLeg
            .Select(p => new Pairing(p.RoundIndex + roundsInFirstLeg, p.Away, p.Home))
            .ToList();

        firstLeg.AddRange(secondLeg);
        return firstLeg;
    }
}
