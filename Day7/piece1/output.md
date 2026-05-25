# Day 7 · Piece 1 — Joins and CTEs

## SQL Query

USE QuotesDb;
GO

WITH QuoteCounts AS (
    SELECT 
        AuthorId,
        COUNT(*) AS QuoteCount
    FROM Quotes
    GROUP BY AuthorId
),
RankedQuotes AS (
    SELECT 
        AuthorId,
        QuoteText,
        CreatedAt,
        ROW_NUMBER() OVER (PARTITION BY AuthorId ORDER BY CreatedAt DESC) AS rn
    FROM Quotes
)
SELECT TOP 10
    a.Name           AS AuthorName,
    qc.QuoteCount,
    rq.QuoteText     AS MostRecentQuote,
    rq.CreatedAt     AS MostRecentDate
FROM Authors a
INNER JOIN QuoteCounts  qc ON qc.AuthorId = a.AuthorId
INNER JOIN RankedQuotes rq ON rq.AuthorId = a.AuthorId AND rq.rn = 1
ORDER BY a.Name;


## Result Set (10 rows, ordered by AuthorName)

| AuthorName          | QuoteCount | MostRecentQuote                                                                                                    | MostRecentDate              |
|---------------------|------------|--------------------------------------------------------------------------------------------------------------------|-----------------------------|
| Albert Einstein     | 2          | Try not to become a man of success, but rather try to become a man of value.                                       | 2024-07-30 00:00:00.0000000 |
| Emily Dickinson     | 2          | If I can stop one heart from breaking, I shall not live in vain.                                                   | 2024-10-21 00:00:00.0000000 |
| Ernest Hemingway    | 2          | The world breaks everyone, and afterward, some are strong at the broken places.                                    | 2024-09-05 00:00:00.0000000 |
| Franz Kafka         | 1          | A book must be the axe for the frozen sea within us.                                                               | 2024-06-08 00:00:00.0000000 |
| Friedrich Nietzsche | 3          | Without music, life would be a mistake.                                                                            | 2024-11-08 00:00:00.0000000 |
| George Orwell       | 4          | Who controls the past controls the future. Who controls the present controls the past.                             | 2024-11-22 00:00:00.0000000 |
| Jane Austen         | 2          | The person, be it gentleman or lady, who has not pleasure in a good novel, must be intolerably stupid.             | 2024-09-17 00:00:00.0000000 |
| Leo Tolstoy         | 3          | The two most powerful warriors are patience and time.                                                              | 2024-12-12 00:00:00.0000000 |
| Mark Twain          | 3          | The two most important days in your life are the day you are born and the day you find out why.                    | 2024-09-10 00:00:00.0000000 |
| Maya Angelou        | 2          | You will face many defeats in life, but never let yourself be defeated.                                            | 2024-08-22 00:00:00.0000000 |

![alt text](<Screenshot (263).png>)

## Why a CTE over a correlated subquery?

the CTE computes counts and rankings once as a set, while a correlated subquery in SELECT re-executes per outer row — set-based scales linearly with row count, row-by-row scales quadratically.

---

## Notes for mentor

**What I learned:**  
Using ROW_NUMBER() with PARTITION BY is a simple way to get the latest record from each group. Putting this logic inside a CTE makes the query easier to read and maintain because the filtering happens before the final join.

**What would break this:**  
If two quotes from the same author have exactly the same CreatedAt time, SQL may choose either one as the "latest" quote. This can cause different results on different runs. Adding another field such as Id to the ORDER BY clause ensures the result is always consistent.
