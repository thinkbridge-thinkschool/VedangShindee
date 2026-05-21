# Piece 6 — Integration Tests with WebApplicationFactory

## WebApplicationFactory Subclass

```csharp
// Quotes.Tests.Integration/QuotesWebAppFactory.cs

public sealed class FakeClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
}

/// <summary>
/// Boots the real app in-process with two substitutions:
///   1. AppDbContext → in-memory SQLite (same provider as production, different connection)
///   2. IClock        → FakeClock (time-controllable)
///
/// Each factory instance owns its own SqliteConnection, so every test that
/// creates a fresh factory gets a completely isolated database.
/// xUnit creates one test-class instance per [Fact], so one factory per test = one DB per test.
/// </summary>
public sealed class QuotesWebAppFactory : WebApplicationFactory<Program>
{
    private const string JwtKey      = "QuotesApi-Dev-SigningKey-ChangeThisInProduction-MustBe32BytesMin";
    private const string JwtIssuer   = "QuotesApi";
    private const string JwtAudience = "QuotesApi";

    private readonly SqliteConnection _keepAlive = new("DataSource=:memory:");
    public FakeClock Clock { get; } = new FakeClock();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _keepAlive.Open();

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

            services.AddDbContext<AppDbContext>(opts => opts.UseSqlite(_keepAlive));

            // Replace the production SystemClock with a test-controllable fake.
            services.RemoveAll<IClock>();
            services.AddSingleton<IClock>(Clock);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _keepAlive.Dispose();
    }

    public string MintLocalJwt(
        int userId = 1, string email = "test@example.com",
        string? scope = "quotes.write", int expiresInMinutes = 15)
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
            issuer: JwtIssuer, audience: JwtAudience, claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiresInMinutes),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
```

---

## Two Integration Tests

### Happy path — POST /api/quotes returns 201 with body

```csharp
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
    Assert.Equal(_factory.Clock.UtcNow, q.CreatedAt);  // FakeClock makes timestamps deterministic
}


### Error path — POST /api/quotes without a token returns 401

[Fact]
public async Task PostQuote_NoToken_Returns401()
{
    var resp = await _client.PostAsJsonAsync("/api/quotes",
        new { author = "Anonymous", text = "Should be rejected." });

    Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
}

---

## Test Run Output

```
dotnet test Quotes.Tests.Integration/Quotes.Tests.Integration.csproj --logger "console;verbosity=normal"

 Passed  QuoteEndpointsTests.GetQuotes_EmptyDb_Returns200WithEmptyList                      [450 ms]
  Passed  QuoteEndpointsTests.GetQuotes_AfterCreating_Returns200WithList                     [490 ms]
  Passed  QuoteEndpointsTests.GetQuotes_Pagination_SecondPageIsEmpty                         [378 ms]
  Passed  QuoteEndpointsTests.GetQuoteById_ExistingId_Returns200WithQuote                    [484 ms]
  Passed  QuoteEndpointsTests.GetQuoteById_NonExistentId_Returns404                          [4 s]
  Passed  QuoteEndpointsTests.PostQuote_NoToken_Returns401                                   [382 ms]
  Passed  QuoteEndpointsTests.PostQuote_TokenWithoutScope_Returns403                         [376 ms]
  Passed  QuoteEndpointsTests.PostQuote_ValidTokenWithScope_Returns201WithCreatedBody        [405 ms]
  Passed  QuoteEndpointsTests.PostQuote_ExpiredToken_Returns401                              [369 ms]
  Passed  QuoteEndpointsTests.PostQuote_EmptyAuthor_Returns400WithValidationProblem          [555 ms]
  Passed  QuoteEndpointsTests.PostQuote_EmptyText_Returns400WithValidationProblem            [395 ms]
  Passed  QuoteEndpointsTests.DeleteQuote_NoToken_Returns401                                 [799 ms]
  Passed  QuoteEndpointsTests.DeleteQuote_ByOwner_Returns204AndQuoteIsGone                   [443 ms]
  Passed  QuoteEndpointsTests.DeleteQuote_ByNonOwner_Returns403                              [364 ms]
  Passed  QuoteEndpointsTests.DeleteQuote_NonExistentId_Returns404                           [386 ms]
  Passed  AuthEndpointsTests.Login_ValidCredentials_Returns200WithTokens                     [669 ms]
  Passed  AuthEndpointsTests.Login_WrongPassword_Returns401                                  [732 ms]
  Passed  AuthEndpointsTests.Login_UnknownEmail_Returns401                                   [496 ms]
  Passed  AuthEndpointsTests.Refresh_ValidToken_Returns200WithNewTokenPair                   [4 s]
  Passed  AuthEndpointsTests.Refresh_ReuseDetection_Returns401AndRevokesSuccessor            [683 ms]
  Passed  AuthEndpointsTests.Refresh_ExpiredToken_Returns401                                 [689 ms]
  Passed  AuthEndpointsTests.Logout_WithoutAuth_Returns401                                   [376 ms]
  Passed  AuthEndpointsTests.Logout_WithAuth_Returns204AndTokenIsRevoked                     [806 ms]

Total tests: 23   Passed: 23   Failed: 0   Skipped: 0
Total time:  12.857 s

---



## What I learned this session

I learned why it's useful to inject things like time instead of using DateTime.UtcNow directly. It made testing much easier because I could replace the real clock with a fake one and control the time during tests.

Using a FakeClock showed me how much easier testing becomes when time is controlled. Since the time stays fixed, I can make exact checks on values like CreatedAt without worrying about the clock changing during the test.
---

## What would break this

One issue is that the fake clock only affects refresh token checks, not JWT expiration. So a JWT that looks expired in a test might still be accepted because JWT validation uses the real system time. Another possible issue is if multiple requests try to write to SQLite at the same time, which could cause database locking problems.
