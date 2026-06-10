namespace QuotesApi.Outbox;

public class OutboxMessage
{
    // Client-generated so the relay can use it as a stable MessageId without a DB round-trip.
    public Guid Id { get; init; } = Guid.NewGuid();

    // Discriminator — lets the relay know which CLR type to deserialize Payload into.
    public string Topic { get; init; } = "";

    // Full event JSON, e.g. a serialized QuoteCreatedEvent.
    public string Payload { get; set; } = "";

    public DateTimeOffset CreatedAt { get; init; }

    // null  = pending relay; set = the row has been successfully published and can be cleaned up.
    public DateTimeOffset? SentAt { get; set; }
}
