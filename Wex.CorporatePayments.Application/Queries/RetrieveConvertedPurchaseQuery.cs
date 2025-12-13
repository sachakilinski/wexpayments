using MediatR;
using Wex.CorporatePayments.Application.Configuration;
using Wex.CorporatePayments.Domain.ValueObjects;

namespace Wex.CorporatePayments.Application.Queries;

public record RetrieveConvertedPurchaseQuery : IRequest<RetrieveConvertedPurchaseResponse>
{
    public Guid Id { get; }
    public string TargetCurrency { get; }

    public RetrieveConvertedPurchaseQuery(Guid id, string targetCurrency = ApplicationConstants.Currency.Default)
    {
        Id = id;
        TargetCurrency = targetCurrency.ToUpperInvariant();
    }
}

public record RetrieveConvertedPurchaseResponse
{
    public Guid Id { get; }
    public string Description { get; }
    public DateTime TransactionDate { get; }
    public Money OriginalAmount { get; }
    public Money ConvertedAmount { get; }
    public string ExchangeRateDate { get; }
    public decimal ExchangeRate { get; }
}
