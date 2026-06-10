# Day 20 — Transactional Outbox Pattern

## What problem does this solve?

Without the outbox pattern, `CreateQuoteHandler` saved the Quote to the DB and then
called `PublishAsync` directly. If the process crashed between those two steps, the
Quote existed in the DB but no event was ever published — a silent divergence with no
way to recover.

The outbox pattern eliminates this by writing the "intent to publish" into the same
DB transaction as the domain change, so the two can never diverge.

---

## The outbox table

```
OutboxMessages
  Id        uniqueidentifier  PK   (client-generated = same Guid as EventId)
  Topic     nvarchar(200)     NOT NULL
  Payload   nvarchar(max)     NOT NULL   — serialized QuoteCreatedEvent JSON
  CreatedAt datetimeoffset    NOT NULL
  SentAt    datetimeoffset    NULL       — null = pending, non-null = delivered

  INDEX IX_OutboxMessages_Pending ON (SentAt, CreatedAt) WHERE SentAt IS NULL
```

The filtered index covers only pending rows. Once all messages are delivered
the index size drops to zero — the relay's poll query stays O(pending), not O(total).

---

## The atomic write (CreateQuoteHandler)

```csharp
await using var tx = await _db.Database.BeginTransactionAsync(ct);

_db.Quotes.Add(quote);
await _db.SaveChangesAsync(ct);          // quote.Id populated via IDENTITY

_db.OutboxMessages.Add(new OutboxMessage
{
    Id      = eventId,                   // same Guid → MessageId on the bus
    Topic   = "quote-created",
    Payload = JsonSerializer.Serialize(evt),
});
await _db.SaveChangesAsync(ct);

await tx.CommitAsync(ct);               // both rows commit or neither does
```

If the transaction never commits (DB error, process kill) neither row lands —
no phantom events, no orphaned quotes.

---

## The crash scenario I tested

**Setup:** one pending OutboxMessage row with `SentAt = null`.

**Tick 1 — crash:**
```
relay.PublishAsync(evt)      ← succeeds
                             ← process is killed here
msg.SentAt = UtcNow          ← never reached
SaveChangesAsync()           ← never reached
```

**Result:** `SentAt` stays `null`. The row is still in the pending set.

**Tick 2 — retry:**
```
relay.PublishAsync(evt)      ← same EventId, same Payload (written once at insert)
msg.SentAt = UtcNow          ← now reached
SaveChangesAsync()           ← committed
```

**Why no message is lost:**
The row persisted in the DB before the crash. The relay finds it on the next poll
and re-delivers it. No state was lost — the DB is the source of truth.

**Why no duplicate observable effect:**
`ServiceBusMessage.MessageId = evt.EventId = outboxMsg.Id.ToString()`.
This Guid is written into the Payload at insert time and never regenerated.
Every retry sends the identical MessageId. The consumer's `IdempotencyStore`
is keyed on MessageId and silently skips the second delivery.

**Result: at-least-once delivery + idempotent consumer = exactly-once observable effect.**

---

## Tests that prove this (no Docker required)

| Test | What it verifies |
|---|---|
| `CrashBeforeSentAt_RowRemainsNull_MessageNotLost` | Row stays `SentAt=null` after crash — nothing lost |
| `RetryAfterCrash_DeliversSameEventId_IdempotentKey` | Retry sends the same `EventId` — stable dedup key |
| `SuccessfulRelay_StampsSentAt_RowLeavesThePendingSet` | Normal delivery marks the row done |
| `PartialFailure_DoesNotBlockHealthyRows` | One bad row does not stall the batch |

All four run in the unit test project against EF InMemory — no Docker, no Azure needed.

---

## Gap I found and fixed

The relay was only registered when `ServiceBus:ConnectionString` was set. Without it,
outbox rows accumulated silently with `SentAt = null` and no log output — the
operator had no visibility into pending messages.

**Fix:** the relay now always starts. When no connection string is present a
`NullQuoteEventPublisher` is registered instead, which logs a `WARN` for every
pending row so the gap is immediately visible in logs.
