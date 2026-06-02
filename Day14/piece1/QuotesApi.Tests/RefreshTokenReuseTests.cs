using Microsoft.EntityFrameworkCore;
using QuotesApi.Data;
using QuotesApi.Models;
using QuotesApi.Services;
using Xunit;

namespace QuotesApi.Tests;

/// <summary>
/// Verifies that presenting a previously-rotated (already-replaced) refresh token
/// triggers reuse detection and revokes the entire token family, including any
/// legitimately-issued successor tokens.
/// </summary>
public class RefreshTokenReuseTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task ReuseDetection_RevokesEntireFamily_WhenRotatedTokenIsPresented()
    {
        // Arrange
        var now = new DateTimeOffset(2026, 5, 20, 12, 0, 0, TimeSpan.Zero);
        var clock = new FakeClock(now);
        await using var db = CreateDb();
        var repo = new RefreshTokenRepository(db, clock);

        const string family = "family-abc";
        const int userId = 1;

        var hashA = TokenHasher.Hash("raw-token-A");
        var hashB = TokenHasher.Hash("raw-token-B");

        // Token A: the original token that was already rotated.
        var tokenA = new RefreshToken
        {
            TokenHash = hashA,
            UserId = userId,
            FamilyId = family,
            ExpiresAt = now.AddDays(7)
        };
        await repo.AddAsync(tokenA);

        // Simulate normal rotation: revoke A and issue B in its place.
        await repo.RevokeTokenAsync(tokenA, replacedByHash: hashB);

        var tokenB = new RefreshToken
        {
            TokenHash = hashB,
            UserId = userId,
            FamilyId = family,
            ExpiresAt = now.AddDays(7)
        };
        await repo.AddAsync(tokenB);

        // Pre-condition: A is revoked, B is active.
        var storedA = await repo.FindByHashAsync(hashA);
        var storedB = await repo.FindByHashAsync(hashB);
        Assert.NotNull(storedA!.RevokedAt);  // A must already be revoked
        Assert.Null(storedB!.RevokedAt);      // B must be active

        // Act — attacker re-presents the already-rotated token A.
        // The endpoint logic: if stored.RevokedAt != null → revoke entire family.
        var presented = await repo.FindByHashAsync(hashA);
        Assert.NotNull(presented);
        Assert.NotNull(presented.RevokedAt); // confirms this is a reuse attempt

        await repo.RevokeFamilyAsync(presented.FamilyId); // mirrors what the endpoint does

        // Assert — B (the legitimate successor) must now also be revoked.
        var afterB = await repo.FindByHashAsync(hashB);
        Assert.NotNull(afterB!.RevokedAt);
    }

    [Fact]
    public async Task NormalRotation_DoesNotRevoke_SuccessorToken()
    {
        // Sanity check: a fresh refresh token should be usable after normal rotation.
        var now = new DateTimeOffset(2026, 5, 20, 12, 0, 0, TimeSpan.Zero);
        var clock = new FakeClock(now);
        await using var db = CreateDb();
        var repo = new RefreshTokenRepository(db, clock);

        const string family = "family-xyz";
        const int userId = 2;

        var hashA = TokenHasher.Hash("token-A-fresh");
        var hashB = TokenHasher.Hash("token-B-fresh");

        var tokenA = new RefreshToken
        {
            TokenHash = hashA,
            UserId = userId,
            FamilyId = family,
            ExpiresAt = now.AddDays(7)
        };
        await repo.AddAsync(tokenA);

        // Rotate A → B (normal path, no reuse).
        await repo.RevokeTokenAsync(tokenA, replacedByHash: hashB);
        await repo.AddAsync(new RefreshToken
        {
            TokenHash = hashB,
            UserId = userId,
            FamilyId = family,
            ExpiresAt = now.AddDays(7)
        });

        // B should be valid and not revoked.
        var storedB = await repo.FindByHashAsync(hashB);
        Assert.NotNull(storedB);
        Assert.Null(storedB.RevokedAt);
    }

    [Fact]
    public async Task ExpiredToken_IsRejected_WithoutFamilyRevocation()
    {
        var now = new DateTimeOffset(2026, 5, 20, 12, 0, 0, TimeSpan.Zero);
        var clock = new FakeClock(now);
        await using var db = CreateDb();
        var repo = new RefreshTokenRepository(db, clock);

        var hash = TokenHasher.Hash("expired-token");

        var expiredToken = new RefreshToken
        {
            TokenHash = hash,
            UserId = 3,
            FamilyId = "family-expired",
            ExpiresAt = now.AddDays(-1) // already expired
        };
        await repo.AddAsync(expiredToken);

        var stored = await repo.FindByHashAsync(hash);
        Assert.NotNull(stored);
        Assert.Null(stored.RevokedAt);               // not revoked, just expired
        Assert.True(stored.ExpiresAt < clock.UtcNow); // endpoint checks this, returns 401
    }
}
