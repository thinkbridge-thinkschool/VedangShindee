using System.Net;

namespace QuotesApi.Resilience;

// Terminal DelegatingHandler that simulates an external dependency.
// Sits innermost in the message-handler pipeline — Polly strategies execute before it.
// Toggle ResilienceDemoState.ExternalServiceShouldFail at runtime to trigger failure scenarios.
public sealed class FakeExternalServiceHandler : DelegatingHandler
{
    private readonly ResilienceDemoState _state;
    private readonly ILogger<FakeExternalServiceHandler> _logger;

    public FakeExternalServiceHandler(
        ResilienceDemoState state,
        ILogger<FakeExternalServiceHandler> logger)
    {
        _state = state;
        _logger = logger;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken ct)
    {
        if (_state.ExternalServiceShouldFail)
        {
            _logger.LogDebug("[FakeExtSvc] Returning 503 — chaos mode is ON");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("Service unavailable (simulated fault)")
            });
        }

        _logger.LogDebug("[FakeExtSvc] Returning 200 — chaos mode is OFF");
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"enrichment":"philosophy","confidence":0.95}""",
                System.Text.Encoding.UTF8,
                "application/json")
        });
    }
}
