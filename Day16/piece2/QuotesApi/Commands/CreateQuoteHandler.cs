using QuotesApi.Models;
using QuotesApi.Repositories;
using QuotesApi.Services;

namespace QuotesApi.Commands;

public class CreateQuoteHandler
{
    private readonly IQuoteRepository _repository;
    private readonly IQuoteValidator _validator;

    public CreateQuoteHandler(IQuoteRepository repository, IQuoteValidator validator)
    {
        _repository = repository;
        _validator = validator;
    }

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
