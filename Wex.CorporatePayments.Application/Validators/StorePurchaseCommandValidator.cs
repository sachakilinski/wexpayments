using FluentValidation;
using Wex.CorporatePayments.Application.Commands;

namespace Wex.CorporatePayments.Application.Validators;

public class StorePurchaseCommandValidator : AbstractValidator<StorePurchaseCommand>
{
    public StorePurchaseCommandValidator()
    {
        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required.")
            .MaximumLength(50).WithMessage("Description must have at most 50 characters.");

        RuleFor(x => x.TransactionDate)
            .NotEqual(default(DateTime)).WithMessage("Transaction date is required.")
            .LessThanOrEqualTo(DateTime.UtcNow).WithMessage("Transaction date cannot be in the future.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Amount must be a positive value.")
            .PrecisionScale(18, 2, true).WithMessage("Amount must have at most 2 decimal places.");
    }
}
