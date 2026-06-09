using QuotesApi.Messaging;
using QuotesApi.Messaging.Models;
using QuotesApi.Messaging.Workers;

namespace QuotesApi.Endpoints;

// ── REST surface for the Service Bus demo ───────────────────────────────────────
//
// POST /sb/publish        – publish a normal QuoteCreatedEvent to the topic
// POST /sb/publish-poison – publish a poison message (Subject=poison) to the topic
// POST /sb/publish-duplicate – re-publish a previous EventId to trigger idempotency
// GET  /sb/dlq            – peek DLQ on both subscriptions (via DlqMonitorWorker)
// GET  /sb/idempotency    – show all keys the in-memory store has seen
public static class ServiceBusDemoEndpoints
{
    public static void MapServiceBusDemoEndpoints(this WebApplication app)
    {
        var grp = app.MapGroup("/sb").WithTags("ServiceBus Demo");

        // ── Publish a normal event ───────────────────────────────────────────────
        grp.MapPost("/publish", async (PublishRequest req, QuoteEventPublisher publisher) =>
        {
            var evt = new QuoteCreatedEvent
            {
                QuoteId = req.QuoteId,
                Author = req.Author,
                Text = req.Text,
                PublishedBy = req.PublishedBy,
                IsPoison = false,
            };

            await publisher.PublishAsync(evt);

            return Results.Ok(new
            {
                EventId = evt.EventId,
                Subject = "quote-created",
                Message = $"Published QuoteCreatedEvent for quote #{evt.QuoteId}",
            });
        });

        // ── Publish a poison message ─────────────────────────────────────────────
        // Both EmailNotificationWorker and AuditLogWorker will dead-letter this message.
        // Check /sb/dlq after a few seconds to see it appear in the dead-letter sub-queue.
        grp.MapPost("/publish-poison", async (PoisonRequest req, QuoteEventPublisher publisher) =>
        {
            var evt = new QuoteCreatedEvent
            {
                QuoteId = -1,
                Author = "Poison Author",
                Text = "This message is intentionally malformed.",
                PublishedBy = req.PublishedBy,
                IsPoison = true,
            };

            await publisher.PublishAsync(evt);

            return Results.Ok(new
            {
                EventId = evt.EventId,
                Subject = "poison",
                Message = "Poison message published — both workers will dead-letter it. " +
                          "Poll GET /sb/dlq in a few seconds to see it in the DLQ.",
            });
        });

        // ── Re-publish the same EventId to demonstrate idempotency ──────────────
        // Pass the EventId returned by a previous /publish call.
        // The workers will log "Duplicate MessageId — skipping" and complete without side-effects.
        grp.MapPost("/publish-duplicate", async (DuplicateRequest req, QuoteEventPublisher publisher) =>
        {
            // Force the same EventId (= MessageId on the ServiceBusMessage).
            // Azure Service Bus does NOT deduplicate at the broker level by default
            // (only if EnableDuplicateDetection was set on the topic), so the message
            // WILL land in both subscriptions — our application-level IdempotencyStore
            // is what skips re-processing.
            var evt = new QuoteCreatedEvent
            {
                EventId = req.OriginalEventId,          // reuse the original GUID
                QuoteId = req.QuoteId,
                Author = req.Author,
                Text = "DUPLICATE — same EventId, should be skipped by both workers",
                PublishedBy = "DuplicateTest",
                IsPoison = false,
            };

            await publisher.PublishAsync(evt);

            return Results.Ok(new
            {
                ReusedEventId = evt.EventId,
                Message = "Duplicate published. Both workers will log 'already processed — skipping'.",
            });
        });

        // ── Peek DLQ ────────────────────────────────────────────────────────────
        grp.MapGet("/dlq", async (DlqMonitorWorker monitor) =>
        {
            var emailDlq = await monitor.GetEmailDlqAsync();
            var auditDlq = await monitor.GetAuditDlqAsync();

            return Results.Ok(new
            {
                EmailNotificationsDlq = emailDlq,
                AuditLogDlq = auditDlq,
                Note = "DLQ is polled every 30 s by DlqMonitorWorker. " +
                       "If empty, either no poison messages were sent or the poll hasn't fired yet.",
            });
        });

        // ── Idempotency store snapshot ───────────────────────────────────────────
        grp.MapGet("/idempotency", (IdempotencyStore store) =>
        {
            var snapshot = store.Snapshot();
            return Results.Ok(new
            {
                TotalSeen = store.Count,
                Keys = snapshot.Select(kvp => new { Key = kvp.Key, ProcessedAt = kvp.Value }),
            });
        });
    }
}

// Request models

public sealed record PublishRequest(
    int QuoteId,
    string Author,
    string Text,
    string PublishedBy = "API");

public sealed record PoisonRequest(string PublishedBy = "API");

public sealed record DuplicateRequest(
    string OriginalEventId,
    int QuoteId,
    string Author);
