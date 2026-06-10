using QuotesApi.Messaging.Models;

namespace QuotesApi.Outbox;

// Registered when no ServiceBus connection string is configured.
// Logs a warning for every pending outbox row so silent accumulation is visible,
// then returns without publishing — rows stay SentAt=null until a real publisher
// is wired up.
public sealed class NullQuoteEventPublisher : IQuoteEventPublisher
{
    private readonly ILogger<NullQuoteEventPublisher> _logger;

    public NullQuoteEventPublisher(ILogger<NullQuoteEventPublisher> logger)
        => _logger = logger;

    public Task PublishAsync(QuoteCreatedEvent evt, CancellationToken ct = default)
    {
        _logger.LogWarning(
            "[OutboxRelay] No Service Bus publisher configured. " +
            "OutboxMessage EventId={EventId} QuoteId={QuoteId} is pending and will not be delivered " +
            "until ServiceBus:ConnectionString is set.",
            evt.EventId, evt.QuoteId);

        return Task.CompletedTask;
    }
}
