# Day 5 – Piece 1: Observability & Slow Endpoint Diagnosis

## What Was Done

Intentionally introduced an **N+1 query bug** into `GET /api/quotes`, diagnosed it using the
OpenTelemetry console exporter (Jaeger-compatible OTLP traces), then fixed it and confirmed the
fix via traces.

---

## The Bug: N+1 in `QuoteRepository.GetPagedAsync`

```csharp
// BEFORE — N+1 (bad)
var quotes = await _db.Quotes.Skip(...).Take(...).ToListAsync();

// One extra DB round-trip per quote to load the owner
foreach (var quote in quotes)
{
    if (quote.OwnerId.HasValue)
        _ = await _db.Users.AsNoTracking()
              .FirstOrDefaultAsync(u => u.Id == quote.OwnerId.Value, ct);
}
return quotes;
```

With 5 quotes on the page, each request fired **6 SQL queries**:
- 1 × `SELECT FROM Quotes` (the paged fetch)
- 5 × `SELECT FROM Users WHERE Id = ?` (one per quote, bypassing the identity cache via `AsNoTracking`)

---

## Before Trace — OTel Console Exporter Output (TraceId: `d6c5c1e2943d62c4248a82bb6aa44cfe`)

```
Activity.DisplayName:  GET /api/quotes/
Activity.Kind:         Server
Activity.StartTime:    2026-05-23T02:59:47.7148975Z
Activity.Duration:     00:00:00.7170216          ← 717 ms total
Activity.Tags:
    http.response.status_code: 200
    http.route: /api/quotes/

  ├── [SpanId: 00cde270] main (db.system: sqlite)
  │     db.statement: SELECT "q"."Id"… FROM "Quotes" LIMIT @p1 OFFSET @p
  │     Duration: 40.9 ms
  │
  ├── [SpanId: 3b8b9922] main (db.system: sqlite)
  │     db.statement: SELECT "u"."Id"… FROM "Users" WHERE "u"."Id" = @quote_OwnerId_Value LIMIT ?
  │     Duration: 1.2 ms   ← quote 1 owner lookup
  │
  ├── [SpanId: 590075ed] main   Duration: 0.4 ms   ← quote 2 owner lookup
  ├── [SpanId: 34355a36] main   Duration: 0.4 ms   ← quote 3 owner lookup
  ├── [SpanId: 50a34866] main   Duration: 0.3 ms   ← quote 4 owner lookup
  └── [SpanId: 92855c80] main   Duration: 0.4 ms   ← quote 5 owner lookup
```

**Span count per request: 6 (1 + N where N = 5 quotes)**

Measured round-trip times via PowerShell:
```
Hit 1 : HTTP 200 in 804 ms
Hit 2 : HTTP 200 in 349 ms
Hit 3 : HTTP 200 in 493 ms
```

---

## After Trace — Fixed, OTel Console Exporter Output (TraceId: `dbf6c892242116c06b09de8aa6c041eb`)

```csharp
// AFTER — single query (good)
return await _db.Quotes
    .Skip((page - 1) * size)
    .Take(size)
    .ToListAsync(cancellationToken);
```

```
Activity.DisplayName:  GET /api/quotes/
Activity.Kind:         Server
Activity.StartTime:    2026-05-23T03:01:04.3319677Z
Activity.Duration:     00:00:00.2946034          ← 295 ms total
Activity.Tags:
    http.response.status_code: 200
    http.route: /api/quotes/

  └── [SpanId: a6201873] main (db.system: sqlite)
        db.statement: SELECT "q"."Id"… FROM "Quotes" LIMIT @p1 OFFSET @p
        Duration: 0.78 ms
```

**Span count per request: 1 (no User lookups)**

Measured round-trip times via PowerShell:
```
Hit 1 : HTTP 200 in 1030 ms   (cold start)
Hit 2 : HTTP 200 in 328 ms    ← warm — only 1 DB span
Hit 3 : HTTP 200 in 862 ms
```

> The N+1 `SELECT FROM "Users"` spans are **completely gone** in the after trace.

---

## Diagnosis Note (≈100 words)

> This trace showed the slow span was `GET /api/quotes/` because of an N+1 query in
> `QuoteRepository.GetPagedAsync`. The EF Core OTel instrumentation exposed it immediately:
> one request with 5 quotes produced 6 database child spans — 1 for the paged Quotes fetch and
> 5 identical `SELECT FROM Users WHERE Id = ?` queries, one per quote. The root cause was a
> `foreach` loop calling `AsNoTracking().FirstOrDefaultAsync` per quote after loading the list.
> The fix was to delete the loop entirely; owner data either shouldn't be loaded on this endpoint
> or should be fetched via a single JOIN/`.Include()`. Post-fix traces confirm exactly 1 DB span
> per request.

---

## KQL — Find Similar Slow Endpoints in Application Insights

```kql
// Find request operations whose average duration exceeds 500 ms
// and that have an unusually high child DB dependency count per operation
let slowThreshold = 500ms;

// Step 1: find slow HTTP operations
let slowOps = requests
| where timestamp > ago(1h)
| where duration > slowThreshold
| where success == true
| project operation_Id, name, duration, timestamp;

// Step 2: count DB dependencies per operation
let dbCounts = dependencies
| where type == "SQL" or type == "sqlite"
| summarize db_call_count = count() by operation_Id;

// Step 3: join — operations with high DB-call counts are likely N+1
slowOps
| join kind=inner dbCounts on operation_Id
| where db_call_count >= 3                  // adjust threshold for page size
| project timestamp, name, duration, db_call_count
| order by db_call_count desc, duration desc

// Alternative: just spot the fanned-out DB spans by operation
dependencies
| where type == "SQL" or type == "sqlite"
| where timestamp > ago(1h)
| summarize call_count = count(), avg_dur_ms = avg(duration)
    by operation_Id, target
| where call_count > 3
| order by call_count desc
```

---

## What Did You Learn This Session?

The thing that clicked: **traces don't just tell you a request was slow — they tell you *why***.
Looking at the response time alone (717 ms vs 295 ms) would have sent me chasing indexes. The
OTel console exporter made the N+1 undeniable: six identical `FROM "Users" WHERE Id = ?` spans
sitting under the same parent `GET /api/quotes/` server span. No guessing needed.

EF Core's `AsNoTracking()` was also a lesson — without it, `FindAsync` silently returns the
cached entity on repeated calls for the same primary key, masking the N+1. Real N+1s in production
often involve many different FK values, so caching doesn't help there.

---

## What Would Break This?

1. **High page size** — if `size=100`, the N+1 fires 100 extra queries; with a real database
   (SQL Server + network latency) at 5 ms each, that's 500 ms of pure overhead per request.

2. **Distributed caching hiding the symptom** — if a Redis layer sits in front, the first
   request warms the cache and subsequent ones look fast, masking the N+1 entirely in APM
   dashboards that only sample 1 % of traces.

3. **OTel sampling set to < 100%** — if the sampler drops DB child spans, the N+1 pattern
   becomes invisible in Jaeger/App Insights. Always sample at 100 % in dev; use head-based
   sampling in prod with a `always_on` rule for error/slow requests.

4. **EF change-tracker caching** — if all quotes share the same `OwnerId`, `FindAsync` returns
   the cached `User` after the first hit, so only 2 DB spans appear instead of N+1. This can make
   a broken query look fine on a homogeneous dataset but blow up in production with diverse data.
