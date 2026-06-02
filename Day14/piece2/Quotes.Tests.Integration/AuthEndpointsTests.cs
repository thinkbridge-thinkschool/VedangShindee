using System.Net;
using System.Net.Http.Json;
using QuotesApi.Models;
using Xunit;

namespace Quotes.Tests.Integration;

/// <summary>
/// Integration tests for POST /api/auth/login, POST /api/auth/refresh,
/// and POST /api/auth/logout.
///
/// Each [Fact] gets its own factory instance (one-class-instance-per-test rule in xUnit)
/// so every test starts with a freshly seeded SQL Server database and a clean FakeClock.
/// The SqlServerFixture (one per class) holds the Testcontainers container lifetime.
/// </summary>
public sealed class AuthEndpointsTests : IClassFixture<SqlServerFixture>, IDisposable
{
    private readonly QuotesWebAppFactory _factory;
    private readonly HttpClient _client;

    public AuthEndpointsTests(SqlServerFixture fixture)
    {
        _factory = new QuotesWebAppFactory(fixture.ConnectionString);
        _client  = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    // ── POST /api/auth/login ──────────────────────────────────────────────────

    [Fact]
    public async Task Login_ValidCredentials_Returns200WithTokens()
    {
        var resp = await _client.PostAsJsonAsync("/api/auth/login",
            new { email = "test@example.com", password = "password123" });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var login = await resp.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(login);
        Assert.False(string.IsNullOrWhiteSpace(login.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(login.RefreshToken));
        Assert.True(login.ExpiresIn > 0);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var resp = await _client.PostAsJsonAsync("/api/auth/login",
            new { email = "test@example.com", password = "wrong-password" });

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Login_UnknownEmail_Returns401()
    {
        var resp = await _client.PostAsJsonAsync("/api/auth/login",
            new { email = "nobody@example.com", password = "password123" });

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // ── POST /api/auth/refresh ────────────────────────────────────────────────

    [Fact]
    public async Task Refresh_ValidToken_Returns200WithNewTokenPair()
    {
        var loginResp = await _client.PostAsJsonAsync("/api/auth/login",
            new { email = "test@example.com", password = "password123" });
        var login = await loginResp.Content.ReadFromJsonAsync<LoginResponse>();

        var refreshResp = await _client.PostAsJsonAsync("/api/auth/refresh",
            new { refresh_token = login!.RefreshToken });

        Assert.Equal(HttpStatusCode.OK, refreshResp.StatusCode);
        var refreshed = await refreshResp.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(refreshed);
        // Refresh token is always a new random value.
        Assert.NotEqual(login.RefreshToken, refreshed.RefreshToken);
        // Access token fields are populated.
        Assert.False(string.IsNullOrWhiteSpace(refreshed.AccessToken));
        Assert.True(refreshed.ExpiresIn > 0);
    }

    [Fact]
    public async Task Refresh_ReuseDetection_Returns401AndRevokesSuccessor()
    {
        // Step 1 — obtain initial pair.
        var loginResp = await _client.PostAsJsonAsync("/api/auth/login",
            new { email = "test@example.com", password = "password123" });
        var login    = await loginResp.Content.ReadFromJsonAsync<LoginResponse>();
        var original = login!.RefreshToken;

        // Step 2 — normal rotation: original is revoked, successor is issued.
        var rotated = await _client.PostAsJsonAsync("/api/auth/refresh",
            new { refresh_token = original });
        var r1        = await rotated.Content.ReadFromJsonAsync<LoginResponse>();
        var successor = r1!.RefreshToken;

        // Step 3 — replay the already-rotated token → reuse detection fires,
        //           RevokeFamilyAsync kills the whole chain.
        var reuse = await _client.PostAsJsonAsync("/api/auth/refresh",
            new { refresh_token = original });
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);

        // Step 4 — the successor issued in step 2 must also be dead.
        var successorAttempt = await _client.PostAsJsonAsync("/api/auth/refresh",
            new { refresh_token = successor });
        Assert.Equal(HttpStatusCode.Unauthorized, successorAttempt.StatusCode);
    }

    [Fact]
    public async Task Refresh_ExpiredToken_Returns401()
    {
        // Login at FakeClock T=0 (2026-01-01 12:00).
        var loginResp = await _client.PostAsJsonAsync("/api/auth/login",
            new { email = "test@example.com", password = "password123" });
        var login = await loginResp.Content.ReadFromJsonAsync<LoginResponse>();

        // Advance the fake clock past the 7-day refresh-token TTL.
        _factory.Clock.UtcNow = _factory.Clock.UtcNow.AddDays(8);

        var refreshResp = await _client.PostAsJsonAsync("/api/auth/refresh",
            new { refresh_token = login!.RefreshToken });

        Assert.Equal(HttpStatusCode.Unauthorized, refreshResp.StatusCode);
    }

    // ── POST /api/auth/logout ─────────────────────────────────────────────────

    [Fact]
    public async Task Logout_WithoutAuth_Returns401()
    {
        // /api/auth/logout requires a bearer token.
        var resp = await _client.PostAsJsonAsync("/api/auth/logout",
            new { refresh_token = "does-not-matter" });

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Logout_WithAuth_Returns204AndTokenIsRevoked()
    {
        // Obtain a valid pair.
        var loginResp = await _client.PostAsJsonAsync("/api/auth/login",
            new { email = "test@example.com", password = "password123" });
        var login = await loginResp.Content.ReadFromJsonAsync<LoginResponse>();

        // Logout — supply the access token as Bearer and refresh token in the body.
        using var logoutReq = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout")
        {
            Headers = { Authorization = new("Bearer", login!.AccessToken) },
            Content = JsonContent.Create(new { refresh_token = login.RefreshToken })
        };
        var logoutResp = await _client.SendAsync(logoutReq);

        Assert.Equal(HttpStatusCode.NoContent, logoutResp.StatusCode);

        // The revoked refresh token must now be rejected.
        var refreshAfterLogout = await _client.PostAsJsonAsync("/api/auth/refresh",
            new { refresh_token = login.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refreshAfterLogout.StatusCode);
    }
}
