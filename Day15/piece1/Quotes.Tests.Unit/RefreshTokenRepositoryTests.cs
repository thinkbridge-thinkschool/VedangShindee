using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using QuotesApi.Data;
using QuotesApi.Models;
using QuotesApi.Services;
using Xunit;

namespace Quotes.Tests.Unit;

public class RefreshTokenRepositoryTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static readonly DateTimeOffset BaseTime =
        new(2026, 5, 21, 12, 0, 0, TimeSpan.Zero);

    // ── RevokeTokenAsync stamps RevokedAt from IClock (NSubstitute demo) ──────

    [Fact]
    public async Task RevokeTokenAsync_SetsRevokedAtFromClock()
    {
        // Arrange — IClock mocked with NSubstitute to return a controlled time
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(BaseTime);

        await using var db = CreateDb();
        var repo = new RefreshTokenRepository(db, clock);
        var token = new RefreshToken
        {
            TokenHash = TokenHasher.Hash("revoke-me"),
            UserId = 1,
            FamilyId = "fam-1",
            ExpiresAt = BaseTime.AddDays(7)
        };
        await repo.AddAsync(token);

        // Act
        await repo.RevokeTokenAsync(token, replacedByHash: null);

        // Assert
        token.RevokedAt.Should().Be(BaseTime);
    }

    [Fact]
    public async Task RevokeTokenAsync_SetsReplacedByHash_WhenHashProvided()
    {
        // Arrange
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(BaseTime);

        await using var db = CreateDb();
        var repo = new RefreshTokenRepository(db, clock);
        var token = new RefreshToken
        {
            TokenHash = TokenHasher.Hash("token-a"),
            UserId = 2,
            FamilyId = "fam-2",
            ExpiresAt = BaseTime.AddDays(7)
        };
        await repo.AddAsync(token);

        const string successorHash = "abc123successor";

        // Act
        await repo.RevokeTokenAsync(token, replacedByHash: successorHash);

        // Assert
        token.ReplacedByToken.Should().Be(successorHash);
    }

    [Fact]
    public async Task RevokeTokenAsync_LeavesReplacedByHashNull_WhenNotProvided()
    {
        // Arrange — logout path: revoke without issuing a successor
        var clock = new FakeClock(BaseTime);
        await using var db = CreateDb();
        var repo = new RefreshTokenRepository(db, clock);
        var token = new RefreshToken
        {
            TokenHash = TokenHasher.Hash("logout-token"),
            UserId = 3,
            FamilyId = "fam-logout",
            ExpiresAt = BaseTime.AddDays(7)
        };
        await repo.AddAsync(token);

        // Act
        await repo.RevokeTokenAsync(token, replacedByHash: null);

        // Assert
        token.ReplacedByToken.Should().BeNull();
    }

    // ── RevokeFamilyAsync: reuse-detection path ───────────────────────────────

    [Fact]
    public async Task RevokeFamilyAsync_RevokesAllActiveTokensInFamily()
    {
        // Arrange
        var clock = new FakeClock(BaseTime);
        await using var db = CreateDb();
        var repo = new RefreshTokenRepository(db, clock);

        const string family = "breach-family";
        var tokenA = new RefreshToken { TokenHash = TokenHasher.Hash("a"), UserId = 1, FamilyId = family, ExpiresAt = BaseTime.AddDays(7) };
        var tokenB = new RefreshToken { TokenHash = TokenHasher.Hash("b"), UserId = 1, FamilyId = family, ExpiresAt = BaseTime.AddDays(7) };
        await repo.AddAsync(tokenA);
        await repo.AddAsync(tokenB);

        // Act — attacker re-presents a revoked token; endpoint calls RevokeFamilyAsync
        await repo.RevokeFamilyAsync(family);

        // Assert — both tokens in the family must now be revoked
        var storedA = await repo.FindByHashAsync(TokenHasher.Hash("a"));
        var storedB = await repo.FindByHashAsync(TokenHasher.Hash("b"));
        storedA!.RevokedAt.Should().NotBeNull();
        storedB!.RevokedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task RevokeFamilyAsync_DoesNotModify_AlreadyRevokedTokens()
    {
        // Arrange — token A was already revoked during normal rotation
        var clock = new FakeClock(BaseTime);
        await using var db = CreateDb();
        var repo = new RefreshTokenRepository(db, clock);

        const string family = "partial-revoke";
        var tokenA = new RefreshToken { TokenHash = TokenHasher.Hash("already-gone"), UserId = 1, FamilyId = family, ExpiresAt = BaseTime.AddDays(7) };
        await repo.AddAsync(tokenA);
        await repo.RevokeTokenAsync(tokenA, replacedByHash: null); // revoked at BaseTime

        // Advance clock so the second revocation would stamp a different time if it ran
        var laterClock = new FakeClock(BaseTime.AddHours(2));
        var repoLater = new RefreshTokenRepository(db, laterClock);

        // Act
        await repoLater.RevokeFamilyAsync(family); // no active tokens remain

        // Assert — RevokedAt must not have been overwritten (still BaseTime, not +2h)
        var stored = await repo.FindByHashAsync(TokenHasher.Hash("already-gone"));
        stored!.RevokedAt.Should().Be(BaseTime);
    }

    [Fact]
    public async Task RevokeFamilyAsync_DoesNotAffect_TokensInOtherFamilies()
    {
        // Arrange
        var clock = new FakeClock(BaseTime);
        await using var db = CreateDb();
        var repo = new RefreshTokenRepository(db, clock);

        var victimToken = new RefreshToken { TokenHash = TokenHasher.Hash("victim"), UserId = 5, FamilyId = "family-A", ExpiresAt = BaseTime.AddDays(7) };
        var innocentToken = new RefreshToken { TokenHash = TokenHasher.Hash("innocent"), UserId = 6, FamilyId = "family-B", ExpiresAt = BaseTime.AddDays(7) };
        await repo.AddAsync(victimToken);
        await repo.AddAsync(innocentToken);

        // Act — only revoke family-A
        await repo.RevokeFamilyAsync("family-A");

        // Assert — token in family-B must remain active
        var innocent = await repo.FindByHashAsync(TokenHasher.Hash("innocent"));
        innocent!.RevokedAt.Should().BeNull();
    }

    // ── FindByHashAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task FindByHashAsync_ReturnsNull_WhenHashNotFound()
    {
        // Arrange
        var clock = new FakeClock(BaseTime);
        await using var db = CreateDb();
        var repo = new RefreshTokenRepository(db, clock);

        // Act
        var result = await repo.FindByHashAsync("hash-that-was-never-stored");

        // Assert
        result.Should().BeNull();
    }
}
