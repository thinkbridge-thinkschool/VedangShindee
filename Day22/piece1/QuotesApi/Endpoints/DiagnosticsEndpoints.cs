using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using QuotesApi.Services;

namespace QuotesApi.Endpoints;

// Thin diagnostic surface used by the load test to observe cache impact without reading logs.
public static class DiagnosticsEndpoints
{
    public static IEndpointRouteBuilder MapDiagnosticsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/diag");

        // Returns which cache backend is wired up as L2.
        group.MapGet("/cache-info", (IDistributedCache distributedCache) =>
        {
            var backendType = distributedCache.GetType().Name;
            var isRedis = backendType.Contains("Redis", StringComparison.OrdinalIgnoreCase);
            return Results.Ok(new
            {
                l1 = "IMemoryCache (in-process)",
                l2 = backendType,
                redisActive = isRedis,
                stampede = "HybridCache in-flight coalescing"
            });
        });

        // Returns the number of EF reader executions since the last reset.
        group.MapGet("/db-queries", (DbQueryCounter counter) =>
            Results.Ok(new { count = counter.Count }));

        // Resets the counter and returns the previous value.
        group.MapPost("/db-queries/reset", (DbQueryCounter counter) =>
        {
            var prev = counter.Reset();
            return Results.Ok(new { previous = prev, now = 0 });
        });

        // Evicts a single quote's cache entry plus all paged-list entries.
        // The load test calls this to simulate a cold-cache scenario.
        group.MapPost("/cache/evict/{id:int}", async (int id, HybridCache cache, CancellationToken ct) =>
        {
            await cache.RemoveByTagAsync($"quote:{id}", ct);
            await cache.RemoveByTagAsync("quotes:list", ct);
            return Results.Ok(new { evicted = $"quote:{id}", also = "quotes:list" });
        });

        // Evicts all paged-list entries.
        group.MapPost("/cache/evict-lists", async (HybridCache cache, CancellationToken ct) =>
        {
            await cache.RemoveByTagAsync("quotes:list", ct);
            return Results.Ok(new { evicted = "quotes:list" });
        });

        return app;
    }
}
