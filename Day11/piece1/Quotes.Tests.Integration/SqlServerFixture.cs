using Testcontainers.MsSql;
using Xunit;

namespace Quotes.Tests.Integration;

/// <summary>
/// Starts a single SQL Server 2022 container that is shared across all tests in a class.
/// xUnit calls InitializeAsync once before the first test and DisposeAsync once after the last.
/// The container is torn down (and all test databases with it) when the fixture is disposed.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    /// <summary>
    /// Base connection string pointing at the running SQL Server instance.
    /// QuotesWebAppFactory appends a unique Initial Catalog per test for isolation.
    /// </summary>
    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
