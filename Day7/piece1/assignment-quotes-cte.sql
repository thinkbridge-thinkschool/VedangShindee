USE QuotesDb;
GO

-- ============================================================
-- Assignment: One statement returning each author with
-- their quote count and most-recent quote, using CTEs
-- (no correlated subquery in the SELECT clause).
--
-- Schema: Quotes(Id, Author NVARCHAR(100), Text NVARCHAR(1000),
--               CreatedAt DATETIMEOFFSET, IsDeleted BIT, OwnerId INT)
-- Author is a denormalised string column — no separate Authors table.
-- ============================================================

WITH QuoteCounts AS (
    -- CTE 1: total non-deleted quotes per author
    SELECT
        Author,
        COUNT(*)  AS QuoteCount
    FROM  Quotes
    WHERE IsDeleted = 0
    GROUP BY Author
),
RankedQuotes AS (
    -- CTE 2: rank each author's quotes newest-first
    SELECT
        Author,
        Text      AS QuoteText,
        CreatedAt,
        ROW_NUMBER() OVER (PARTITION BY Author ORDER BY CreatedAt DESC) AS rn
    FROM  Quotes
    WHERE IsDeleted = 0
)
SELECT
    qc.Author                            AS AuthorName,
    qc.QuoteCount,
    rq.QuoteText                         AS MostRecentQuote,
    CAST(rq.CreatedAt AS DATE)           AS MostRecentDate
FROM       QuoteCounts  qc
INNER JOIN RankedQuotes rq
       ON  rq.Author = qc.Author
       AND rq.rn = 1
ORDER BY   qc.Author;
