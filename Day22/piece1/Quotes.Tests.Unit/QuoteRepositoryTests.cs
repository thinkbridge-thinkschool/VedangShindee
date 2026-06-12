using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using QuotesApi.Data;
using QuotesApi.Models;
using Xunit;

namespace Quotes.Tests.Unit;

public class QuoteRepositoryTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static readonly DateTimeOffset FixedNow =
        new(2026, 5, 21, 9, 0, 0, TimeSpan.Zero);

    // ── IClock-using: CreateAsync stamps CreatedAt ────────────────────────────

    [Fact]
    public async Task CreateAsync_SetsCreatedAtFromClock()
    {
        // Arrange
        var clock = new FakeClock(FixedNow);
        await using var db = CreateDb();
        var repo = new QuoteRepository(db, clock);

        // Act
        var created = await repo.CreateAsync(
            new Quote { Author = "Seneca", Text = "Nusquam est qui ubique est." },
            CancellationToken.None);

        // Assert
        created.CreatedAt.Should().Be(FixedNow);
    }

    [Fact]
    public async Task CreateAsync_PersistsQuoteToDatabase()
    {
        // Arrange
        var clock = new FakeClock(FixedNow);
        await using var db = CreateDb();
        var repo = new QuoteRepository(db, clock);

        // Act
        var created = await repo.CreateAsync(
            new Quote { Author = "Aurelius", Text = "Be tolerant with others." },
            CancellationToken.None);

        // Assert
        var fromDb = await db.Quotes.FindAsync(created.Id);
        fromDb.Should().NotBeNull();
        fromDb!.Author.Should().Be("Aurelius");
    }

    // ── GetByIdAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenIdDoesNotExist()
    {
        // Arrange
        var clock = new FakeClock(FixedNow);
        await using var db = CreateDb();
        var repo = new QuoteRepository(db, clock);

        // Act
        var result = await repo.GetByIdAsync(id: 999, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    // ── DeleteAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_ReturnsFalse_WhenQuoteNotFound()
    {
        // Arrange
        var clock = new FakeClock(FixedNow);
        await using var db = CreateDb();
        var repo = new QuoteRepository(db, clock);

        // Act
        var deleted = await repo.DeleteAsync(id: 404, CancellationToken.None);

        // Assert
        deleted.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_ReturnsTrue_AndRemovesFromDatabase()
    {
        // Arrange
        var clock = new FakeClock(FixedNow);
        await using var db = CreateDb();
        var repo = new QuoteRepository(db, clock);
        var quote = await repo.CreateAsync(
            new Quote { Author = "Heraclitus", Text = "Change is the only constant." },
            CancellationToken.None);

        // Act
        var deleted = await repo.DeleteAsync(quote.Id, CancellationToken.None);

        // Assert
        deleted.Should().BeTrue();
        var gone = await db.Quotes.FindAsync(quote.Id);
        gone.Should().BeNull();
    }
}
