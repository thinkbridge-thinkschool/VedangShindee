using FluentAssertions;
using QuotesApi.Services;
using Xunit;

namespace Quotes.Tests.Unit;

public class TokenHasherTests
{
    [Fact]
    public void Hash_SameInputTwice_ReturnsSameHash()
    {
        // Arrange
        const string input = "my-raw-refresh-token";

        // Act
        var first = TokenHasher.Hash(input);
        var second = TokenHasher.Hash(input);

        // Assert
        first.Should().Be(second);
    }

    [Fact]
    public void Hash_DifferentInputs_ReturnDifferentHashes()
    {
        // Arrange
        const string inputA = "token-alpha";
        const string inputB = "token-beta";

        // Act
        var hashA = TokenHasher.Hash(inputA);
        var hashB = TokenHasher.Hash(inputB);

        // Assert
        hashA.Should().NotBe(hashB);
    }

    [Fact]
    public void Hash_OutputIsLowercaseHexOnly()
    {
        // Arrange
        const string input = "MixedCaseInput-123";

        // Act
        var hash = TokenHasher.Hash(input);

        // Assert — every character is a lowercase hex digit
        hash.Should().MatchRegex("^[0-9a-f]+$");
    }

    [Fact]
    public void Hash_OutputLengthIs64Characters()
    {
        // Arrange — SHA-256 produces 32 bytes = 64 hex characters

        // Act
        var hash = TokenHasher.Hash("any-value");

        // Assert
        hash.Should().HaveLength(64);
    }

    [Fact]
    public void Hash_KnownInput_MatchesExpectedSha256()
    {
        // Arrange — SHA-256("hello") is a well-known constant
        const string expectedHex = "2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824";

        // Act
        var hash = TokenHasher.Hash("hello");

        // Assert
        hash.Should().Be(expectedHex);
    }
}
