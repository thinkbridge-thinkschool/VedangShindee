using QuotesApi.Services;

namespace QuotesApi.BackgroundServices;

// BackgroundService is an IHostedService base class. It owns the Start/Stop lifecycle
// and hands you one method to implement: ExecuteAsync, which runs for the process lifetime.
public sealed class QuoteAuditWorker : BackgroundService
{
    private readonly QuoteAuditQueue _queue;
    private readonly ILogger<QuoteAuditWorker> _logger;

    public QuoteAuditWorker(QuoteAuditQueue queue, ILogger<QuoteAuditWorker> logger)
    {
        _queue = queue;
        _logger = logger;
    }

    // ExecuteAsync runs on a background thread from the moment the host starts.
    // stoppingToken is cancelled when the host receives SIGTERM (or Ctrl+C in dev).
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("QuoteAuditWorker started");

        // ReadAllAsync completes the async-enumerable when stoppingToken fires.
        // No try/catch needed — OperationCanceledException is swallowed internally.
        // In a real project this would batch-insert into DB or publish to a message bus.
        await foreach (var evt in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            _logger.LogInformation(
                "[Audit] {Action} quote {QuoteId} (author: {Author}) by user {UserId} at {OccurredAt}",
                evt.Action, evt.QuoteId, evt.Author, evt.UserId, evt.OccurredAt);
        }

        // ── Graceful-shutdown path ─────────────────────────────────────────────────
        // When ReadAllAsync exits (stoppingToken cancelled) we drain whatever is still
        // buffered so no event is silently dropped during shutdown.
        while (_queue.Reader.TryRead(out var remaining))
        {
            _logger.LogInformation(
                "[Audit/drain] {Action} quote {QuoteId} at {OccurredAt}",
                remaining.Action, remaining.QuoteId, remaining.OccurredAt);
        }

        _logger.LogInformation("QuoteAuditWorker stopped cleanly");
        // Returning from ExecuteAsync tells BackgroundService.StopAsync the task is done.
        // The host waits up to its ShutdownTimeout (default 30 s) before force-killing.
    }
}

// ── IHostedService vs BackgroundService ──────────────────────────────────────
//
//  IHostedService (interface)
//    StartAsync(CancellationToken) — called once on app start; you own the task lifecycle
//    StopAsync(CancellationToken)  — called on shutdown; you own cancellation
//    Use when you need asymmetric start/stop logic (e.g. start a timer, cancel it on stop)
//
//  BackgroundService (abstract class implementing IHostedService)
//    Wraps the above: StartAsync fires your ExecuteAsync in the background,
//    StopAsync cancels stoppingToken and awaits the running task.
//    Use for anything that looks like "loop forever until shutdown" — which is almost everything.
//
// ── When to reach for Hangfire instead of a hosted service ───────────────────
//
//  Use Hangfire when:
//    • Jobs must survive process restarts (persisted to SQL/Redis)
//    • You need cron scheduling (RecurringJob.AddOrUpdate)
//    • You want automatic retries on failure
//    • You want a management dashboard
//
//  One-liner answer to the exercise:
//    "Use Hangfire when job durability, scheduling, or retry semantics matter;
//     BackgroundService for lightweight in-process work that can restart with the app."
