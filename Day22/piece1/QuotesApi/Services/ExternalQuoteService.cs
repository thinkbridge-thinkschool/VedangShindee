namespace QuotesApi.Services;

// Typed HTTP client — DI injects an HttpClient pre-configured with the Polly resilience pipeline.
// All operations are GET (idempotent), satisfying the retry-only-for-idempotent contract.
public sealed class ExternalQuoteService : IExternalQuoteService
{
    private readonly HttpClient _client;

    public ExternalQuoteService(HttpClient client) => _client = client;

    public async Task<string> GetEnrichmentAsync(int quoteId, CancellationToken ct = default)
    {
        var response = await _client.GetAsync($"/external-svc/enrich/{quoteId}", ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }
}
