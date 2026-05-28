using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using QueryProjectionDemo;

const string DbPath = "projection_demo.db";
if (File.Exists(DbPath)) File.Delete(DbPath);

// ── Application-level logger (structured logging with named parameters) ──────
// Mentor feedback: use named parameters like {Count}, NOT string interpolation $"{count}"
// Named parameters let log sinks (Seq, Application Insights, etc.) index the value
// separately from the message text, enabling structured queries like Count > 100.
using var loggerFactory = LoggerFactory.Create(b =>
    b.AddSimpleConsole(o => { o.SingleLine = true; o.TimestampFormat = "HH:mm:ss "; })
     .SetMinimumLevel(LogLevel.Information));
var log = loggerFactory.CreateLogger("Demo");

// ── SQL capture helper ───────────────────────────────────────────────────────
// sqlLog collects raw EF log lines; LastSql() extracts and clears them.
var sqlLog = new List<string>();

// LogTo with the Database.Command category means only SQL execution events
// are captured — no noise from state changes, migrations, or model building.
DbContextOptions<AppDbContext> BuildOptions() =>
    new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite($"Data Source={DbPath}")
        .LogTo(msg => sqlLog.Add(msg),
               [DbLoggerCategory.Database.Command.Name],
               LogLevel.Information)
        .EnableSensitiveDataLogging()
        .Options;

// Extracts lines that look like SQL from an EF log entry and clears the buffer.
static string LastSql(List<string> messages)
{
    var lines = messages
        .SelectMany(m => m.Split('\n'))
        .Select(l => l.Trim())
        .Where(l => l.StartsWith("SELECT", StringComparison.Ordinal) ||
                    l.StartsWith("FROM",   StringComparison.Ordinal) ||
                    l.StartsWith("WHERE",  StringComparison.Ordinal) ||
                    l.StartsWith("ORDER",  StringComparison.Ordinal) ||
                    l.StartsWith("INNER",  StringComparison.Ordinal) ||
                    l.StartsWith("LEFT",   StringComparison.Ordinal) ||
                    l.StartsWith("INSERT", StringComparison.Ordinal) ||
                    l.StartsWith("LIMIT",  StringComparison.Ordinal))
        .ToList();
    messages.Clear();
    return string.Join("\n  ", lines);
}

// ── Seed ─────────────────────────────────────────────────────────────────────
using (var ctx = new AppDbContext(BuildOptions()))
{
    ctx.Database.EnsureCreated();
    sqlLog.Clear(); // discard DDL noise

    var products = Enumerable.Range(1, 5_000)
        .Select(i => new Product
        {
            Name     = $"Product-{i}",
            Category = i % 3 == 0 ? "Electronics" : i % 3 == 1 ? "Books" : "Clothing",
            Price    = Math.Round(9.99m + i * 0.01m, 2),
            Stock    = i * 2
        }).ToList();
    ctx.Products.AddRange(products);
    ctx.SaveChanges(); // flush products so their PKs are assigned before order items reference them

    var p1 = products[0]; var p2 = products[1];
    ctx.Orders.AddRange(
        new Order
        {
            CustomerName = "Alice",
            PlacedAt     = DateTime.UtcNow,
            Items        =
            [
                new OrderItem { ProductId = p1.Id, Quantity = 2, UnitPrice = p1.Price },
                new OrderItem { ProductId = p2.Id, Quantity = 1, UnitPrice = p2.Price }
            ]
        },
        new Order
        {
            CustomerName = "Bob",
            PlacedAt     = DateTime.UtcNow,
            Items        = [new OrderItem { ProductId = p1.Id, Quantity = 5, UnitPrice = p1.Price }]
        }
    );
    ctx.SaveChanges();
    sqlLog.Clear();
}

// Correct: named parameters — {ProductCount} is indexed by value in structured log sinks
log.LogInformation("Database seeded with {ProductCount} products and {OrderCount} orders", 5_000, 2);

// ═════════════════════════════════════════════════════════════════════════════
// DEMO 1 – Fat entity query: EF fetches ALL columns even though we only use 3
// ═════════════════════════════════════════════════════════════════════════════
Console.WriteLine();
Console.WriteLine("═══════════════════════════════════════════════════════════");
Console.WriteLine("DEMO 1 – Fat entity query (SELECT * — all 5 columns)");
Console.WriteLine("═══════════════════════════════════════════════════════════");
Console.WriteLine();
Console.WriteLine("  Query:");
Console.WriteLine("    ctx.Products");
Console.WriteLine("        .Where(p => p.Category == \"Electronics\")");
Console.WriteLine("        .OrderBy(p => p.Price)");
Console.WriteLine("        .ToList();");

List<Product> fatResults;
string fatSql;

sqlLog.Clear();
using (var ctx = new AppDbContext(BuildOptions()))
{
    fatResults = ctx.Products
        .Where(p => p.Category == "Electronics")
        .OrderBy(p => p.Price)
        .ToList();
    fatSql = LastSql(sqlLog);
}

// Named parameter logging — value is structured, not embedded in message string
log.LogInformation(
    "Fat query returned {Count} full Product entities (5 columns: Id, Category, Name, Price, Stock)",
    fatResults.Count);

Console.WriteLine();
Console.WriteLine("  Generated SQL:");
Console.WriteLine($"  {fatSql}");

// ═════════════════════════════════════════════════════════════════════════════
// DEMO 2 – Projected query: only fetch the 3 columns the DTO actually uses
// ═════════════════════════════════════════════════════════════════════════════
Console.WriteLine();
Console.WriteLine("═══════════════════════════════════════════════════════════");
Console.WriteLine("DEMO 2 – Projected query (.Select → DTO, only 3 columns)");
Console.WriteLine("═══════════════════════════════════════════════════════════");
Console.WriteLine();
Console.WriteLine("  Query:");
Console.WriteLine("    ctx.Products");
Console.WriteLine("        .Where(p => p.Category == \"Electronics\")");
Console.WriteLine("        .OrderBy(p => p.Price)");
Console.WriteLine("        .Select(p => new ProductSummaryDto(p.Id, p.Name, p.Price))");
Console.WriteLine("        .ToList();");

List<ProductSummaryDto> projectedResults;
string projectedSql;

sqlLog.Clear();
using (var ctx = new AppDbContext(BuildOptions()))
{
    projectedResults = ctx.Products
        .Where(p => p.Category == "Electronics")
        .OrderBy(p => p.Price)
        .Select(p => new ProductSummaryDto(p.Id, p.Name, p.Price))
        .ToList();
    projectedSql = LastSql(sqlLog);
}

log.LogInformation(
    "Projected query returned {Count} ProductSummaryDto objects (3 columns only: Id, Name, Price)",
    projectedResults.Count);

Console.WriteLine();
Console.WriteLine("  Generated SQL:");
Console.WriteLine($"  {projectedSql}");
Console.WriteLine();
Console.WriteLine($"  Results match: {fatResults.Count == projectedResults.Count}  ← same rows, fewer bytes on the wire");

// ═════════════════════════════════════════════════════════════════════════════
// DEMO 3 – Client-side evaluation: bug → fix
//
// BUG: .ToList() before .Where() collapses IQueryable<T> to IEnumerable<T>.
//      The remaining operators (.Where, .Select) are now LINQ-to-Objects, not
//      LINQ-to-Entities — they run in C# after every row has been fetched.
//      The generated SQL has NO WHERE clause; 5 000 rows travel over the wire.
//
// FIX: keep .Where() in the IQueryable chain so EF can translate it into SQL.
//      Only matching rows are fetched; unneeded columns are pruned by .Select().
// ═════════════════════════════════════════════════════════════════════════════
Console.WriteLine();
Console.WriteLine("═══════════════════════════════════════════════════════════");
Console.WriteLine("DEMO 3 – Client-side evaluation: bug vs fix");
Console.WriteLine("═══════════════════════════════════════════════════════════");

// ── BUG ──────────────────────────────────────────────────────────────────────
Console.WriteLine();
Console.WriteLine("  [BUG] Query (accidental client-side evaluation):");
Console.WriteLine("    ctx.Products");
Console.WriteLine("        .ToList()                        // ← full table into memory!");
Console.WriteLine("        .Where(p => p.Price > 50m)       // runs in C#, not SQL");
Console.WriteLine("        .Select(p => new ProductSummaryDto(p.Id, p.Name, p.Price))");
Console.WriteLine("        .ToList();");

List<ProductSummaryDto> buggyResults;
string buggySql;
long buggyMs;

sqlLog.Clear();
using (var ctx = new AppDbContext(BuildOptions()))
{
    var sw = Stopwatch.StartNew();
    buggyResults = ctx.Products          // IQueryable<Product>
        .ToList()                        // materialises ALL 5 000 rows into C# memory
        .Where(p => p.Price > 50m)       // LINQ-to-Objects: WHERE runs in C#
        .Select(p => new ProductSummaryDto(p.Id, p.Name, p.Price))
        .ToList();
    buggyMs = sw.ElapsedMilliseconds;
    buggySql = LastSql(sqlLog);
}

// Named parameters — not string interpolation
log.LogWarning(
    "BUG: loaded all {TotalRows} rows to return {MatchingRows} in {ElapsedMs}ms — WHERE ran in C#",
    5_000, buggyResults.Count, buggyMs);

Console.WriteLine();
Console.WriteLine("  [BUG] Generated SQL (no WHERE — entire table loaded!):");
Console.WriteLine($"  {buggySql}");

// ── FIX ──────────────────────────────────────────────────────────────────────
Console.WriteLine();
Console.WriteLine("  [FIX] Query (WHERE stays in the IQueryable chain):");
Console.WriteLine("    ctx.Products");
Console.WriteLine("        .Where(p => p.Price > 50m)       // translates to SQL WHERE");
Console.WriteLine("        .Select(p => new ProductSummaryDto(p.Id, p.Name, p.Price))");
Console.WriteLine("        .ToList();");

List<ProductSummaryDto> fixedResults;
string fixedSql;
long fixedMs;

sqlLog.Clear();
using (var ctx = new AppDbContext(BuildOptions()))
{
    var sw = Stopwatch.StartNew();
    fixedResults = ctx.Products
        .Where(p => p.Price > 50m)       // IQueryable: translates to SQL WHERE clause
        .Select(p => new ProductSummaryDto(p.Id, p.Name, p.Price))
        .ToList();
    fixedMs = sw.ElapsedMilliseconds;
    fixedSql = LastSql(sqlLog);
}

log.LogInformation(
    "FIX: SQL WHERE returned {MatchingRows} rows in {ElapsedMs}ms — only matching rows fetched",
    fixedResults.Count, fixedMs);

Console.WriteLine();
Console.WriteLine("  [FIX] Generated SQL (WHERE clause in SQL):");
Console.WriteLine($"  {fixedSql}");

// ── Side-by-side ─────────────────────────────────────────────────────────────
Console.WriteLine();
Console.WriteLine("  ── Side-by-side ──────────────────────────────────────────");
Console.WriteLine($"  {"Variant",-10} {"Rows returned",14} {"Time (ms)",10} {"WHERE location"}");
Console.WriteLine($"  {"──────────",-10} {"─────────────",14} {"─────────",10} {"──────────────"}");
Console.WriteLine($"  {"BUG",-10} {buggyResults.Count,14} {buggyMs,10} {"C# (after loading 5 000 rows)"}");
Console.WriteLine($"  {"FIX",-10} {fixedResults.Count,14} {fixedMs,10} {"SQL (only matching rows)"}");
Console.WriteLine();
Console.WriteLine("  Both produce the same result set: " +
                  (buggyResults.Count == fixedResults.Count ? "YES" : "NO"));
Console.WriteLine("  The difference is invisible in the output — only visible in the SQL.");
