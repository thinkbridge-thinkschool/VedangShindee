using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using QuotesApi.Data;
using QuotesApi.Models;
using Xunit;

namespace Quotes.Tests.Unit;

public class QuoteRepositoryGetPagedTests
{
    private static readonly DateTimeOffset FixedNow =
        new(2026, 5, 21, 9, 0, 0, TimeSpan.Zero);

    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task<QuoteRepository> SeedAsync(AppDbContext db, int count)
    {
        var clock = new FakeClock(FixedNow);
        var repo = new QuoteRepository(db, clock);
        for (var i = 1; i <= count; i++)
            await repo.CreateAsync(new Quote { Author = $"Author{i}", Text = $"Text{i}" }, CancellationToken.None);
        return repo;
    }

    [Fact]
    public async Task GetPagedAsync_ReturnsFirstPage()
    {
        await using var db = CreateDb();
        var repo = await SeedAsync(db, 5);

        var page = await repo.GetPagedAsync(1, 3, CancellationToken.None);

        page.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetPagedAsync_ReturnsSecondPage()
    {
        await using var db = CreateDb();
        var repo = await SeedAsync(db, 5);

        var page = await repo.GetPagedAsync(2, 3, CancellationToken.None);

        page.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetPagedAsync_PageBeyondData_ReturnsEmptyList()
    {
        await using var db = CreateDb();
        var repo = await SeedAsync(db, 3);

        var page = await repo.GetPagedAsync(2, 10, CancellationToken.None);

        page.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPagedAsync_EmptyDatabase_ReturnsEmptyList()
    {
        await using var db = CreateDb();
        var clock = new FakeClock(FixedNow);
        var repo = new QuoteRepository(db, clock);

        var page = await repo.GetPagedAsync(1, 10, CancellationToken.None);

        page.Should().BeEmpty();
    }
}
