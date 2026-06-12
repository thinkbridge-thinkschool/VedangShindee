using QuotesApi.Messaging.Models;

namespace QuotesApi.Outbox;

public interface IQuoteEventPublisher
{
    Task PublishAsync(QuoteCreatedEvent evt, CancellationToken ct = default);
}
