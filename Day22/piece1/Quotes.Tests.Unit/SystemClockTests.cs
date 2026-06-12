using FluentAssertions;
using QuotesApi.Services;
using Xunit;

namespace Quotes.Tests.Unit;

public class SystemClockTests
{
    [Fact]
    public void UtcNow_ReturnsCurrentUtcTime()
    {
        var clock = new SystemClock();
        var before = DateTimeOffset.UtcNow;
        var now = clock.UtcNow;
        var after = DateTimeOffset.UtcNow;

        now.Should().BeOnOrAfter(before);
        now.Should().BeOnOrBefore(after);
        now.Offset.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void UtcNow_CalledTwice_SecondIsNotEarlierThanFirst()
    {
        var clock = new SystemClock();
        var first = clock.UtcNow;
        var second = clock.UtcNow;

        second.Should().BeOnOrAfter(first);
    }
}
