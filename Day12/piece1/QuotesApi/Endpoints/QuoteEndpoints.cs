using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using QuotesApi.Commands;
using QuotesApi.Models;
using QuotesApi.Queries;
using QuotesApi.Repositories;
using QuotesApi.Telemetry;

namespace QuotesApi.Endpoints;

public static class QuoteEndpoints
{
    public static IEndpointRouteBuilder MapQuoteEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/quotes");

        group.MapGet("/", async (int page, int size, ListQuotesHandler handler, ILoggerFactory loggerFactory, CancellationToken ct) =>
        {
            var logger = loggerFactory.CreateLogger("QuotesApi.Quotes");
            logger.LogInformation("Listing quotes {Page} page with size {Size}", page, size);
            var quotes = await handler.HandleAsync(new ListQuotesQuery(page, size), ct);
            return Results.Ok(quotes);
        });

        group.MapGet("/{id:int}", async (int id, GetQuoteByIdHandler handler, ILoggerFactory loggerFactory, CancellationToken ct) =>
        {
            var logger = loggerFactory.CreateLogger("QuotesApi.Quotes");
            var quote = await handler.HandleAsync(new GetQuoteByIdQuery(id), ct);
            if (quote is null)
            {
                logger.LogWarning("Quote {QuoteId} not found", id);
                return Results.NotFound();
            }
            logger.LogInformation("Retrieved quote {QuoteId}", id);
            return Results.Ok(quote);
        });

        group.MapPost("/", async (
            CreateQuoteRequest request,
            CreateQuoteHandler handler,
            ClaimsPrincipal user,
            ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            using var activity = QuoteActivitySource.Instance.StartActivity("validate-and-create-quote");

            var sub = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var ownerId = sub is not null && int.TryParse(sub, out var id) ? (int?)id : null;

            activity?.SetTag("quote.author", request.Author);
            activity?.SetTag("user.id", ownerId?.ToString() ?? "anonymous");

            var (result, errors) = await handler.HandleAsync(new CreateQuoteCommand(request.Author, request.Text, ownerId), ct);

            if (errors is not null)
            {
                activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error, "Validation failed");
                return Results.ValidationProblem(errors);
            }

            activity?.SetTag("quote.id", result?.Id);

            var logger = loggerFactory.CreateLogger("QuotesApi.Quotes");
            logger.LogInformation("Created quote {QuoteId} for user {UserId}", result!.Id, ownerId);

            return Results.Created($"/api/quotes/{result.Id}", result);
        }).RequireAuthorization("can-edit-quotes");

        // Delete still goes through IQuoteRepository directly because the authorization handler
        // needs the full Quote entity (OwnerId) — the write store owns that fact.
        group.MapDelete("/{id:int}", async (
            int id,
            IQuoteRepository repository,
            IAuthorizationService authService,
            ClaimsPrincipal user,
            ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            var logger = loggerFactory.CreateLogger("QuotesApi.Quotes");
            var quote = await repository.GetByIdAsync(id, ct);
            if (quote is null)
                return Results.NotFound();

            var result = await authService.AuthorizeAsync(user, quote, "can-delete-own-quote");
            if (!result.Succeeded)
            {
                logger.LogWarning("User {UserId} forbidden from deleting quote {QuoteId}", user.FindFirst(ClaimTypes.NameIdentifier)?.Value, id);
                return Results.Forbid();
            }

            await repository.DeleteAsync(id, ct);
            logger.LogInformation("Deleted quote {QuoteId} by user {UserId}", id, user.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            return Results.NoContent();
        }).RequireAuthorization();

        return app;
    }
}
