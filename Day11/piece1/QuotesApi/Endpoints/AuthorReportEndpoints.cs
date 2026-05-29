using Microsoft.EntityFrameworkCore;
using QuotesApi.Data;

namespace QuotesApi.Endpoints;

public static class AuthorReportEndpoints
{
    public static IEndpointRouteBuilder MapAuthorReportEndpoints(this IEndpointRouteBuilder app)
    {
        // Deliberately slow: N+1 queries + no index on Author column.
        // Query 1 fetches distinct authors; then one SELECT per author (full table scan each time).
        app.MapGet("/api/author-report", async (AppDbContext db, CancellationToken ct) =>
        {
            var authors = await db.Quotes
                .Select(q => q.Author)
                .Distinct()
                .ToListAsync(ct);

            var report = new List<object>();
            foreach (var author in authors)
            {
                // Each iteration fires: SELECT * FROM Quotes WHERE Author = @p0
                // No index on Author → full table scan on every iteration.
                var quotes = await db.Quotes
                    .Where(q => q.Author == author)
                    .ToListAsync(ct);

                report.Add(new
                {
                    Author = author,
                    QuoteCount = quotes.Count,
                    Quotes = quotes.Select(q => new { q.Id, q.Text, q.CreatedAt })
                });
            }

            return Results.Ok(report);
        });

        return app;
    }
}
