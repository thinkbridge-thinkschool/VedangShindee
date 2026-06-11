using Microsoft.Extensions.Caching.Hybrid;
using QuotesApi.Repositories;

namespace QuotesApi.Queries;

public record ListQuotesQuery(int Page, int Size);

public class ListQuotesHandler
{
    private readonly IQuoteRepository _repository;
    private readonly HybridCache _cache;

    public ListQuotesHandler(IQuoteRepository repository, HybridCache cache)
    {
        _repository = repository;
        _cache = cache;
    }

    public async Task<List<QuoteSummaryDto>> HandleAsync(ListQuotesQuery query, CancellationToken ct)
    {
        // Shorter TTL (2 min) for pages because their content shifts on create/delete.
        // Tag "quotes:list" lets us evict all page keys in one call when the list mutates.
        return await _cache.GetOrCreateAsync(
            $"q:page:{query.Page}:sz:{query.Size}",
            async innerCt =>
            {
                var quotes = await _repository.GetPagedAsync(query.Page, query.Size, innerCt);
                return quotes.Select(q => new QuoteSummaryDto(q.Id, q.Author, q.Text, q.CreatedAt)).ToList();
            },
            new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(2),
                LocalCacheExpiration = TimeSpan.FromMinutes(2)
            },
            tags: ["quotes:list"],
            cancellationToken: ct
        ) ?? [];
    }
}
