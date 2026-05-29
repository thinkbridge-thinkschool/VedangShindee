using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using QuotesApi.Data;
using QuotesApi.Models;
using Xunit;

namespace QuotesApi.Tests;

/// <summary>
/// In-process test server. Uses SQLite in-memory (same provider as production) via a
/// single kept-alive connection so the schema persists for the factory's lifetime.
/// </summary>
public sealed class QuotesApiFactory : WebApplicationFactory<Program>
{
    // These values mirror appsettings.json — no config override needed for JWT.
    private const string JwtKey = "QuotesApi-Dev-SigningKey-ChangeThisInProduction-MustBe32BytesMin";
    private const string JwtIssuer = "QuotesApi";
    private const string JwtAudience = "QuotesApi";

    // One open connection keeps the in-memory SQLite database alive for the whole test class.
    private readonly SqliteConnection _keepAlive = new("DataSource=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _keepAlive.Open();

        builder.ConfigureTestServices(services =>
        {
            // Remove every service descriptor whose type mentions AppDbContext.
            // This covers DbContextOptions<AppDbContext> AND the internal
            // IDbContextOptionsConfiguration<AppDbContext> that stores the SQLite
            // provider setup — leaving both in place would trigger EF Core's
            // "only one provider allowed" guard.
            var toRemove = services
                .Where(d =>
                    d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                    d.ServiceType == typeof(AppDbContext) ||
                    (d.ServiceType.IsGenericType &&
                     d.ServiceType.GetGenericArguments().Any(t => t == typeof(AppDbContext))))
                .ToList();
            foreach (var d in toRemove)
                services.Remove(d);

            // Re-register using the same SQLite provider, just wired to the in-memory
            // connection instead of a file — no EF Core internal service conflict.
            services.AddDbContext<AppDbContext>(opts => opts.UseSqlite(_keepAlive));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _keepAlive.Dispose();
    }

    /// <summary>
    /// Mint a signed JWT accepted by the API's LocalJwt scheme.
    /// <list type="bullet">
    ///   <item><c>scope: null</c> — no scope claim → POST /api/quotes returns 403</item>
    ///   <item><c>expiresInMinutes &lt; 0</c> — already-expired token → returns 401</item>
    /// </list>
    /// </summary>
    public string MintLocalJwt(
        int userId = 1,
        string email = "test@example.com",
        string? scope = "quotes.write",
        int expiresInMinutes = 15)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtKey));
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
        };
        if (scope is not null)
            claims.Add(new Claim("scope", scope));

        var token = new JwtSecurityToken(
            issuer: JwtIssuer,
            audience: JwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiresInMinutes),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

/// <summary>
/// End-to-end integration tests covering the full HTTP stack.
///
/// Scenarios verified:
///   1. Anonymous request                                  → 401
///   2. Authenticated, scope claim absent                  → 403
///   3. Authenticated, correct scope                       → 201
///   4. Expired access token                               → 401
///   5. Revoked refresh-token chain (reuse detection)      → 401
/// </summary>
public sealed class IntegrationTests : IClassFixture<QuotesApiFactory>
{
    private readonly QuotesApiFactory _factory;

    public IntegrationTests(QuotesApiFactory factory) => _factory = factory;

    // ── 1. Anonymous → 401 ───────────────────────────────────────────────────

    [Fact]
    public async Task PostQuotes_NoToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/quotes",
            new { author = "Anonymous", text = "Should be rejected." });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── 2. Authenticated but missing scope claim → 403 ───────────────────────

    [Fact]
    public async Task PostQuotes_ValidTokenWithoutScope_Returns403()
    {
        // User is authenticated (JWT validates) but has no scope claim →
        // the can-edit-quotes policy fails → 403, not 401.
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new("Bearer", _factory.MintLocalJwt(scope: null));

        var response = await client.PostAsJsonAsync("/api/quotes",
            new { author = "No-scope user", text = "Missing write permission." });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── 3. Authenticated + correct scope → 201 ───────────────────────────────

    [Fact]
    public async Task PostQuotes_ValidTokenWithScope_Returns201()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new("Bearer", _factory.MintLocalJwt(scope: "quotes.write"));

        var response = await client.PostAsJsonAsync("/api/quotes",
            new { author = "Integration Test", text = "Created via integration test." });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // ── 4. Expired access token → 401 ────────────────────────────────────────

    [Fact]
    public async Task PostQuotes_ExpiredToken_Returns401()
    {
        // expiresInMinutes: -1 → expires in the past.
        // ClockSkew=TimeSpan.Zero (set in InfrastructureExtensions) means no tolerance.
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new("Bearer", _factory.MintLocalJwt(expiresInMinutes: -1));

        var response = await client.PostAsJsonAsync("/api/quotes",
            new { author = "Expired", text = "Must be rejected." });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── 5. Revoked refresh chain (reuse detection) → 401 ────────────────────

    [Fact]
    public async Task Refresh_ReuseOfRotatedToken_Returns401AndRevokesSuccessor()
    {
        var client = _factory.CreateClient();

        // Step 1 — obtain an initial token pair via login.
        var loginResp = await client.PostAsJsonAsync("/api/auth/login",
            new { email = "test@example.com", password = "password123" });
        loginResp.EnsureSuccessStatusCode();

        var login = await loginResp.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(login);
        var originalRefresh = login.RefreshToken;

        // Step 2 — perform a normal rotation: revokes the original token and
        //           returns a fresh successor token.
        var firstRotation = await client.PostAsJsonAsync("/api/auth/refresh",
            new { refresh_token = originalRefresh });
        firstRotation.EnsureSuccessStatusCode();

        var rotated = await firstRotation.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(rotated);
        var successorRefresh = rotated.RefreshToken;

        // Step 3 — re-present the already-rotated original token.
        //           Reuse detection revokes the entire family and returns 401.
        var reuseAttempt = await client.PostAsJsonAsync("/api/auth/refresh",
            new { refresh_token = originalRefresh });

        Assert.Equal(HttpStatusCode.Unauthorized, reuseAttempt.StatusCode);

        // Step 4 — the successor issued in step 2 must also be dead now
        //           (family revocation kills all tokens in the chain).
        var successorAttempt = await client.PostAsJsonAsync("/api/auth/refresh",
            new { refresh_token = successorRefresh });

        Assert.Equal(HttpStatusCode.Unauthorized, successorAttempt.StatusCode);
    }
}
