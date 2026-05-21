# Piece 1 – Wire Entra ID as Identity Provider

## Program.cs auth setup

`AddInfrastructure` in [Extensions/InfrastructureExtensions.cs](QuotesApi/Extensions/InfrastructureExtensions.cs) now registers three schemes:

```csharp
services.AddAuthentication(MultiScheme)          // "MultiScheme" is the default
    .AddPolicyScheme(MultiScheme, "Local or Entra JWT", options =>
    {
        // Peek at the raw token before validation to choose the right handler.
        options.ForwardDefaultSelector = context =>
        {
            var auth = context.Request.Headers.Authorization.FirstOrDefault();
            if (auth?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true)
            {
                var raw = auth["Bearer ".Length..].Trim();
                var handler = new JwtSecurityTokenHandler();
                if (handler.CanReadToken(raw))
                {
                    var issuer = handler.ReadJwtToken(raw).Issuer;
                    if (issuer.StartsWith("https://login.microsoftonline.com/", ...) ||
                        issuer.StartsWith("https://sts.windows.net/", ...))
                        return EntraScheme;   // "EntraId"
                }
            }
            return LocalScheme;   // "LocalJwt"
        };
    })
    .AddJwtBearer(LocalScheme, options =>
    {
        // Existing HMAC-SHA256 symmetric key scheme for internal callers.
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer  = configuration["Jwt:Issuer"],
            ValidAudience = configuration["Jwt:Audience"],
            IssuerSigningKey = signingKey,
            ClockSkew = TimeSpan.Zero
        };
    })
    .AddJwtBearer(EntraScheme, options =>
    {
        // OIDC discovery at Authority/.well-known/openid-configuration fetches
        // Entra's public keys automatically — no manual key management needed.
        options.Authority = $"https://login.microsoftonline.com/{tenantId}/v2.0";
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = true,
            ValidAudience = clientId    // "cbd99da1-dee1-4a9c-9f82-16ffc5bb486e"
        };
    });
```

`appsettings.json` gains:
```json
"AzureAd": {
  "TenantId": "0a0aa63d-82d0-4ba1-b909-d7986ece4c4c",
  "ClientId": "cbd99da1-dee1-4a9c-9f82-16ffc5bb486e"
}
```

---

## curl with an Entra-issued token

### Step 1 – acquire the token

```bash
TOKEN=$(az account get-access-token --resource cbd99da1-dee1-4a9c-9f82-16ffc5bb486e \
        --query accessToken -o tsv)
```

### Step 2 – hit the protected endpoint

```bash
curl -s -o /dev/null -w "%{http_code}" \
  -X POST http://localhost:5051/api/quotes \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"author":"Marcus Aurelius","text":"The obstacle is the way."}'
```

Expected response: **201 Created**

---

## What I learned this session

The moment that clicked: `AddAuthentication` registers the *default* scheme, so by making the default a `PolicyScheme` I keep all existing code (`RequireAuthorization()`, `[Authorize]`) unchanged — they still resolve through the same single entry point, but the entry point now fans out to whichever handler owns the token. No endpoint changes needed at all.

The other thing worth keeping: `AddJwtBearer` with `Authority` set is essentially self-configuring. The middleware fetches `{Authority}/.well-known/openid-configuration`, discovers the JWKS endpoint, and rotates signing keys automatically. Compare that to the local scheme where you own the key forever.

---

## What would break this

| Scenario | Failure mode |
|---|---|
| `AzureAd:ClientId` / `TenantId` missing from config | `InvalidOperationException` at startup |
| API not exposed in the Entra app registration | Tokens issued with wrong `aud`; the `EntraId` handler rejects them (audience mismatch) |
| Entra v1 endpoint used (`/oauth2/token` instead of `/oauth2/v2.0/token`) | `iss` claim is `https://sts.windows.net/...` — covered by the fallback check in the selector |
| A guest user from a different tenant | `iss` won't match the configured tenant in the Authority discovery doc; token rejected |
| Network outage during first request | `EntraId` handler can't fetch JWKS → 500; keys are cached after first fetch, so this only affects cold start |
| Token signed with old Entra key after rotation | Handled automatically — the middleware re-fetches JWKS when an unknown key ID (`kid`) is seen |
