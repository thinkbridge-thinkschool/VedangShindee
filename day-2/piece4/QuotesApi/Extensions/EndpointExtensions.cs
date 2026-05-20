using QuotesApi.Data;
using QuotesApi.Models;

namespace QuotesApi.Extensions;

public static class EndpointExtensions
{
    public static void MapQuoteEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/quotes");

        group.MapGet("/", async (int? page, int? size, IQuoteRepository repo, CancellationToken ct) =>
        {
            var p = page ?? 1;
            var s = size ?? 10;
            var (quotes, total) = await repo.GetPaginatedAsync(p, s, ct);
            return Results.Ok(new { Data = quotes, Total = total, Page = p, Size = s });
        });

        group.MapGet("/{id:int}", async (int id, IQuoteRepository repo, CancellationToken ct) =>
        {
            var quote = await repo.GetByIdAsync(id, ct);
            return quote is not null ? Results.Ok(quote) : Results.NotFound();
        });

        group.MapPost("/", async (CreateQuoteRequest request, IQuoteRepository repo, CancellationToken ct) =>
        {
            var (quote, error) = Quote.Create(request.Author, request.Text);
            if (error is not null)
                return Results.Problem(error.Message, statusCode: 400);

            var created = await repo.AddAsync(quote!, ct);
            return Results.Created($"/api/quotes/{created.Id}", created);
        });

        group.MapDelete("/{id:int}", async (int id, IQuoteRepository repo, CancellationToken ct) =>
        {
            var success = await repo.DeleteAsync(id, ct);
            return success ? Results.NoContent() : Results.NotFound();
        });
    }
}
