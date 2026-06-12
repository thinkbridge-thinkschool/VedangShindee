using Xunit;

namespace Quotes.Tests.Integration;

[CollectionDefinition(nameof(SharedSqlServer))]
public sealed class SharedSqlServer : ICollectionFixture<SqlServerFixture> { }
