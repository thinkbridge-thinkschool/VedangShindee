using FluentAssertions;
using QuotesApi.Messaging;
using Xunit;

namespace Quotes.Tests.Unit;

public class IdempotencyStoreTests
{
    // ── New key ───────────────────────────────────────────────────────────────

    [Fact]
    public void TryMarkSeen_NewKey_ReturnsTrue()
    {
        var store = new IdempotencyStore();

        var result = store.TryMarkSeen("email:abc-123");

        result.Should().BeTrue("a key that has never been seen should be accepted");
    }

    // ── Duplicate key ─────────────────────────────────────────────────────────

    [Fact]
    public void TryMarkSeen_DuplicateKey_ReturnsFalse()
    {
        var store = new IdempotencyStore();
        store.TryMarkSeen("email:abc-123");

        var result = store.TryMarkSeen("email:abc-123");

        result.Should().BeFalse("re-presenting the same key must be rejected");
    }

    // ── Subscription namespacing ──────────────────────────────────────────────

    [Fact]
    public void TryMarkSeen_SameEventIdDifferentSubscriptionPrefix_BothReturnTrue()
    {
        // The same EventId can be processed once per subscription.
        // email: and audit: are independent namespaces in the store.
        var store = new IdempotencyStore();

        var emailResult = store.TryMarkSeen("email:abc-123");
        var auditResult = store.TryMarkSeen("audit:abc-123");

        emailResult.Should().BeTrue();
        auditResult.Should().BeTrue("different prefix = different key, not a duplicate");
    }

    // ── Count ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Count_ReflectsNumberOfUniqueKeys()
    {
        var store = new IdempotencyStore();

        store.TryMarkSeen("email:1");
        store.TryMarkSeen("audit:1");
        store.TryMarkSeen("email:1"); // duplicate — should not increment

        store.Count.Should().Be(2, "only two distinct keys were added");
    }

    // ── Snapshot ──────────────────────────────────────────────────────────────

    [Fact]
    public void Snapshot_ContainsAllSeenKeys()
    {
        var store = new IdempotencyStore();
        store.TryMarkSeen("email:x");
        store.TryMarkSeen("audit:x");

        var snapshot = store.Snapshot();

        snapshot.Should().ContainKey("email:x");
        snapshot.Should().ContainKey("audit:x");
    }

    // ── Thread-safety ─────────────────────────────────────────────────────────

    [Fact]
    public async Task TryMarkSeen_ConcurrentCallsSameKey_ExactlyOneSucceeds()
    {
        var store = new IdempotencyStore();
        const string key = "email:race-condition";
        const int threads = 20;

        // Fire 20 concurrent tasks all trying to mark the same key.
        var results = await Task.WhenAll(
            Enumerable.Range(0, threads)
                      .Select(_ => Task.Run(() => store.TryMarkSeen(key))));

        results.Count(r => r).Should().Be(1, "exactly one concurrent caller should win");
        results.Count(r => !r).Should().Be(threads - 1, "all others must be rejected as duplicates");
        store.Count.Should().Be(1);
    }
}
