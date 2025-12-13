using MediatR;

namespace Wex.CorporatePayments.Application.Commands;

public record StorePurchaseCommand : IRequest<Guid>
{
    public string Description { get; init; } = string.Empty;
    public DateTime TransactionDate { get; init; }
    public decimal Amount { get; init; }
    public string? IdempotencyKey { get; init; }
}
