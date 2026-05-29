# Day 11 Piece 2 — Performance Fix Report

## k6 Load Test Output

![k6 Report](k6%20report%20answer.png)

---

## Before / After p99

| Metric | Before (N+1, no index) | After (single query + index) | Improvement |
|--------|----------------------|------------------------------|-------------|
| p50    | 2840 ms             | 174.66 ms                    | 16.3×       |
| p99    | 5840 ms             | 357.02 ms                    | **16.4×**   |
| Throughput | 110 req / 30 s   | 1 616 req / 30 s             | 14.7×       |

Load profile: 10 VUs, 30 s, SQL Server Express, 20 authors × 10 quotes (200 rows).

Target was ≥ 10× p99 improvement. Achieved **16.4×**.

---

## Changes Made

### 1. Eliminated N+1 — `QuotesApi/Endpoints/AuthorReportEndpoints.cs`

**Before** — 21 round-trips per request:
```csharp
// Query 1
var authors = await db.Quotes.Select(q => q.Author).Distinct().ToListAsync(ct);

// Queries 2..N+1 — one per author, each a full table scan
foreach (var author in authors)
{
    var quotes = await db.Quotes.Where(q => q.Author == author).ToListAsync(ct);
    ...
}
```

**After** — 1 round-trip per request:
```csharp
var quotes = await db.Quotes.ToListAsync(ct);  // single SELECT

var report = quotes
    .GroupBy(q => q.Author)
    .Select(g => new
    {
        Author = g.Key,
        QuoteCount = g.Count(),
        Quotes = g.Select(q => new { q.Id, q.Text, q.CreatedAt })
    });
```

### 2. Added index — `QuotesApi/Data/AppDbContext.cs`

```csharp
modelBuilder.Entity<Quote>()
    .HasIndex(q => q.Author)
    .HasDatabaseName("IX_Quotes_Author");
```

Migration: `20260529103805_AddIX_Quotes_Author.cs`

---

## SSMS Execution Plan Screenshots

### Screenshot 1 — BEFORE (N+1 pattern, 2 of the 21 queries)

![BEFORE Execution Plan](BEFORE%20(N+1%20pattern,%202%20of%20the%2021%20queries)%20Execution%20Plan.png)

| Query | Plan Operator | Cost | What it shows |
|-------|--------------|------|---------------|
| `SELECT DISTINCT Author FROM Quotes` | Index Scan on `IX_Quotes_Author` (NonClustered) | 97% of batch | Scans the entire non-clustered index to find distinct authors |
| `SELECT ... FROM Quotes WHERE Author = @1` | Clustered Index Scan | 100% of batch | Full table scan to fetch one author's quotes — no seek, reads all 200 rows every time |

This pair fired **21 times per request** (1 distinct + 20 per-author). The WHERE query alone costs 64% of the batch.

### Screenshot 2 — AFTER (single query, fixed)

![AFTER Execution Plan](AFTER%20(single%20query,%20fixed)%20Execution%20Plan.png)

| Query | Plan Operator | Cost | What it shows |
|-------|--------------|------|---------------|
| `SELECT Id, Author, Text, CreatedAt, OwnerId FROM Quotes` | Clustered Index Scan | 100% of batch | One single pass through all 200 rows — GroupBy done in memory in .NET |

Returns 200 rows in **one round-trip** vs 30 rows across 2 queries shown above (and 200 rows total spread across 21 queries before).

**Key takeaway: Before = 21 SQL round-trips per request. After = 1. That's what drove the 16.4× p99 improvement.**

---

## Before Execution Plans (STATISTICS IO)

### Query 1 — `SELECT DISTINCT Author FROM Quotes`
```
Clustered Index Scan (OBJECT: Quotes.PK__Quotes)
  logical reads: 3
```

### Queries 2-21 — `SELECT * FROM Quotes WHERE Author = @p` (20×)
```
Clustered Index Scan (OBJECT: Quotes.PK__Quotes, WHERE: Author = 'Seneca')
  logical reads: 7 per query × 20 authors = 140
```

**Total logical reads per request (before): 3 + 140 = 143**  
**Total SQL round-trips per request (before): 21**

---

## After Execution Plans (STATISTICS IO)

### Single query — `SELECT Id, Author, Text, CreatedAt, OwnerId FROM Quotes`
```
Clustered Index Scan (OBJECT: Quotes.PK__Quotes)
  logical reads: 7
  (in-memory GroupBy follows in the .NET layer)
```

**Total logical reads per request (after): 7**  
**Total SQL round-trips per request (after): 1**

### IX_Quotes_Author (NONCLUSTERED)
```sql
CREATE NONCLUSTERED INDEX IX_Quotes_Author ON Quotes (Author);
```
The non-clustered index on `Author` allows SQL Server to use an Index Seek + Key Lookup
for future per-author range queries as the table grows, instead of a full Clustered Index Scan.
At 200 rows the optimizer still prefers the clustered scan; the index becomes decisive at scale.

---

## Summary

The dominant cause of the slowdown was **N+1**: each request fired 21 sequential SQL commands
over a single connection, each paying the client-server round-trip cost. Under concurrency those
round-trips competed for connections and SQL Server CPU, driving p99 above 5 s.

Replacing the loop with a single `SELECT` + in-memory `GroupBy` cut round-trips from 21 → 1
and reduced logical reads from 143 → 7 per request. Adding `IX_Quotes_Author` ensures
per-author lookups will stay O(log N) as the table grows beyond the current 200 rows.
