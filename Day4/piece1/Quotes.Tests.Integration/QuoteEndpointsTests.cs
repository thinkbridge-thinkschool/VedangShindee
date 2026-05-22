using System.Net;
using System.Net.Http.Json;
using QuotesApi.Models;
using Xunit;

namespace Quotes.Tests.Integration;

/// <summary>
/// Integration tests for GET /api/quotes, GET /api/quotes/{id},
/// POST /api/quotes, and DELETE /api/quotes/{id}.
///
/// Isolation guarantee: xUnit constructs one instance of this class per [Fact],
/// so each test gets its own QuotesWebAppFactory → its own SQL Server database
/// → fully isolated schema and data.  No shared state across tests.
/// The SqlServerFixture (one per class) holds the Testcontainers container lifetime.
/// </summary>
public sealed class QuoteEndpointsTests : IClassFixture<SqlServerFixture>, IDisposable
{
    private readonly QuotesWebAppFactory _factory;
    private readonly HttpClient _client;

    public QuoteEndpointsTests(SqlServerFixture fixture)
    {
        _factory = new QuotesWebAppFactory(fixture.ConnectionString);
        _client  = _factory.CreateClient();  // triggers host start + EnsureCreated() + user seed
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    /// POST /api/quotes as the given user; returns the new quote's id.
    private async Task<int> CreateQuoteAsync(
        string author = "Test Author",
        string text   = "Test text.",
        int    userId = 1)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/quotes")
        {
            Headers  = { Authorization = new("Bearer", _factory.MintLocalJwt(userId)) },
            Content  = JsonContent.Create(new { author, text })
        };
        var resp = await _client.SendAsync(req);
        resp.EnsureSuccessStatusCode();
        var q = await resp.Content.ReadFromJsonAsync<Quote>();
        return q!.Id;
    }

    // ── GET /api/quotes ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetQuotes_EmptyDb_Returns200WithEmptyList()
    {
        var resp = await _client.GetAsync("/api/quotes?page=1&size=10");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var quotes = await resp.Content.ReadFromJsonAsync<List<Quote>>();
        Assert.NotNull(quotes);
        Assert.Empty(quotes);
    }

    [Fact]
    public async Task GetQuotes_AfterCreating_Returns200WithList()
    {
        await CreateQuoteAsync("Seneca", "Dum differtur vita transcurrit.");

        var resp = await _client.GetAsync("/api/quotes?page=1&size=10");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var quotes = await resp.Content.ReadFromJsonAsync<List<Quote>>();
        Assert.NotNull(quotes);
        Assert.Single(quotes);
        Assert.Equal("Seneca", quotes[0].Author);
    }

    [Fact]
    public async Task GetQuotes_Pagination_SecondPageIsEmpty()
    {
        await CreateQuoteAsync("Epictetus", "He is a wise man who does not grieve for the things which he has not.");

        // Page 2 with size 10 should be empty when only 1 quote exists.
        var resp = await _client.GetAsync("/api/quotes?page=2&size=10");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var quotes = await resp.Content.ReadFromJsonAsync<List<Quote>>();
        Assert.NotNull(quotes);
        Assert.Empty(quotes);
    }

    // ── GET /api/quotes/{id} ──────────────────────────────────────────────────

    [Fact]
    public async Task GetQuoteById_ExistingId_Returns200WithQuote()
    {
        var id = await CreateQuoteAsync("Marcus Aurelius", "You have power over your mind, not outside events.");

        var resp = await _client.GetAsync($"/api/quotes/{id}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var q = await resp.Content.ReadFromJsonAsync<Quote>();
        Assert.NotNull(q);
        Assert.Equal(id, q.Id);
        Assert.Equal("Marcus Aurelius", q.Author);
    }

    [Fact]
    public async Task GetQuoteById_NonExistentId_Returns404()
    {
        var resp = await _client.GetAsync("/api/quotes/99999");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // ── POST /api/quotes ──────────────────────────────────────────────────────

    [Fact]
    public async Task PostQuote_NoToken_Returns401()
    {
        var resp = await _client.PostAsJsonAsync("/api/quotes",
            new { author = "Anonymous", text = "Should be rejected." });

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task PostQuote_TokenWithoutScope_Returns403()
    {
        // Authenticated user, but the scope=quotes.write claim is absent.
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/quotes")
        {
            Headers = { Authorization = new("Bearer", _factory.MintLocalJwt(scope: null)) },
            Content = JsonContent.Create(new { author = "Stoic", text = "No permission." })
        };

        var resp = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task PostQuote_ValidTokenWithScope_Returns201WithCreatedBody()
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/quotes")
        {
            Headers = { Authorization = new("Bearer", _factory.MintLocalJwt()) },
            Content = JsonContent.Create(new { author = "Epictetus", text = "It is not what happens to you, but how you react." })
        };

        var resp = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var q = await resp.Content.ReadFromJsonAsync<Quote>();
        Assert.NotNull(q);
        Assert.True(q.Id > 0);
        Assert.Equal("Epictetus", q.Author);
        // CreatedAt is stamped by FakeClock — value is deterministic.
        Assert.Equal(_factory.Clock.UtcNow, q.CreatedAt);
    }

    [Fact]
    public async Task PostQuote_ExpiredToken_Returns401()
    {
        // ClockSkew=TimeSpan.Zero (set in InfrastructureExtensions) means no grace period.
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/quotes")
        {
            Headers = { Authorization = new("Bearer", _factory.MintLocalJwt(expiresInMinutes: -1)) },
            Content = JsonContent.Create(new { author = "Expired", text = "Must be rejected." })
        };

        var resp = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task PostQuote_EmptyAuthor_Returns400WithValidationProblem()
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/quotes")
        {
            Headers = { Authorization = new("Bearer", _factory.MintLocalJwt()) },
            Content = JsonContent.Create(new { author = "", text = "Author is missing." })
        };

        var resp = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Equal("application/problem+json", resp.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task PostQuote_EmptyText_Returns400WithValidationProblem()
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/quotes")
        {
            Headers = { Authorization = new("Bearer", _factory.MintLocalJwt()) },
            Content = JsonContent.Create(new { author = "Aristotle", text = "" })
        };

        var resp = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Equal("application/problem+json", resp.Content.Headers.ContentType?.MediaType);
    }

    // ── DELETE /api/quotes/{id} ───────────────────────────────────────────────

    [Fact]
    public async Task DeleteQuote_NoToken_Returns401()
    {
        var id = await CreateQuoteAsync();

        var resp = await _client.DeleteAsync($"/api/quotes/{id}");

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task DeleteQuote_ByOwner_Returns204AndQuoteIsGone()
    {
        var id = await CreateQuoteAsync(userId: 1);

        using var req = new HttpRequestMessage(HttpMethod.Delete, $"/api/quotes/{id}")
        {
            Headers = { Authorization = new("Bearer", _factory.MintLocalJwt(userId: 1)) }
        };
        var resp = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);

        // Verify the quote was actually deleted.
        var getResp = await _client.GetAsync($"/api/quotes/{id}");
        Assert.Equal(HttpStatusCode.NotFound, getResp.StatusCode);
    }

    [Fact]
    public async Task DeleteQuote_ByNonOwner_Returns403()
    {
        // Quote owned by user 1.
        var id = await CreateQuoteAsync(userId: 1);

        // User 2 is authenticated but not the owner.
        using var req = new HttpRequestMessage(HttpMethod.Delete, $"/api/quotes/{id}")
        {
            Headers = { Authorization = new("Bearer", _factory.MintLocalJwt(userId: 2, email: "other@example.com")) }
        };
        var resp = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task DeleteQuote_NonExistentId_Returns404()
    {
        using var req = new HttpRequestMessage(HttpMethod.Delete, "/api/quotes/99999")
        {
            Headers = { Authorization = new("Bearer", _factory.MintLocalJwt()) }
        };

        var resp = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }
}
