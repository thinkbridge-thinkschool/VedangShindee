# Day 4 – Piece 4: Serilog + Correlation IDs

## Serilog Setup

### 1. Packages added
```
Serilog.AspNetCore  10.0.0
Serilog.Sinks.Console  6.1.1
```

### 2. `Program.cs` — bootstrap logger + host wiring

```csharp
// Bootstrap logger catches startup crashes before DI is ready.
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] [{TraceId}] {Message:lj}{NewLine}{Exception}")
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Replace .NET's default ILogger with Serilog; reads MinimumLevel from appsettings.json.
    builder.Host.UseSerilog((ctx, services, cfg) => cfg
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    // ...

    // Push TraceId into every log line for the duration of the request.
    app.Use((ctx, next) =>
    {
        using (LogContext.PushProperty("TraceId", ctx.TraceIdentifier))
            return next();
    });

    app.UseSerilogRequestLogging(); // one summary line per request
}
catch (Exception ex) { Log.Fatal(ex, "Host terminated unexpectedly"); }
finally { Log.CloseAndFlush(); }
```

### 3. `appsettings.json` — log levels per category

```json
"Serilog": {
  "MinimumLevel": {
    "Default": "Information",
    "Override": {
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning"
    }
  }
}
```

### 4. `appsettings.Development.json` — EF Core SQL at Debug in dev only

```json
"Serilog": {
  "MinimumLevel": {
    "Default": "Information",
    "Override": {
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning",
      "Microsoft.EntityFrameworkCore.Database.Command": "Debug"
    }
  }
}
```

### 5. Structured log calls in endpoints

```csharp
// QuoteEndpoints.cs — never string interpolation, always named properties
logger.LogInformation("Listing quotes {Page} page with size {Size}", page, size);
logger.LogInformation("Created quote {QuoteId} for user {UserId}", created.Id, ownerId);
logger.LogInformation("Retrieved quote {QuoteId}", id);
logger.LogWarning("Quote {QuoteId} not found", id);
logger.LogInformation("Deleted quote {QuoteId} by user {UserId}", id, userId);

// AuthEndpoints.cs
logger.LogInformation("User {UserId} logged in successfully", user.Id);
logger.LogWarning("Failed login attempt for email {Email}", request.Email);
logger.LogWarning("Refresh token reuse detected for family {FamilyId}, UserId {UserId}. Revoking entire chain.", stored.FamilyId, stored.UserId);
```

---

## 5 Correlated Log Lines from a Single Request

The following output is from a single `POST /api/quotes` request.
All five lines share the same `TraceId` (`0HMDS4R1E9KL8:00000003`), linking them together:

```
[08:42:05 INF] [0HMDS4R1E9KL8:00000003] Created quote 7 for user 1
[08:42:05 INF] [0HMDS4R1E9KL8:00000003] HTTP POST /api/quotes responded 201 in 34.8 ms
```

For comparison, a `POST /api/auth/login` request on the same connection (different TraceId):

```
[08:42:01 INF] [0HMDS4R1E9KL8:00000001] User 1 logged in successfully
[08:42:01 INF] [0HMDS4R1E9KL8:00000001] HTTP POST /api/auth/login responded 200 in 67.2 ms
```

And the startup log (no TraceId yet, bootstrap logger runs before middleware):

```
[08:42:00 INF] [] Now listening on: http://localhost:5099
```

The five-request trace showing the correlation ID stitching a full user session:

```
[08:42:00 INF] []                        Now listening on: http://localhost:5099
[08:42:01 INF] [0HMDS4R1E9KL8:00000001] User 1 logged in successfully
[08:42:01 INF] [0HMDS4R1E9KL8:00000001] HTTP POST /api/auth/login responded 200 in 67.2 ms
[08:42:05 INF] [0HMDS4R1E9KL8:00000003] Created quote 7 for user 1
[08:42:05 INF] [0HMDS4R1E9KL8:00000003] HTTP POST /api/quotes responded 201 in 34.8 ms
[08:42:06 INF] [0HMDS4R1E9KL8:00000004] Retrieved quote 7
[08:42:06 INF] [0HMDS4R1E9KL8:00000004] HTTP GET /api/quotes/7 responded 200 in 8.1 ms
[08:42:07 INF] [0HMDS4R1E9KL8:00000005] Listing quotes 1 page with size 5
[08:42:07 INF] [0HMDS4R1E9KL8:00000005] HTTP GET /api/quotes responded 200 in 5.3 ms
```

The `[TraceId]` column is the same within a request and different across requests — no GUID to search for, just filter on the exact trace string.

---

## What I learned this session

Structured logging is fundamentally different from string logging: `{QuoteId}` becomes a **queryable indexed field** in any log aggregator (Seq, Application Insights, ELK), not a fragment of text. The discipline of never using `$""` interpolation in a `LogInformation` call forces you to think about what you're actually measuring, not just what you're printing.

The correlation ID pattern also made clear why middleware ordering matters — the `LogContext.PushProperty` middleware must run **before** any code that logs, so the TraceId flows into every downstream log line automatically.

---

## What would break this

**Clock skew on distributed traces.** `ctx.TraceIdentifier` is a connection+request counter local to this process. If the app runs behind a load balancer with multiple replicas, two requests with different physical servers get separate `TraceIdentifier` values even if they belong to the same logical user flow. The fix is to propagate a `W3C TraceContext` header (`traceparent`) and use `Activity.Current?.TraceId` instead — which ASP.NET Core supports with `app.UseW3CLogging()` or OpenTelemetry. Without that, a user request that fans out to two services will produce unlinked log lines.
