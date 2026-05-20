using Microsoft.EntityFrameworkCore;
using QuotesApi.Models;

namespace QuotesApi.Data;

public class QuoteDbContext : DbContext
{
    public QuoteDbContext(DbContextOptions<QuoteDbContext> options) : base(options) { }

    public DbSet<Quote> Quotes => Set<Quote>();
    public DbSet<Collection> Collections => Set<Collection>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Quote>(b =>
        {
            b.Property(q => q.Author).HasMaxLength(200).IsRequired();
            b.Property(q => q.Text).HasMaxLength(1000).IsRequired();
            b.Property(q => q.IsDeleted).IsRequired().HasDefaultValue(false);
        });

        modelBuilder.Entity<Collection>(b =>
        {
            b.HasKey(c => c.Id);
            b.Property(c => c.Name).HasMaxLength(80).IsRequired();
            b.Property(c => c.OwnerId).IsRequired();

            b.OwnsMany(c => c.Items, item =>
            {
                item.ToTable("CollectionItems");
                item.WithOwner().HasForeignKey("CollectionId");
                // Use (CollectionId, QuoteId) as composite PK — no auto-generated shadow integer needed
                item.HasKey("CollectionId", nameof(CollectionItem.QuoteId));
                item.Property(i => i.QuoteId).IsRequired();
                item.Property(i => i.AddedAt).IsRequired();
            });

            b.Navigation(c => c.Items)
                .HasField("_items")
                .UsePropertyAccessMode(PropertyAccessMode.Field)
                .AutoInclude();
        });
    }
}
