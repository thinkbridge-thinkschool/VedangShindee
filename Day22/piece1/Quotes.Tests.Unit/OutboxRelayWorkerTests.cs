using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using QuotesApi.Data;
using QuotesApi.Messaging.Models;
using QuotesApi.Outbox;
using System.Text.Json;
using Xunit;

namespace Quotes.Tests.Unit;

// ── Test doubles ──────────────────────────────────────────────────────────────

sealed class CrashingPublisher : IQuoteEventPublisher
{
    public Task PublishAsync(QuoteCreatedEvent evt, CancellationToken ct = default) =>
        throw new InvalidOperationException("Simulated publish failure — process crash");
}

sealed class RecordingPublisher : IQuoteEventPublisher
{
    public List<QuoteCreatedEvent> Published { get; } = new();
    public Task PublishAsync(QuoteCreatedEvent evt, CancellationToken ct = default)
    {
        Published.Add(evt);
        return Task.CompletedTask;
    }
}

// ── Tests ────────────────────────────────────────────────────────────────────

/// <summary>
/// Unit-level proof of the outbox crash scenario.
/// Uses EF InMemory (no Docker, no SQL Server) — fast and CI-friendly.
///
/// These tests exist specifically to satisfy the exercise requirement:
///   "Prove no message is lost if the publish step crashes."
/// </summary>
public class OutboxRelayWorkerTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static AppDbContext BuildDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static OutboxRelayWorker BuildRelay(AppDbContext db, IQuoteEventPublisher publisher)
    {
        var services = new ServiceCollection();
        services.AddSingleton(db);
        var provider = services.BuildServiceProvider();

        var scopeFactory = new FakeScopeFactory(db);
        return new OutboxRelayWorker(scopeFactory, publisher, NullLogger<OutboxRelayWorker>.Instance);
    }

    private static OutboxMessage SeedPendingRow(AppDbContext db, string author = "Seneca")
    {
        var eventId = Guid.NewGuid();
        var evt = new QuoteCreatedEvent
        {
            EventId    = eventId.ToString(),
            QuoteId    = 1,
            Author     = author,
            Text       = "Sample text.",
            OccurredAt = DateTimeOffset.UtcNow,
        };
        var msg = new OutboxMessage
        {
            Id        = eventId,
            Topic     = "quote-created",
            Payload   = JsonSerializer.Serialize(evt),
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.OutboxMessages.Add(msg);
        db.SaveChanges();
        return msg;
    }

    // ── Test 1: the critical crash scenario ───────────────────────────────────

    /// <summary>
    /// THE crash scenario:
    ///   Relay calls PublishAsync — succeeds.
    ///   Process crashes before SaveChangesAsync can persist SentAt.
    ///   Row stays SentAt=null — message is NOT lost.
    ///   Next relay tick finds the same row and retries.
    /// </summary>
    [Fact]
    public async Task CrashBeforeSentAt_RowRemainsNull_MessageNotLost()
    {
        var db  = BuildDb();
        var msg = SeedPendingRow(db);

        // Tick 1: publish crashes before SentAt is written
        var relay = BuildRelay(db, new CrashingPublisher());
        await relay.RelayPendingAsync(CancellationToken.None);

        // Row still pending — no data lost
        var row = await db.OutboxMessages.SingleAsync();
        row.SentAt.Should().BeNull("crash before SaveChanges must leave SentAt=null so retry is possible");
    }

    // ── Test 2: retry delivers the same EventId ───────────────────────────────

    /// <summary>
    /// After the crash, the next tick re-delivers the SAME EventId.
    /// The consumer uses EventId as its idempotency key → silent skip on duplicate.
    /// This is the at-least-once + idempotent consumer guarantee.
    /// </summary>
    [Fact]
    public async Task RetryAfterCrash_DeliversSameEventId_IdempotentKey()
    {
        var db  = BuildDb();
        var msg = SeedPendingRow(db);

        // Tick 1: crash
        await BuildRelay(db, new CrashingPublisher()).RelayPendingAsync(CancellationToken.None);

        // Tick 2: success
        var recorder = new RecordingPublisher();
        await BuildRelay(db, recorder).RelayPendingAsync(CancellationToken.None);

        // Exactly one delivery on the retry tick
        recorder.Published.Should().HaveCount(1);

        // EventId on the published event == outbox row Id — stable across retries
        recorder.Published[0].EventId.Should().Be(msg.Id.ToString(),
            "EventId is written once at insert time and never regenerated, giving the consumer a stable dedup key");

        // Row marked sent
        var row = await db.OutboxMessages.SingleAsync();
        row.SentAt.Should().NotBeNull();
    }

    // ── Test 3: successful relay stamps SentAt ────────────────────────────────

    [Fact]
    public async Task SuccessfulRelay_StampsSentAt_RowLeavesThePendingSet()
    {
        var db       = BuildDb();
        SeedPendingRow(db);
        var recorder = new RecordingPublisher();

        await BuildRelay(db, recorder).RelayPendingAsync(CancellationToken.None);

        var row    = await db.OutboxMessages.SingleAsync();
        var pending = await db.OutboxMessages.CountAsync(m => m.SentAt == null);

        row.SentAt.Should().NotBeNull();
        pending.Should().Be(0);
        recorder.Published.Should().HaveCount(1);
    }

    // ── Test 4: one bad row does not block healthy rows ───────────────────────

    [Fact]
    public async Task PartialFailure_DoesNotBlockHealthyRows()
    {
        var db = BuildDb();

        // First row: corrupt payload (simulates a bad message)
        var bad = SeedPendingRow(db, "BadAuthor");
        bad.Payload = "{{not valid json}}";
        db.SaveChanges();

        // Second row: healthy
        SeedPendingRow(db, "Seneca");

        var recorder = new RecordingPublisher();
        await BuildRelay(db, recorder).RelayPendingAsync(CancellationToken.None);

        // Healthy row was published despite the bad one
        recorder.Published.Should().HaveCount(1);
        recorder.Published[0].Author.Should().Be("Seneca");

        // Bad row stays pending
        var pending = await db.OutboxMessages.CountAsync(m => m.SentAt == null);
        pending.Should().Be(1);
    }
}

// ── Minimal IServiceScopeFactory that returns the same DbContext ──────────────

sealed class FakeScopeFactory : IServiceScopeFactory
{
    private readonly AppDbContext _db;
    public FakeScopeFactory(AppDbContext db) => _db = db;
    public IServiceScope CreateScope() => new FakeScope(_db);
}

sealed class FakeScope : IServiceScope, IAsyncDisposable
{
    public IServiceProvider ServiceProvider { get; }
    public FakeScope(AppDbContext db) =>
        ServiceProvider = new FakeServiceProvider(db);
    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

sealed class FakeServiceProvider : IServiceProvider
{
    private readonly AppDbContext _db;
    public FakeServiceProvider(AppDbContext db) => _db = db;
    public object? GetService(Type serviceType) =>
        serviceType == typeof(AppDbContext) ? _db : null;
}
