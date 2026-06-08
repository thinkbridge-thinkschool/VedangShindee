namespace QuotesApi.Services;

public record QuoteAuditEvent(
    string Action,      // "created" | "deleted"
    int QuoteId,
    string Author,
    int? UserId,
    DateTimeOffset OccurredAt);
