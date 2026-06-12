using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Options;
using QuotesApi.Messaging.Models;
using QuotesApi.Options;
using QuotesApi.Outbox;

namespace QuotesApi.Messaging;

// Singleton service that sends messages to the "quotes" Service Bus topic.
//
// Topic vs Queue:
//   A topic fans out each message to every subscription independently.
//   Both email-notifications and audit-log subscriptions receive their own copy.
//   A queue would deliver each message to exactly ONE competing consumer.
public sealed class QuoteEventPublisher : IQuoteEventPublisher, IAsyncDisposable
{
    private readonly ServiceBusSender _sender;
    private readonly ILogger<QuoteEventPublisher> _logger;

    public QuoteEventPublisher(ServiceBusClient client, IOptions<ServiceBusOptions> opts,
        ILogger<QuoteEventPublisher> logger)
    {
        // ServiceBusSender is per-topic — cheap to create, reused for the process lifetime.
        _sender = client.CreateSender(opts.Value.TopicName);
        _logger = logger;
    }

    public async Task PublishAsync(QuoteCreatedEvent evt, CancellationToken ct = default)
    {
        var body = JsonSerializer.Serialize(evt);

        var message = new ServiceBusMessage(body)
        {
            // MessageId is the idempotency key used by both consumers.
            // We set it explicitly to evt.EventId so it survives any broker retry/re-enqueue.
            MessageId = evt.EventId,

            // Subject doubles as a routing hint and poison-message flag.
            Subject = evt.IsPoison ? "poison" : "quote-created",

            ContentType = "application/json",
        };

        // Optional application property — subscribers can filter on this without deserializing the body.
        message.ApplicationProperties["author"] = evt.Author;

        await _sender.SendMessageAsync(message, ct);

        _logger.LogInformation(
            "[Publisher] Sent {Subject} | MessageId={MessageId} | QuoteId={QuoteId} | Author={Author}",
            message.Subject, message.MessageId, evt.QuoteId, evt.Author);
    }

    public async ValueTask DisposeAsync() => await _sender.DisposeAsync();
}
