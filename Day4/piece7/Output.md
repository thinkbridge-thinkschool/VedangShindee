# Piece 7 — Configuration Done Right

## JwtOptions Class

```csharp
// QuotesApi/Options/JwtOptions.cs
namespace QuotesApi.Options;

public record JwtOptions
{
    // Secret: never commit a real value here. Local dev: dotnet user-secrets set "Jwt:Key" "..."
    public string Key { get; init; } = string.Empty;
    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public TimeSpan AccessTokenLifetime { get; init; } = TimeSpan.FromMinutes(15);
    public TimeSpan RefreshTokenLifetime { get; init; } = TimeSpan.FromDays(7);
}
```

## appsettings.json — Jwt Section

```json
"Jwt": {
  "Key": "QuotesApi-Dev-SigningKey-ChangeThisInProduction-MustBe32BytesMin",
  "Issuer": "QuotesApi",
  "Audience": "QuotesApi",
  "AccessTokenLifetime": "00:15:00",
  "RefreshTokenLifetime": "7.00:00:00"
}
```

`Key` is a local-dev placeholder only. In production, inject it via an environment variable (`Jwt__Key`) or a Key Vault reference — never commit a real signing secret.

## DI Registration (InfrastructureExtensions.cs)

```csharp
// Bind once; the DI container resolves IOptions<JwtOptions> anywhere it is injected.
services.Configure<JwtOptions>(configuration.GetSection("Jwt"));

// Eager snapshot at startup for bearer validation setup (runs once, not per-request).
var jwt = configuration.GetSection("Jwt").Get<JwtOptions>()
    ?? throw new InvalidOperationException("Jwt configuration section is missing.");
```

`services.Configure<T>` registers the binding with the DI container.
`GetSection("Jwt").Get<JwtOptions>()` is a one-time eager read used inside `AddInfrastructure` itself to configure the JWT bearer middleware at startup — it does not go through `IOptions<T>`.

## Injecting in a Service (AuthEndpoints.cs)

```csharp
app.MapPost("/api/auth/login", async (
    LoginRequest request,
    AppDbContext db,
    IRefreshTokenRepository tokenRepo,
    IClock clock,
    IOptions<JwtOptions> jwtOptions,   // <-- injected by the DI container
    ILogger<Program> logger) =>
{
    var jwt = jwtOptions.Value;         // <-- typed, validated, no magic strings
    var accessToken = MintAccessToken(user, jwt);
    // ...
    return Results.Ok(new LoginResponse(accessToken, rawRefresh,
        (int)jwt.AccessTokenLifetime.TotalSeconds));
});
```

`IOptions<JwtOptions>` is a singleton — the value is read once at first access and cached. This is the right choice for JWT settings that do not change while the process is running.

---

## What did I learn this session?

The `IOptions` pattern separates *what* configuration means (the typed class) from *how* it is delivered (env vars, JSON files, Key Vault). The call site never has magic strings like `config["Jwt:ExpiresInMinutes"]`; it just reads a strongly typed `TimeSpan`. That also means a misconfigured value fails at startup with a clear message, not silently at the first request.

Using `TimeSpan` fields (`"00:15:00"`) instead of bare integers (`ExpiresInMinutes: 15`) also forces the intent into the config file — you can't accidentally feed minutes where seconds are expected.

## What would break this?

**Missing or blank `Jwt:Key` at startup.** The guard in `AddInfrastructure` throws, so the process refuses to start rather than running with an unsigned token. That is the desired behaviour — fail fast rather than issuing tokens that cannot be verified.

**`IOptions<T>` is a singleton snapshot.** If `Key` or lifetimes were rotated in Key Vault without restarting the process, the running app would keep using the old values. For secrets that must hot-rotate, `IOptionsMonitor<T>` is needed — it re-reads on config-change notifications. The trade-off is that signing key rotation via `IOptionsMonitor` requires ensuring in-flight tokens are still verifiable during the overlap window.
