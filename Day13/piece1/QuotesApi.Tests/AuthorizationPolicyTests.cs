using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using QuotesApi.Authorization;
using QuotesApi.Models;
using Xunit;

namespace QuotesApi.Tests;

/// <summary>
/// Verifies that authorization policies fail (→ 403) when their conditions are not met,
/// and succeed when they are. Tests use IAuthorizationService directly — no HTTP layer needed.
/// </summary>
public class AuthorizationPolicyTests
{
    // Build an IAuthorizationService wired with the same policies as production.
    private static IAuthorizationService BuildAuthService()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorizationCore(options =>
        {
            // Policy 1 — claim-based: token must carry scope=quotes.write.
            options.AddPolicy("can-edit-quotes", p => p.RequireClaim("scope", "quotes.write"));

            // Policy 2 — custom requirement: caller must own the targeted Quote.
            options.AddPolicy("can-delete-own-quote", p => p.AddRequirements(new OwnQuoteRequirement()));
        });
        services.AddSingleton<IAuthorizationHandler, OwnQuoteHandler>();
        return services.BuildServiceProvider().GetRequiredService<IAuthorizationService>();
    }

    private static ClaimsPrincipal AuthenticatedUser(int id, string? scope = null)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, id.ToString()) };
        if (scope is not null)
            claims.Add(new Claim("scope", scope));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "test"));
    }

    // ── Policy 1: can-edit-quotes (claim-based) ──────────────────────────────

    [Fact]
    public async Task CanEditQuotes_Fails_WhenScopeClaimMissing()
    {
        // A valid, authenticated user whose token has no scope claim.
        // Endpoint returns 403 Forbidden — not 401, because the user IS authenticated.
        var authService = BuildAuthService();
        var user = AuthenticatedUser(id: 1, scope: null);

        var result = await authService.AuthorizeAsync(user, resource: null, "can-edit-quotes");

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task CanEditQuotes_Succeeds_WhenScopeClaimPresent()
    {
        var authService = BuildAuthService();
        var user = AuthenticatedUser(id: 1, scope: "quotes.write");

        var result = await authService.AuthorizeAsync(user, resource: null, "can-edit-quotes");

        Assert.True(result.Succeeded);
    }

    // ── Policy 2: can-delete-own-quote (custom IAuthorizationRequirement) ────

    [Fact]
    public async Task CanDeleteOwnQuote_Fails_WhenQuoteOwnedByDifferentUser()
    {
        // User 1 tries to delete a quote that belongs to User 2 → must fail → 403.
        var authService = BuildAuthService();
        var user = AuthenticatedUser(id: 1);
        var quote = new Quote { Id = 99, OwnerId = 2, Author = "Other", Text = "Not yours." };

        var result = await authService.AuthorizeAsync(user, quote, "can-delete-own-quote");

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task CanDeleteOwnQuote_Fails_WhenQuoteHasNoOwner()
    {
        // Quotes created before ownership tracking have OwnerId = null.
        // Deny by default — fail-closed is safer than fail-open.
        var authService = BuildAuthService();
        var user = AuthenticatedUser(id: 1);
        var quote = new Quote { Id = 10, OwnerId = null, Author = "Legacy", Text = "Old quote." };

        var result = await authService.AuthorizeAsync(user, quote, "can-delete-own-quote");

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task CanDeleteOwnQuote_Succeeds_WhenUserOwnsQuote()
    {
        // Happy path: User 1 deletes their own quote.
        var authService = BuildAuthService();
        var user = AuthenticatedUser(id: 1);
        var quote = new Quote { Id = 7, OwnerId = 1, Author = "Self", Text = "Mine to delete." };

        var result = await authService.AuthorizeAsync(user, quote, "can-delete-own-quote");

        Assert.True(result.Succeeded);
    }
}
