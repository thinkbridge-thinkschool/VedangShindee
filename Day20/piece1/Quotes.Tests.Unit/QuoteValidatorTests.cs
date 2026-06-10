using FluentAssertions;
using QuotesApi.Models;
using QuotesApi.Services;
using Xunit;

namespace Quotes.Tests.Unit;

public class QuoteValidatorTests
{
    // ── Branch: author missing ────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_InvalidAuthor_ReturnsAuthorRequiredError(string? author)
    {
        // Arrange
        var validator = new QuoteValidator();
        var request = new CreateQuoteRequest { Author = author!, Text = "Some valid text." };

        // Act
        var errors = validator.Validate(request);

        // Assert
        errors.Should().ContainKey("author");
        errors["author"].Should().Contain("Author is required");
    }

    // ── Branch: text missing ──────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_InvalidText_ReturnsTextRequiredError(string? text)
    {
        // Arrange
        var validator = new QuoteValidator();
        var request = new CreateQuoteRequest { Author = "Seneca", Text = text! };

        // Act
        var errors = validator.Validate(request);

        // Assert
        errors.Should().ContainKey("text");
        errors["text"].Should().Contain("Text is required");
    }

    // ── Branch: both fields missing ───────────────────────────────────────────

    [Fact]
    public void Validate_BothFieldsMissing_ReturnsBothErrors()
    {
        // Arrange
        var validator = new QuoteValidator();
        var request = new CreateQuoteRequest { Author = "", Text = "" };

        // Act
        var errors = validator.Validate(request);

        // Assert
        errors.Should().ContainKey("author");
        errors.Should().ContainKey("text");
        errors.Should().HaveCount(2);
    }

    // ── Branch: all valid ─────────────────────────────────────────────────────

    [Fact]
    public void Validate_BothFieldsValid_ReturnsEmptyDictionary()
    {
        // Arrange
        var validator = new QuoteValidator();
        var request = new CreateQuoteRequest
        {
            Author = "Marcus Aurelius",
            Text = "The impediment to action advances action. What stands in the way becomes the way."
        };

        // Act
        var errors = validator.Validate(request);

        // Assert
        errors.Should().BeEmpty();
    }

    // ── Branch: author too long ───────────────────────────────────────────────

    [Fact]
    public void Validate_AuthorExceedsMaxLength_ReturnsAuthorLengthError()
    {
        var validator = new QuoteValidator();
        var request = new CreateQuoteRequest
        {
            Author = new string('A', 201),
            Text = "Valid text."
        };

        var errors = validator.Validate(request);

        errors.Should().ContainKey("author");
        errors["author"].Should().Contain("Author must be 200 characters or less");
    }

    [Fact]
    public void Validate_AuthorAtExactMaxLength_ReturnsNoError()
    {
        var validator = new QuoteValidator();
        var request = new CreateQuoteRequest
        {
            Author = new string('A', 200),
            Text = "Valid text."
        };

        var errors = validator.Validate(request);

        errors.Should().NotContainKey("author");
    }

    // ── Branch: text too long ─────────────────────────────────────────────────

    [Fact]
    public void Validate_TextExceedsMaxLength_ReturnsTextLengthError()
    {
        var validator = new QuoteValidator();
        var request = new CreateQuoteRequest
        {
            Author = "Seneca",
            Text = new string('x', 1001)
        };

        var errors = validator.Validate(request);

        errors.Should().ContainKey("text");
        errors["text"].Should().Contain("Quote text must be 1000 characters or less");
    }

    [Fact]
    public void Validate_TextAtExactMaxLength_ReturnsNoError()
    {
        var validator = new QuoteValidator();
        var request = new CreateQuoteRequest
        {
            Author = "Seneca",
            Text = new string('x', 1000)
        };

        var errors = validator.Validate(request);

        errors.Should().NotContainKey("text");
    }
}
