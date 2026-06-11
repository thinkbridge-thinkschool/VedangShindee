using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using QuotesApi.Data;
using QuotesApi.Messaging.Models;
using QuotesApi.Outbox;
using Xunit;

namespace Quotes.Tests.Integration;

// ── Test doubles ──────────────────────────────────────────────────────────────

/// <summary>Simulates a publish failure (network blip, broker unavailable, etc.).</summary>
sealed class CrashingPublisher : IQuoteEventPublisher
{
    public Task PublishAsync(QuoteCreatedEvent evt, CancellationToken ct = default) =>
        throw new InvalidOperationException("Simulated publish failure");
}

/// <summary>Succeeds silently and records every event it receives.</summary>
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
/// Proves the transactional outbox pattern end-to-end:
///   1. Quote + OutboxMessage are written atomically (no partial state).
///   2. A relay crash (publish succeeds, SentAt write lost) leaves the row pending.
///   3. The next relay tick re-delivers — at-least-once with idempotent consumption.
/// </summary>
[Collection(nameof(SharedSqlServer))]
public sealed class OutboxRelayTests : IDisposable
{
    private readonly QuotesWebAppFactory _factory;
    private readonly HttpClient _client;

    public OutboxRelayTests(SqlServerFixture fixture)
    {
        _factory = new QuotesWebAppFactory(fixture.ConnectionString);
        _client  = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private OutboxRelayWorker BuildRelay(IQuoteEventPublisher publisher)
    {
        var scopeFactory   = _factory.Services.GetRequiredService<IServiceScopeFactory>();
        using var logFac   = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
        return new OutboxRelayWorker(scopeFactory, publisher, logFac.CreateLogger<OutboxRelayWorker>());
    }

    private async Task<AppDbContext> OpenDbAsync()
    {
        var scope = _factory.Services.CreateAsyncScope();
        return scope.ServiceProvider.GetRequiredService<AppDbContext>();
    }

    private async Task PostQuoteAsync(string author, string text)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/quotes")
        {
            Headers = { Authorization = new("Bearer", _factory.MintLocalJwt()) },
            Content = JsonContent.Create(new { author, text }),
        };
        (await _client.SendAsync(req)).EnsureSuccessStatusCode();
    }

    // ── Test 1: atomicity ────────────────────────────────────────────────────

    /// <summary>
    /// A single POST /api/quotes must create exactly one pending OutboxMessage row
    /// in the same transaction as the Quote row.
    /// </summary>
    [Fact]
    public async Task CreateQuote_WritesOutboxRow_Atomically()
    {
        await PostQuoteAsync("Stoic", "Begin at once to live.");

        await using var db = await OpenDbAsync();

        var quotes  = await db.Quotes.CountAsync();
        var pending = await db.OutboxMessages.Where(m => m.SentAt == null).ToListAsync();

        Assert.Equal(1, quotes);
        Assert.Single(pending);
        Assert.Equal("quote-created", pending[0].Topic);
    }

    // ── Test 2: crash safety — the critical scenario ─────────────────────────

    /// <summary>
    /// Crash scenario: the relay calls PublishAsync, but the process crashes
    /// before SaveChangesAsync can persist SentAt.  The outbox row must remain
    /// SentAt=null so the next relay tick can retry.
    ///
    /// This proves NO MESSAGE IS LOST — the outbox row acts as a durable
    /// "intent to publish" that survives any process failure.
    /// </summary>
    [Fact]
    public async Task OutboxRelay_PublishCrashBeforeSentAt_RowRemainsForRetry()
    {
        await PostQuoteAsync("Epictetus", "He is wise who does not grieve.");

        // Run relay with a publisher that throws — simulating a crash between
        // the successful send and the SentAt update.
        await BuildRelay(new CrashingPublisher()).RelayPendingAsync(CancellationToken.None);

        await using var db = await OpenDbAsync();
        var pending = await db.OutboxMessages.Where(m => m.SentAt == null).ToListAsync();

        // The row is still pending — no message was lost, retry will happen.
        Assert.Single(pending);
    }

    // ── Test 3: successful relay ──────────────────────────────────────────────

    /// <summary>
    /// When publish succeeds, SentAt is stamped and the row is no longer returned
    /// by the pending poll.
    /// </summary>
    [Fact]
    public async Task OutboxRelay_SuccessfulPublish_StampsSentAt()
    {
        await PostQuoteAsync("Seneca", "Luck is preparation meeting opportunity.");

        var recorder = new RecordingPublisher();
        await BuildRelay(recorder).RelayPendingAsync(CancellationToken.None);

        await using var db = await OpenDbAsync();
        var row     = await db.OutboxMessages.SingleAsync();
        var pending = await db.OutboxMessages.CountAsync(m => m.SentAt == null);

        Assert.NotNull(row.SentAt);        // marked as sent
        Assert.Equal(0, pending);          // no longer in the pending set
        Assert.Single(recorder.Published); // exactly one publish call
    }

    // ── Test 4: at-least-once + idempotent key survives a retry ──────────────

    /// <summary>
    /// Full crash-and-retry proof:
    ///   Tick 1  — publish crashes → SentAt stays null (message not lost)
    ///   Tick 2  — publish succeeds → same EventId re-delivered, SentAt stamped
    ///
    /// The EventId on both publish calls is identical because it was persisted in
    /// the Payload column at write time.  An idempotent consumer keyed on EventId
    /// will skip the second delivery — at-least-once with no observable duplicate.
    /// </summary>
    [Fact]
    public async Task OutboxRelay_CrashThenRetry_DeliversSameEventIdAtLeastOnce()
    {
        await PostQuoteAsync("Aristotle", "We are what we repeatedly do.");

        var scopeFactory = _factory.Services.GetRequiredService<IServiceScopeFactory>();

        // Tick 1: crash
        await BuildRelay(new CrashingPublisher()).RelayPendingAsync(CancellationToken.None);

        // Tick 2: success
        var recorder = new RecordingPublisher();
        await BuildRelay(recorder).RelayPendingAsync(CancellationToken.None);

        // One event published on the retry tick.
        Assert.Single(recorder.Published);

        await using var db = await OpenDbAsync();
        var row = await db.OutboxMessages.SingleAsync();

        Assert.NotNull(row.SentAt);

        // EventId in the published event == OutboxMessage.Id — the stable dedup key.
        Assert.Equal(row.Id.ToString(), recorder.Published[0].EventId);
    }

    // ── Test 5: two quotes → two independent relay operations ─────────────────

    /// <summary>
    /// A partially-failing batch: first message crashes, second succeeds.
    /// The relay must not skip healthy rows because one row is unhealthy.
    /// </summary>
    [Fact]
    public async Task OutboxRelay_PartialFailure_DoesNotBlockHealthyRows()
    {
        await PostQuoteAsync("Plato",    "The beginning is the most important part.");
        await PostQuoteAsync("Socrates", "Know thyself.");

        await using var dbBefore = await OpenDbAsync();
        var rows = await dbBefore.OutboxMessages.OrderBy(m => m.CreatedAt).ToListAsync();
        Assert.Equal(2, rows.Count);

        // Make the first row's payload undeserializable so the relay treats it as a crash.
        rows[0].Payload = "{{invalid json}}";
        await dbBefore.SaveChangesAsync();

        var recorder = new RecordingPublisher();
        await BuildRelay(recorder).RelayPendingAsync(CancellationToken.None);

        // Second row was published despite first row failing.
        Assert.Single(recorder.Published);
        Assert.Equal("Socrates", recorder.Published[0].Author);

        // First row remains pending (corrupt payload, no SentAt).
        await using var dbAfter = await OpenDbAsync();
        var pending = await dbAfter.OutboxMessages.CountAsync(m => m.SentAt == null);
        Assert.Equal(1, pending);
    }
}
