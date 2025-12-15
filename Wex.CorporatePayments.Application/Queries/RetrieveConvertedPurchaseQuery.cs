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
        TargetCurrency = targetCurrency;
    }
}

public class RetrieveConvertedPurchaseResponse
{
    public Guid Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime TransactionDate { get; set; }
    public Money OriginalAmount { get; set; } = Money.Zero();
    public Money ConvertedAmount { get; set; } = Money.Zero();
    public string ExchangeRateDate { get; set; } = string.Empty;
    public decimal ExchangeRate { get; set; }
}
