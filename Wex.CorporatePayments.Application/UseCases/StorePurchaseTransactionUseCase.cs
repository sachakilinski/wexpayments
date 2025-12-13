using Wex.CorporatePayments.Application.Commands;
using Wex.CorporatePayments.Application.Interfaces;
using Wex.CorporatePayments.Domain.Entities;

namespace Wex.CorporatePayments.Application.UseCases;

public class StorePurchaseTransactionUseCase : IStorePurchaseTransactionUseCase
{
    private readonly IPurchaseRepository _purchaseRepository;

    public StorePurchaseTransactionUseCase(IPurchaseRepository purchaseRepository)
    {
        _purchaseRepository = purchaseRepository;
    }

    public async Task<Guid> HandleAsync(StorePurchaseCommand command, CancellationToken cancellationToken = default)
    {
        // Check for idempotency if key is provided
        if (!string.IsNullOrEmpty(command.IdempotencyKey))
        {
            var existingPurchase = await _purchaseRepository.GetByIdempotencyKeyAsync(command.IdempotencyKey, cancellationToken);
            if (existingPurchase != null)
            {
                return existingPurchase.Id; // Return existing purchase for idempotency
            }
        }

        // Create new purchase entity
        var purchase = new Purchase(
            command.Description,
            command.TransactionDate,
            command.Amount,
            command.IdempotencyKey
        );

        // Persist the purchase
        await _purchaseRepository.AddAsync(purchase, cancellationToken);

        return purchase.Id;
    }
}
