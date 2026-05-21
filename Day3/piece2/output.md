# Day 3 – Piece 2: Authorization Policies & Claims

---

## Policy 1 — Claim-based: `can-edit-quotes`

**Definition** (`InfrastructureExtensions.cs`):
```csharp
options.AddPolicy("can-edit-quotes", p => p.RequireClaim("scope", "quotes.write"));
```

**Applied to** `POST /api/quotes`:
```csharp
group.MapPost("/", async (...) => { ... })
     .RequireAuthorization("can-edit-quotes");
```

**Login tokens now carry the scope claim** (`AuthEndpoints.cs`):
```csharp
new Claim("scope", "quotes.write")
```

---

## Policy 2 — Custom `IAuthorizationRequirement`: `can-delete-own-quote`

**Requirement** (`Authorization/OwnQuoteRequirement.cs`):
```csharp
public class OwnQuoteRequirement : IAuthorizationRequirement { }
```

**Handler** (`Authorization/OwnQuoteHandler.cs`):
```csharp
public class OwnQuoteHandler : AuthorizationHandler<OwnQuoteRequirement, Quote>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OwnQuoteRequirement requirement,
        Quote resource)
    {
        var sub = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
               ?? context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

        if (sub is not null && int.TryParse(sub, out var userId) && resource.OwnerId == userId)
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
```

**Definition** (`InfrastructureExtensions.cs`):
```csharp
options.AddPolicy("can-delete-own-quote", p => p.AddRequirements(new OwnQuoteRequirement()));
```

**Applied to** `DELETE /api/quotes/{id}` (resource-based — evaluated inside the endpoint):
```csharp
group.MapDelete("/{id:int}", async (int id, IQuoteRepository repository,
    IAuthorizationService authService, ClaimsPrincipal user, CancellationToken ct) =>
{
    var quote = await repository.GetByIdAsync(id, ct);
    if (quote is null) return Results.NotFound();

    var result = await authService.AuthorizeAsync(user, quote, "can-delete-own-quote");
    if (!result.Succeeded) return Results.Forbid();   // 403

    await repository.DeleteAsync(id, ct);
    return Results.NoContent();
}).RequireAuthorization();
```

---

## Tests showing 403 when policy fails (`AuthorizationPolicyTests.cs`)

```csharp
// Policy 1: 403 when scope claim is missing
[Fact]
public async Task CanEditQuotes_Fails_WhenScopeClaimMissing()
{
    var authService = BuildAuthService();
    var user = AuthenticatedUser(id: 1, scope: null);   // no scope claim

    var result = await authService.AuthorizeAsync(user, resource: null, "can-edit-quotes");

    Assert.False(result.Succeeded);   // → endpoint returns 403
}

// Policy 2: 403 when user tries to delete someone else's quote
[Fact]
public async Task CanDeleteOwnQuote_Fails_WhenQuoteOwnedByDifferentUser()
{
    var authService = BuildAuthService();
    var user = AuthenticatedUser(id: 1);
    var quote = new Quote { Id = 99, OwnerId = 2, Author = "Other", Text = "Not yours." };

    var result = await authService.AuthorizeAsync(user, quote, "can-delete-own-quote");

    Assert.False(result.Succeeded);   // → endpoint returns 403
}
```

All 10 tests pass:

```
Passed  CanEditQuotes_Fails_WhenScopeClaimMissing
Passed  CanEditQuotes_Succeeds_WhenScopeClaimPresent
Passed  CanDeleteOwnQuote_Fails_WhenQuoteOwnedByDifferentUser
Passed  CanDeleteOwnQuote_Fails_WhenQuoteHasNoOwner
Passed  CanDeleteOwnQuote_Succeeds_WhenUserOwnsQuote
Passed  CreateAsync_StampsCreatedAtFromClock
Passed  CreateAsync_DifferentClockTimes_ProduceDifferentTimestamps
Passed  ReuseDetection_RevokesEntireFamily_WhenRotatedTokenIsPresented
Passed  NormalRotation_DoesNotRevoke_SuccessorToken
Passed  ExpiredToken_IsRejected_WithoutFamilyRevocation
```

---

## Manual verification

**Login and save token:**
```powershell
$response = Invoke-RestMethod -Method Post `
  -Uri "http://localhost:5051/api/auth/login" `
  -ContentType "application/json" `
  -Body '{"email":"test@example.com","password":"password123"}'

$token = $response.access_token
```

**Create a quote (scope claim present → 201):**
```powershell
Invoke-RestMethod -Method Post `
  -Uri "http://localhost:5051/api/quotes" `
  -ContentType "application/json" `
  -Headers @{ Authorization = "Bearer $token" } `
  -Body '{"author":"Seneca","text":"Nusquam est qui ubique est."}'
```
```
id        : 1
author    : Seneca
text      : Nusquam est qui ubique est.
createdAt : 2026-05-21T08:54:40.9383297+00:00
ownerId   : 1
```

**List quotes (no auth needed):**
```powershell
Invoke-RestMethod "http://localhost:5051/api/quotes?page=1&size=10"
```

**Delete own quote (OwnerId matches → 204):**
```powershell
Invoke-RestMethod -Method Delete `
  -Uri "http://localhost:5051/api/quotes/1" `
  -Headers @{ Authorization = "Bearer $token" }
```

---

## What I learned

The thing that actually clicked was the difference between authentication and authorization — like, I knew they were different words but always used them interchangeably. Now I get it: the JWT just proves who you are, the policy decides what you're allowed to do. And using RequireClaim("scope", "quotes.write") instead of RequireRole("admin") makes way more sense because roles can change but the rule stays the same.

## What would break this

If someone logs in and their token doesn't have the scope claim for some reason, they'd get a 403 even though they're a valid user — that would be confusing to debug. Also the OwnerId thing — quotes created before I added that field have OwnerId = null, so nobody can delete them now, not even an admin. I didn't handle that case at all.
