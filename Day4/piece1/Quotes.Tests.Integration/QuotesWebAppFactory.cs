using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using QuotesApi.Data;
using QuotesApi.Services;

namespace Quotes.Tests.Integration;

/// <summary>
/// Controllable clock for integration tests — lets tests freeze or advance time.
/// </summary>
public sealed class FakeClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
}

/// <summary>
/// Boots the real app in-process with two substitutions:
///   1. AppDbContext → SQL Server (via Testcontainers) with a unique per-test database
///   2. IClock        → FakeClock (time-controllable)
///
/// Isolation model: each factory instance generates a fresh GUID-named database inside
/// the shared SQL Server container.  EnsureCreated() provisions schema + seed data at
/// first request.  The database is destroyed when the container is torn down after the
/// test class finishes.
/// </summary>
public sealed class QuotesWebAppFactory : WebApplicationFactory<Program>
{
    // Mirror of appsettings.json — no configuration override needed.
    private const string JwtKey      = "QuotesApi-Dev-SigningKey-ChangeThisInProduction-MustBe32BytesMin";
    private const string JwtIssuer   = "QuotesApi";
    private const string JwtAudience = "QuotesApi";

    // Unique database per factory instance = one isolated DB per test.
    private readonly string _connectionString;

    public FakeClock Clock { get; } = new FakeClock();

    public QuotesWebAppFactory(string baseConnectionString)
    {
        var csb = new SqlConnectionStringBuilder(baseConnectionString)
        {
            InitialCatalog = $"TestDb_{Guid.NewGuid():N}",
            TrustServerCertificate = true
        };
        _connectionString = csb.ConnectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            // Remove every descriptor that touches AppDbContext to avoid EF Core's
            // "only one provider per context" guard when we re-register below.
            var toRemove = services
                .Where(d =>
                    d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                    d.ServiceType == typeof(AppDbContext) ||
                    (d.ServiceType.IsGenericType &&
                     d.ServiceType.GetGenericArguments().Any(t => t == typeof(AppDbContext))))
                .ToList();
            foreach (var d in toRemove)
                services.Remove(d);

            // Re-register pointing at the real SQL Server container.
            // EnsureCreated() in Program.cs creates the database + schema on first use.
            services.AddDbContext<AppDbContext>(opts =>
                opts.UseSqlServer(_connectionString));

            // Replace the production SystemClock with a test-controllable fake.
            services.RemoveAll<IClock>();
            services.AddSingleton<IClock>(Clock);
        });
    }

    /// <summary>
    /// Mints a signed JWT accepted by the API's LocalJwt authentication scheme.
    /// <list type="bullet">
    ///   <item><c>scope: null</c>          — no scope claim → POST /api/quotes returns 403</item>
    ///   <item><c>expiresInMinutes &lt; 0</c> — already-expired token → returns 401</item>
    /// </list>
    /// </summary>
    public string MintLocalJwt(
        int userId           = 1,
        string email         = "test@example.com",
        string? scope        = "quotes.write",
        int expiresInMinutes = 15)
    {
        var key    = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtKey));
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub,   userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
        };
        if (scope is not null)
            claims.Add(new Claim("scope", scope));

        var token = new JwtSecurityToken(
            issuer:             JwtIssuer,
            audience:           JwtAudience,
            claims:             claims,
            expires:            DateTime.UtcNow.AddMinutes(expiresInMinutes),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
