namespace QuotesApi.Messaging.Models;

// The envelope published to the "quotes" topic for every new quote.
// Consumers on both subscriptions receive an independent copy of this message.
public sealed record QuoteCreatedEvent
{
    // Stable identity for this event — set once by the publisher, never regenerated.
    // Both subscription consumers use this as their idempotency key so re-deliveries
    // (network blip, at-least-once guarantee) are silently skipped after first processing.
    public string EventId { get; init; } = Guid.NewGuid().ToString();

    public int QuoteId { get; init; }
    public string Author { get; init; } = "";
    public string Text { get; init; } = "";
    public string PublishedBy { get; init; } = "";
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

    // When true the publisher sets Subject = "poison" on the ServiceBusMessage.
    // The handlers detect this and explicitly dead-letter the message, demonstrating
    // the DLQ path without waiting for MaxDeliveryCount natural retries.
    public bool IsPoison { get; init; }
}
