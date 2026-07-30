using Fixturely.Application.Tournaments.Formats;
using FluentAssertions;
using Xunit;

namespace Fixturely.UnitTests.Application.Formats;

public sealed class RoundRobinSchedulerTests
{
    [Fact]
    public void GenerateSingleLeg_WithEvenParticipants_EachPlaysEveryOtherOnce()
    {
        var ids = Enumerable.Range(0, 4).Select(_ => (Guid?)Guid.NewGuid()).ToList();

        var pairings = RoundRobinScheduler.GenerateSingleLeg(ids);

        pairings.Should().HaveCount(6);

        var uniquePairs = pairings
            .Select(p => new[] { p.Home, p.Away }.OrderBy(x => x).ToArray())
            .Distinct(new PairComparer())
            .ToList();

        uniquePairs.Should().HaveCount(6);
    }

    [Fact]
    public void GenerateSingleLeg_WithOddParticipants_InsertsByeSlot()
    {
        var ids = Enumerable.Range(0, 5).Select(_ => (Guid?)Guid.NewGuid()).ToList();

        var pairings = RoundRobinScheduler.GenerateSingleLeg(ids);

        // 5 participants padded to 6 -> 5 rounds x 3 matches/round = 15 slots,
        // exactly one of which per round is a BYE (5 total).
        pairings.Should().HaveCount(15);
        pairings.Count(p => p.Home is null || p.Away is null).Should().Be(5);
    }

    [Fact]
    public void GenerateDoubleLeg_MirrorsHomeAwayForSecondLeg()
    {
        var ids = Enumerable.Range(0, 4).Select(_ => (Guid?)Guid.NewGuid()).ToList();

        var pairings = RoundRobinScheduler.GenerateDoubleLeg(ids);

        pairings.Should().HaveCount(12);

        var firstLegRoundsCount = pairings.Where(p => p.RoundIndex < 3).ToList();
        var secondLegRoundsCount = pairings.Where(p => p.RoundIndex >= 3).ToList();

        firstLegRoundsCount.Should().HaveCount(6);
        secondLegRoundsCount.Should().HaveCount(6);

        foreach (var firstLegPairing in firstLegRoundsCount)
        {
            secondLegRoundsCount.Should().ContainSingle(p => p.Home == firstLegPairing.Away && p.Away == firstLegPairing.Home);
        }
    }

    [Fact]
    public void GenerateSingleLeg_WithLessThanTwoParticipants_ReturnsEmpty()
    {
        var ids = new List<Guid?> { Guid.NewGuid() };

        var pairings = RoundRobinScheduler.GenerateSingleLeg(ids);

        pairings.Should().BeEmpty();
    }

    private sealed class PairComparer : IEqualityComparer<Guid?[]>
    {
        public bool Equals(Guid?[]? x, Guid?[]? y) => x is not null && y is not null && x[0] == y[0] && x[1] == y[1];

        public int GetHashCode(Guid?[] obj) => HashCode.Combine(obj[0], obj[1]);
    }
}
