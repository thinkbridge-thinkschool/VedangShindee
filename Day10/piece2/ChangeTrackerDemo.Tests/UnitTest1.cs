using ChangeTrackerDemo;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ChangeTrackerDemo.Tests;

// Each test backs a specific claim made in the demo output.
// If a test breaks, the claim in the demo is wrong.

public class ChangeTrackerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _ctx;

    public ChangeTrackerTests()
    {
        // Keep one open connection so the in-memory DB persists for the test lifetime
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _ctx = new AppDbContext(options);
        _ctx.Database.EnsureCreated();

        _ctx.Products.AddRange(
            new Product { Id = 1, Name = "Widget",  Category = "Electronics", Price = 9.99m,  Stock = 10 },
            new Product { Id = 2, Name = "Gadget",  Category = "Electronics", Price = 19.99m, Stock = 5  },
            new Product { Id = 3, Name = "Doohick", Category = "Books",       Price = 4.99m,  Stock = 20 }
        );
        _ctx.SaveChanges();
        _ctx.ChangeTracker.Clear();
    }

    // ── Identity Resolution ───────────────────────────────────────────────────

    [Fact]
    public void TwoQueriesForSamePK_ReturnSameObjectReference()
    {
        // Demo claim: "Same object? True"
        var a = _ctx.Products.First(p => p.Id == 1);
        var b = _ctx.Products.Single(p => p.Id == 1);

        Assert.True(ReferenceEquals(a, b),
            "Expected the change tracker to return the cached instance for the same PK.");
    }

    [Fact]
    public void MutatingOneReference_IsVisibleThroughOther()
    {
        // Demo claim: productB.Name == "MODIFIED" after mutating productA
        var a = _ctx.Products.First(p => p.Id == 1);
        var b = _ctx.Products.Single(p => p.Id == 1);

        a.Name = "MODIFIED";

        Assert.Equal("MODIFIED", b.Name);
    }

    [Fact]
    public void SameProductInTwoNavigations_ResolvesToSingleTrackedInstance()
    {
        // Demo claim: "Same object across two orders? True"
        var order1 = new Order
        {
            CustomerName = "Alice", PlacedAt = DateTime.UtcNow,
            Items = [ new OrderItem { ProductId = 1, Quantity = 1, UnitPrice = 9.99m } ]
        };
        var order2 = new Order
        {
            CustomerName = "Bob", PlacedAt = DateTime.UtcNow,
            Items = [ new OrderItem { ProductId = 1, Quantity = 2, UnitPrice = 9.99m } ]
        };
        _ctx.Orders.AddRange(order1, order2);
        _ctx.SaveChanges();
        _ctx.ChangeTracker.Clear();

        var orders = _ctx.Orders
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .ToList();

        var prodFromOrder1 = orders[0].Items[0].Product;
        var prodFromOrder2 = orders[1].Items[0].Product;

        Assert.True(ReferenceEquals(prodFromOrder1, prodFromOrder2),
            "Expected Product(1) to resolve to the same tracked instance across both orders.");
    }

    // ── Tracking States ───────────────────────────────────────────────────────

    [Fact]
    public void NewEntity_HasAddedState()
    {
        var p = new Product { Name = "New", Category = "X", Price = 1m, Stock = 1 };
        _ctx.Products.Add(p);

        Assert.Equal(EntityState.Added, _ctx.Entry(p).State);
    }

    [Fact]
    public void FreshlyLoadedEntity_HasUnchangedState()
    {
        var p = _ctx.Products.First(x => x.Id == 1);

        Assert.Equal(EntityState.Unchanged, _ctx.Entry(p).State);
    }

    [Fact]
    public void MutatedProperty_TransitionsToModifiedState()
    {
        var p = _ctx.Products.First(x => x.Id == 1);
        p.Price = 1.00m;

        Assert.Equal(EntityState.Modified, _ctx.Entry(p).State);
    }

    [Fact]
    public void MutatedProperty_OnlyThatPropertyFlaggedDirty()
    {
        var p = _ctx.Products.First(x => x.Id == 1);
        p.Price = 1.00m;

        var dirty = _ctx.Entry(p).Properties
            .Where(x => x.IsModified)
            .Select(x => x.Metadata.Name)
            .ToList();

        Assert.Equal(["Price"], dirty);
    }

    [Fact]
    public void RemovedEntity_HasDeletedState()
    {
        var p = _ctx.Products.First(x => x.Id == 1);
        _ctx.Products.Remove(p);

        Assert.Equal(EntityState.Deleted, _ctx.Entry(p).State);
    }

    // ── AsNoTracking ──────────────────────────────────────────────────────────

    [Fact]
    public void AsNoTrackingEntity_HasDetachedState()
    {
        // Demo claim: "AsNoTracking query → State: Detached"
        var p = _ctx.Products.AsNoTracking().First(x => x.Id == 1);

        Assert.Equal(EntityState.Detached, _ctx.Entry(p).State);
    }

    [Fact]
    public void AsNoTrackingQuery_DoesNotPopulateIdentityMap()
    {
        // Untracked load should not add any entries to the change tracker
        _ = _ctx.Products.AsNoTracking().ToList();

        Assert.Empty(_ctx.ChangeTracker.Entries());
    }

    [Fact]
    public void AsNoTrackingQuery_ReturnsDifferentInstanceThanTrackedQuery()
    {
        // Two separate object references for the same row
        var tracked   = _ctx.Products.First(x => x.Id == 1);
        var untracked = _ctx.Products.AsNoTracking().First(x => x.Id == 1);

        Assert.False(ReferenceEquals(tracked, untracked),
            "AsNoTracking should materialise a new instance, not return the cached one.");
    }

    public void Dispose()
    {
        _ctx.Dispose();
        _connection.Dispose();
    }
}
