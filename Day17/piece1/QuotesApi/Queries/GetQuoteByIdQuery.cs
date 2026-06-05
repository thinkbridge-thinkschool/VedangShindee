using QuotesApi.Repositories;

namespace QuotesApi.Queries;

public record GetQuoteByIdQuery(int Id);

public class GetQuoteByIdHandler
{
    private readonly IQuoteRepository _repository;

    public GetQuoteByIdHandler(IQuoteRepository repository) => _repository = repository;

    public async Task<QuoteSummaryDto?> HandleAsync(GetQuoteByIdQuery query, CancellationToken ct)
    {
        var quote = await _repository.GetByIdAsync(query.Id, ct);
        return quote is null ? null : new QuoteSummaryDto(quote.Id, quote.Author, quote.Text, quote.CreatedAt);
    }
}
