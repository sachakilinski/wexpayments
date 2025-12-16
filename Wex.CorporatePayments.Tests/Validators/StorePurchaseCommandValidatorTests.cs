using FluentValidation;
using FluentValidation.TestHelper;
using Wex.CorporatePayments.Application.Commands;
using Wex.CorporatePayments.Application.Validators;
using Xunit;

namespace Wex.CorporatePayments.Tests.Validators;

public class StorePurchaseCommandValidatorTests
{
    private readonly StorePurchaseCommandValidator _validator;

    public StorePurchaseCommandValidatorTests()
    {
        _validator = new StorePurchaseCommandValidator();
    }

    [Fact]
    public void Should_Have_Error_When_Description_Is_Empty()
    {
        // Arrange
        var command = new StorePurchaseCommand
        {
            Description = "",
            TransactionDate = DateTime.Now,
            Amount = 100
        };

        // Act & Assert
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Should_Have_Error_When_Description_Exceeds_50_Characters()
    {
        // Arrange
        var command = new StorePurchaseCommand
        {
            Description = "This is a very long description that exceeds fifty characters limit",
            TransactionDate = DateTime.Now,
            Amount = 100
        };

        // Act & Assert
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Should_Have_Error_When_Amount_Is_Zero()
    {
        // Arrange
        var command = new StorePurchaseCommand
        {
            Description = "Valid Description",
            TransactionDate = DateTime.Now,
            Amount = 0
        };

        // Act & Assert
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Amount);
    }

    [Fact]
    public void Should_Have_Error_When_Amount_Is_Negative()
    {
        // Arrange
        var command = new StorePurchaseCommand
        {
            Description = "Valid Description",
            TransactionDate = DateTime.Now,
            Amount = -50
        };

        // Act & Assert
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Amount);
    }

    [Fact]
    public void Should_Have_Error_When_TransactionDate_Is_Default()
    {
        // Arrange
        var command = new StorePurchaseCommand
        {
            Description = "Valid Description",
            TransactionDate = default(DateTime),
            Amount = 100
        };

        // Act & Assert
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.TransactionDate);
    }

    [Fact]
    public void Should_Not_Have_Error_When_Command_Is_Valid()
    {
        // Arrange
        var command = new StorePurchaseCommand
        {
            Description = "Valid Description",
            TransactionDate = DateTime.Now,
            Amount = 100
        };

        // Act & Assert
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Not_Have_Error_When_Description_Is_Exactly_50_Characters()
    {
        // Arrange
        var command = new StorePurchaseCommand
        {
            Description = "This description is exactly fifty chars long!",
            TransactionDate = DateTime.Now,
            Amount = 100
        };

        // Act & Assert
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.Description);
    }
}
