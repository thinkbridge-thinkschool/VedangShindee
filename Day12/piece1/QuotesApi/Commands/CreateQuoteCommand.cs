namespace QuotesApi.Commands;

// Write model: normalized, carries everything the write side needs (including OwnerId for the entity).
public record CreateQuoteCommand(string Author, string Text, int? OwnerId);

public record CreateQuoteResult(int Id, string Author, string Text, DateTimeOffset CreatedAt);
