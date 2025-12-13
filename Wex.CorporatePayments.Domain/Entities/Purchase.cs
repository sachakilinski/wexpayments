namespace Wex.CorporatePayments.Domain.Entities;

public class Purchase
{
    public Guid Id { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public DateTime TransactionDate { get; private set; }
    public decimal Amount { get; private set; }
    public string? IdempotencyKey { get; private set; }

    public Purchase(string description, DateTime transactionDate, decimal amount, string? idempotencyKey = null)
    {
        Id = Guid.NewGuid();
        Description = description;
        TransactionDate = transactionDate;
        Amount = amount;
        IdempotencyKey = idempotencyKey;
    }

    // For EF Core
    private Purchase() { }
}
