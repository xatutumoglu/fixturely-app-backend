namespace Fixturely.Application.Tournaments.Formats;

/// <summary>
/// Computes the standard single-elimination bracket seeding order, which guarantees
/// that BYE slots (the highest seed numbers) are spread out and never face each other
/// in the first round.
/// </summary>
public static class BracketSeedOrderCalculator
{
    public static int NextPowerOfTwo(int value)
    {
        var power = 1;
        while (power < value)
        {
            power *= 2;
        }

        return power;
    }

    public static List<int> ComputeSeedOrder(int bracketSize)
    {
        if (bracketSize <= 1)
        {
            return new List<int> { 1 };
        }

        var order = new List<int> { 1, 2 };

        while (order.Count < bracketSize)
        {
            var size = order.Count * 2;
            var next = new List<int>(size);

            foreach (var seed in order)
            {
                next.Add(seed);
                next.Add(size + 1 - seed);
            }

            order = next;
        }

        return order;
    }
}
