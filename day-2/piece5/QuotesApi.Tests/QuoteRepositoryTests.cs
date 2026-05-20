using Microsoft.EntityFrameworkCore;
using QuotesApi.Data;
using QuotesApi.Models;
using Xunit;

namespace QuotesApi.Tests;

public class QuoteRepositoryTests
{
    [Fact]
    public async Task CreateAsync_StampsCreatedAtFromClock()
    {
        var fixedTime = new DateTimeOffset(2024, 1, 15, 10, 0, 0, TimeSpan.Zero);
        var clock = new FakeClock(fixedTime);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new AppDbContext(options);
        var repo = new QuoteRepository(db, clock);

        var created = await repo.CreateAsync(
            new Quote { Author = "Seneca", Text = "Nusquam est qui ubique est." },
            CancellationToken.None);

        Assert.Equal(fixedTime, created.CreatedAt);
    }

    [Fact]
    public async Task CreateAsync_DifferentClockTimes_ProduceDifferentTimestamps()
    {
        var time1 = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var time2 = new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new AppDbContext(options);

        var q1 = await new QuoteRepository(db, new FakeClock(time1))
            .CreateAsync(new Quote { Author = "A", Text = "T1" }, CancellationToken.None);

        var q2 = await new QuoteRepository(db, new FakeClock(time2))
            .CreateAsync(new Quote { Author = "A", Text = "T2" }, CancellationToken.None);

        Assert.Equal(time1, q1.CreatedAt);
        Assert.Equal(time2, q2.CreatedAt);
        Assert.NotEqual(q1.CreatedAt, q2.CreatedAt);
    }
}
