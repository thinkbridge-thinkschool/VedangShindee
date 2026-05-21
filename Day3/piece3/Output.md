# Day 3 – Piece 3: Lock Down the API End-to-End

**PR verdict: this is solid**

---

## What this piece adds

Piece 3 is the capstone that ties together everything from Pieces 1 and 2. No new API features were needed — the dual-scheme auth, refresh-token rotation, and policies were already wired. The work here was writing the integration-test harness that proves all of it actually works over a real HTTP stack, and adding a CI workflow so the tests run automatically on every push.

---

## 1. Dual-scheme JWT (already in place, verified by integration tests)

`MultiScheme` (policy scheme) peeks at the incoming token's `iss` claim and routes to either:
- **`LocalJwt`** — HMAC-SHA256 symmetric key, issued by the API itself for internal callers
- **`EntraId`** — RSA public keys fetched via OIDC discovery from `login.microsoftonline.com`, for SPA users

```csharp
options.ForwardDefaultSelector = context =>
{
    var issuer = handler.ReadJwtToken(raw).Issuer;
    if (issuer.StartsWith("https://login.microsoftonline.com/") ||
        issuer.StartsWith("https://sts.windows.net/"))
        return EntraScheme;
    return LocalScheme;
};
```

---

## 2. Refresh-token rotation with reuse detection (already in place, verified)

Every `POST /api/auth/refresh` call:
1. Revokes the presented token (`RevokedAt = now`, `ReplacedByToken = new hash`)
2. Issues a brand-new token in the same family

If a **revoked** token is presented again (replay / token theft):
```csharp
if (stored.RevokedAt is not null)
{
    await tokenRepo.RevokeFamilyAsync(stored.FamilyId); // kill every live token in the chain
    return Results.Unauthorized();
}
```

---

## 3. Policies on every mutating endpoint

| Endpoint | Policy |
|---|---|
| `POST /api/quotes` | `can-edit-quotes` (claim: `scope=quotes.write`) |
| `DELETE /api/quotes/{id}` | `RequireAuthorization` + `can-delete-own-quote` (resource-based: `OwnerId == sub`) |
| `POST /api/auth/logout` | `RequireAuthorization` (any authenticated caller) |

Public (no auth): `GET /api/quotes`, `GET /api/quotes/{id}`, `POST /api/auth/login`, `POST /api/auth/refresh` (refresh token in body acts as credential).

---

## 4. Integration tests (`IntegrationTests.cs`)

`WebApplicationFactory<Program>` spins up the real ASP.NET Core pipeline in-process. The test DB is a **SQLite in-memory** database (same provider as production, no EF Core internal service conflict) kept alive by a single `SqliteConnection` for the test class's lifetime.

### Test factory setup

```csharp
public sealed class QuotesApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _keepAlive = new("DataSource=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _keepAlive.Open();
        builder.ConfigureTestServices(services =>
        {
            // Remove ALL EF Core descriptors referencing AppDbContext —
            // including the internal IDbContextOptionsConfiguration<AppDbContext>
            // that carries the SQLite file-path setup. Leaving it alongside
            // the in-memory registration triggers EF Core's "only one provider" guard.
            var toRemove = services
                .Where(d =>
                    d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                    d.ServiceType == typeof(AppDbContext) ||
                    (d.ServiceType.IsGenericType &&
                     d.ServiceType.GetGenericArguments().Any(t => t == typeof(AppDbContext))))
                .ToList();
            foreach (var d in toRemove) services.Remove(d);

            services.AddDbContext<AppDbContext>(opts => opts.UseSqlite(_keepAlive));
        });
    }
}
```

JWTs are minted directly in tests using the same key/issuer/audience as `appsettings.json` — no round-trip to the login endpoint needed for most scenarios.

### Five scenarios, all passing

```
Passed  PostQuotes_NoToken_Returns401                         → anonymous caller blocked
Passed  PostQuotes_ValidTokenWithoutScope_Returns403          → authenticated but policy fails
Passed  PostQuotes_ValidTokenWithScope_Returns201             → happy path, quote created
Passed  PostQuotes_ExpiredToken_Returns401                    → ClockSkew=Zero enforced
Passed  Refresh_ReuseOfRotatedToken_Returns401AndRevokesSuccessor → full chain revoked on reuse
```

Full suite: **15/15 passing** (10 pre-existing unit tests + 5 new integration tests).

---

## 5. CI workflow (`.github/workflows/day3-piece3-tests.yml`)

Triggers on any push or PR touching `Day3/piece3/**`.

```yaml
steps:
  - uses: actions/checkout@v4
  - uses: actions/setup-dotnet@v4
    with:
      dotnet-version: '10.0.x'
  - run: dotnet restore
  - run: dotnet build --no-restore --configuration Release
  - run: dotnet test QuotesApi.Tests/QuotesApi.Tests.csproj --no-build --configuration Release --verbosity normal
```

---

## What I learned

The integration test `WebApplicationFactory` setup surfaced something I wouldn't have caught unit-testing: EF Core registers an internal `IDbContextOptionsConfiguration<TContext>` alongside `DbContextOptions<TContext>`, and removing only the latter leaves the SQLite provider action intact. The second `AddDbContext` call then sees both provider setups and throws. Filtering by generic type argument catches both descriptors cleanly.

The other thing: `ClockSkew = TimeSpan.Zero` is easy to set but easy to forget to test. The expired-token integration test proves it's actually enforced end-to-end — not just in configuration.

---

## What would break this

| Scenario | Failure mode |
|---|---|
| `Jwt:Key` shorter than 32 bytes in production | `ArgumentOutOfRangeException` at startup; HMAC-SHA256 requires 256-bit key |
| Rotating to a new signing key without a transition period | All existing tokens immediately invalid; clients get 401 with no warning |
| Entra OIDC metadata endpoint unreachable at cold start | First Entra-token request fails; keys are cached after first successful fetch |
| SQLite WAL mode not enabled in production | Write contention under load; refresh-token rotation becomes a bottleneck |
| Refresh endpoint hit by many concurrent requests with same token | Race condition: two workers could both see `RevokedAt == null` before either writes; fix with DB-level unique constraint or optimistic concurrency check |
