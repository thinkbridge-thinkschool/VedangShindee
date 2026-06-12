namespace QuotesApi.Services;

public interface IExternalQuoteService
{
    // GET-only — idempotent by design; the Polly retry pipeline relies on this contract.
    Task<string> GetEnrichmentAsync(int quoteId, CancellationToken ct = default);
}
