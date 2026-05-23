using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using QuotesApi.Authorization;
using QuotesApi.Data;
using QuotesApi.Middleware;
using QuotesApi.Options;
using QuotesApi.Repositories;
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
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite(configuration.GetConnectionString("Default") ?? "Data Source=quotes.db"));

        // Scoped: one instance per HTTP request — shares the open DbContext transaction
        services.AddScoped<IQuoteRepository, QuoteRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

        // Singleton: stateless time source, safe to share across all requests and threads
        services.AddSingleton<IClock, SystemClock>();

        // Transient: new instance per injection — validation is stateless and cheap to allocate
        services.AddTransient<IQuoteValidator, QuoteValidator>();

        // Typed HttpClient for Entra OIDC discovery, wrapped in a Polly resilience pipeline.
        // The pipeline order (outer → inner): Retry → CircuitBreaker → Timeout.
        // Each retry attempt is gated by the circuit breaker and capped by the per-attempt timeout.
        services.AddHttpClient<IEntraDiscoveryService, EntraDiscoveryService>()
            .AddResilienceHandler("default", (builder, context) =>
            {
                var logger = context.ServiceProvider
                    .GetRequiredService<ILogger<EntraDiscoveryService>>();

                // 3 retries, exponential backoff with jitter starting at 500 ms.
                builder.AddRetry(new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = 3,
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    Delay = TimeSpan.FromMilliseconds(500),
                    OnRetry = args =>
                    {
                        var reason = args.Outcome.Exception?.Message
                            ?? $"HTTP {(int)args.Outcome.Result!.StatusCode}";
                        logger.LogWarning(
                            "[Polly] Retry {Attempt}/{Max} for EntraDiscovery after {Delay:g} — {Reason}",
                            args.AttemptNumber + 1, 3, args.RetryDelay, reason);
                        return ValueTask.CompletedTask;
                    }
                });

                // Circuit opens when ≥50 % of calls fail over a 30-second window (min 5 calls).
                builder.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
                {
                    FailureRatio = 0.5,
                    SamplingDuration = TimeSpan.FromSeconds(30),
                    MinimumThroughput = 5,
                    BreakDuration = TimeSpan.FromSeconds(15),
                    OnOpened = args =>
                    {
                        logger.LogError(
                            "[Polly] Circuit OPENED for EntraDiscovery — breaking for {BreakDuration}",
                            args.BreakDuration);
                        return ValueTask.CompletedTask;
                    },
                    OnClosed = _ =>
                    {
                        logger.LogInformation("[Polly] Circuit CLOSED for EntraDiscovery — resuming calls");
                        return ValueTask.CompletedTask;
                    }
                });

                // Per-attempt timeout: abandon a single call after 10 s.
                builder.AddTimeout(TimeSpan.FromSeconds(10));
            });

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

                if (environment.IsDevelopment())
                    t.AddConsoleExporter();
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
