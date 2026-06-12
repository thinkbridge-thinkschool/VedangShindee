using FluentAssertions;
using QuotesApi.Models;
using Xunit;

namespace Quotes.Tests.Unit;

public class QuoteFactoryTests
{
    private static readonly DateTimeOffset FixedNow =
        new(2026, 5, 21, 10, 0, 0, TimeSpan.Zero);

    // ── Success path ──────────────────────────────────────────────────────────

    [Fact]
    public void Create_ValidInputs_ReturnsQuoteWithCorrectValues()
    {
        // Arrange
        const string author = "Epictetus";
        const string text = "He is a wise man who does not grieve for the things which he has not.";

        // Act
        var (quote, errors) = Quote.Create(author, text, ownerId: 7, FixedNow);

        // Assert
        errors.Should().BeNull();
        quote.Should().NotBeNull();
        quote!.Author.Should().Be(author);
        quote.Text.Should().Be(text);
        quote.OwnerId.Should().Be(7);
    }

    [Fact]
    public void Create_ValidInputs_SetsCreatedAtFromPassedTimestamp()
    {
        // Arrange
        var clock = new FakeClock(FixedNow);

        // Act
        var (quote, _) = Quote.Create("Author", "Text", null, clock.UtcNow);

        // Assert
        quote!.CreatedAt.Should().Be(FixedNow);
    }

    [Fact]
    public void Create_ValidInputs_WithNullOwnerId_LeavesOwnerIdNull()
    {
        // Arrange — backward-compat: pre-ownership quotes have no owner

        // Act
        var (quote, errors) = Quote.Create("Author", "Text", ownerId: null, FixedNow);

        // Assert
        errors.Should().BeNull();
        quote!.OwnerId.Should().BeNull();
    }

    [Fact]
    public void Create_ValidInputs_WithOwnerId_SetsOwnerId()
    {
        // Arrange

        // Act
        var (quote, errors) = Quote.Create("Author", "Text", ownerId: 42, FixedNow);

        // Assert
        errors.Should().BeNull();
        quote!.OwnerId.Should().Be(42);
    }

    // ── Failure: invalid author ───────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_InvalidAuthor_ReturnsErrorsAndNullQuote(string? author)
    {
        // Arrange

        // Act
        var (quote, errors) = Quote.Create(author!, "Valid text", null, FixedNow);

        // Assert
        quote.Should().BeNull();
        errors.Should().NotBeNull();
        errors.Should().ContainKey("author");
        errors!["author"].Should().Contain("Author is required");
    }

    // ── Failure: invalid text ─────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_InvalidText_ReturnsErrorsAndNullQuote(string? text)
    {
        // Arrange

        // Act
        var (quote, errors) = Quote.Create("Seneca", text!, null, FixedNow);

        // Assert
        quote.Should().BeNull();
        errors.Should().NotBeNull();
        errors.Should().ContainKey("text");
        errors!["text"].Should().Contain("Text is required");
    }

    // ── Failure: field too long ───────────────────────────────────────────────

    [Fact]
    public void Create_AuthorExceedsMaxLength_ReturnsAuthorLengthError()
    {
        // Arrange
        var tooLong = new string('A', Quote.AuthorMaxLength + 1); // 101 chars

        // Act
        var (quote, errors) = Quote.Create(tooLong, "Valid text", null, FixedNow);

        // Assert
        quote.Should().BeNull();
        errors.Should().ContainKey("author");
        errors!["author"].Should().Contain($"Author must be {Quote.AuthorMaxLength} characters or fewer");
    }

    [Fact]
    public void Create_TextExceedsMaxLength_ReturnsTextLengthError()
    {
        // Arrange
        var tooLong = new string('x', Quote.TextMaxLength + 1); // 1001 chars

        // Act
        var (quote, errors) = Quote.Create("Author", tooLong, null, FixedNow);

        // Assert
        quote.Should().BeNull();
        errors.Should().ContainKey("text");
        errors!["text"].Should().Contain($"Text must be {Quote.TextMaxLength} characters or fewer");
    }

    [Fact]
    public void Create_BothFieldsBlank_ReturnsBothErrors()
    {
        // Arrange

        // Act
        var (quote, errors) = Quote.Create("", "", null, FixedNow);

        // Assert
        quote.Should().BeNull();
        errors.Should().ContainKey("author");
        errors.Should().ContainKey("text");
        errors!.Should().HaveCount(2);
    }
}
