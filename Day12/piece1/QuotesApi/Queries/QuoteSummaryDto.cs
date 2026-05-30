namespace QuotesApi.Queries;

// Read model: denormalized projection for the screen — OwnerId is omitted because callers never need it.
public record QuoteSummaryDto(int Id, string Author, string Text, DateTimeOffset CreatedAt);
