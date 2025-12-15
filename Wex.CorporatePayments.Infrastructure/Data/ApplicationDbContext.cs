using Microsoft.EntityFrameworkCore;
using Wex.CorporatePayments.Domain.Entities;
using Wex.CorporatePayments.Domain.ValueObjects;

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
            
            // Configure Money Value Object as owned entity
            entity.OwnsOne(e => e.OriginalAmount, money =>
            {
                money.Property(m => m.Amount)
                    .HasPrecision(18, 2) // Financial precision
                    .HasColumnName("OriginalAmount");
                
                money.Property(m => m.Currency)
                    .IsRequired()
                    .HasMaxLength(3)
                    .HasColumnName("OriginalCurrency");
            });
            
            entity.Property(e => e.IdempotencyKey)
                .HasMaxLength(255);

            // CRITICAL: Unique index on IdempotencyKey for concurrency protection
            entity.HasIndex(e => e.IdempotencyKey)
                .IsUnique()
                .HasDatabaseName("IX_Purchases_IdempotencyKey");
        });
    }
}
