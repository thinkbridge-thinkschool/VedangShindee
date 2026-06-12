using QuotesApi.Resilience;
using QuotesApi.Services;

namespace QuotesApi.Endpoints;

// ── Resilience demo endpoints ────────────────────────────────────────────────────────────────
//
// POST /resilience/chaos/enable   — make the fake external service return 503
// POST /resilience/chaos/disable  — make it return 200
// GET  /resilience/probe/{id}     — call the service through the full Polly pipeline
// GET  /resilience/circuit-status — show current circuit-breaker state
//
// Typical demo sequence to observe circuit opening and recovery:
//   1. POST /resilience/chaos/enable
//   2. GET  /resilience/probe/1  × 4   (retries exhaust → CB opens after ≥3 failures)
//   3. GET  /resilience/circuit-status → "Open"
//   4. Wait 15 s (break duration)
//   5. GET  /resilience/circuit-status → "HalfOpen"
//   6. POST /resilience/chaos/disable
//   7. GET  /resilience/probe/1         (half-open probe succeeds → CB closes)
//   8. GET  /resilience/circuit-status → "Closed"
public static class ResilienceEndpoints
{
    public static void MapResilienceEndpoints(this WebApplication app)
    {
        var grp = app.MapGroup("/resilience").WithTags("Resilience Demo");

        // ── Chaos toggle ─────────────────────────────────────────────────────────────────────
        grp.MapPost("/chaos/enable", (ResilienceDemoState state) =>
        {
            state.ExternalServiceShouldFail = true;
            return Results.Ok(new
            {
                ChaosEnabled = true,
                Message = "External service will now return 503. " +
                          "Call GET /resilience/probe/1 several times to trip the circuit breaker."
            });
        });

        grp.MapPost("/chaos/disable", (ResilienceDemoState state) =>
        {
            state.ExternalServiceShouldFail = false;
            return Results.Ok(new
            {
                ChaosEnabled = false,
                Message = "External service will now return 200. " +
                          "The next probe through the half-open window will close the circuit."
            });
        });

        // ── Probe ────────────────────────────────────────────────────────────────────────────
        // Each call goes through: ConcurrencyLimiter → Retry → CircuitBreaker → Timeout → FakeHandler
        grp.MapGet("/probe/{id:int}", async (
            int id,
            IExternalQuoteService svc,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            try
            {
                var data = await svc.GetEnrichmentAsync(id, ct);
                logger.LogInformation("[Probe] Success — data: {Data}", data);
                return Results.Ok(new { Success = true, QuoteId = id, Data = data });
            }
            catch (Exception ex)
            {
                logger.LogWarning("[Probe] Failed — {ExType}: {Msg}", ex.GetType().Name, ex.Message);
                return Results.Problem(
                    title: "Dependency call failed",
                    detail: ex.GetType().Name + ": " + ex.Message,
                    statusCode: 503);
            }
        });

        // ── Circuit breaker state ─────────────────────────────────────────────────────────────
        grp.MapGet("/circuit-status", (ResilienceDemoState state) =>
        {
            var circuitState = state.CircuitStateProvider?.CircuitState.ToString() ?? "not-yet-created";
            return Results.Ok(new
            {
                CircuitState = circuitState,
                ChaosEnabled = state.ExternalServiceShouldFail,
                Hint = circuitState switch
                {
                    "Closed"   => "Normal — all calls go through.",
                    "Open"     => "Broken — calls fail immediately. Wait for BreakDuration (15 s).",
                    "HalfOpen" => "Probing — one test call allowed. Disable chaos then call /probe.",
                    _          => "Trigger a probe first to initialise the pipeline."
                }
            });
        });
    }
}
