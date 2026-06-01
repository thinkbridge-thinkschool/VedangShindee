using FluentAssertions;
using NSubstitute;
using QuotesApi.Commands;
using QuotesApi.Models;
using QuotesApi.Repositories;
using QuotesApi.Services;
using Xunit;

namespace Quotes.Tests.Unit;

public class CreateQuoteHandlerTests
{
    private static readonly DateTimeOffset FixedNow =
        new(2026, 5, 30, 9, 0, 0, TimeSpan.Zero);

    // ── Validation failure ────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_EmptyAuthor_ReturnsErrors()
    {
        // Arrange
        var repository = Substitute.For<IQuoteRepository>();
        var validator  = new QuoteValidator();
        var handler    = new CreateQuoteHandler(repository, validator);

        var command = new CreateQuoteCommand(Author: "", Text: "Some text.", OwnerId: 1);

        // Act
        var (result, errors) = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        result.Should().BeNull();
        errors.Should().ContainKey("author");
        await repository.DidNotReceive().CreateAsync(Arg.Any<Quote>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_EmptyText_ReturnsErrors()
    {
        // Arrange
        var repository = Substitute.For<IQuoteRepository>();
        var validator  = new QuoteValidator();
        var handler    = new CreateQuoteHandler(repository, validator);

        var command = new CreateQuoteCommand(Author: "Seneca", Text: "", OwnerId: 1);

        // Act
        var (result, errors) = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        result.Should().BeNull();
        errors.Should().ContainKey("text");
        await repository.DidNotReceive().CreateAsync(Arg.Any<Quote>(), Arg.Any<CancellationToken>());
    }

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_ValidCommand_PersistsAndReturnsResult()
    {
        // Arrange
        var repository = Substitute.For<IQuoteRepository>();
        var validator  = new QuoteValidator();
        var handler    = new CreateQuoteHandler(repository, validator);

        var command = new CreateQuoteCommand(Author: "Seneca", Text: "Luck is preparation meeting opportunity.", OwnerId: 42);

        repository.CreateAsync(Arg.Any<Quote>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var q = callInfo.Arg<Quote>();
                q.Id        = 7;
                q.CreatedAt = FixedNow;
                return q;
            });

        // Act
        var (result, errors) = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        errors.Should().BeNull();
        result.Should().NotBeNull();
        result!.Id.Should().Be(7);
        result.Author.Should().Be("Seneca");
        result.Text.Should().Be("Luck is preparation meeting opportunity.");
        result.CreatedAt.Should().Be(FixedNow);
    }

    [Fact]
    public async Task HandleAsync_ValidCommand_PassesOwnerIdToRepository()
    {
        // Arrange
        var repository = Substitute.For<IQuoteRepository>();
        var validator  = new QuoteValidator();
        var handler    = new CreateQuoteHandler(repository, validator);

        var command = new CreateQuoteCommand(Author: "Epictetus", Text: "Make the best use of what is in your power.", OwnerId: 99);

        repository.CreateAsync(Arg.Any<Quote>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<Quote>());

        // Act
        await handler.HandleAsync(command, CancellationToken.None);

        // Assert: OwnerId is passed through to the write model
        await repository.Received(1).CreateAsync(
            Arg.Is<Quote>(q => q.OwnerId == 99),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ValidCommand_ResultDoesNotExposeOwnerId()
    {
        // Arrange
        var repository = Substitute.For<IQuoteRepository>();
        var validator  = new QuoteValidator();
        var handler    = new CreateQuoteHandler(repository, validator);

        var command = new CreateQuoteCommand(Author: "Stoic", Text: "A valid quote.", OwnerId: 5);

        repository.CreateAsync(Arg.Any<Quote>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var q = callInfo.Arg<Quote>();
                q.Id = 1;
                return q;
            });

        // Act
        var (result, _) = await handler.HandleAsync(command, CancellationToken.None);

        // Assert: CreateQuoteResult has no OwnerId — write-side concern stays on the write side
        result.Should().NotBeNull();
        result!.GetType().GetProperty("OwnerId").Should().BeNull();
    }
}
