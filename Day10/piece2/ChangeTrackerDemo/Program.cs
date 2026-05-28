using ChangeTrackerDemo;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

const string DbPath = "tracker_demo.db";
if (File.Exists(DbPath)) File.Delete(DbPath);

var options = new DbContextOptionsBuilder<AppDbContext>()
    .UseSqlite($"Data Source={DbPath}")
    .Options;

// ── Seed ─────────────────────────────────────────────────────────────────────
using (var ctx = new AppDbContext(options))
{
    ctx.Database.EnsureCreated();

    var products = Enumerable.Range(1, 10_000)
        .Select(i => new Product
        {
            Name     = $"Product-{i}",
            Category = i % 3 == 0 ? "Electronics" : i % 3 == 1 ? "Books" : "Clothing",
            Price    = Math.Round(9.99m + i * 0.01m, 2),
            Stock    = i * 3
        }).ToList();

    ctx.Products.AddRange(products);
    ctx.SaveChanges();
}

// SQL is captured into a list so we can display it right next to each query.
var sqlLog = new List<string>();
var loggingOptions = new DbContextOptionsBuilder<AppDbContext>()
    .UseSqlite($"Data Source={DbPath}")
    .LogTo(
        sqlLog.Add,
        new[] { DbLoggerCategory.Database.Command.Name },
        LogLevel.Information)
    .EnableSensitiveDataLogging()
    .Options;

static string ExtractSql(List<string> log)
{
    var sqlLines = log
        .SelectMany(entry => entry.Split('\n'))
        .Where(line =>
            !line.TrimStart().StartsWith("info:", StringComparison.OrdinalIgnoreCase) &&
            !line.TrimStart().StartsWith("warn:", StringComparison.OrdinalIgnoreCase) &&
            !line.TrimStart().StartsWith("fail:", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(line))
        .Select(line => "  " + line.Trim());
    return string.Join("\n", sqlLines);
}

// ═════════════════════════════════════════════════════════════════════════════
// DEMO 1 – Original entity query (fetches every column)
// ═════════════════════════════════════════════════════════════════════════════
Console.WriteLine("═══════════════════════════════════════════════════════════");
Console.WriteLine("DEMO 1 – Original entity query (fetches every column)");
Console.WriteLine("═══════════════════════════════════════════════════════════");
Console.WriteLine();
Console.WriteLine("  ctx.Products.AsNoTracking().Where(p => p.Stock > 100).ToList()");

sqlLog.Clear();
using (var ctx = new AppDbContext(loggingOptions))
{
    var products = ctx.Products
        .AsNoTracking()
        .Where(p => p.Stock > 100)
        .ToList();
    Console.WriteLine($"  Rows returned: {products.Count}");
}
Console.WriteLine();
Console.WriteLine("  Generated SQL:");
Console.WriteLine(ExtractSql(sqlLog));

// ═════════════════════════════════════════════════════════════════════════════
// DEMO 2 – Projected query (only the columns the DTO needs)
// ═════════════════════════════════════════════════════════════════════════════
Console.WriteLine();
Console.WriteLine("═══════════════════════════════════════════════════════════");
Console.WriteLine("DEMO 2 – Projected query (only the columns the DTO needs)");
Console.WriteLine("═══════════════════════════════════════════════════════════");
Console.WriteLine();
Console.WriteLine("  ctx.Products.AsNoTracking()");
Console.WriteLine("      .Where(p => p.Stock > 100)");
Console.WriteLine("      .Select(p => new ProductSummaryDto(p.Id, p.Name, p.Category))");
Console.WriteLine("      .ToList()");

sqlLog.Clear();
using (var ctx = new AppDbContext(loggingOptions))
{
    var summaries = ctx.Products
        .AsNoTracking()
        .Where(p => p.Stock > 100)
        .Select(p => new ProductSummaryDto(p.Id, p.Name, p.Category))
        .ToList();
    Console.WriteLine($"  Rows returned: {summaries.Count}");
}
Console.WriteLine();
Console.WriteLine("  Generated SQL (Price + Stock gone):");
Console.WriteLine(ExtractSql(sqlLog));

// ═════════════════════════════════════════════════════════════════════════════
// DEMO 3 – Client-side evaluation: bug caught and fixed
// ═════════════════════════════════════════════════════════════════════════════
Console.WriteLine();
Console.WriteLine("═══════════════════════════════════════════════════════════");
Console.WriteLine("DEMO 3 – Client-side evaluation: bug caught and fixed");
Console.WriteLine("═══════════════════════════════════════════════════════════");

Console.WriteLine();
Console.WriteLine("  [BUG] .ToList() before .Where() materialises the whole table in C# memory");
Console.WriteLine("  ctx.Products.AsNoTracking()");
Console.WriteLine("      .ToList()                      // ← 10,000 rows pulled here");
Console.WriteLine("      .Where(p => p.Stock > 100)     // ← C# filter, not SQL");
Console.WriteLine("      .Select(...)");
Console.WriteLine("      .ToList()");

sqlLog.Clear();
using (var ctx = new AppDbContext(loggingOptions))
{
    // BUG: .ToList() forces all 10,000 rows into C# before the filter runs.
    // EF hands control back to LINQ-to-Objects; the WHERE never reaches the DB.
    var bugResult = ctx.Products
        .AsNoTracking()
        .ToList()                                   // ← 10,000 rows pulled here
        .Where(p => p.Stock > 100)                  // ← C# filter, not SQL
        .Select(p => new ProductSummaryDto(p.Id, p.Name, p.Category))
        .ToList();
    Console.WriteLine($"  Rows returned: {bugResult.Count}  (correct result, wrong cost)");
}
Console.WriteLine();
Console.WriteLine("  Generated SQL (no WHERE — whole table scanned):");
Console.WriteLine(ExtractSql(sqlLog));

Console.WriteLine();
Console.WriteLine("  [FIX] Move .Where() inside the IQueryable chain");
Console.WriteLine("  ctx.Products.AsNoTracking()");
Console.WriteLine("      .Where(p => p.Stock > 100)     // ← SQL WHERE, evaluated by DB");
Console.WriteLine("      .Select(p => new ProductSummaryDto(p.Id, p.Name, p.Category))");
Console.WriteLine("      .ToList()");

sqlLog.Clear();
using (var ctx = new AppDbContext(loggingOptions))
{
    var fixResult = ctx.Products
        .AsNoTracking()
        .Where(p => p.Stock > 100)                  // ← SQL WHERE, evaluated by DB
        .Select(p => new ProductSummaryDto(p.Id, p.Name, p.Category))
        .ToList();
    Console.WriteLine($"  Rows returned: {fixResult.Count}");
}
Console.WriteLine();
Console.WriteLine("  Generated SQL (WHERE pushed to DB, only needed columns):");
Console.WriteLine(ExtractSql(sqlLog));
