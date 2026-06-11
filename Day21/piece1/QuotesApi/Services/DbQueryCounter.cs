namespace QuotesApi.Services;

// Singleton counter incremented by CountingDbCommandInterceptor on every EF reader execution.
// Exposed via /diag/db-queries so the load test can observe cache impact without touching logs.
public sealed class DbQueryCounter
{
    private long _count;
    public long Count => Volatile.Read(ref _count);
    public void Increment() => Interlocked.Increment(ref _count);
    public long Reset() => Interlocked.Exchange(ref _count, 0);
}
