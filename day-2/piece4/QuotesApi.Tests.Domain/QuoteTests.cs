using FluentAssertions;
using QuotesApi.Models;
using Xunit;

namespace QuotesApi.Tests.Domain;

public class QuoteTests
{
    [Fact]
    public void Create_ValidInputs_ReturnsQuote()
    {
        var (quote, error) = Quote.Create("Seneca", "Luck is what happens when preparation meets opportunity.");

        error.Should().BeNull();
        quote.Should().NotBeNull();
        quote!.Author.Should().Be("Seneca");
        quote.Text.Should().Be("Luck is what happens when preparation meets opportunity.");
        quote.IsDeleted.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_EmptyAuthor_ReturnsDomainError(string author)
    {
        var (quote, error) = Quote.Create(author, "Some text");

        quote.Should().BeNull();
        error.Should().NotBeNull();
        error!.Message.Should().Contain("Author");
    }

    [Fact]
    public void Create_AuthorExceeds200Chars_ReturnsDomainError()
    {
        var longAuthor = new string('A', 201);

        var (quote, error) = Quote.Create(longAuthor, "Some text");

        quote.Should().BeNull();
        error!.Message.Should().Contain("Author");
    }

    [Fact]
    public void Create_AuthorExactly200Chars_Succeeds()
    {
        var author = new string('A', 200);

        var (quote, error) = Quote.Create(author, "Some text");

        error.Should().BeNull();
        quote.Should().NotBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_EmptyText_ReturnsDomainError(string text)
    {
        var (quote, error) = Quote.Create("Seneca", text);

        quote.Should().BeNull();
        error!.Message.Should().Contain("Text");
    }

    [Fact]
    public void Create_TextExceeds1000Chars_ReturnsDomainError()
    {
        var longText = new string('x', 1001);

        var (quote, error) = Quote.Create("Seneca", longText);

        quote.Should().BeNull();
        error!.Message.Should().Contain("Text");
    }

    [Fact]
    public void Create_TextExactly1000Chars_Succeeds()
    {
        var text = new string('x', 1000);

        var (quote, error) = Quote.Create("Seneca", text);

        error.Should().BeNull();
        quote.Should().NotBeNull();
    }

    [Fact]
    public void Create_TrimsWhitespace()
    {
        var (quote, error) = Quote.Create("  Seneca  ", "  Some text  ");

        error.Should().BeNull();
        quote!.Author.Should().Be("Seneca");
        quote.Text.Should().Be("Some text");
    }

    [Fact]
    public void SoftDelete_SetsIsDeletedTrue()
    {
        var (quote, _) = Quote.Create("Seneca", "Luck is preparation meeting opportunity.");

        quote!.SoftDelete();

        quote.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public void Text_IsImmutableAfterCreation()
    {
        var (quote, _) = Quote.Create("Seneca", "Original text");

        // Text property has a private setter — this verifies no public mutation path exists.
        var textProperty = typeof(Quote).GetProperty(nameof(Quote.Text));
        textProperty!.SetMethod!.IsPublic.Should().BeFalse();
    }
}
