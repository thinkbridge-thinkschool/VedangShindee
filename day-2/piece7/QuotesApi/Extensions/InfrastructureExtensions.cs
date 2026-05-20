using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using QuotesApi.Data;
using QuotesApi.Middleware;
using QuotesApi.Repositories;
using QuotesApi.Services;

namespace QuotesApi.Extensions;

public static class InfrastructureExtensions
{
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
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = configuration["Jwt:Issuer"],
                    ValidAudience = configuration["Jwt:Audience"],
                    IssuerSigningKey = key,
                    ClockSkew = TimeSpan.Zero
                };
            });

        services.AddAuthorization();
    }
}
