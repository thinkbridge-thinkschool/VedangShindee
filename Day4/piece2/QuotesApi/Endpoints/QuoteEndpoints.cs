using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
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
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var errors = validator.Validate(request);
            if (errors.Count > 0)
                return Results.ValidationProblem(errors);

            var sub = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var ownerId = sub is not null && int.TryParse(sub, out var id) ? (int?)id : null;

            var quote = new Quote { Author = request.Author, Text = request.Text, OwnerId = ownerId };
            var created = await repository.CreateAsync(quote, ct);
            return Results.Created($"/api/quotes/{created.Id}", created);
        }).RequireAuthorization("can-edit-quotes");

        group.MapDelete("/{id:int}", async (
            int id,
            IQuoteRepository repository,
            IAuthorizationService authService,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var quote = await repository.GetByIdAsync(id, ct);
            if (quote is null)
                return Results.NotFound();

            var result = await authService.AuthorizeAsync(user, quote, "can-delete-own-quote");
            if (!result.Succeeded)
                return Results.Forbid();

            await repository.DeleteAsync(id, ct);
            return Results.NoContent();
        }).RequireAuthorization();

        return app;
    }
}
