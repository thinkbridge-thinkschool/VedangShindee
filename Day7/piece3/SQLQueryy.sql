USE QuotesDb;
GO

-- ============================================================
-- Day 7 / Piece 3 — Set Operations
-- Schema: Authors, Quotes (Category), Tags, QuoteTags
-- Run setup.sql first to create tables and seed data.
-- ============================================================


-- ------------------------------------------------------------
-- Q1: Authors with quotes but NO tags
-- Operator : EXCEPT  (set difference — "A minus B")
-- Why      : Build the "all quoting authors" set, then remove
--            any author whose quote appears in QuoteTags.
--            EXCEPT makes this a one-step subtraction without
--            LEFT JOIN / IS NULL gymnastics.
-- ------------------------------------------------------------
SELECT DISTINCT a.AuthorId, a.Name
FROM Authors a
INNER JOIN Quotes q ON q.AuthorId = a.AuthorId

EXCEPT

SELECT DISTINCT a.AuthorId, a.Name
FROM Authors a
INNER JOIN Quotes q     ON q.AuthorId = a.AuthorId
INNER JOIN QuoteTags qt ON qt.QuoteId = q.QuoteId

ORDER BY Name;

/*
RESULT (2 rows)
┌──────────┬─────────────────────┐
│ AuthorId │ Name                │
├──────────┼─────────────────────┤
│ 5        │ Friedrich Nietzsche │
│ 4        │ Maya Angelou        │
└──────────┴─────────────────────┘
Both authors have quotes, but none of their quotes have been
tagged in the QuoteTags table.
*/


-- ------------------------------------------------------------
-- Q2: Authors in BOTH 'classic' AND 'modern'
-- Operator : INTERSECT  (common rows in both result sets)
-- Why      : INTERSECT returns only the rows that appear in
--            the LEFT result set AND the RIGHT result set.
--            A single-query WHERE + GROUP BY would work too,
--            but INTERSECT expresses the business intent
--            ("in set A and also in set B") more directly.
-- ------------------------------------------------------------
SELECT DISTINCT a.AuthorId, a.Name
FROM Authors a
INNER JOIN Quotes q ON q.AuthorId = a.AuthorId
WHERE q.Category = 'classic'

INTERSECT

SELECT DISTINCT a.AuthorId, a.Name
FROM Authors a
INNER JOIN Quotes q ON q.AuthorId = a.AuthorId
WHERE q.Category = 'modern'

ORDER BY Name;

/*
RESULT (2 rows)
┌──────────┬─────────────────┐
│ AuthorId │ Name            │
├──────────┼─────────────────┤
│ 1        │ Marcus Aurelius │
│ 2        │ Seneca          │
└──────────┴─────────────────┘
These two authors each have at least one quote in 'classic'
AND at least one quote in 'modern'.
*/


-- ------------------------------------------------------------
-- Q3: Combined distinct tag list across 'classic' + 'modern'
-- Operator : UNION  (merge + deduplicate)
-- Why      : UNION (without ALL) automatically removes
--            duplicate tag names that appear in both
--            categories — e.g., 'stoicism' is used by both
--            classic and modern quotes but appears once.
--            UNION ALL would double-count shared tags.
-- ------------------------------------------------------------
SELECT DISTINCT t.TagName
FROM Tags t
INNER JOIN QuoteTags qt ON qt.TagId  = t.TagId
INNER JOIN Quotes q     ON q.QuoteId = qt.QuoteId
WHERE q.Category = 'classic'

UNION

SELECT DISTINCT t.TagName
FROM Tags t
INNER JOIN QuoteTags qt ON qt.TagId  = t.TagId
INNER JOIN Quotes q     ON q.QuoteId = qt.QuoteId
WHERE q.Category = 'modern'

ORDER BY TagName;

/*
RESULT (4 rows)
┌────────────┐
│ TagName    │
├────────────┤
│ creativity │   ← modern only
│ resilience │   ← classic only
│ stoicism   │   ← classic AND modern (deduplicated by UNION)
│ wisdom     │   ← classic AND modern (deduplicated by UNION)
└────────────┘
4 distinct tags span the two categories; without deduplication
there would be 6 rows (stoicism and wisdom each counted twice).
*/


-- ============================================================
-- Mentor Notes
-- ============================================================
--
-- What clicked this session
-- ─────────────────────────
-- The mental model that flipped everything: set operators work
-- on *result sets*, not on tables directly.  EXCEPT/INTERSECT
-- ask "is this whole row present in the other result set?" —
-- so the SELECT lists on both sides must be column-compatible,
-- and you get implicit DISTINCT for free.
--
-- What would break this
-- ──────────────────────
-- 1. Partial tagging: an author who has *some* tagged quotes
--    and some untagged quotes would NOT appear in Q1, because
--    the right-hand side of EXCEPT only needs one match to
--    eliminate the author from the result.
--
-- 2. Case sensitivity: if Category is stored as 'Classic' vs
--    'classic' (mixed case), the WHERE filters in Q2 and Q3
--    silently miss rows on case-sensitive collations.
--
-- 3. UNION vs UNION ALL: swapping UNION for UNION ALL in Q3
--    returns 6 rows instead of 4 — a hard-to-spot bug when
--    downstream code counts or aggregates the tag list.
-- ============================================================
