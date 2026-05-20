# Piece 7 — Refresh Token Rotation with Reuse Detection


Refresh Endpoint -

//QuotesApi/Endpoints/AuthEndpoints.cs

app.MapPost("/api/auth/refresh", async (
    RefreshRequest request,
    IRefreshTokenRepository tokenRepo,
    IClock clock,
    IConfiguration config,
    ILoggerFactory loggerFactory,
    AppDbContext db) =>
{
    var logger = loggerFactory.CreateLogger("QuotesApi.Auth");
    var incomingHash = TokenHasher.Hash(request.RefreshToken);
    var stored = await tokenRepo.FindByHashAsync(incomingHash);

    if (stored is null)
        return Results.Unauthorized();

    // Reuse detected: token was already rotated away — revoke entire family.
    if (stored.RevokedAt is not null)
    {
        logger.LogWarning(
            "Refresh token reuse detected for family {FamilyId}, UserId {UserId}. Revoking entire chain.",
            stored.FamilyId, stored.UserId);

        await tokenRepo.RevokeFamilyAsync(stored.FamilyId);
        return Results.Unauthorized();
    }

    if (stored.ExpiresAt < clock.UtcNow)
        return Results.Unauthorized();

    var user = await db.Users.FindAsync(stored.UserId);
    if (user is null)
        return Results.Unauthorized();

    var newAccessToken = MintAccessToken(user, config);
    var (newRawRefresh, newRefreshEntity) = MintRefreshToken(user.Id, stored.FamilyId, clock, config);

    // Revoke old token (pointing at its replacement) and persist the new one.
    await tokenRepo.RevokeTokenAsync(stored, newRefreshEntity.TokenHash);
    await tokenRepo.AddAsync(newRefreshEntity);

    return Results.Ok(new LoginResponse(newAccessToken, newRawRefresh, AccessExpirySeconds(config)));
});

-----------------------------------------------------------------------


Test — Reuse Detection Kills the Chain - 

// QuotesApi.Tests/RefreshTokenReuseTests.cs
[Fact]
public async Task ReuseDetection_RevokesEntireFamily_WhenRotatedTokenIsPresented()
{
    // Arrange
    var now = new DateTimeOffset(2026, 5, 20, 12, 0, 0, TimeSpan.Zero);
    var clock = new FakeClock(now);
    await using var db = new AppDbContext(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    var repo = new RefreshTokenRepository(db, clock);

    const string family = "family-abc";

    var hashA = TokenHasher.Hash("raw-token-A");  // original token
    var hashB = TokenHasher.Hash("raw-token-B");  // successor after rotation

    // Issue token A
    var tokenA = new RefreshToken
        { TokenHash = hashA, UserId = 1, FamilyId = family, ExpiresAt = now.AddDays(7) };
    await repo.AddAsync(tokenA);

    // Normal rotation: A → B (A gets revoked, B is issued)
    await repo.RevokeTokenAsync(tokenA, replacedByHash: hashB);
    await repo.AddAsync(new RefreshToken
        { TokenHash = hashB, UserId = 1, FamilyId = family, ExpiresAt = now.AddDays(7) });

    // Pre-condition: A is revoked, B is active
    Assert.NotNull((await repo.FindByHashAsync(hashA))!.RevokedAt);
    Assert.Null((await repo.FindByHashAsync(hashB))!.RevokedAt);

    // Act — attacker replays the already-rotated token A
    var presented = await repo.FindByHashAsync(hashA);
    Assert.NotNull(presented!.RevokedAt);           // confirms reuse path
    await repo.RevokeFamilyAsync(presented.FamilyId); // what the endpoint does

    // Assert — B is now also revoked; attacker's replay killed the entire chain
    Assert.NotNull((await repo.FindByHashAsync(hashB))!.RevokedAt);
}


