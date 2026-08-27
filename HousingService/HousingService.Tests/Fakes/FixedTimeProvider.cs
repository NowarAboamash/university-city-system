namespace HousingService.Tests.Fakes;

/// <summary>Deterministic clock so tests can assert on stamped timestamps without flakiness.</summary>
public class FixedTimeProvider : TimeProvider
{
    private DateTimeOffset _now;

    public FixedTimeProvider(DateTimeOffset now) => _now = now;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now = _now.Add(by);
}
