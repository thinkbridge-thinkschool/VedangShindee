# Smoke Test — QuotesApi

**Base URL:** `https://ca-api-7hvk5aaj2zuug.livelydune-368712a9.centralindia.azurecontainerapps.io`
**Date:** 2026-05-23
**Stack:** ASP.NET Core 10 · EF Core (SQLite) · JWT auth · Refresh token rotation · Serilog · OpenTelemetry · Polly resilience

---

## GET /health

```
GET /health
→ 200 OK
```
```json
{"status":"healthy"}
```

`Program.cs`:
```csharp
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
```

---

## POST /api/auth/login — valid

```
POST /api/auth/login
Body: {"email":"test@example.com","password":"password123"}
→ 200 OK
```
```json
{
  "access_token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.<payload>.<sig>",
  "refresh_token": "GDnswGNCBYa2cNCbvxzr0Y4Cu8CIyEzMzN1K4CDWESk=",
  "expires_in": 900
}
```

Decoded JWT payload:
```json
{"sub":"1","email":"test@example.com","scope":"quotes.write","exp":1779542386,"iss":"QuotesApi","aud":"QuotesApi"}
```

Seed user in `Program.cs`:
```csharp
db.Users.Add(new User
{
    Email = "test@example.com",
    PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123")
});
```

`AuthEndpoints.cs`:
```csharp
var user = await db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
    return Results.Unauthorized();

var accessToken = MintAccessToken(user, jwt);
var (rawRefresh, refreshEntity) = MintRefreshToken(user.Id, Guid.NewGuid().ToString(), clock, jwt);
await tokenRepo.AddAsync(refreshEntity);
return Results.Ok(new LoginResponse(accessToken, rawRefresh, (int)jwt.AccessTokenLifetime.TotalSeconds));
```

Access token lifetime: `00:15:00` (900 s). Refresh token lifetime: `7.00:00:00`. Both in `appsettings.json` under `Jwt`.

---

## POST /api/auth/login — wrong credentials

```
POST /api/auth/login
Body: {"email":"nobody@x.com","password":"wrong"}
→ 401 Unauthorized
Body: (empty)
```

```csharp
if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
    return Results.Unauthorized();
```

---

## GET /api/quotes?page=1&size=10

```
GET /api/quotes?page=1&size=10
→ 200 OK
```
```json
[
  {"id":1,"author":"Marcus Aurelius","text":"The impediment to action advances action.","createdAt":"2026-05-23T11:32:57.8959321+00:00","ownerId":1},
  {"id":2,"author":"Seneca","text":"We suffer more in imagination than in reality.","createdAt":"2026-05-23T11:32:58.4135841+00:00","ownerId":1},
  {"id":3,"author":"Marcus Aurelius","text":"The impediment to action advances action.","createdAt":"2026-05-23T13:05:04.9918728+00:00","ownerId":1}
]
```

`QuoteEndpoints.cs`:
```csharp
group.MapGet("/", async (int page, int size, IQuoteRepository repository, ILoggerFactory loggerFactory, CancellationToken ct) =>
{
    var quotes = await repository.GetPagedAsync(page, size, ct);
    return Results.Ok(quotes);
});
```

`QuoteRepository.cs`:
```csharp
public async Task<List<Quote>> GetPagedAsync(int page, int size, CancellationToken cancellationToken)
{
    return await _db.Quotes
        .Skip((page - 1) * size)
        .Take(size)
        .ToListAsync(cancellationToken);
}
```

---

## GET /api/quotes (no params)

```
GET /api/quotes/
→ 400 Bad Request
Body: (ASP.NET model binding error — page and size are required query params)
```

`page` and `size` are required in the handler signature. ASP.NET returns 400 before the handler runs if either is missing.

---

## GET /api/quotes/3

```
GET /api/quotes/3
→ 200 OK
```
```json
{"id":3,"author":"Marcus Aurelius","text":"The impediment to action advances action.","createdAt":"2026-05-23T13:05:04.9918728+00:00","ownerId":1}
```

`QuoteRepository.cs`:
```csharp
public async Task<Quote?> GetByIdAsync(int id, CancellationToken cancellationToken)
{
    return await _db.Quotes.FirstOrDefaultAsync(q => q.Id == id, cancellationToken);
}
```

---

## GET /api/quotes/99999

```
GET /api/quotes/99999
→ 404 Not Found
Body: (empty)
```

`QuoteEndpoints.cs`:
```csharp
var quote = await repository.GetByIdAsync(id, ct);
if (quote is null) return Results.NotFound();
```

---

## POST /api/quotes — no auth

```
POST /api/quotes/
Body: {"author":"Anon","text":"Hello world"}
→ 401 Unauthorized
Body: (empty)
```

`QuoteEndpoints.cs`:
```csharp
group.MapPost("/", ...).RequireAuthorization("can-edit-quotes");
```

---

## POST /api/quotes — authenticated

```
POST /api/quotes/
Authorization: Bearer <token>
Body: {"author":"Marcus Aurelius","text":"The impediment to action advances action."}
→ 201 Created
```
```json
{"id":6,"author":"Marcus Aurelius","text":"The impediment to action advances action.","createdAt":"2026-05-23T14:20:00.0000000+00:00","ownerId":1}
```

`QuoteEndpoints.cs` — OwnerId is extracted from the JWT `sub` claim:
```csharp
var sub = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
var ownerId = sub is not null && int.TryParse(sub, out var id) ? (int?)id : null;
var quote = new Quote { Author = request.Author, Text = request.Text, OwnerId = ownerId };
var created = await repository.CreateAsync(quote, ct);
return Results.Created($"/api/quotes/{created.Id}", created);
```

`QuoteRepository.cs`:
```csharp
public async Task<Quote> CreateAsync(Quote quote, CancellationToken cancellationToken)
{
    quote.CreatedAt = _clock.UtcNow;
    _db.Quotes.Add(quote);
    await _db.SaveChangesAsync(cancellationToken);
    return quote;
}
```

---

## POST /api/quotes — empty author/text

```
POST /api/quotes/
Authorization: Bearer <token>
Body: {"author":"","text":""}
→ 400 Bad Request
```
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "author": ["Author is required"],
    "text": ["Text is required"]
  },
  "traceId": "00-af3df4396e5ee729293f2e0302e6e467-f3b085978e87e2d5-01"
}
```

`QuoteValidator.cs`:
```csharp
public Dictionary<string, string[]> Validate(CreateQuoteRequest request)
{
    var errors = new Dictionary<string, string[]>();
    if (string.IsNullOrWhiteSpace(request.Author))
        errors["author"] = ["Author is required"];
    if (string.IsNullOrWhiteSpace(request.Text))
        errors["text"] = ["Text is required"];
    return errors;
}
```

`QuoteEndpoints.cs`:
```csharp
var errors = validator.Validate(request);
if (errors.Count > 0) return Results.ValidationProblem(errors);
```

---

## DELETE /api/quotes/6 — own quote, authenticated

```
DELETE /api/quotes/6
Authorization: Bearer <token>
→ 204 No Content
Body: (empty)
```

`OwnQuoteHandler.cs` checks the JWT `sub` claim against `quote.OwnerId`:
```csharp
var sub = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
       ?? context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

if (sub is not null && int.TryParse(sub, out var userId) && resource.OwnerId == userId)
    context.Succeed(requirement);
```

`QuoteEndpoints.cs`:
```csharp
var result = await authService.AuthorizeAsync(user, quote, "can-delete-own-quote");
if (!result.Succeeded) return Results.Forbid();
await repository.DeleteAsync(id, ct);
return Results.NoContent();
```

---

## DELETE /api/quotes/6 — already deleted

```
DELETE /api/quotes/6
Authorization: Bearer <token>
→ 404 Not Found
Body: (empty)
```

---

## DELETE /api/quotes/99999 — non-existent

```
DELETE /api/quotes/99999
Authorization: Bearer <token>
→ 404 Not Found
Body: (empty)
```

---

## DELETE /api/quotes/1 — no auth

```
DELETE /api/quotes/1
→ 401 Unauthorized
Body: (empty)
```

```csharp
group.MapDelete("/{id:int}", ...).RequireAuthorization();
```

---

## POST /api/auth/refresh — valid token

```
POST /api/auth/refresh
Body: {"refresh_token":"GDnswGNCBYa2cNCbvxzr0Y4Cu8CIyEzMzN1K4CDWESk="}
→ 200 OK
```
```json
{
  "access_token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.<payload>.<sig>",
  "refresh_token": "Nq5doH2SUvaVrA8JL7q0XEK0G9gUmHKXY0677BRrAHI=",
  "expires_in": 900
}
```

`AuthEndpoints.cs` — old token revoked, new token issued atomically:
```csharp
var newAccessToken = MintAccessToken(user, jwt);
var (newRawRefresh, newRefreshEntity) = MintRefreshToken(user.Id, stored.FamilyId, clock, jwt);
await tokenRepo.RevokeTokenAsync(stored, newRefreshEntity.TokenHash);
await tokenRepo.AddAsync(newRefreshEntity);
return Results.Ok(new LoginResponse(newAccessToken, newRawRefresh, (int)jwt.AccessTokenLifetime.TotalSeconds));
```

---

## POST /api/auth/refresh — reused (rotated) token

```
POST /api/auth/refresh
Body: {"refresh_token":"GDnswGNCBYa2cNCbvxzr0Y4Cu8CIyEzMzN1K4CDWESk="}  ← same old token
→ 401 Unauthorized
Body: (empty — entire token family revoked)
```

`AuthEndpoints.cs`:
```csharp
if (stored.RevokedAt is not null)
{
    logger.LogWarning(
        "Refresh token reuse detected for family {FamilyId}, UserId {UserId}. Revoking entire chain.",
        stored.FamilyId, stored.UserId);
    await tokenRepo.RevokeFamilyAsync(stored.FamilyId);
    return Results.Unauthorized();
}
```

---

## POST /api/auth/refresh — invalid token

```
POST /api/auth/refresh
Body: {"refresh_token":"totally-fake-token"}
→ 401 Unauthorized
Body: (empty)
```

```csharp
var incomingHash = TokenHasher.Hash(request.RefreshToken);
var stored = await tokenRepo.FindByHashAsync(incomingHash);
if (stored is null) return Results.Unauthorized();
```

---

## POST /api/auth/logout — no auth

```
POST /api/auth/logout
Body: {"refresh_token":"Nq5doH2SUvaVrA8JL7q0XEK0G9gUmHKXY0677BRrAHI="}
→ 401 Unauthorized
Body: (empty)
```

```csharp
app.MapPost("/api/auth/logout", ...).RequireAuthorization();
```

---

## POST /api/auth/logout — authenticated

```
POST /api/auth/logout
Authorization: Bearer <new-access-token>
Body: {"refresh_token":"Nq5doH2SUvaVrA8JL7q0XEK0G9gUmHKXY0677BRrAHI="}
→ 204 No Content
Body: (empty)
```

`AuthEndpoints.cs`:
```csharp
var hash = TokenHasher.Hash(request.RefreshToken);
var stored = await tokenRepo.FindByHashAsync(hash);
if (stored is not null && stored.RevokedAt is null)
    await tokenRepo.RevokeTokenAsync(stored, replacedByHash: null);
return Results.NoContent();
```

---

## POST /api/auth/refresh — after logout (revoked)

```
POST /api/auth/refresh
Body: {"refresh_token":"Nq5doH2SUvaVrA8JL7q0XEK0G9gUmHKXY0677BRrAHI="}
→ 401 Unauthorized
Body: (empty)
```

---

## Bug — Wrong Field Name → 500

```
POST /api/auth/refresh
Body: {"refreshToken":"GDnswGNCBYa2cNCbvxzr0Y4Cu8CIyEzMzN1K4CDWESk="}
→ 500 Internal Server Error
```
```json
{"title":"Server Error","status":500,"detail":"Value cannot be null. (Parameter 's')"}
```

**Root cause:** `RefreshRequest` binds via `[JsonPropertyName("refresh_token")]`. Sending `refreshToken` (camelCase) leaves the property as `null`. `TokenHasher.Hash(null)` calls `Encoding.UTF8.GetBytes(null)` → `ArgumentNullException`.

`Models/RefreshRequest.cs`:
```csharp
public record RefreshRequest(
    [property: JsonPropertyName("refresh_token")] string RefreshToken);
```

`Services/TokenHasher.cs`:
```csharp
public static string Hash(string rawToken)
{
    var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)); // throws if null
    return Convert.ToHexString(bytes).ToLowerInvariant();
}
```

**Fix** — null-guard in the refresh endpoint before hashing:
```csharp
if (string.IsNullOrEmpty(request.RefreshToken))
    return Results.Unauthorized();
```

---

## Summary

| Endpoint | Status | Result |
|---|---|---|
| GET /health | 200 | Pass |
| POST /api/auth/login (valid) | 200 | Pass |
| POST /api/auth/login (wrong creds) | 401 | Pass |
| GET /api/quotes?page=1&size=10 | 200 | Pass |
| GET /api/quotes (no params) | 400 | Pass |
| GET /api/quotes/3 | 200 | Pass |
| GET /api/quotes/99999 | 404 | Pass |
| POST /api/quotes (no auth) | 401 | Pass |
| POST /api/quotes (authenticated) | 201 | Pass |
| POST /api/quotes (validation error) | 400 | Pass |
| DELETE /api/quotes/6 (own, authed) | 204 | Pass |
| DELETE /api/quotes/6 (already deleted) | 404 | Pass |
| DELETE /api/quotes/99999 | 404 | Pass |
| DELETE /api/quotes/1 (no auth) | 401 | Pass |
| POST /api/auth/refresh (valid) | 200 | Pass |
| POST /api/auth/refresh (reuse) | 401 | Pass |
| POST /api/auth/refresh (invalid) | 401 | Pass |
| POST /api/auth/logout (no auth) | 401 | Pass |
| POST /api/auth/logout (authed) | 204 | Pass |
| POST /api/auth/refresh (after logout) | 401 | Pass |
| POST /api/auth/refresh (wrong field) | **500** | **Bug** |

**20/21 pass. 1 known bug: null `RefreshToken` reaches `TokenHasher.Hash` and throws 500 instead of returning 401.**

Output - azd up

Provisioning and deploying (azd up)
Packaging overlaps with provisioning for faster execution.

  (-) Skipped: Didn't find new changes.

  Service  Status        Duration
  ───────  ────────────  ──────────
  ● api      Done          1m24s
  - Endpoint: https://ca-api-7hvk5aaj2zuug.livelydune-368712a9.centralindia.azurecontainerapps.io/ 

