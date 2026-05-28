using System.Diagnostics;
using ChangeTrackerDemo;
using Microsoft.EntityFrameworkCore;

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

    // Add orders that share the same products (needed for identity-resolution demo)
    var p1 = products[0];
    var p2 = products[1];
    ctx.Orders.AddRange(
        new Order
        {
            CustomerName = "Alice",
            PlacedAt     = DateTime.UtcNow,
            Items =
            [
                new OrderItem { ProductId = p1.Id, Quantity = 2, UnitPrice = p1.Price },
                new OrderItem { ProductId = p2.Id, Quantity = 1, UnitPrice = p2.Price }
            ]
        },
        new Order
        {
            CustomerName = "Bob",
            PlacedAt     = DateTime.UtcNow,
            Items =
            [
                new OrderItem { ProductId = p1.Id, Quantity = 5, UnitPrice = p1.Price }
            ]
        }
    );
    ctx.SaveChanges();
}

// ═════════════════════════════════════════════════════════════════════════════
// DEMO 1 – Identity Resolution
// The change tracker acts as a first-level cache keyed by entity type + PK.
// Two separate queries for the same PK return the *same object reference*.
// ═════════════════════════════════════════════════════════════════════════════
Console.WriteLine("═══════════════════════════════════════════════════════════");
Console.WriteLine("DEMO 1 – Identity Resolution");
Console.WriteLine("═══════════════════════════════════════════════════════════");

using (var ctx = new AppDbContext(options))
{
    // Query 1 – loads Product(1) and inserts it into the identity map
    var productA = ctx.Products.First(p => p.Id == 1);

    // Query 2 – EF finds Id=1 already in the tracker; returns the cached instance
    var productB = ctx.Products.Single(p => p.Id == 1);

    Console.WriteLine($"  productA hash: {productA.GetHashCode()}");
    Console.WriteLine($"  productB hash: {productB.GetHashCode()}");
    Console.WriteLine($"  Same object?   {ReferenceEquals(productA, productB)}   ← identity resolution at work");

    // Mutation through one reference is immediately visible through the other
    productA.Name = "MODIFIED";
    Console.WriteLine($"  productB.Name after mutating productA: \"{productB.Name}\"");
    Console.WriteLine();

    // Cross-query: two orders each reference Product(1).
    // EF resolves them both to the single tracked instance.
    var orders = ctx.Orders
        .Include(o => o.Items)
        .ThenInclude(i => i.Product)
        .ToList();

    var prodFromAlice = orders[0].Items[0].Product;
    var prodFromBob   = orders[1].Items[0].Product;
    Console.WriteLine($"  Product(1) via Alice's order:  hash={prodFromAlice.GetHashCode()}");
    Console.WriteLine($"  Product(1) via Bob's order:    hash={prodFromBob.GetHashCode()}");
    Console.WriteLine($"  Same object across two orders? {ReferenceEquals(prodFromAlice, prodFromBob)}");
}

// ═════════════════════════════════════════════════════════════════════════════
// DEMO 2 – Tracking States
// Added / Unchanged / Modified / Deleted are all visible through Entry().State.
// AsNoTracking → Detached (never enters the StateManager).
// ═════════════════════════════════════════════════════════════════════════════
Console.WriteLine();
Console.WriteLine("═══════════════════════════════════════════════════════════");
Console.WriteLine("DEMO 2 – Tracking States (Added / Unchanged / Modified / Deleted / Detached)");
Console.WriteLine("═══════════════════════════════════════════════════════════");

using (var ctx = new AppDbContext(options))
{
    // Added – new entity not yet flushed to the database
    var newProduct = new Product { Name = "NewGadget", Category = "Electronics", Price = 49.99m, Stock = 100 };
    ctx.Products.Add(newProduct);
    Console.WriteLine($"  After Add()         → State: {ctx.Entry(newProduct).State}");

    // Unchanged – freshly loaded, no edits
    var existing = ctx.Products.First(p => p.Id == 2);
    Console.WriteLine($"  After fresh query   → State: {ctx.Entry(existing).State}");

    // Modified – EF snapshots originals at load time; comparing snapshot → current detects the delta
    existing.Price = 1.00m;
    Console.WriteLine($"  After Price change  → State: {ctx.Entry(existing).State}");

    var dirtyProps = ctx.Entry(existing).Properties
        .Where(p => p.IsModified)
        .Select(p => p.Metadata.Name);
    Console.WriteLine($"  Dirty properties:     [{string.Join(", ", dirtyProps)}]");

    // Deleted
    var toDelete = ctx.Products.First(p => p.Id == 3);
    ctx.Products.Remove(toDelete);
    Console.WriteLine($"  After Remove()      → State: {ctx.Entry(toDelete).State}");

    Console.WriteLine($"  Total tracked entries right now: {ctx.ChangeTracker.Entries().Count()}");

    // Detached – AsNoTracking bypasses the identity map and snapshot machinery
    var untracked = ctx.Products.AsNoTracking().First(p => p.Id == 4);
    Console.WriteLine($"  AsNoTracking query  → State: {ctx.Entry(untracked).State}");
}

// ═════════════════════════════════════════════════════════════════════════════
// DEMO 3 – Read-path performance: Tracked vs AsNoTracking (10 000 rows)
//
// Manual measurement with Stopwatch + GC.GetTotalAllocatedBytes()
// so we do not need a full BenchmarkDotNet release-mode run.
// ═════════════════════════════════════════════════════════════════════════════
Console.WriteLine();
Console.WriteLine("═══════════════════════════════════════════════════════════");
Console.WriteLine("DEMO 3 – Performance: Tracked vs AsNoTracking (10 000 rows)");
Console.WriteLine("═══════════════════════════════════════════════════════════");

static (long ElapsedMs, long AllocBytes) Measure(Action action)
{
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();
    long before = GC.GetTotalAllocatedBytes(precise: true);
    var  sw     = Stopwatch.StartNew();
    action();
    sw.Stop();
    return (sw.ElapsedMilliseconds, GC.GetTotalAllocatedBytes(precise: true) - before);
}

// Warm up the JIT and SQLite page cache before measuring
for (int i = 0; i < 2; i++)
{
    using var w1 = new AppDbContext(options);
    _ = w1.Products.ToList();
    using var w2 = new AppDbContext(options);
    _ = w2.Products.AsNoTracking().ToList();
}

const int Runs = 5;
long trackedMs = 0, trackedBytes = 0;
long noTrackMs = 0, noTrackBytes = 0;

for (int run = 0; run < Runs; run++)
{
    // ── Tracked ──────────────────────────────────────────────────────────────
    // var products = ctx.Products.ToList();
    var (tMs, tB) = Measure(() =>
    {
        using var ctx = new AppDbContext(options);
        var products = ctx.Products.ToList();               // tracked variant
        _ = products.Count;
    });
    trackedMs    += tMs;
    trackedBytes += tB;

    // ── AsNoTracking ─────────────────────────────────────────────────────────
    // var products = ctx.Products.AsNoTracking().ToList();
    var (ntMs, ntB) = Measure(() =>
    {
        using var ctx = new AppDbContext(options);
        var products = ctx.Products.AsNoTracking().ToList(); // no-tracking variant
        _ = products.Count;
    });
    noTrackMs    += ntMs;
    noTrackBytes += ntB;
}

double avgTrackedMs  = (double)trackedMs    / Runs;
double avgNoTrackMs  = (double)noTrackMs    / Runs;
double avgTrackedKB  = trackedBytes / Runs  / 1024.0;
double avgNoTrackKB  = noTrackBytes / Runs  / 1024.0;

Console.WriteLine();
Console.WriteLine($"  {"Query variant",-35} {"Avg time (ms)",12} {"Avg alloc (KB)",16}");
Console.WriteLine($"  {"─────────────────────────────────────",35} {"────────────",12} {"────────────────",16}");
Console.WriteLine($"  {"ctx.Products.ToList()",-35} {avgTrackedMs,12:F1} {avgTrackedKB,16:F1}");
Console.WriteLine($"  {"ctx.Products.AsNoTracking().ToList()",-35} {avgNoTrackMs,12:F1} {avgNoTrackKB,16:F1}");
Console.WriteLine();

double speedupX      = avgNoTrackMs > 0 ? avgTrackedMs / avgNoTrackMs : 0;
double allocSavingPct = avgTrackedKB > 0 ? (1 - avgNoTrackKB / avgTrackedKB) * 100 : 0;
Console.WriteLine($"  AsNoTracking is ~{speedupX:F2}x faster and allocates ~{allocSavingPct:F0}% less memory.");

// ═════════════════════════════════════════════════════════════════════════════
// EXERCISE ANSWERS
// ═════════════════════════════════════════════════════════════════════════════
// Console.WriteLine();
// Console.WriteLine("═══════════════════════════════════════════════════════════");
// Console.WriteLine("EXERCISE ANSWERS");
// Console.WriteLine("═══════════════════════════════════════════════════════════");
// Console.WriteLine("""

//   ┌── TWO QUERY VARIANTS ─────────────────────────────────────┐
//   │                                                            │
//   │  Tracked (default):                                        │
//   │    var products = ctx.Products.ToList();                   │
//   │                                                            │
//   │  AsNoTracking (read-only fast path):                       │
//   │    var products = ctx.Products                             │
//   │                      .AsNoTracking()                       │
//   │                      .ToList();                            │
//   └────────────────────────────────────────────────────────────┘

//   TIMING / ALLOCATION DIFFERENCE  (see table above)
//     For every tracked entity EF allocates a snapshot of all column values
//     so it can detect changes before SaveChanges. At 10 000 rows that adds up
//     to noticeably more heap pressure and GC time. AsNoTracking skips the
//     snapshot, the identity-map insertion, and the StateManager wiring, so
//     it is both faster and produces far fewer allocations.

//   WHEN NOT TO USE AsNoTracking
//     Never use AsNoTracking when you plan to mutate the entities and call
//     SaveChanges() — the context won't detect the changes and nothing will
//     be written back to the database.

//   ────────────────────────────────────────────────────────────
// """);
