using System.Security.Cryptography;

namespace Fixturely.Application.Common;

/// <summary>
/// Provides draw/shuffle utilities used by fixture generation and random draws.
/// The seed itself is produced using a cryptographically secure random number
/// generator, while the shuffle algorithm is a deterministic Fisher-Yates
/// implementation seeded from that value so that a draw can be reproduced and
/// audited later from the stored seed (see FixtureGenerationHistory.RandomSeed).
/// </summary>
public static class SecureDraw
{
    public static string GenerateSeed()
    {
        var bytes = RandomNumberGenerator.GetBytes(16);
        return Convert.ToHexString(bytes);
    }

    public static List<T> Shuffle<T>(IEnumerable<T> items, string seed)
    {
        var list = items.ToList();
        var rng = CreateDeterministicRandom(seed);

        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }

        return list;
    }

    private static Random CreateDeterministicRandom(string seed)
    {
        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(seed));
        var seedInt = BitConverter.ToInt32(hash, 0);
        return new Random(seedInt);
    }
}
