using FluentAssertions;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using QuotesApi.Models;
using QuotesApi.Queries;
using QuotesApi.Repositories;
using Xunit;

namespace Quotes.Tests.Unit;

public class GetQuoteByIdHandlerTests
{
    private static readonly DateTimeOffset FixedNow =
        new(2026, 5, 21, 9, 0, 0, TimeSpan.Zero);

    // Each test gets a fresh in-memory HybridCache so the cache always starts cold.
    // The factory lambda inside the handler is therefore always invoked, which lets
    // us verify repository interactions without mocking the abstract HybridCache.
    private static HybridCache CreateCache()
    {
        var services = new ServiceCollection();
        services.AddDistributedMemoryCache();
        services.AddHybridCache();
        return services.BuildServiceProvider().GetRequiredService<HybridCache>();
    }

    [Fact]
    public async Task HandleAsync_ReturnsNull_WhenQuoteNotFound()
    {
        var repo = Substitute.For<IQuoteRepository>();
        repo.GetByIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((Quote?)null);

        var handler = new GetQuoteByIdHandler(repo, CreateCache());
        var result = await handler.HandleAsync(new GetQuoteByIdQuery(99), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_ReturnsDto_WhenQuoteExists()
    {
        var repo = Substitute.For<IQuoteRepository>();
        repo.GetByIdAsync(5, Arg.Any<CancellationToken>())
            .Returns(new Quote { Id = 5, Author = "Epictetus", Text = "Make the best use.", CreatedAt = FixedNow });

        var handler = new GetQuoteByIdHandler(repo, CreateCache());
        var result = await handler.HandleAsync(new GetQuoteByIdQuery(5), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(5);
        result.Author.Should().Be("Epictetus");
        result.Text.Should().Be("Make the best use.");
        result.CreatedAt.Should().Be(FixedNow);
    }

    [Fact]
    public async Task HandleAsync_PassesIdToRepository()
    {
        var repo = Substitute.For<IQuoteRepository>();
        repo.GetByIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((Quote?)null);

        var handler = new GetQuoteByIdHandler(repo, CreateCache());
        await handler.HandleAsync(new GetQuoteByIdQuery(42), CancellationToken.None);

        await repo.Received(1).GetByIdAsync(42, Arg.Any<CancellationToken>());
    }
}
