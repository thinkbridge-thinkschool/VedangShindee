# Day 11 – Piece 1: Performance Profiling

## What Was Built

A deliberately slow endpoint (`GET /api/author-report`) was added to the existing Quotes API.
It exhibits two classic performance anti-patterns:

1. **N+1 query**: fetches all distinct authors in one query, then issues a separate `SELECT` per author to get their quotes.
2. **Missing index**: the `Quotes.Author` column has no index, so every per-author lookup is a full table scan.

The database was seeded with **95 rows across 19 authors** (5 quotes per author) to make the pathology measurable under load.
The API was switched from SQLite to **SQL Server Express** so execution plans could be analysed in SSMS.

---

## Baseline p50 / p99

Load test: `k6` · 20 req/s constant arrival rate · 30 s · `http://localhost:5051/api/author-report` · **SQL Server Express** · N+1 + no index (both problems active)

```
avg=596.81ms  min=21.87ms  med=298.56ms  max=3.95s
p(50)=298.56ms   p(90)=1.74s   p(95)=2.22s   p(99)=3.13s
```

| Metric | Value |
|--------|-------|
| **p50** | **298.56 ms** |
| **p99** | **3130 ms** |
| p90 | 1740 ms |
| p95 | 2220 ms |
| avg | 596.81 ms |
| max | 3950 ms |
| Requests completed | 525 / 600 (76 dropped) |
| Error rate | 0 % |

![k6 Load Test — Slow endpoint p50=298ms p99=3.13s](Test%20Load%20Screenshot.png)

### OpenTelemetry Trace Confirmation — N+1 Proof

The OTel console exporter shows **19 repeated `db.statement` spans** for a single request — each one firing the same SQL with a different `@author` value. The total request took **592ms** for one browser hit.

```
db.statement: SELECT [q].[Id], [q].[Text], [q].[CreatedAt]
FROM [Quotes] AS [q]
WHERE [q].[Author] = @author
← repeated 19 times, one per author

Activity.DisplayName:    GET /api/author-report
Activity.Duration:       00:00:00.5925634   ← 592ms for ONE request
http.response.status_code: 200
```

![N+1 proof — OTel trace: 19 repeated db.statement queries, single request took 592ms](N%2B1%20proof%20%E2%80%94%20OTel%20trace%2019%20repeated%20db.statement%20queries%2C%20single%20request%20took%20592ms.png)

---

## Offending SQL

EF Core emits exactly **20 queries per request** (captured via `Microsoft.EntityFrameworkCore.Database.Command` at `Information` level):

**Query 1 — 1 × `SELECT DISTINCT` (the "1" side of N+1)**
```sql
SELECT DISTINCT [q].[Author]
FROM [Quotes] AS [q]
```

**Queries 2–20 — 19 × per-author lookup (the "N" side, one per distinct author)**
```sql
SELECT [q].[Id], [q].[Text], [q].[CreatedAt]
FROM [Quotes] AS [q]
WHERE [q].[Author] = @author
```

`@author` is bound once per iteration of the C# `foreach` loop in `SlowAuthorEndpoints.cs`.
Total DB round-trips per HTTP request: **20**.

---

## IO Statistics — SSMS (`SET STATISTICS IO ON`)

Captured from SSMS Messages tab for 3 queries (DISTINCT + 2 per-author):

```
(19 rows affected)
Table 'Quotes'. Scan count 1, logical reads 5    ← SELECT DISTINCT — reads all 95 rows

(5 rows affected)
Table 'Quotes'. Scan count 1, logical reads 2    ← WHERE Author = 'Seneca'

(5 rows affected)
Table 'Quotes'. Scan count 1, logical reads 3    ← WHERE Author = 'Marcus Aurelius'
```

Three queries = three separate `Scan count 1` entries = three independent full table reads.
In a real HTTP request all 19 authors fire — **19 separate scans of the Quotes table per request**.

![SSMS IO Stats — 3× Scan count 1 proving N+1 full table scan per author](SSMS%20IO%20Stats%20%E2%80%94%20Scan%20count%201%20repeated%20per%20author%20=%20N+1%20full%20table%20scan.png)

---

## Execution Plan — SSMS (No Index)

Captured in SQL Server Management Studio with **Ctrl+M (Include Actual Execution Plan)**.

**Query – `SELECT Id, Text, CreatedAt FROM Quotes WHERE Author = 'Seneca'`**
```
Query cost (relative to the batch): 100%
SELECT [Id],[Text],[CreatedAt] FROM [Quotes] WHERE [Author]=@1

Clustered Index Scan [Quotes].[PK_Quotes]
Cost: 100%
0.000s
5 of 5 (100%)
```
- **Clustered Index Scan** = SQL Server reads every row in the table to find matching rows.
- No index on `Author` → the only option is a full scan of `PK_Quotes`.
- Happens **19 times** per HTTP request — once per distinct author.
- Total row reads per request: **19 × 95 = 1,805**.

![Execution Plan BEFORE index — Clustered Index Scan full table read](Execution%20Plan%20BEFORE%20index.png)

---

## Two Biggest Problems

### 1 · N+1 Query Pattern (20 round-trips → should be 1)

The endpoint fetches all distinct authors (`SELECT DISTINCT Author FROM Quotes`), then loops
over that list in C# and fires a new `SELECT` for each author. With 19 authors that is
**20 database round-trips per HTTP request**. Each round-trip is serial — the next cannot
start until the previous returns. The cost grows linearly with the number of distinct authors.

### 2 · Missing Index on `Quotes.Author` (every lookup is a full table scan)

Every per-author `SELECT` shows a **Clustered Index Scan** — SQL Server reads all 95 rows
to find the 5 that match. At 95 rows this is fast; at 1 million rows each of the 19 inner
queries reads the entire table. The missing index compounds the N+1 by multiplying both
the number of queries *and* the cost per query.

---

## Optional: What I Learned

The main thing I learned is that response times alone do not tell the full story. Seeing a slow p50 or p99 value tells us there is a performance problem, but it does not explain the reason. By checking the SQL logs and execution plan, I could see that the application was making many database queries and that SQL Server was scanning the entire table each time. Looking at the metrics, query count, and execution plan together made it much easier to understand where the time was being spent and why some requests were much slower than others.


## Optional: What Would Break This

As the amount of data grows, performance can get worse very quickly because more queries and more table scans are required. If many users are updating data while these queries are running, database contention can increase response times even further. Another issue is handling NULL values. Queries that compare values using = do not match NULL, which can lead to missing or incorrect results without showing any obvious error.
