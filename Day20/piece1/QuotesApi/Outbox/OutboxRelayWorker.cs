using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QuotesApi.Data;
using QuotesApi.Messaging.Models;

namespace QuotesApi.Outbox;

// Polls OutboxMessages for unsent rows, publishes each to the Service Bus topic,
// then stamps SentAt.  Runs every 5 seconds as a hosted background service.
//
// Delivery guarantee: at-least-once.
//   If the process crashes after PublishAsync succeeds but before SaveChangesAsync
//   commits SentAt, the row stays SentAt=null.  The next poll tick re-publishes
//   the same event with the same EventId, which becomes the same ServiceBus
//   MessageId.  The consumer's IdempotencyStore silently skips the duplicate.
public sealed class OutboxRelayWorker : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IQuoteEventPublisher _publisher;
    private readonly ILogger<OutboxRelayWorker> _logger;

    public OutboxRelayWorker(
        IServiceScopeFactory scopeFactory,
        IQuoteEventPublisher publisher,
        ILogger<OutboxRelayWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _publisher    = publisher;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await RelayPendingAsync(stoppingToken);
            await Task.Delay(PollInterval, stoppingToken)
                .ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }
    }

    // Internal so integration tests can invoke a single relay tick directly
    // without waiting for the timer to fire.
    public async Task RelayPendingAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Bound each tick to 50 rows so a backlog doesn't stall a single iteration.
        var pending = await db.OutboxMessages
            .Where(m => m.SentAt == null)
            .OrderBy(m => m.CreatedAt)
            .Take(50)
            .ToListAsync(ct);

        foreach (var msg in pending)
        {
            try
            {
                var evt = JsonSerializer.Deserialize<QuoteCreatedEvent>(msg.Payload)
                    ?? throw new InvalidOperationException(
                           $"Cannot deserialize OutboxMessage {msg.Id} (Topic={msg.Topic})");

                await _publisher.PublishAsync(evt, ct);

                // ── CRASH WINDOW ──────────────────────────────────────────────────
                // A process kill here leaves SentAt = null.  The next relay tick
                // re-delivers the same EventId to the bus — at-least-once.
                // Consumers keyed on MessageId = evt.EventId absorb the duplicate.

                msg.SentAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(ct);

                _logger.LogInformation(
                    "[OutboxRelay] Published OutboxId={OutboxId} QuoteId={QuoteId} EventId={EventId}",
                    msg.Id, evt.QuoteId, evt.EventId);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                // Log and continue so one bad row doesn't block the rest.
                _logger.LogWarning(ex,
                    "[OutboxRelay] Failed to relay OutboxId={OutboxId}; will retry on next tick",
                    msg.Id);
            }
        }
    }
}
