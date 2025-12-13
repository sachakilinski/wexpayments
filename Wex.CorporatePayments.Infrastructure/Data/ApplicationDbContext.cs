using Microsoft.EntityFrameworkCore;
using Wex.CorporatePayments.Domain.Entities;

namespace Wex.CorporatePayments.Infrastructure.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Purchase> Purchases { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Purchase entity
        modelBuilder.Entity<Purchase>(entity =>
        {
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.Description)
                .IsRequired()
                .HasMaxLength(50);
            
            entity.Property(e => e.Amount)
                .HasPrecision(18, 2); // Financial precision
            
            entity.Property(e => e.IdempotencyKey)
                .HasMaxLength(255);

            // CRITICAL: Unique index on IdempotencyKey for concurrency protection
            entity.HasIndex(e => e.IdempotencyKey)
                .IsUnique()
                .HasDatabaseName("IX_Purchases_IdempotencyKey")
                .HasFilter("IdempotencyKey IS NOT NULL");
        });
    }
}
