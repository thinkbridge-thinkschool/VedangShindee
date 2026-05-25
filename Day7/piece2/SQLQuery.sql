USE QuotesDb;
GO

SELECT
    a.Name        AS AuthorName,
    q.QuoteText,
    q.CreatedAt,

    SUM(1) OVER (
        PARTITION BY q.AuthorId
        ORDER BY     q.CreatedAt, q.QuoteId
        ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
    ) AS RunningCount,

    ROW_NUMBER() OVER (
        PARTITION BY q.AuthorId
        ORDER BY     q.CreatedAt, q.QuoteId
    ) AS QuoteRowNum,

    RANK() OVER (
        PARTITION BY q.AuthorId
        ORDER BY     q.CreatedAt
    ) AS QuoteRank,

    LAG(q.CreatedAt) OVER (
        PARTITION BY q.AuthorId
        ORDER BY     q.CreatedAt, q.QuoteId
    ) AS PreviousQuoteDate,

    DATEDIFF(
        DAY,
        LAG(q.CreatedAt) OVER (
            PARTITION BY q.AuthorId
            ORDER BY     q.CreatedAt, q.QuoteId
        ),
        q.CreatedAt
    ) AS DaysSincePrevious

FROM       Authors a
INNER JOIN Quotes  q ON q.AuthorId = a.AuthorId
ORDER BY   a.Name, q.CreatedAt;