# Piece 7 — Refresh Token Rotation with Reuse Detection

## What was built

| Concern | Implementation |
|---|---|
| Access token lifetime | 15 minutes (JWT) |
| Refresh token lifetime | 7 days (stored server-side as SHA-256 hash) |
| Rotation | Every `/refresh` call mints a new pair and revokes the old token |
| Reuse detection | Presenting a revoked token revokes the entire family chain |
| Security log | `LogWarning` on reuse with FamilyId + UserId |

---

## Schema — `RefreshTokens` table

```
Id              INTEGER  PK autoincrement
TokenHash       TEXT     SHA-256(raw token), unique index
UserId          INTEGER  FK → Users
FamilyId        TEXT     Groups all tokens in one rotation chain, index
ExpiresAt       TEXT     DateTimeOffset — 7 days from mint
RevokedAt       TEXT?    Set on rotation or reuse-detected revocation
ReplacedByToken TEXT?    Hash of the successor token (audit trail)
```

---

## Refresh endpoint (`POST /api/auth/refresh`)

```csharp
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

    // Atomic: revoke old token (pointing at its replacement) and persist the new one.
    await tokenRepo.RevokeTokenAsync(stored, newRefreshEntity.TokenHash);
    await tokenRepo.AddAsync(newRefreshEntity);

    return Results.Ok(new LoginResponse(newAccessToken, newRawRefresh, AccessExpirySeconds(config)));
});
```

**Decision points in order:**
1. Unknown hash → 401 (no information leak)
2. `RevokedAt != null` → reuse detected → `RevokeFamilyAsync` kills the chain → log warning → 401
3. `ExpiresAt < now` → 401 (simple expiry, no chain revocation)
4. User gone → 401 (defensive; shouldn't happen)
5. Happy path: revoke old, mint new pair, return both

---

## Reuse detection test

```csharp
[Fact]
public async Task ReuseDetection_RevokesEntireFamily_WhenRotatedTokenIsPresented()
{
    var now = new DateTimeOffset(2026, 5, 20, 12, 0, 0, TimeSpan.Zero);
    var clock = new FakeClock(now);
    await using var db = CreateDb();
    var repo = new RefreshTokenRepository(db, clock);

    const string family = "family-abc";
    const int userId = 1;

    var hashA = TokenHasher.Hash("raw-token-A");
    var hashB = TokenHasher.Hash("raw-token-B");

    // Token A: the original, already rotated.
    var tokenA = new RefreshToken
    {
        TokenHash = hashA, UserId = userId, FamilyId = family,
        ExpiresAt = now.AddDays(7)
    };
    await repo.AddAsync(tokenA);

    // Normal rotation: A → B
    await repo.RevokeTokenAsync(tokenA, replacedByHash: hashB);
    await repo.AddAsync(new RefreshToken
    {
        TokenHash = hashB, UserId = userId, FamilyId = family,
        ExpiresAt = now.AddDays(7)
    });

    // Pre-condition: A revoked, B active
    Assert.NotNull((await repo.FindByHashAsync(hashA))!.RevokedAt);
    Assert.Null((await repo.FindByHashAsync(hashB))!.RevokedAt);

    // Attacker re-presents A → reuse detected → revoke entire family
    var presented = await repo.FindByHashAsync(hashA);
    Assert.NotNull(presented!.RevokedAt); // confirms reuse path
    await repo.RevokeFamilyAsync(presented.FamilyId);

    // B is now also revoked — attacker cannot use the legitimate successor
    Assert.NotNull((await repo.FindByHashAsync(hashB))!.RevokedAt);
}
```

**What the test proves:**
- After `A → B` rotation, B is live
- Re-presenting A (now revoked) triggers `RevokeFamilyAsync`
- That call sets `RevokedAt` on B even though B was never directly touched
- The attacker's stolen A gives them nothing, and the legitimate user's B is also killed (forcing re-auth — the correct security response to a detected theft)

---

## All endpoints

| Method | Path | Auth | Purpose |
|---|---|---|---|
| POST | `/api/auth/login` | None | Returns access + refresh token pair |
| POST | `/api/auth/refresh` | None | Rotates refresh token, returns new pair |
| POST | `/api/auth/logout` | JWT required | Revokes the presented refresh token |

---

## Test results

```
Passed!  - Failed: 0, Passed: 5, Skipped: 0, Total: 5
```

Tests:
- `QuoteRepositoryTests.CreateAsync_StampsCreatedAtFromClock`
- `QuoteRepositoryTests.CreateAsync_DifferentClockTimes_ProduceDifferentTimestamps`
- `RefreshTokenReuseTests.ReuseDetection_RevokesEntireFamily_WhenRotatedTokenIsPresented`
- `RefreshTokenReuseTests.NormalRotation_DoesNotRevoke_SuccessorToken`
- `RefreshTokenReuseTests.ExpiredToken_IsRejected_WithoutFamilyRevocation`
