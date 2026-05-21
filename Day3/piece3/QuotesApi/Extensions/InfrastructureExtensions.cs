using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using QuotesApi.Authorization;
using QuotesApi.Data;
using QuotesApi.Middleware;
using QuotesApi.Repositories;
using QuotesApi.Services;

namespace QuotesApi.Extensions;

public static class InfrastructureExtensions
{
    private const string LocalScheme = "LocalJwt";
    private const string EntraScheme = "EntraId";
    private const string MultiScheme = "MultiScheme";

    public static void AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
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

        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();

        var jwtKey = configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("Jwt:Key is not configured.");
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

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
                    ValidIssuer = configuration["Jwt:Issuer"],
                    ValidAudience = configuration["Jwt:Audience"],
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
