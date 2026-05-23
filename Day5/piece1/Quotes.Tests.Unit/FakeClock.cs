using QuotesApi.Services;

namespace Quotes.Tests.Unit;

public sealed class FakeClock : IClock
{
    private readonly DateTimeOffset _fixed;

    public FakeClock(DateTimeOffset fixedTime) => _fixed = fixedTime;

    public DateTimeOffset UtcNow => _fixed;
}
