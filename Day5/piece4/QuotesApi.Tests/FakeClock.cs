using QuotesApi.Services;

namespace QuotesApi.Tests;

public sealed class FakeClock : IClock
{
    private readonly DateTimeOffset _fixedTime;

    public FakeClock(DateTimeOffset fixedTime) => _fixedTime = fixedTime;

    public DateTimeOffset UtcNow => _fixedTime;
}
