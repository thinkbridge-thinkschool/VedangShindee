using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Quotes.Tests.Integration;

public sealed class AuthorReportEndpointsTests : IClassFixture<SqlServerFixture>, IDisposable
{
    private readonly QuotesWebAppFactory _factory;
    private readonly HttpClient _client;

    public AuthorReportEndpointsTests(SqlServerFixture fixture)
    {
        _factory = new QuotesWebAppFactory(fixture.ConnectionString);
        _client  = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private async Task CreateQuoteAsync(string author, string text)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/quotes")
        {
            Headers  = { Authorization = new("Bearer", _factory.MintLocalJwt()) },
            Content  = JsonContent.Create(new { author, text })
        };
        var resp = await _client.SendAsync(req);
        resp.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task EfAuthorReport_Returns200_WithElapsedMsAndGroupedAuthors()
    {
        await CreateQuoteAsync("Seneca", "Dum differtur vita transcurrit.");
        await CreateQuoteAsync("Seneca", "Per aspera ad astra.");
        await CreateQuoteAsync("Marcus Aurelius", "You have power over your mind.");

        var resp = await _client.GetAsync("/api/author-report");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var json = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(json);

        var elapsedMs = json.RootElement.GetProperty("elapsedMs").GetInt64();
        Assert.True(elapsedMs >= 0);

        var report = json.RootElement.GetProperty("report").EnumerateArray().ToList();
        Assert.Equal(2, report.Count);

        var seneca = report.Single(r => r.GetProperty("author").GetString() == "Seneca");
        Assert.Equal(2, seneca.GetProperty("quoteCount").GetInt32());

        var marcus = report.Single(r => r.GetProperty("author").GetString() == "Marcus Aurelius");
        Assert.Equal(1, marcus.GetProperty("quoteCount").GetInt32());
    }

    [Fact]
    public async Task EfAuthorReport_EmptyDb_Returns200WithEmptyReport()
    {
        var resp = await _client.GetAsync("/api/author-report");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var json = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(json);

        var report = json.RootElement.GetProperty("report").EnumerateArray().ToList();
        Assert.Empty(report);
    }
}
