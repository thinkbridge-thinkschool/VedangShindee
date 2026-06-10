using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using QuotesApi.Commands;
using QuotesApi.Data;
using QuotesApi.Services;
using Xunit;

namespace Quotes.Tests.Unit;

public class CreateQuoteHandlerTests
{
    private static readonly DateTimeOffset FixedNow =
        new(2026, 5, 30, 9, 0, 0, TimeSpan.Zero);

    // ── Helpers ────────────────────────────────────────────────────────────────

    /// Each test gets its own isolated InMemory database.
    /// TransactionIgnoredWarning is suppressed because InMemory silently ignores
    /// transactions — the handler's BeginTransactionAsync call is a no-op, but the
    /// writes still happen atomically from the test's perspective.
    private static AppDbContext BuildDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static (CreateQuoteHandler handler, AppDbContext db) BuildSut()
    {
        var db      = BuildDb();
        var clock   = new FakeClock { UtcNow = FixedNow };
        var handler = new CreateQuoteHandler(db, clock, new QuoteValidator(), new QuoteAuditQueue());
        return (handler, db);
    }

    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; }
    }

    // ── Validation failure ─────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_EmptyAuthor_ReturnsErrors()
    {
        var (handler, db) = BuildSut();
        var command = new CreateQuoteCommand(Author: "", Text: "Some text.", OwnerId: 1);

        var (result, errors) = await handler.HandleAsync(command, CancellationToken.None);

        result.Should().BeNull();
        errors.Should().ContainKey("author");
        db.Quotes.Should().BeEmpty();
        db.OutboxMessages.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_EmptyText_ReturnsErrors()
    {
        var (handler, db) = BuildSut();
        var command = new CreateQuoteCommand(Author: "Seneca", Text: "", OwnerId: 1);

        var (result, errors) = await handler.HandleAsync(command, CancellationToken.None);

        result.Should().BeNull();
        errors.Should().ContainKey("text");
        db.Quotes.Should().BeEmpty();
        db.OutboxMessages.Should().BeEmpty();
    }

    // ── Happy path ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_ValidCommand_PersistsAndReturnsResult()
    {
        var (handler, db) = BuildSut();
        var command = new CreateQuoteCommand(
            Author: "Seneca", Text: "Luck is preparation meeting opportunity.", OwnerId: 42);

        var (result, errors) = await handler.HandleAsync(command, CancellationToken.None);

        errors.Should().BeNull();
        result.Should().NotBeNull();
        result!.Id.Should().BeGreaterThan(0);
        result.Author.Should().Be("Seneca");
        result.Text.Should().Be("Luck is preparation meeting opportunity.");
        result.CreatedAt.Should().Be(FixedNow);
    }

    [Fact]
    public async Task HandleAsync_ValidCommand_WritesOutboxRowWithSameQuoteId()
    {
        var (handler, db) = BuildSut();
        var command = new CreateQuoteCommand(
            Author: "Epictetus", Text: "Make the best use of what is in your power.", OwnerId: 99);

        var (result, _) = await handler.HandleAsync(command, CancellationToken.None);

        // One pending outbox row for the new quote.
        var outbox = await db.OutboxMessages.SingleAsync();
        outbox.SentAt.Should().BeNull();
        outbox.Topic.Should().Be("quote-created");

        // The outbox payload must reference the same QuoteId that was returned.
        outbox.Payload.Should().Contain(result!.Id.ToString());
    }

    [Fact]
    public async Task HandleAsync_ValidCommand_OwnedByCorrectUser()
    {
        var (handler, db) = BuildSut();
        var command = new CreateQuoteCommand(
            Author: "Stoic", Text: "A valid quote.", OwnerId: 5);

        var (result, _) = await handler.HandleAsync(command, CancellationToken.None);

        var quote = await db.Quotes.SingleAsync();
        quote.OwnerId.Should().Be(5);
    }

    [Fact]
    public async Task HandleAsync_ValidCommand_ResultDoesNotExposeOwnerId()
    {
        var (handler, _) = BuildSut();
        var command = new CreateQuoteCommand(Author: "Stoic", Text: "A valid quote.", OwnerId: 5);

        var (result, _) = await handler.HandleAsync(command, CancellationToken.None);

        result.Should().NotBeNull();
        // CreateQuoteResult deliberately omits OwnerId — write-side concern.
        result!.GetType().GetProperty("OwnerId").Should().BeNull();
    }
}
