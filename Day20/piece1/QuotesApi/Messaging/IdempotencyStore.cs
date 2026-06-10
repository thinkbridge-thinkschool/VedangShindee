using System.Collections.Concurrent;

namespace QuotesApi.Messaging;

// Thread-safe in-memory deduplication store, registered as singleton.
//
// Keying convention: "{subscription-prefix}:{eventId}" — each subscription namespace
// is independent so the same EventId can legitimately be processed once per subscription
// (email once, audit once) without blocking either.
//
// Production note: replace with a distributed store (Redis SET NX, SQL upsert) to
// survive pod restarts and work across multiple replicas.
public sealed class IdempotencyStore
{
    // Value = UTC timestamp of first processing; useful for debugging duplicate-rate dashboards.
    private readonly ConcurrentDictionary<string, DateTimeOffset> _seen = new();

    /// <summary>
    /// Returns true (and records the key) when this key is NEW.
    /// Returns false when it was already seen — caller should skip processing.
    /// </summary>
    public bool TryMarkSeen(string key) => _seen.TryAdd(key, DateTimeOffset.UtcNow);

    public int Count => _seen.Count;

    // Expose snapshot for the demo endpoint so we can display what's been deduplicated.
    public IReadOnlyDictionary<string, DateTimeOffset> Snapshot() =>
        new Dictionary<string, DateTimeOffset>(_seen);
}
