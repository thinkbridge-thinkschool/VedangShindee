# Solution – CQRS-lite

## Command Handler

```csharp
public class CreateQuoteHandler
{
    public async Task<(CreateQuoteResult? Result, Dictionary<string, string[]>? Errors)> HandleAsync(
        CreateQuoteCommand command, CancellationToken ct)
    {
        var errors = _validator.Validate(new CreateQuoteRequest { Author = command.Author, Text = command.Text });
        if (errors.Count > 0)
            return (null, errors);

        var quote = new Quote { Author = command.Author, Text = command.Text, OwnerId = command.OwnerId };
        var created = await _repository.CreateAsync(quote, ct);

        return (new CreateQuoteResult(created.Id, created.Author, created.Text, created.CreatedAt), null);
    }
}
```

---

## Query / Read Model

```csharp
// Read model — OwnerId intentionally omitted, callers never need it
public record QuoteSummaryDto(int Id, string Author, string Text, DateTimeOffset CreatedAt);

public class ListQuotesHandler
{
    public async Task<List<QuoteSummaryDto>> HandleAsync(ListQuotesQuery query, CancellationToken ct)
    {
        var quotes = await _repository.GetPagedAsync(query.Page, query.Size, ct);
        return quotes.Select(q => new QuoteSummaryDto(q.Id, q.Author, q.Text, q.CreatedAt)).ToList();
    }
}
```

---

## What got simpler

The endpoint no longer validates, projects, or extracts ownership — it just builds a command or query and delegates, so the read and write paths can change independently without touching each other.
