using Microsoft.Extensions.Caching.Hybrid;
using QuotesApi.Repositories;

namespace QuotesApi.Queries;

public record GetQuoteByIdQuery(int Id);

public class GetQuoteByIdHandler
{
    private readonly IQuoteRepository _repository;
    private readonly HybridCache _cache;

    public GetQuoteByIdHandler(IQuoteRepository repository, HybridCache cache)
    {
        _repository = repository;
        _cache = cache;
    }

    public async Task<QuoteSummaryDto?> HandleAsync(GetQuoteByIdQuery query, CancellationToken ct)
    {
        // HybridCache coalesces concurrent requests for the same key: only ONE factory call
        // reaches the DB while all other in-flight requests for that key await the result.
        // This is the stampede protection — no thundering herd on a cold miss.
        return await _cache.GetOrCreateAsync(
            $"q:id:{query.Id}",
            async innerCt =>
            {
                var q = await _repository.GetByIdAsync(query.Id, innerCt);
                return q is null ? null : new QuoteSummaryDto(q.Id, q.Author, q.Text, q.CreatedAt);
            },
            new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(5),
                LocalCacheExpiration = TimeSpan.FromMinutes(5)
            },
            tags: [$"quote:{query.Id}"],
            cancellationToken: ct
        );
    }
}
