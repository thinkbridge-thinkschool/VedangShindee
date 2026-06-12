using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Options;
using QuotesApi.Messaging.Models;
using QuotesApi.Options;

namespace QuotesApi.Messaging.Workers;

// ── Consumer for the "audit-log" subscription ───────────────────────────────────
//
// This is the SECOND subscription on the same "quotes" topic.
// Service Bus delivers an independent copy of every message to this subscription,
// regardless of what EmailNotificationWorker does with its own copy.
//
// Both workers share the singleton IdempotencyStore but namespace their keys differently
// (prefix "audit:" vs "email:") so a duplicate arriving at the audit subscription is
// still deduplicated correctly even though the email copy was already processed.
public sealed class AuditLogWorker : BackgroundService
{
    private readonly ServiceBusProcessor _processor;
    private readonly IdempotencyStore _idempotency;
    private readonly ILogger<AuditLogWorker> _logger;

    public AuditLogWorker(ServiceBusClient client, IOptions<ServiceBusOptions> opts,
        IdempotencyStore idempotency, ILogger<AuditLogWorker> logger)
    {
        _idempotency = idempotency;
        _logger = logger;

        _processor = client.CreateProcessor(
            opts.Value.TopicName,
            opts.Value.AuditSubscriptionName,
            new ServiceBusProcessorOptions
            {
                MaxConcurrentCalls = opts.Value.MaxConcurrentCalls,
                AutoCompleteMessages = false,
                MaxAutoLockRenewalDuration = TimeSpan.FromMinutes(2),
            });
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _processor.ProcessMessageAsync += HandleMessageAsync;
        _processor.ProcessErrorAsync += HandleErrorAsync;

        await _processor.StartProcessingAsync(stoppingToken);

        _logger.LogInformation("[AuditWorker] Started — listening on '{Topic}/{Sub}'",
            _processor.EntityPath, _processor.Identifier);

        try { await Task.Delay(Timeout.Infinite, stoppingToken); }
        catch (OperationCanceledException) { }

        _logger.LogInformation("[AuditWorker] Stopping...");
        await _processor.StopProcessingAsync();
        await _processor.DisposeAsync();
        _logger.LogInformation("[AuditWorker] Stopped cleanly");
    }

    private async Task HandleMessageAsync(ProcessMessageEventArgs args)
    {
        var msg = args.Message;
        var messageId = msg.MessageId;

        _logger.LogInformation(
            "[AuditWorker] Received MessageId={MessageId} Subject={Subject} DeliveryCount={DeliveryCount}",
            messageId, msg.Subject, msg.DeliveryCount);

        // Poison messages on this subscription are also dead-lettered independently.
        if (msg.Subject == "poison")
        {
            _logger.LogError(
                "[AuditWorker] Poison message (MessageId={MessageId}) — dead-lettering on audit-log subscription",
                messageId);

            await args.DeadLetterMessageAsync(
                msg,
                deadLetterReason: "PoisonMessage",
                deadLetterErrorDescription: "Subject=poison; detected by AuditLogWorker",
                args.CancellationToken);

            return;
        }

        // Subscription-namespaced key: "audit:{messageId}"
        var idempotencyKey = $"audit:{messageId}";
        if (!_idempotency.TryMarkSeen(idempotencyKey))
        {
            _logger.LogWarning(
                "[AuditWorker] Duplicate MessageId={MessageId} — skipping",
                messageId);
            await args.CompleteMessageAsync(msg, args.CancellationToken);
            return;
        }

        var evt = JsonSerializer.Deserialize<QuoteCreatedEvent>(msg.Body.ToString());
        if (evt is null)
        {
            await args.DeadLetterMessageAsync(msg, "DeserializationFailure", "Body is not a valid QuoteCreatedEvent", args.CancellationToken);
            return;
        }

        // Simulate writing an audit record — in production this would INSERT into an audit table.
        await Task.Delay(30, args.CancellationToken);

        _logger.LogInformation(
            "[AuditWorker] Audit record written → quote #{QuoteId} by {Author} published by {PublishedBy} at {OccurredAt} (EventId={EventId})",
            evt.QuoteId, evt.Author, evt.PublishedBy, evt.OccurredAt, evt.EventId);

        await args.CompleteMessageAsync(msg, args.CancellationToken);
    }

    private Task HandleErrorAsync(ProcessErrorEventArgs args)
    {
        _logger.LogError(args.Exception,
            "[AuditWorker] Processor error: Source={Source} EntityPath={EntityPath}",
            args.ErrorSource, args.EntityPath);
        return Task.CompletedTask;
    }
}
