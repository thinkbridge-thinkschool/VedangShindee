using QuotesApi.Repositories;

namespace QuotesApi.Queries;

public record ListQuotesQuery(int Page, int Size);

public class ListQuotesHandler
{
    private readonly IQuoteRepository _repository;

    public ListQuotesHandler(IQuoteRepository repository) => _repository = repository;

    public async Task<List<QuoteSummaryDto>> HandleAsync(ListQuotesQuery query, CancellationToken ct)
    {
        var quotes = await _repository.GetPagedAsync(query.Page, query.Size, ct);
        return quotes.Select(q => new QuoteSummaryDto(q.Id, q.Author, q.Text, q.CreatedAt)).ToList();
    }
}
