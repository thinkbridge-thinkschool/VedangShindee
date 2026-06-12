using Polly.CircuitBreaker;

namespace QuotesApi.Resilience;

// Singleton shared by the fake external-service handler and the demo endpoints.
// CircuitStateProvider is wired in by AddInfrastructure after the Polly pipeline is built.
public sealed class ResilienceDemoState
{
    private volatile bool _shouldFail;

    public bool ExternalServiceShouldFail
    {
        get => _shouldFail;
        set => _shouldFail = value;
    }

    // Set once by InfrastructureExtensions when the pipeline builder runs.
    public CircuitBreakerStateProvider? CircuitStateProvider { get; set; }
}
