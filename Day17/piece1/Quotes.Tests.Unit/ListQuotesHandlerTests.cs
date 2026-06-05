using FluentAssertions;
using NSubstitute;
using QuotesApi.Models;
using QuotesApi.Queries;
using QuotesApi.Repositories;
using Xunit;

namespace Quotes.Tests.Unit;

public class ListQuotesHandlerTests
{
    private static readonly DateTimeOffset FixedNow =
        new(2026, 5, 21, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task HandleAsync_ReturnsEmptyList_WhenRepositoryReturnsNone()
    {
        var repo = Substitute.For<IQuoteRepository>();
        repo.GetPagedAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<Quote>());

        var handler = new ListQuotesHandler(repo);
        var result = await handler.HandleAsync(new ListQuotesQuery(1, 10), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_MapsToDtoCorrectly()
    {
        var repo = Substitute.For<IQuoteRepository>();
        repo.GetPagedAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<Quote>
            {
                new() { Id = 1, Author = "Seneca", Text = "Luck is preparation.", CreatedAt = FixedNow },
                new() { Id = 2, Author = "Aurelius", Text = "You have power.", CreatedAt = FixedNow }
            });

        var handler = new ListQuotesHandler(repo);
        var result = await handler.HandleAsync(new ListQuotesQuery(1, 10), CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].Id.Should().Be(1);
        result[0].Author.Should().Be("Seneca");
        result[0].Text.Should().Be("Luck is preparation.");
        result[0].CreatedAt.Should().Be(FixedNow);
        result[1].Id.Should().Be(2);
        result[1].Author.Should().Be("Aurelius");
    }

    [Fact]
    public async Task HandleAsync_PassesPageAndSizeToRepository()
    {
        var repo = Substitute.For<IQuoteRepository>();
        repo.GetPagedAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<Quote>());

        var handler = new ListQuotesHandler(repo);
        await handler.HandleAsync(new ListQuotesQuery(3, 20), CancellationToken.None);

        await repo.Received(1).GetPagedAsync(3, 20, Arg.Any<CancellationToken>());
    }
}
