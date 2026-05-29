using Microsoft.EntityFrameworkCore;
using QuotesApi.Data;

namespace QuotesApi.Endpoints;

public static class AuthorReportEndpoints
{
    public static IEndpointRouteBuilder MapAuthorReportEndpoints(this IEndpointRouteBuilder app)
    {
        // Single query: fetch all quotes in one round-trip, group in memory.
        // IX_Quotes_Author lets SQL Server satisfy ORDER BY / range scans if added later.
        app.MapGet("/api/author-report", async (AppDbContext db, CancellationToken ct) =>
        {
            var quotes = await db.Quotes.ToListAsync(ct);

            var report = quotes
                .GroupBy(q => q.Author)
                .Select(g => new
                {
                    Author = g.Key,
                    QuoteCount = g.Count(),
                    Quotes = g.Select(q => new { q.Id, q.Text, q.CreatedAt })
                });

            return Results.Ok(report);
        });

        return app;
    }
}
