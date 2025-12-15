using Wex.CorporatePayments.Domain.ValueObjects;

namespace Wex.CorporatePayments.Domain.Entities;

public class Purchase
{
    public Guid Id { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public DateTime TransactionDate { get; private set; }
    public Money OriginalAmount { get; private set; } = default!;
    public string? IdempotencyKey { get; private set; }

    public Purchase(string description, DateTime transactionDate, Money originalAmount, string? idempotencyKey = null)
    {
        Id = Guid.NewGuid();
        Description = description;
        TransactionDate = transactionDate;
        OriginalAmount = originalAmount;
        IdempotencyKey = idempotencyKey;
    }

    // For EF Core
    private Purchase() { }
}
