# Piece 7 – Real SQL Server in CI with Testcontainers

## Testcontainers Fixture

```csharp
// Quotes.Tests.Integration/SqlServerFixture.cs
using Testcontainers.MsSql;

namespace Quotes.Tests.Integration;

/// <summary>
/// Starts a single SQL Server 2022 container shared across all tests in a class.
/// xUnit calls InitializeAsync once before the first test and DisposeAsync once after the last.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
```

## WebApplicationFactory – SQL Server override

```csharp
// Quotes.Tests.Integration/QuotesWebAppFactory.cs  (key parts)
public sealed class QuotesWebAppFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;
    public FakeClock Clock { get; } = new FakeClock();

    public QuotesWebAppFactory(string baseConnectionString)
    {
        // Each factory instance gets its own database — full isolation per test.
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
            // Strip the SQLite registration added by InfrastructureExtensions.
            var toRemove = services
                .Where(d =>
                    d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                    d.ServiceType == typeof(AppDbContext) ||
                    (d.ServiceType.IsGenericType &&
                     d.ServiceType.GetGenericArguments().Any(t => t == typeof(AppDbContext))))
                .ToList();
            foreach (var d in toRemove) services.Remove(d);

            // Wire AppDbContext to SQL Server.
            // Program.cs calls EnsureCreated() on startup, which provisions the schema.
            services.AddDbContext<AppDbContext>(opts =>
                opts.UseSqlServer(_connectionString));

            services.RemoveAll<IClock>();
            services.AddSingleton<IClock>(Clock);
        });
    }
}
```

## Test class wiring

```csharp
// IClassFixture<SqlServerFixture> gives each test class one shared container.
// Each test still gets its own factory → own database → full data isolation.
public sealed class QuoteEndpointsTests : IClassFixture<SqlServerFixture>, IDisposable
{
    public QuoteEndpointsTests(SqlServerFixture fixture)
    {
        _factory = new QuotesWebAppFactory(fixture.ConnectionString);
        _client  = _factory.CreateClient();
    }
    // ...
}
```

## GitHub Actions snippet

```yaml
# .github/workflows/piece7-ci.yml
name: piece7 – Integration tests (SQL Server via Testcontainers)

on:
  push:
    paths: ['Day3/piece7/**']

jobs:
  integration-tests:
    runs-on: ubuntu-latest   # GitHub-hosted runners include Docker

    steps:
      - uses: actions/checkout@v4

      - name: Cache SQL Server image layers
        uses: actions/cache@v4
        with:
          path: /tmp/.docker-cache
          key: docker-mssql-2022-${{ runner.os }}

      - name: Setup .NET 10
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Restore & build
        working-directory: Day3/piece7
        run: |
          dotnet restore
          dotnet build --no-restore --configuration Release

      - name: Run integration tests
        working-directory: Day3/piece7
        run: |
          dotnet test Quotes.Tests.Integration \
            --no-build --configuration Release \
            --logger "console;verbosity=normal"
```

---

## Isolation model

| Layer | Mechanism |
|---|---|
| Container | One per test **class** (via `IClassFixture`) — started once, torn down once |
| Database | One per test **method** — `QuotesWebAppFactory` generates `TestDb_<guid>` |
| Schema | `EnsureCreated()` in `Program.cs` creates tables on first HTTP call |
| Seed data | `Program.cs` seeds the test user if `Users` table is empty |

---

## What did you learn this session?

I learned that sharing one test container across a test class saves a lot of time, while giving each test its own database keeps the tests independent. It’s a good balance between faster test runs and proper test isolation.

---

## What would break this?

If the migration was created specifically for SQLite, it may fail when switching to SQL Server because some database settings are different. Another common issue is Docker not running locally, which would cause all database container tests to fail before they even start.

## Unit tests - 
dotnet test Quotes.Tests.Unit
Restore complete (1.7s)
  QuotesApi net10.0 succeeded (1.5s) → QuotesApi\bin\Debug\net10.0\QuotesApi.dll
  Quotes.Tests.Unit net10.0 succeeded (4.6s) → Quotes.Tests.Unit\bin\Debug\net10.0\Quotes.Tests.Unit.dll
[xUnit.net 00:00:00.00] xUnit.net VSTest Adapter v2.8.2+699d445a1a (64-bit .NET 10.0.8)
[xUnit.net 00:00:03.43]   Discovering: Quotes.Tests.Unit
[xUnit.net 00:00:03.65]   Discovered:  Quotes.Tests.Unit
[xUnit.net 00:00:03.69]   Starting:    Quotes.Tests.Unit
[xUnit.net 00:00:09.84]   Finished:    Quotes.Tests.Unit
  Quotes.Tests.Unit test net10.0 succeeded (19.2s)

Test summary: total: 43, failed: 0, succeeded: 43, skipped: 0, duration: 19.2s
Build succeeded in 28.7s

Integration Tests - 

