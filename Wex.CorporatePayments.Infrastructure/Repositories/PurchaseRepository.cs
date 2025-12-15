using Microsoft.EntityFrameworkCore;
using Wex.CorporatePayments.Application.Interfaces;
using Wex.CorporatePayments.Domain.Entities;
using Wex.CorporatePayments.Infrastructure.Data;

namespace Wex.CorporatePayments.Infrastructure.Repositories;

public class PurchaseRepository : IPurchaseRepository
{
    private readonly ApplicationDbContext _context;

    public PurchaseRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Purchase?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Purchases.FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task<Purchase?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        // Force a fresh query to avoid caching issues with separate DbContext instances
        _context.ChangeTracker.Clear();
        
        return await _context.Purchases
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.IdempotencyKey == idempotencyKey, cancellationToken);
    }

    public async Task AddAsync(Purchase purchase, CancellationToken cancellationToken = default)
    {
        await _context.Purchases.AddAsync(purchase, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ExistsByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        return await _context.Purchases
            .AnyAsync(p => p.IdempotencyKey == idempotencyKey, cancellationToken);
    }
}
