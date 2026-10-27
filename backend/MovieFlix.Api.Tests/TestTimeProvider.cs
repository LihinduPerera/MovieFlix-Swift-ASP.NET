namespace MovieFlix.Api.Tests;

/// <summary>
/// A controllable clock for tests. All services resolve TimeProvider, so
/// advancing this value deterministically moves token/progress timestamps.
/// </summary>
public sealed class TestTimeProvider : TimeProvider
{
    private readonly object _gate = new();
    private DateTimeOffset _utcNow;

    public TestTimeProvider(DateTimeOffset utcNow)
    {
        _utcNow = utcNow;
    }

    public override DateTimeOffset GetUtcNow()
    {
        lock (_gate)
        {
            return _utcNow;
        }
    }

    public void SetUtcNow(DateTimeOffset value)
    {
        lock (_gate)
        {
            _utcNow = value;
        }
    }

    public void Advance(TimeSpan amount)
    {
        lock (_gate)
        {
            _utcNow = _utcNow.Add(amount);
        }
    }
}
