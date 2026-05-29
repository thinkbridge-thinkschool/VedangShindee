# Day 11 – Performance Profiling

## Baseline p50 / p99

Load test: **k6, 10 VUs, 30 seconds** against `GET /api/author-report`

| Metric | Run 1 (cold start) | Run 2 (warm) |
|--------|-------------------|--------------|
| **p50** | 4.99 s | **3.35 s** |
| **p99** | 6.87 s | **7.03 s** |

![k6 Load Test Output](Test%20Load%20Output.png)

---

## Offending SQL

Each HTTP request fires **21 queries** (N+1 pattern).

**Query 1 — fired once per request**
```sql
SELECT DISTINCT [q].[Author]
FROM [Quotes] AS [q]
```

**Query 2 — fired once per author (×20 = 20 round-trips)**
```sql
SELECT [q].[Id], [q].[Author], [q].[CreatedAt], [q].[OwnerId], [q].[Text]
FROM [Quotes] AS [q]
WHERE [q].[Author] = @author
```

Total per request: **1 + 20 = 21 SQL round-trips**

---

## Execution Plan

Query run in SSMS with **Ctrl+M** (Include Actual Execution Plan):

```sql
USE QuotesDb;
SELECT [q].[Id], [q].[Author], [q].[CreatedAt], [q].[OwnerId], [q].[Text]
FROM [Quotes] AS [q]
WHERE [q].[Author] = N'Seneca'
```

**Result:**

```
Clustered Index Scan (PK__Quotes__)
  Cost: 100%
  Rows read: 10 of 10 (100%)
```

The plan shows a **Clustered Index Scan** at 100% cost — SQL Server reads every row
in the table to find matching authors because there is no index on the `Author` column.
A healthy plan would show an **Index Seek** at near 0% cost.

![Execution Plan](Execution%20Plan.png)

---

## Two Biggest Problems

### Problem 1 — N+1 Query Pattern

The `/api/author-report` endpoint first fetches all distinct authors in one query,
then fires a **separate query for every author** inside a `foreach` loop:

```csharp
// Query 1
var authors = await db.Quotes.Select(q => q.Author).Distinct().ToListAsync(ct);

// N queries — one per author
foreach (var author in authors)
{
    var quotes = await db.Quotes.Where(q => q.Author == author).ToListAsync(ct);
}
```

With 20 distinct authors this is **21 queries per request**. Under 10 concurrent users
that is up to 210 queries in-flight at once. The number grows linearly with the number
of authors — add 10 more authors and you get 31 queries per request.

![N+1 Repeated Query](N%2B1%20repeated%20query.png)

---

### Problem 2 — Missing Index on `Quotes.Author`

There is no index on the `Author` column. Every per-author `WHERE Author = @p` query
performs a **full table scan** — all rows are read and discarded except the matching ones.

The execution plan confirms this: **Clustered Index Scan, Cost 100%**.

With 200 rows and 20 authors, each request causes **20 full table scans**.
As the table grows the cost grows proportionally — at 10,000 rows each scan
reads 10,000 rows just to return ~500 matching ones.

**Fix (not applied — exercise baseline only):**
```sql
CREATE INDEX IX_Quotes_Author ON Quotes(Author);
```

---

## IO Statistics Proof (`SET STATISTICS IO ON`)

Running the actual queries with `SET STATISTICS IO ON` in SSMS shows exactly
how many pages SQL Server had to read for each query:

```sql
SET STATISTICS IO ON;

SELECT DISTINCT Author FROM Quotes;
SELECT Id, Text, CreatedAt FROM Quotes WHERE Author = 'Seneca';
SELECT Id, Text, CreatedAt FROM Quotes WHERE Author = 'Marcus Aurelius';
```

![SSMS IO Statistics](SSMS%20IO%20Stats%20%E2%80%94%20Scan%20count%201%20repeated%20per%20author%20%3D%20N%2B1%20full%20table%20scan.png)

### What the numbers mean

| Query | Scan count | Logical reads |
|-------|-----------|---------------|
| `SELECT DISTINCT Author` | 1 | 5 |
| `WHERE Author = 'Seneca'` | 1 | 2 |
| `WHERE Author = 'Marcus Aurelius'` | 1 | 3 |

**Scan count** is the number of times SQL Server scanned the table.
A value of `1` on every query means every single query did a **full table scan**
instead of an index seek. With 20 authors this happens 20 times per request.

**Logical reads** is the number of data pages read from the buffer pool (memory).
Even though the numbers look small here (200 rows is a tiny table), the pattern
is what matters — every `WHERE Author = ?` reads the **entire table** regardless
of how many matching rows exist. At 100,000 rows, logical reads would be
in the hundreds per query, multiplied by 20 authors = thousands of page reads
per single HTTP request.

### Why this matters

Without an index on `Author`, SQL Server has no shortcut to find rows by author.
It must load every page of the `Quotes` table into memory and check each row one
by one. This is called a **Table Scan** (or Clustered Index Scan when a clustered
index exists). Adding an index like:

```sql
CREATE INDEX IX_Quotes_Author ON Quotes(Author);
```

would drop `Scan count` to `0` and `logical reads` to `1-2` per author query,
because SQL Server could jump directly to the matching rows via the index B-tree.
