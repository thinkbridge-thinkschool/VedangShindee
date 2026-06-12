using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using QuotesApi.Authorization;
using QuotesApi.BackgroundServices;
using QuotesApi.Commands;
using QuotesApi.Data;
using QuotesApi.Middleware;
using QuotesApi.Options;
using QuotesApi.Queries;
using QuotesApi.Repositories;
using QuotesApi.Resilience;
using QuotesApi.Services;
using QuotesApi.Telemetry;

namespace QuotesApi.Extensions;

public static class InfrastructureExtensions
{
    private const string LocalScheme = "LocalJwt";
    private const string EntraScheme = "EntraId";
    private const string MultiScheme = "MultiScheme";

    public static void AddInfrastructure(this IServiceCollection services, IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddServiceBus(configuration);

        // ── Resilience demo ──────────────────────────────────────────────────────────────────
        // Shared singleton: the chaos toggle + circuit-breaker state are read/written here.
        services.AddSingleton<ResilienceDemoState>();

        // FakeExternalServiceHandler is a terminal DelegatingHandler — transient per HttpClient.
        services.AddTransient<FakeExternalServiceHandler>();

        // Typed HTTP client wrapped with a four-layer Polly pipeline (outermost → innermost):
        //   1. Bulkhead  — ConcurrencyLimiter: max 2 parallel calls, queue of 4
        //   2. Retry     — exponential backoff, idempotent GET operations only, 3 attempts
        //   3. Circuit breaker — opens at 60% failure ratio; 15 s break; min 3 throughput
        //   4. Timeout   — 2 s per attempt (innermost, closest to the wire)
        //
        // AddResilienceHandler returns IHttpResiliencePipelineBuilder (not IHttpClientBuilder),
        // so capture the builder first and call AddHttpMessageHandler separately.
        var externalClientBuilder = services.AddHttpClient<IExternalQuoteService, ExternalQuoteService>(client =>
        {
            // BaseAddress is arbitrary — FakeExternalServiceHandler never dials the network.
            client.BaseAddress = new Uri("http://fake-external-svc/");
        });

        externalClientBuilder.AddResilienceHandler("external-quotes", (pipeline, ctx) =>
        {
            var logger = ctx.ServiceProvider
                .GetRequiredService<ILogger<ExternalQuoteService>>();
            var demoState = ctx.ServiceProvider
                .GetRequiredService<ResilienceDemoState>();

            // ── 1. Bulkhead ──────────────────────────────────────────────────────────────────
            // Limit the number of concurrent calls to the dependency.
            // Excess calls are queued (up to queueLimit); beyond that, IsolationException is thrown.
            pipeline.AddConcurrencyLimiter(new ConcurrencyLimiterOptions
            {
                PermitLimit = 2,
                QueueLimit = 4,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            });

            // ── 2. Retry with exponential backoff ────────────────────────────────────────────
            // Only retries idempotent operations — the IExternalQuoteService contract is GET-only.
            // ShouldHandle targets transient server errors (5xx / 408 / 429 / HttpRequestException).
            // BrokenCircuitException is NOT in that set, so an open circuit fails fast without retrying.
            pipeline.AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = TimeSpan.FromMilliseconds(200),
                OnRetry = args =>
                {
                    logger.LogWarning(
                        "[Polly RETRY] Attempt #{Attempt} after {Delay:F0} ms — {Reason}",
                        args.AttemptNumber + 1,
                        args.RetryDelay.TotalMilliseconds,
                        args.Outcome.Exception?.GetType().Name
                            ?? args.Outcome.Result?.StatusCode.ToString());
                    return ValueTask.CompletedTask;
                }
            });

            // ── 3. Circuit Breaker ───────────────────────────────────────────────────────────
            // State machine: Closed → Open (after failure threshold) → Half-Open → Closed/Open.
            // ShouldHandle is inherited from HttpCircuitBreakerStrategyOptions: same transient set as retry.
            var stateProvider = new CircuitBreakerStateProvider();
            demoState.CircuitStateProvider = stateProvider;

            pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
            {
                // Open when ≥60% of the last 10-second window are failures
                FailureRatio = 0.6,
                SamplingDuration = TimeSpan.FromSeconds(10),
                MinimumThroughput = 3,
                BreakDuration = TimeSpan.FromSeconds(15),
                StateProvider = stateProvider,
                OnOpened = args =>
                {
                    logger.LogError(
                        "[Polly CB] Circuit OPENED — breaking for {Duration:F0} s",
                        args.BreakDuration.TotalSeconds);
                    return ValueTask.CompletedTask;
                },
                OnHalfOpened = _ =>
                {
                    logger.LogWarning("[Polly CB] Circuit HALF-OPEN — sending probe request");
                    return ValueTask.CompletedTask;
                },
                OnClosed = _ =>
                {
                    logger.LogInformation("[Polly CB] Circuit CLOSED — dependency recovered");
                    return ValueTask.CompletedTask;
                }
            });

            // ── 4. Timeout (per-attempt) ─────────────────────────────────────────────────────
            // Cancels any individual attempt that takes longer than 2 s.
            // Placed innermost so each retry attempt gets its own fresh timeout budget.
            pipeline.AddTimeout(TimeSpan.FromSeconds(2));
        });

        // FakeExternalServiceHandler sits innermost: Polly pipeline → FakeHandler.
        // It returns 200 or 503 based on ResilienceDemoState without touching the network.
        externalClientBuilder.AddHttpMessageHandler<FakeExternalServiceHandler>();

        // ── DB query counter: a singleton that the EF interceptor increments on every SELECT.
        // Exposed via /diag/db-queries so the load test can measure DB load with/without cache.
        services.AddSingleton<DbQueryCounter>();
        services.AddSingleton<CountingDbCommandInterceptor>();

        services.AddDbContext<AppDbContext>((sp, options) =>
            options.UseSqlServer(configuration.GetConnectionString("Default")
                ?? "Server=.\\SQLEXPRESS;Database=QuotesDb;Trusted_Connection=True;TrustServerCertificate=True;")
                   .AddInterceptors(sp.GetRequiredService<CountingDbCommandInterceptor>()));

        // ── HybridCache: L1 in-memory + optional L2 Redis.
        // If ConnectionStrings:Redis is absent the distributed layer falls back to an in-process
        // MemoryDistributedCache — stampede protection and the API are identical either way.
        var redisCs = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrWhiteSpace(redisCs))
            services.AddStackExchangeRedisCache(o => o.Configuration = redisCs);
        else
            services.AddDistributedMemoryCache();

        services.AddHybridCache(o =>
        {
            o.MaximumPayloadBytes = 512 * 1024; // 512 KB ceiling per entry
            o.DefaultEntryOptions = new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(5),
                LocalCacheExpiration = TimeSpan.FromMinutes(5)
            };
        });

        // Scoped: one instance per HTTP request — shares the open DbContext transaction
        services.AddScoped<IQuoteRepository, QuoteRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

        // Singleton: stateless time source, safe to share across all requests and threads
        services.AddSingleton<IClock, SystemClock>();

        // Singleton queue: one Channel<T> for the process lifetime; many writers, one reader.
        // AddHostedService registers QuoteAuditWorker as IHostedService — host starts/stops it.
        services.AddSingleton<QuoteAuditQueue>();
        services.AddHostedService<QuoteAuditWorker>();

        // Transient: new instance per injection — validation is stateless and cheap to allocate
        services.AddTransient<IQuoteValidator, QuoteValidator>();

        // Command handlers: scoped so they share the same DbContext/repository as their dependencies
        services.AddScoped<CreateQuoteHandler>();

        // Query handlers: scoped for the same reason
        services.AddScoped<ListQuotesHandler>();
        services.AddScoped<GetQuoteByIdHandler>();

        var otlpEndpoint = configuration["OpenTelemetry:OtlpEndpoint"] ?? "http://localhost:4317";

        // Connection string is resolved at runtime from Key Vault (via Azure:KeyVaultUri overlay in Program.cs).
        // Secret stored in Key Vault as "ApplicationInsights--ConnectionString"; never hardcode it here.
        var appInsightsConnectionString = configuration["ApplicationInsights:ConnectionString"];

        var otelBuilder = services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService("QuotesApi"))
            .WithTracing(t =>
            {
                t.AddSource(QuoteActivitySource.Name)
                 .AddAspNetCoreInstrumentation()
                 .AddEntityFrameworkCoreInstrumentation()
                 .AddHttpClientInstrumentation()
                 .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));

                // Console exporter removed — it floods stdout with Activity.* blocks for every span.
            })
            .WithMetrics(m => m
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation());

        // UseAzureMonitor exports traces + metrics + logs to App Insights.
        // The connection string comes from Key Vault — no credential ever lives in code.
        if (!string.IsNullOrEmpty(appInsightsConnectionString))
            otelBuilder.UseAzureMonitor(o => o.ConnectionString = appInsightsConnectionString);

        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();

        services.Configure<JwtOptions>(configuration.GetSection("Jwt"));

        var jwt = configuration.GetSection("Jwt").Get<JwtOptions>()
            ?? throw new InvalidOperationException("Jwt configuration section is missing.");

        if (string.IsNullOrWhiteSpace(jwt.Key))
            throw new InvalidOperationException("Jwt:Key is not configured.");

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key));

        var tenantId = configuration["AzureAd:TenantId"]
            ?? throw new InvalidOperationException("AzureAd:TenantId is not configured.");
        var clientId = configuration["AzureAd:ClientId"]
            ?? throw new InvalidOperationException("AzureAd:ClientId is not configured.");

        services.AddAuthentication(MultiScheme)
            // Route to LocalJwt or EntraId based on the issuer claim in the incoming token.
            .AddPolicyScheme(MultiScheme, "Local or Entra JWT", options =>
            {
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
                            if (issuer.StartsWith("https://login.microsoftonline.com/", StringComparison.OrdinalIgnoreCase) ||
                                issuer.StartsWith("https://sts.windows.net/", StringComparison.OrdinalIgnoreCase))
                                return EntraScheme;
                        }
                    }
                    return LocalScheme;
                };
            })
            .AddJwtBearer(LocalScheme, options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = signingKey,
                    ClockSkew = TimeSpan.Zero
                };
            })
            .AddJwtBearer(EntraScheme, options =>
            {
                // OIDC discovery at {Authority}/.well-known/openid-configuration fetches
                // Entra's public signing keys automatically — no manual key management needed.
                options.Authority = $"https://login.microsoftonline.com/{tenantId}/v2.0";
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateAudience = true,
                    ValidAudience = clientId
                };
            });

        // OwnQuoteHandler is singleton: it has no state and handles resource-based auth for Quote deletion.
        services.AddSingleton<IAuthorizationHandler, OwnQuoteHandler>();

        services.AddAuthorization(options =>
        {
            // Policy 1 (claim-based): token must carry scope=quotes.write to mutate quotes.
            options.AddPolicy("can-edit-quotes", p => p.RequireClaim("scope", "quotes.write"));

            // Policy 2 (custom requirement): evaluated against the Quote resource in the endpoint;
            // OwnQuoteHandler succeeds only when quote.OwnerId matches the caller's sub claim.
            options.AddPolicy("can-delete-own-quote", p => p.AddRequirements(new OwnQuoteRequirement()));
        });
    }
}
