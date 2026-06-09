using QuotesApi.Models;
using QuotesApi.Repositories;
using QuotesApi.Services;

namespace QuotesApi.Commands;

public class CreateQuoteHandler
{
    private readonly IQuoteRepository _repository;
    private readonly IQuoteValidator _validator;
    private readonly QuoteAuditQueue _auditQueue;

    public CreateQuoteHandler(IQuoteRepository repository, IQuoteValidator validator, QuoteAuditQueue auditQueue)
    {
        _repository = repository;
        _validator = validator;
        _auditQueue = auditQueue;
    }

    public async Task<(CreateQuoteResult? Result, Dictionary<string, string[]>? Errors)> HandleAsync(
        CreateQuoteCommand command, CancellationToken ct)
    {
        var errors = _validator.Validate(new CreateQuoteRequest { Author = command.Author, Text = command.Text });
        if (errors.Count > 0)
            return (null, errors);

        var quote = new Quote { Author = command.Author, Text = command.Text, OwnerId = command.OwnerId };
        var created = await _repository.CreateAsync(quote, ct);

        // Enqueue off the request thread; the BackgroundService drains this asynchronously.
        _auditQueue.Enqueue(new QuoteAuditEvent("created", created.Id, created.Author, command.OwnerId, created.CreatedAt));

        return (new CreateQuoteResult(created.Id, created.Author, created.Text, created.CreatedAt), null);
    }
}
