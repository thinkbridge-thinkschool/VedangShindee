using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using QuotesApi.Authorization;
using QuotesApi.Models;
using Xunit;

namespace Quotes.Tests.Unit;

public class OwnQuoteHandlerTests
{
    private static ClaimsPrincipal UserWithId(int id) =>
        new(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, id.ToString()) },
            authenticationType: "test"));

    private static AuthorizationHandlerContext BuildContext(ClaimsPrincipal user, Quote quote) =>
        new(new IAuthorizationRequirement[] { new OwnQuoteRequirement() }, user, quote);

    // ── Success path ──────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleRequirementAsync_Succeeds_WhenUserOwnsQuote()
    {
        // Arrange
        var handler = new OwnQuoteHandler();
        var quote = new Quote { Id = 1, OwnerId = 42, Author = "A", Text = "T" };
        var user = UserWithId(42);
        var context = BuildContext(user, quote);

        // Act
        await handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeTrue();
    }

    // ── Failure: wrong owner ──────────────────────────────────────────────────

    [Fact]
    public async Task HandleRequirementAsync_Fails_WhenUserOwnsADifferentQuote()
    {
        // Arrange
        var handler = new OwnQuoteHandler();
        var quote = new Quote { Id = 2, OwnerId = 99, Author = "A", Text = "T" };
        var user = UserWithId(1); // user 1 tries to touch user 99's quote

        var context = BuildContext(user, quote);

        // Act
        await handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeFalse();
    }

    // ── Failure: no owner (legacy quote) ─────────────────────────────────────

    [Fact]
    public async Task HandleRequirementAsync_Fails_WhenQuoteHasNoOwner()
    {
        // Arrange — quotes created before ownership tracking have OwnerId = null;
        //            fail-closed: nobody can delete an unowned quote via this policy.
        var handler = new OwnQuoteHandler();
        var quote = new Quote { Id = 3, OwnerId = null, Author = "A", Text = "T" };
        var user = UserWithId(1);
        var context = BuildContext(user, quote);

        // Act
        await handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeFalse();
    }

    // ── Failure: no sub claim ─────────────────────────────────────────────────

    [Fact]
    public async Task HandleRequirementAsync_Fails_WhenSubClaimMissing()
    {
        // Arrange — authenticated user whose token carries no NameIdentifier or sub
        var handler = new OwnQuoteHandler();
        var quote = new Quote { Id = 4, OwnerId = 1, Author = "A", Text = "T" };
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            Array.Empty<Claim>(), authenticationType: "test"));

        var context = BuildContext(user, quote);

        // Act
        await handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeFalse();
    }

    // ── Failure: sub claim is not a valid integer ─────────────────────────────

    [Fact]
    public async Task HandleRequirementAsync_Fails_WhenSubClaimIsNotNumeric()
    {
        // Arrange — malformed token where sub is a non-integer string
        var handler = new OwnQuoteHandler();
        var quote = new Quote { Id = 5, OwnerId = 1, Author = "A", Text = "T" };
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, "not-a-number") },
            authenticationType: "test"));

        var context = BuildContext(user, quote);

        // Act
        await handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeFalse();
    }
}
