using QuotesApi.Models;
using QuotesApi.Repositories;
using QuotesApi.Services;

namespace QuotesApi.Endpoints;

public static class QuoteEndpoints
{
    public static IEndpointRouteBuilder MapQuoteEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/quotes");

        group.MapGet("/", async (int page, int size, IQuoteRepository repository, CancellationToken ct) =>
        {
            var quotes = await repository.GetPagedAsync(page, size, ct);
            return Results.Ok(quotes);
        });

        group.MapGet("/{id:int}", async (int id, IQuoteRepository repository, CancellationToken ct) =>
        {
            var quote = await repository.GetByIdAsync(id, ct);
            return quote is null ? Results.NotFound() : Results.Ok(quote);
        });

        group.MapPost("/", async (
            CreateQuoteRequest request,
            IQuoteRepository repository,
            IQuoteValidator validator,
            CancellationToken ct) =>
        {
            var errors = validator.Validate(request);
            if (errors.Count > 0)
                return Results.ValidationProblem(errors);

            var quote = new Quote { Author = request.Author, Text = request.Text };
            var created = await repository.CreateAsync(quote, ct);
            return Results.Created($"/api/quotes/{created.Id}", created);
        }).RequireAuthorization();

        group.MapDelete("/{id:int}", async (int id, IQuoteRepository repository, CancellationToken ct) =>
        {
            var deleted = await repository.DeleteAsync(id, ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        }).RequireAuthorization();

        return app;
    }
}
