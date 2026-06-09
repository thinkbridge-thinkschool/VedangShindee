using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Options;
using QuotesApi.Messaging.Models;
using QuotesApi.Options;

namespace QuotesApi.Messaging.Workers;

// ── Competing-consumer worker for the "email-notifications" subscription ────────
//
// MaxConcurrentCalls = 2 means ServiceBusProcessor will invoke HandleMessageAsync
// on up to 2 concurrent Tasks at the same time, against the SAME subscription.
// Each concurrent call is a "competing consumer" — they race to lock and settle messages.
//
// Why competing consumers?
//   Scale-out throughput: N workers drain a high-volume subscription N× faster.
//   Fault tolerance: one worker can crash mid-process; the broker re-delivers to another.
//
// AutoCompleteMessages = false: we settle (Complete / DeadLetter) manually so we can
// call DeadLetterMessageAsync on poison messages instead of letting the broker complete.
public sealed class EmailNotificationWorker : BackgroundService
{
    private readonly ServiceBusProcessor _processor;
    private readonly IdempotencyStore _idempotency;
    private readonly ILogger<EmailNotificationWorker> _logger;

    public EmailNotificationWorker(ServiceBusClient client, IOptions<ServiceBusOptions> opts,
        IdempotencyStore idempotency, ILogger<EmailNotificationWorker> logger)
    {
        _idempotency = idempotency;
        _logger = logger;

        _processor = client.CreateProcessor(
            opts.Value.TopicName,
            opts.Value.EmailSubscriptionName,
            new ServiceBusProcessorOptions
            {
                // Each call to HandleMessageAsync runs concurrently — competing consumers.
                MaxConcurrentCalls = opts.Value.MaxConcurrentCalls,

                // We settle manually so poison messages can be dead-lettered without a throw.
                AutoCompleteMessages = false,

                // Keep the lock alive while slow handlers run (default 60 s).
                MaxAutoLockRenewalDuration = TimeSpan.FromMinutes(2),
            });
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _processor.ProcessMessageAsync += HandleMessageAsync;
        _processor.ProcessErrorAsync += HandleErrorAsync;

        await _processor.StartProcessingAsync(stoppingToken);

        _logger.LogInformation("[EmailWorker] Started — listening on '{Topic}/{Sub}'",
            _processor.EntityPath, _processor.Identifier);

        try { await Task.Delay(Timeout.Infinite, stoppingToken); }
        catch (OperationCanceledException) { }

        _logger.LogInformation("[EmailWorker] Stopping...");
        await _processor.StopProcessingAsync();
        await _processor.DisposeAsync();
        _logger.LogInformation("[EmailWorker] Stopped cleanly");
    }

    // ── Message handler (runs up to MaxConcurrentCalls times in parallel) ───────
    private async Task HandleMessageAsync(ProcessMessageEventArgs args)
    {
        var msg = args.Message;
        var messageId = msg.MessageId;
        var deliveryCount = msg.DeliveryCount;

        _logger.LogInformation(
            "[EmailWorker] Received MessageId={MessageId} Subject={Subject} DeliveryCount={DeliveryCount}",
            messageId, msg.Subject, deliveryCount);

        // ── Poison-message path ──────────────────────────────────────────────────
        // Subject="poison" is set by the publisher on demo poison messages.
        // We explicitly dead-letter rather than throwing, which is cleaner than
        // relying on MaxDeliveryCount exhaustion in a live demo.
        if (msg.Subject == "poison")
        {
            _logger.LogError(
                "[EmailWorker] Poison message detected (MessageId={MessageId}) — dead-lettering immediately",
                messageId);

            await args.DeadLetterMessageAsync(
                msg,
                deadLetterReason: "PoisonMessage",
                deadLetterErrorDescription: "Subject=poison; detected by EmailNotificationWorker",
                args.CancellationToken);

            return;
        }

        // ── Idempotency check ────────────────────────────────────────────────────
        // Key is subscription-namespaced so the same EventId can still be processed
        // once in AuditLogWorker without being blocked here.
        var idempotencyKey = $"email:{messageId}";
        if (!_idempotency.TryMarkSeen(idempotencyKey))
        {
            _logger.LogWarning(
                "[EmailWorker] Duplicate MessageId={MessageId} — already processed, completing without work",
                messageId);
            await args.CompleteMessageAsync(msg, args.CancellationToken);
            return;
        }

        // ── Normal processing ────────────────────────────────────────────────────
        var evt = JsonSerializer.Deserialize<QuoteCreatedEvent>(msg.Body.ToString());
        if (evt is null)
        {
            _logger.LogError("[EmailWorker] Failed to deserialize body — dead-lettering (MessageId={MessageId})", messageId);
            await args.DeadLetterMessageAsync(msg, "DeserializationFailure", "Body is not a valid QuoteCreatedEvent", args.CancellationToken);
            return;
        }

        // Simulate sending an email — in production this would call SendGrid / Azure Communication Services.
        await Task.Delay(50, args.CancellationToken);

        _logger.LogInformation(
            "[EmailWorker] Email sent → quote #{QuoteId} by {Author} (EventId={EventId}, MessageId={MessageId})",
            evt.QuoteId, evt.Author, evt.EventId, messageId);

        await args.CompleteMessageAsync(msg, args.CancellationToken);
    }

    private Task HandleErrorAsync(ProcessErrorEventArgs args)
    {
        _logger.LogError(args.Exception,
            "[EmailWorker] Processor error: Source={Source} EntityPath={EntityPath}",
            args.ErrorSource, args.EntityPath);
        return Task.CompletedTask;
    }
}
