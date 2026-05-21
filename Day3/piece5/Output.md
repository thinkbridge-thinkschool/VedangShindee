A total of 1 test files matched the specified pattern.
[xUnit.net 00:00:00.00] xUnit.net VSTest Adapter v2.8.2+699d445a1a (64-bit .NET 10.0.8)
[xUnit.net 00:00:00.00] xUnit.net VSTest Adapter v2.8.2+699d445a1a (64-bit .NET 10.0.8)
[xUnit.net 00:00:00.30]   Discovering: Quotes.Tests.Unit
[xUnit.net 00:00:00.30]   Discovering: Quotes.Tests.Unit
[xUnit.net 00:00:00.52]   Discovered:  Quotes.Tests.Unit
[xUnit.net 00:00:00.52]   Discovered:  Quotes.Tests.Unit
[xUnit.net 00:00:00.54]   Starting:    Quotes.Tests.Unit
[xUnit.net 00:00:00.54]   Starting:    Quotes.Tests.Unit
  Passed Quotes.Tests.Unit.TokenHasherTests.Hash_OutputLengthIs64Characters [25 ms]
  Passed Quotes.Tests.Unit.QuoteValidatorTests.Validate_BothFieldsMissing_ReturnsBothErrors [25 ms]
  Passed Quotes.Tests.Unit.QuoteValidatorTests.Validate_InvalidText_ReturnsTextRequiredError(text: null) [5 ms]
  Passed Quotes.Tests.Unit.QuoteValidatorTests.Validate_InvalidText_ReturnsTextRequiredError(text: "   ") [< 1 ms]
  Passed Quotes.Tests.Unit.QuoteValidatorTests.Validate_InvalidText_ReturnsTextRequiredError(text: "") [< 1 ms]
  Passed Quotes.Tests.Unit.QuoteValidatorTests.Validate_InvalidAuthor_ReturnsAuthorRequiredError(author: "   ") [< 1 ms]
  Passed Quotes.Tests.Unit.QuoteValidatorTests.Validate_InvalidAuthor_ReturnsAuthorRequiredError(author: null) [< 1 ms]
  Passed Quotes.Tests.Unit.TokenHasherTests.Hash_KnownInput_MatchesExpectedSha256 [6 ms]
  Passed Quotes.Tests.Unit.QuoteValidatorTests.Validate_InvalidAuthor_ReturnsAuthorRequiredError(author: "") [< 1 ms]
  Passed Quotes.Tests.Unit.TokenHasherTests.Hash_DifferentInputs_ReturnDifferentHashes [< 1 ms]
  Passed Quotes.Tests.Unit.TokenHasherTests.Hash_SameInputTwice_ReturnsSameHash [< 1 ms]
  Passed Quotes.Tests.Unit.TokenHasherTests.Hash_OutputIsLowercaseHexOnly [2 ms]
  Passed Quotes.Tests.Unit.QuoteFactoryTests.Create_ValidInputs_WithNullOwnerId_LeavesOwnerIdNull [3 ms]
  Passed Quotes.Tests.Unit.QuoteFactoryTests.Create_InvalidText_ReturnsErrorsAndNullQuote(text: "   ") [1 ms]
  Passed Quotes.Tests.Unit.QuoteFactoryTests.Create_InvalidText_ReturnsErrorsAndNullQuote(text: "") [< 1 ms]
  Passed Quotes.Tests.Unit.QuoteFactoryTests.Create_InvalidText_ReturnsErrorsAndNullQuote(text: null) [< 1 ms]
  Passed Quotes.Tests.Unit.QuoteFactoryTests.Create_ValidInputs_ReturnsQuoteWithCorrectValues [1 ms]
  Passed Quotes.Tests.Unit.QuoteFactoryTests.Create_ValidInputs_SetsCreatedAtFromPassedTimestamp [2 ms]
  Passed Quotes.Tests.Unit.QuoteFactoryTests.Create_AuthorExceedsMaxLength_ReturnsAuthorLengthError [< 1 ms]
  Passed Quotes.Tests.Unit.QuoteFactoryTests.Create_InvalidAuthor_ReturnsErrorsAndNullQuote(author: null) [87 ms]
  Passed Quotes.Tests.Unit.QuoteFactoryTests.Create_InvalidAuthor_ReturnsErrorsAndNullQuote(author: "") [< 1 ms]
  Passed Quotes.Tests.Unit.QuoteFactoryTests.Create_InvalidAuthor_ReturnsErrorsAndNullQuote(author: "   ") [< 1 ms]
  Passed Quotes.Tests.Unit.QuoteFactoryTests.Create_TextExceedsMaxLength_ReturnsTextLengthError [< 1 ms]
  Passed Quotes.Tests.Unit.QuoteFactoryTests.Create_BothFieldsBlank_ReturnsBothErrors [< 1 ms]
  Passed Quotes.Tests.Unit.QuoteFactoryTests.Create_ValidInputs_WithOwnerId_SetsOwnerId [< 1 ms]
  Passed Quotes.Tests.Unit.QuoteValidatorTests.Validate_BothFieldsValid_ReturnsEmptyDictionary [124 ms]
  Passed Quotes.Tests.Unit.OwnQuoteHandlerTests.HandleRequirementAsync_Fails_WhenUserOwnsADifferentQuote [6 ms]        
  Passed Quotes.Tests.Unit.OwnQuoteHandlerTests.HandleRequirementAsync_Succeeds_WhenUserOwnsQuote [1 ms]
  Passed Quotes.Tests.Unit.OwnQuoteHandlerTests.HandleRequirementAsync_Fails_WhenSubClaimMissing [1 ms]
  Passed Quotes.Tests.Unit.OwnQuoteHandlerTests.HandleRequirementAsync_Fails_WhenSubClaimIsNotNumeric [1 ms]
  Passed Quotes.Tests.Unit.OwnQuoteHandlerTests.HandleRequirementAsync_Fails_WhenQuoteHasNoOwner [< 1 ms]
  Passed Quotes.Tests.Unit.RefreshTokenRepositoryTests.RevokeTokenAsync_LeavesReplacedByHashNull_WhenNotProvided [2 s]
  Passed Quotes.Tests.Unit.QuoteRepositoryTests.CreateAsync_PersistsQuoteToDatabase [2 s]
  Passed Quotes.Tests.Unit.QuoteRepositoryTests.CreateAsync_SetsCreatedAtFromClock [115 ms]
  Passed Quotes.Tests.Unit.QuoteRepositoryTests.DeleteAsync_ReturnsFalse_WhenQuoteNotFound [393 ms]
  Passed Quotes.Tests.Unit.RefreshTokenRepositoryTests.RevokeFamilyAsync_DoesNotAffect_TokensInOtherFamilies [541 ms]
  Passed Quotes.Tests.Unit.QuoteRepositoryTests.DeleteAsync_ReturnsTrue_AndRemovesFromDatabase [84 ms]
  Passed Quotes.Tests.Unit.QuoteRepositoryTests.GetByIdAsync_ReturnsNull_WhenIdDoesNotExist [4 ms]
  Passed Quotes.Tests.Unit.RefreshTokenRepositoryTests.RevokeTokenAsync_SetsRevokedAtFromClock [244 ms]
  Passed Quotes.Tests.Unit.RefreshTokenRepositoryTests.RevokeTokenAsync_SetsReplacedByHash_WhenHashProvided [9 ms]
[xUnit.net 00:00:04.22]   Finished:    Quotes.Tests.Unit
[xUnit.net 00:00:04.22]   Finished:    Quotes.Tests.Unit
  Passed Quotes.Tests.Unit.RefreshTokenRepositoryTests.RevokeFamilyAsync_RevokesAllActiveTokensInFamily [14 ms]
  Passed Quotes.Tests.Unit.RefreshTokenRepositoryTests.FindByHashAsync_ReturnsNull_WhenHashNotFound [2 ms]
  Passed Quotes.Tests.Unit.RefreshTokenRepositoryTests.RevokeFamilyAsync_DoesNotModify_AlreadyRevokedTokens [6 ms]

Test Run Successful.
Total tests: 43
     Passed: 43
 Total time: 5.9179 Seconds
  Quotes.Tests.Unit test net10.0 succeeded (6.6s)

Test summary: total: 43, failed: 0, succeeded: 43, skipped: 0, duration: 6.5s
Build succeeded in 10.9s

Test Run Output - 

QuotesApi net10.0 succeeded (1.1s) → QuotesApi\bin\Debug\net10.0\QuotesApi.dll
Quotes.Tests.Unit net10.0 succeeded (0.7s) → Quotes.Tests.Unit\bin\Debug\net10.0\Quotes.Tests.Unit.dll
[xUnit.net 00:00:00.01] xUnit.net VSTest Adapter v2.8.2+699d445a1a (64-bit .NET 10.0.8)
[xUnit.net 00:00:00.28]   Discovering: Quotes.Tests.Unit
[xUnit.net 00:00:00.52]   Discovered:  Quotes.Tests.Unit
[xUnit.net 00:00:00.53]   Starting:    Quotes.Tests.Unit
[xUnit.net 00:00:03.07]   Finished:    Quotes.Tests.Unit
  Quotes.Tests.Unit test net10.0 succeeded (6.5s)

Test summary: total: 43, failed: 0, succeeded: 43, skipped: 0, duration: 6.5s
Build succeeded in 12.2s