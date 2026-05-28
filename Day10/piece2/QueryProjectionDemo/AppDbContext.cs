using Microsoft.EntityFrameworkCore;

namespace QueryProjectionDemo;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>().Property(p => p.Price).HasColumnType("TEXT");
        modelBuilder.Entity<OrderItem>().Property(oi => oi.UnitPrice).HasColumnType("TEXT");
    }
}
