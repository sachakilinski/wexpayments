namespace Wex.CorporatePayments.Domain.ValueObjects;

public sealed record Money
{
    public decimal Amount { get; }
    public string Currency { get; }

    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public static Money Zero(string currency = "USD")
    {
        if (string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException("Currency is required", nameof(currency));

        return new Money(0, currency.ToUpperInvariant());
    }

    public static Money Create(decimal amount, string currency = "USD")
    {
        if (amount <= 0)
            throw new ArgumentException("Amount must be positive", nameof(amount));
        
        if (string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException("Currency is required", nameof(currency));

        // Round to 2 decimal places for monetary precision
        var roundedAmount = Math.Round(amount, 2, MidpointRounding.AwayFromZero);

        return new Money(roundedAmount, currency.ToUpperInvariant());
    }

    public static Money Usd(decimal amount) => Create(amount, "USD");

    public override string ToString() => $"{Currency} {Amount:N2}";

    // Equality operators for record type are automatically implemented
    // Additional convenience methods can be added here if needed
}
