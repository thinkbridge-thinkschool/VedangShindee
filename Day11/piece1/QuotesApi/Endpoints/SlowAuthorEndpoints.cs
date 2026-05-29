using Microsoft.EntityFrameworkCore;
using QuotesApi.Data;

namespace QuotesApi.Endpoints;

public static class SlowAuthorEndpoints
{
    public static IEndpointRouteBuilder MapSlowAuthorEndpoints(this IEndpointRouteBuilder app)
    {
        // Deliberately slow: 1 query to get all distinct authors, then N separate queries
        // for each author's quotes. Author column has no index → each inner query is a full
        // table scan. This is the classic N+1 problem.
        app.MapGet("/api/author-report", async (AppDbContext db, ILoggerFactory loggerFactory, CancellationToken ct) =>
        {
            var logger = loggerFactory.CreateLogger("QuotesApi.SlowReport");

            // Query 1: distinct authors — 1 round-trip
            var authors = await db.Quotes
                .Select(q => q.Author)
                .Distinct()
                .ToListAsync(ct);

            logger.LogInformation("Author report: found {AuthorCount} distinct authors", authors.Count);

            // Query 2..N+1: one extra round-trip per author, full table scan each time
            // (no index on Quotes.Author)
            var report = new List<object>(authors.Count);
            foreach (var author in authors)
            {
                var quotes = await db.Quotes
                    .Where(q => q.Author == author)
                    .Select(q => new { q.Id, q.Text, q.CreatedAt })
                    .ToListAsync(ct);

                logger.LogInformation("Fetched {QuoteCount} quotes for author {AuthorName}", quotes.Count, author);

                report.Add(new { Author = author, QuoteCount = quotes.Count, Quotes = quotes });
            }

            return Results.Ok(report);
        });

        return app;
    }
}
