using FluentValidation;
using Wex.CorporatePayments.Application.Commands;

namespace Wex.CorporatePayments.Application.Validators;

public class StorePurchaseCommandValidator : AbstractValidator<StorePurchaseCommand>
{
    public StorePurchaseCommandValidator()
    {
        RuleFor(x => x.Description)
            .MaximumLength(50).WithMessage("Description must have at most 50 characters.")
            .NotEmpty().WithMessage("Description is required.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Amount must be a positive value.");

        RuleFor(x => x.TransactionDate)
            .NotEqual(default(DateTime)).WithMessage("Transaction date is required.");
    }
}
