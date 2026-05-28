using ChangeTrackerDemo;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace ChangeTrackerDemo.Tests;

// Tests are written BEFORE the demo code so every claim in DEMO 4 is verified,
// not just asserted verbally.

public class QueryProjectionTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public QueryProjectionTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var seed = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var ctx = new AppDbContext(seed);
        ctx.Database.EnsureCreated();
        ctx.Products.AddRange(
            new Product { Id = 1, Name = "Widget",  Category = "Electronics", Price =  9.99m, Stock =  30 },
            new Product { Id = 2, Name = "Novel",   Category = "Books",       Price = 14.99m, Stock = 150 },
            new Product { Id = 3, Name = "T-Shirt", Category = "Clothing",    Price = 19.99m, Stock = 200 }
        );
        ctx.SaveChanges();
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private (AppDbContext ctx, List<string> sqlLog) SqlLoggingContext()
    {
        var sqlLog = new List<string>();
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .LogTo(
                sqlLog.Add,
                new[] { DbLoggerCategory.Database.Command.Name },
                LogLevel.Information)
            .Options;
        return (new AppDbContext(opts), sqlLog);
    }

    // ── projection tests ──────────────────────────────────────────────────────

    [Fact]
    public void EntityQuery_SqlSelectsAllColumns()
    {
        // Verify the "before" claim: entity query fetches every column.
        var (ctx, sqlLog) = SqlLoggingContext();
        using (ctx)
            _ = ctx.Products.AsNoTracking()
                    .Where(p => p.Stock > 100)
                    .ToList();

        var sql = string.Join("\n", sqlLog);
        Assert.Contains("\"Price\"", sql,  StringComparison.Ordinal);
        Assert.Contains("\"Stock\"", sql,  StringComparison.Ordinal);
        Assert.Contains("\"Name\"",  sql,  StringComparison.Ordinal);
    }

    [Fact]
    public void ProjectedQuery_SqlOmitsPriceAndStock()
    {
        // Verify the "after" claim: projection drops the unneeded columns from SQL.
        // Filter by Category (a projected column) so neither Price nor Stock
        // appears anywhere in the SQL — not in SELECT, not in WHERE.
        var (ctx, sqlLog) = SqlLoggingContext();
        using (ctx)
            _ = ctx.Products.AsNoTracking()
                    .Where(p => p.Category == "Electronics")
                    .Select(p => new ProductSummaryDto(p.Id, p.Name, p.Category))
                    .ToList();

        var sql = string.Join("\n", sqlLog);
        Assert.DoesNotContain("\"Price\"", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Stock\"", sql, StringComparison.Ordinal);
        Assert.Contains("\"Name\"",        sql, StringComparison.Ordinal);
        Assert.Contains("\"Category\"",    sql, StringComparison.Ordinal);
    }

    [Fact]
    public void ProjectedQuery_ReturnsCorrectRows()
    {
        // OrderBy must come before Select so EF translates it to SQL ORDER BY.
        var (ctx, _) = SqlLoggingContext();
        using (ctx)
        {
            var results = ctx.Products.AsNoTracking()
                .Where(p => p.Stock > 100)
                .OrderBy(p => p.Id)                                  // ← before Select
                .Select(p => new ProductSummaryDto(p.Id, p.Name, p.Category))
                .ToList();

            Assert.Equal(2, results.Count);
            Assert.Equal("Novel",   results[0].Name);
            Assert.Equal("T-Shirt", results[1].Name);
        }
    }

    // ── client-side evaluation tests ──────────────────────────────────────────

    [Fact]
    public void ClientEvalBug_GeneratesSqlWithoutWhereClause()
    {
        // The bug: .ToList() before .Where() forces the whole table into memory.
        // The generated SQL must NOT contain a WHERE because EF materialises all
        // rows before C# does the filtering.
        var (ctx, sqlLog) = SqlLoggingContext();
        using (ctx)
        {
            _ = ctx.Products.AsNoTracking()
                    .ToList()                         // ← whole table pulled here
                    .Where(p => p.Stock > 100)        // ← C# Linq, not SQL
                    .Select(p => new ProductSummaryDto(p.Id, p.Name, p.Category))
                    .ToList();
        }

        var sql = string.Join("\n", sqlLog);
        Assert.DoesNotContain("WHERE", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ClientEvalFix_GeneratesSqlWithWhereClause()
    {
        // The fix: move .Where() inside the IQueryable chain so the filter
        // becomes a SQL WHERE predicate.
        var (ctx, sqlLog) = SqlLoggingContext();
        using (ctx)
        {
            _ = ctx.Products.AsNoTracking()
                    .Where(p => p.Stock > 100)        // ← translates to SQL WHERE
                    .Select(p => new ProductSummaryDto(p.Id, p.Name, p.Category))
                    .ToList();
        }

        var sql = string.Join("\n", sqlLog);
        Assert.Contains("WHERE", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ClientEvalFix_ReturnsOnlyMatchingRows()
    {
        // With the fix, exactly 2 of 3 seed rows satisfy Stock > 100.
        var (ctx, _) = SqlLoggingContext();
        using (ctx)
        {
            var results = ctx.Products.AsNoTracking()
                .Where(p => p.Stock > 100)
                .Select(p => new ProductSummaryDto(p.Id, p.Name, p.Category))
                .ToList();

            Assert.Equal(2, results.Count);
        }
    }

    public void Dispose() => _connection.Dispose();
}
