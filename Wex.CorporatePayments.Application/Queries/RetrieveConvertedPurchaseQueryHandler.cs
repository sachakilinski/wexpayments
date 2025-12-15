using MediatR;
using Microsoft.Extensions.Logging;
using Wex.CorporatePayments.Application.Interfaces;
using Wex.CorporatePayments.Application.Services;
using Wex.CorporatePayments.Domain.Entities;
using Wex.CorporatePayments.Domain.ValueObjects;

namespace Wex.CorporatePayments.Application.Queries;

public class RetrieveConvertedPurchaseQueryHandler : IRequestHandler<RetrieveConvertedPurchaseQuery, RetrieveConvertedPurchaseResponse>
{
    private readonly IPurchaseRepository _purchaseRepository;
    private readonly IExchangeRateService _exchangeRateService;
    private readonly ILogger<RetrieveConvertedPurchaseQueryHandler> _logger;

    public RetrieveConvertedPurchaseQueryHandler(
        IPurchaseRepository purchaseRepository,
        IExchangeRateService exchangeRateService,
        ILogger<RetrieveConvertedPurchaseQueryHandler> logger)
    {
        _purchaseRepository = purchaseRepository;
        _exchangeRateService = exchangeRateService;
        _logger = logger;
    }

    public async Task<RetrieveConvertedPurchaseResponse> Handle(RetrieveConvertedPurchaseQuery request, CancellationToken cancellationToken)
    {
        // Get the purchase from repository
        var purchase = await _purchaseRepository.GetByIdAsync(request.Id, cancellationToken);
        if (purchase == null)
        {
            _logger.LogWarning("Purchase with ID {PurchaseId} not found", request.Id);
            throw new KeyNotFoundException($"Purchase with ID {request.Id} not found");
        }

        // If target currency is the same as original, return original amount
        if (request.TargetCurrency == purchase.OriginalAmount.Currency)
        {
            _logger.LogInformation("Target currency {TargetCurrency} is same as original currency {OriginalCurrency}", 
                request.TargetCurrency, purchase.OriginalAmount.Currency);
            
            return new RetrieveConvertedPurchaseResponse
            {
                Id = purchase.Id,
                Description = purchase.Description,
                TransactionDate = purchase.TransactionDate,
                OriginalAmount = purchase.OriginalAmount,
                ConvertedAmount = purchase.OriginalAmount,
                ExchangeRateDate = purchase.TransactionDate.ToString("yyyy-MM-dd"),
                ExchangeRate = 1.0m
            };
        }

        // Get exchange rate
        var exchangeRateResult = await _exchangeRateService.GetExchangeRateWithDateAsync(
            request.TargetCurrency, 
            purchase.TransactionDate, 
            cancellationToken);

        // Convert the amount
        var convertedAmount = ConvertAmount(purchase.OriginalAmount, exchangeRateResult.Rate, request.TargetCurrency);

        _logger.LogInformation("Converted purchase {PurchaseId}: {OriginalAmount} {OriginalCurrency} = {ConvertedAmount} {TargetCurrency} (rate: {ExchangeRate} from {RateDate})",
            purchase.Id,
            purchase.OriginalAmount.Amount,
            purchase.OriginalAmount.Currency,
            convertedAmount.Amount,
            request.TargetCurrency,
            exchangeRateResult.Rate,
            exchangeRateResult.RateDate);

        return new RetrieveConvertedPurchaseResponse
        {
            Id = purchase.Id,
            Description = purchase.Description,
            TransactionDate = purchase.TransactionDate,
            OriginalAmount = purchase.OriginalAmount,
            ConvertedAmount = convertedAmount,
            ExchangeRateDate = exchangeRateResult.RateDate.ToString("yyyy-MM-dd"),
            ExchangeRate = exchangeRateResult.Rate
        };
    }

    private static Money ConvertAmount(Money originalAmount, decimal exchangeRate, string targetCurrency)
    {
        // For USD to other currencies, multiply by exchange rate
        // For other currencies to USD, divide by exchange rate
        // Treasury API provides rates as foreign currency to 1 USD
        
        decimal convertedAmount;
        if (originalAmount.Currency == "USD")
        {
            // USD to target currency: multiply by rate
            convertedAmount = originalAmount.Amount * exchangeRate;
        }
        else if (targetCurrency == "USD")
        {
            // Target currency to USD: divide by rate
            convertedAmount = originalAmount.Amount / exchangeRate;
        }
        else
        {
            // Non-USD to non-USD: convert through USD as intermediate
            // First convert to USD, then to target currency
            var usdAmount = originalAmount.Amount / exchangeRate;
            convertedAmount = usdAmount * exchangeRate;
        }

        return Money.Create(convertedAmount, targetCurrency);
    }
}
