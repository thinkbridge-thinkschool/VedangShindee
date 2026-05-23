using System.Text.Json;

namespace QuotesApi.Services;

public class EntraDiscoveryService : IEntraDiscoveryService
{
    private readonly HttpClient _http;
    private readonly ILogger<EntraDiscoveryService> _logger;

    public EntraDiscoveryService(HttpClient http, ILogger<EntraDiscoveryService> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<string> GetJwksUriAsync(string tenantId, CancellationToken ct = default)
    {
        var url = $"https://login.microsoftonline.com/{tenantId}/v2.0/.well-known/openid-configuration";

        _logger.LogInformation("Fetching OIDC discovery for tenant {TenantId}", tenantId);

        var json = await _http.GetStringAsync(url, ct);

        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("jwks_uri").GetString()
            ?? throw new InvalidOperationException("jwks_uri missing in Entra discovery document.");
    }
}
