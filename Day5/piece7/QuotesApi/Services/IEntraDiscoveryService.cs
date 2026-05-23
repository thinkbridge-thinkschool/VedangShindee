namespace QuotesApi.Services;

public interface IEntraDiscoveryService
{
    Task<string> GetJwksUriAsync(string tenantId, CancellationToken ct = default);
}
