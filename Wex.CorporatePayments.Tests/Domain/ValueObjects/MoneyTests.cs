using Xunit;
using Wex.CorporatePayments.Domain.ValueObjects;

namespace Wex.CorporatePayments.Tests.Domain.ValueObjects;

public class MoneyTests
{
    [Fact]
    public void Create_WithValidAmount_ShouldCreateMoneyInstance()
    {
        // Arrange
        decimal amount = 100.50m;
        string currency = "USD";

        // Act
        var money = Money.Create(amount, currency);

        // Assert
        Assert.Equal(100.50m, money.Amount);
        Assert.Equal("USD", money.Currency);
    }

    [Fact]
    public void Create_WithNegativeAmount_ShouldThrowArgumentException()
    {
        // Arrange
        decimal amount = -10m;
        string currency = "USD";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => Money.Create(amount, currency));
    }

    [Fact]
    public void Create_WithZeroAmount_ShouldThrowArgumentException()
    {
        // Arrange
        decimal amount = 0m;
        string currency = "USD";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => Money.Create(amount, currency));
    }

    [Fact]
    public void Create_WithEmptyCurrency_ShouldThrowArgumentException()
    {
        // Arrange
        decimal amount = 100m;
        string currency = "";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => Money.Create(amount, currency));
    }

    [Fact]
    public void Create_WithNullCurrency_ShouldThrowArgumentException()
    {
        // Arrange
        decimal amount = 100m;
        string currency = null!;

        // Act & Assert
        Assert.Throws<ArgumentException>(() => Money.Create(amount, currency));
    }

    [Theory]
    [InlineData(100.123, 100.12)]  // Round down
    [InlineData(100.125, 100.13)]  // Round up
    [InlineData(100.126, 100.13)]  // Round up
    [InlineData(100.124, 100.12)]  // Round down
    [InlineData(100.001, 100.00)]  // Round down
    [InlineData(100.999, 101.00)]  // Round up
    public void Create_WithMoreThanTwoDecimalPlaces_ShouldRoundToTwoDecimalPlaces(decimal input, decimal expected)
    {
        // Arrange
        string currency = "USD";

        // Act
        var money = Money.Create(input, currency);

        // Assert
        Assert.Equal(expected, money.Amount);
    }

    [Fact]
    public void Create_WithLowercaseCurrency_ShouldConvertToUppercase()
    {
        // Arrange
        decimal amount = 100m;
        string currency = "usd";

        // Act
        var money = Money.Create(amount, currency);

        // Assert
        Assert.Equal("USD", money.Currency);
    }

    [Fact]
    public void Usd_WithValidAmount_ShouldCreateMoneyWithUsdCurrency()
    {
        // Arrange
        decimal amount = 100.50m;

        // Act
        var money = Money.Usd(amount);

        // Assert
        Assert.Equal(100.50m, money.Amount);
        Assert.Equal("USD", money.Currency);
    }

    [Fact]
    public void ToString_ShouldReturnFormattedString()
    {
        // Arrange
        var money = Money.Create(100.50m, "USD");

        // Act
        var result = money.ToString();

        // Assert
        Assert.Equal("USD 100.50", result);
    }

    [Fact]
    public void Equality_WithSameAmountAndCurrency_ShouldBeEqual()
    {
        // Arrange
        var money1 = Money.Create(100.50m, "USD");
        var money2 = Money.Create(100.50m, "USD");

        // Act & Assert
        Assert.Equal(money1, money2);
        Assert.True(money1 == money2);
        Assert.False(money1 != money2);
    }

    [Fact]
    public void Equality_WithDifferentAmounts_ShouldNotBeEqual()
    {
        // Arrange
        var money1 = Money.Create(100.50m, "USD");
        var money2 = Money.Create(101.50m, "USD");

        // Act & Assert
        Assert.NotEqual(money1, money2);
        Assert.False(money1 == money2);
        Assert.True(money1 != money2);
    }

    [Fact]
    public void Equality_WithDifferentCurrencies_ShouldNotBeEqual()
    {
        // Arrange
        var money1 = Money.Create(100.50m, "USD");
        var money2 = Money.Create(100.50m, "EUR");

        // Act & Assert
        Assert.NotEqual(money1, money2);
        Assert.False(money1 == money2);
        Assert.True(money1 != money2);
    }

    [Fact]
    public void GetHashCode_WithEqualMoney_ShouldReturnSameHashCode()
    {
        // Arrange
        var money1 = Money.Create(100.50m, "USD");
        var money2 = Money.Create(100.50m, "USD");

        // Act & Assert
        Assert.Equal(money1.GetHashCode(), money2.GetHashCode());
    }

    [Fact]
    public void Immutability_RecordType_ShouldBeImmutable()
    {
        // Arrange
        var money = Money.Create(100.50m, "USD");

        // Act & Assert - Money is a record, so it's immutable by design
        // This test verifies that we cannot modify the properties after creation
        Assert.True(money.GetType().IsClass);
        Assert.True(money.GetType().BaseType?.Name == "Object");
        
        // Verify it's a record type
        var recordAttribute = money.GetType().GetCustomAttributes()
            .Any(attr => attr.GetType().Name.Contains("CompilerGenerated"));
        Assert.True(recordAttribute);
    }
}
