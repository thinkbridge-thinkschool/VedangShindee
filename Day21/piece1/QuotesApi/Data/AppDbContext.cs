using Microsoft.EntityFrameworkCore;
using QuotesApi.Models;
using QuotesApi.Outbox;

namespace QuotesApi.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Quote> Quotes => Set<Quote>();
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RefreshToken>()
            .HasIndex(t => t.TokenHash)
            .IsUnique();

        modelBuilder.Entity<RefreshToken>()
            .HasIndex(t => t.FamilyId);

        modelBuilder.Entity<Quote>()
            .HasIndex(q => q.Author)
            .HasDatabaseName("IX_Quotes_Author");

        modelBuilder.Entity<OutboxMessage>(b =>
        {
            // Id is client-generated (Guid.NewGuid()) — never let the DB assign it.
            b.Property(m => m.Id).ValueGeneratedNever();
            b.Property(m => m.Topic).IsRequired().HasMaxLength(200);
            b.Property(m => m.Payload).IsRequired();

            // Partial index covers only unsent rows — keeps the relay's poll query O(pending) not O(total).
            b.HasIndex(m => new { m.SentAt, m.CreatedAt })
                .HasFilter("[SentAt] IS NULL")
                .HasDatabaseName("IX_OutboxMessages_Pending");
        });
    }
}
