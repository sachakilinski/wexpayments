using Wex.CorporatePayments.Application.Commands;

namespace Wex.CorporatePayments.Application.UseCases;

public interface IStorePurchaseTransactionUseCase
{
    Task<Guid> HandleAsync(StorePurchaseCommand command, string? idempotencyKey = null, CancellationToken cancellationToken = default);
}
