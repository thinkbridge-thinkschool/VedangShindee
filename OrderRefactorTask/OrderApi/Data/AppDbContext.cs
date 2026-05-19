using Microsoft.EntityFrameworkCore;
using OrderApi.Models;

namespace OrderApi.Data;

/// <summary>
/// Entity Framework Core database context for Order API
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// DbSet for Order entities
    /// </summary>
    public DbSet<Order> Orders => Set<Order>();

    /// <summary>
    /// DbSet for OrderItem entities
    /// </summary>
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    /// <summary>
    /// Configures the model and relationships
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Order entity
        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.CustomerName)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.CustomerEmail)
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(e => e.ShippingAddress)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.TotalAmount)
                .HasColumnType("decimal(18, 2)");

            entity.Property(e => e.Status)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(e => e.OrderDate)
                .IsRequired();

            // Configure relationship
            entity.HasMany(e => e.Items)
                .WithOne(i => i.Order)
                .HasForeignKey(i => i.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            // Create index for efficient queries
            entity.HasIndex(e => e.CustomerEmail);
            entity.HasIndex(e => e.OrderDate);
        });

        // Configure OrderItem entity
        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.ProductId)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(e => e.Quantity)
                .IsRequired();

            entity.Property(e => e.UnitPrice)
                .HasColumnType("decimal(18, 2)");

            entity.Property(e => e.OrderId)
                .IsRequired();
        });
    }
}
