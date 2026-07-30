namespace Fixturely.UnitTests.TestHelpers;

public sealed class FixedTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow;

    public FixedTimeProvider(DateTimeOffset utcNow)
    {
        _utcNow = utcNow;
    }

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void Advance(TimeSpan timeSpan) => _utcNow = _utcNow.Add(timeSpan);

    public void Set(DateTimeOffset utcNow) => _utcNow = utcNow;
}
