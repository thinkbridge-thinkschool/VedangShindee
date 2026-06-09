using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using QuotesApi.BackgroundServices;
using QuotesApi.Services;
using Xunit;

namespace Quotes.Tests.Unit;

public class QuoteAuditWorkerTests
{
    private static QuoteAuditEvent MakeEvent(string action = "created") =>
        new(action, QuoteId: 1, Author: "Seneca", UserId: 42, OccurredAt: DateTimeOffset.UtcNow);

    // ── Drains the queue ──────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_EnqueuedEvents_AreProcessedBeforeShutdown()
    {
        // Arrange
        var queue  = new QuoteAuditQueue();
        var worker = new QuoteAuditWorker(queue, NullLogger<QuoteAuditWorker>.Instance);

        queue.Enqueue(MakeEvent("created"));
        queue.Enqueue(MakeEvent("deleted"));

        using var cts = new CancellationTokenSource();

        // Act — start the worker, let it drain, then cancel
        var task = worker.StartAsync(cts.Token);
        await Task.Delay(100);          // give the worker time to process both events
        await cts.CancelAsync();
        await worker.StopAsync(CancellationToken.None);

        // Assert — queue is empty: both events were consumed
        queue.Reader.TryRead(out _).Should().BeFalse("all enqueued events should have been drained");
        await task;
    }

    // ── Shuts down cleanly ────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_CancellationRequested_CompletesWithoutException()
    {
        // Arrange
        var queue  = new QuoteAuditQueue();
        var worker = new QuoteAuditWorker(queue, NullLogger<QuoteAuditWorker>.Instance);

        using var cts = new CancellationTokenSource();

        // Act
        await worker.StartAsync(cts.Token);
        await cts.CancelAsync();

        // StopAsync should return without throwing — graceful shutdown
        var act = async () => await worker.StopAsync(CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    // ── Empty queue at shutdown ───────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_EmptyQueue_StopsCleanlyWithNoEvents()
    {
        // Arrange
        var queue  = new QuoteAuditQueue();
        var worker = new QuoteAuditWorker(queue, NullLogger<QuoteAuditWorker>.Instance);

        using var cts = new CancellationTokenSource();

        // Act — cancel immediately, nothing was ever enqueued
        await worker.StartAsync(cts.Token);
        await cts.CancelAsync();
        await worker.StopAsync(CancellationToken.None);

        // Assert — no items left, no exception thrown
        queue.Reader.TryRead(out _).Should().BeFalse();
    }
}
