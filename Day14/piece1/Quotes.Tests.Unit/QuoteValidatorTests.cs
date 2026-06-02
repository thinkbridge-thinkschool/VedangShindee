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
}
