using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using Wex.CorporatePayments.Application.Commands;
using Wex.CorporatePayments.Application.Exceptions;
using Wex.CorporatePayments.Application.Interfaces;
using Wex.CorporatePayments.Application.UseCases;
using Wex.CorporatePayments.Domain.Entities;
using Wex.CorporatePayments.Domain.ValueObjects;
using Microsoft.Data.Sqlite;

namespace Wex.CorporatePayments.Tests.Application.UseCases;

public class StorePurchaseTransactionUseCaseTests
{
    private readonly Mock<IPurchaseRepository> _purchaseRepositoryMock;
    private readonly StorePurchaseTransactionUseCase _useCase;

    public StorePurchaseTransactionUseCaseTests()
    {
        _purchaseRepositoryMock = new Mock<IPurchaseRepository>();
        _useCase = new StorePurchaseTransactionUseCase(_purchaseRepositoryMock.Object);
    }

    [Fact]
    public async Task HandleAsync_WithValidCommand_ShouldCreatePurchaseAndReturnId()
    {
        // Arrange
        var command = new StorePurchaseCommand
        {
            Description = "Test Purchase",
            TransactionDate = DateTime.UtcNow,
            Amount = 100.50m,
            Currency = "USD",
            IdempotencyKey = null
        };

        _purchaseRepositoryMock
            .Setup(r => r.GetByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Purchase?)null);

        _purchaseRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Purchase>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _useCase.HandleAsync(command);

        // Assert
        _purchaseRepositoryMock.Verify(r => r.GetByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _purchaseRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Purchase>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotEqual(Guid.Empty, result);
    }

    [Fact]
    public async Task HandleAsync_WithExistingIdempotencyKey_ShouldReturnExistingPurchaseId()
    {
        // Arrange
        var existingPurchaseId = Guid.NewGuid();
        var money = Money.Create(50.00m, "USD");
        var existingPurchase = new Purchase(
            "Existing Purchase",
            DateTime.UtcNow.AddDays(-1),
            money,
            "existing-key"
        );
        // Use reflection to set the Id property since it has a private setter
        var idProperty = typeof(Purchase).GetProperty(nameof(Purchase.Id));
        idProperty?.SetValue(existingPurchase, existingPurchaseId);

        var command = new StorePurchaseCommand
        {
            Description = "Test Purchase",
            TransactionDate = DateTime.UtcNow,
            Amount = 100.50m,
            Currency = "USD",
            IdempotencyKey = "existing-key"
        };

        _purchaseRepositoryMock
            .Setup(r => r.GetByIdempotencyKeyAsync("existing-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPurchase);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<IdempotencyConflictException>(() => _useCase.HandleAsync(command));
        
        Assert.Equal("existing-key", exception.IdempotencyKey);
        _purchaseRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Purchase>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WithUniqueConstraintViolation_ShouldThrowIdempotencyConflictException()
    {
        // Arrange
        var command = new StorePurchaseCommand
        {
            Description = "Test Purchase",
            TransactionDate = DateTime.UtcNow,
            Amount = 100.50m,
            Currency = "USD",
            IdempotencyKey = "test-key"
        };

        _purchaseRepositoryMock
            .Setup(r => r.GetByIdempotencyKeyAsync("test-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Purchase?)null);

        var sqliteException = new SqliteException("UNIQUE constraint failed: Purchases.IdempotencyKey", 2067);
        var dbUpdateException = new DbUpdateException("Database update failed", sqliteException, Array.Empty<Microsoft.EntityFrameworkCore.Update.IUpdateEntry>());

        _purchaseRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Purchase>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(dbUpdateException);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<IdempotencyConflictException>(
            () => _useCase.HandleAsync(command));

        Assert.Equal("test-key", exception.IdempotencyKey);
        Assert.Equal(dbUpdateException, exception.InnerException);
    }

    [Fact]
    public void IsUniqueConstraintViolation_WithSqliteErrorCode2067_ShouldReturnTrue()
    {
        // Arrange
        var sqliteException = new SqliteException("UNIQUE constraint failed", 2067);
        var dbUpdateException = new DbUpdateException("Database update failed", sqliteException, Array.Empty<Microsoft.EntityFrameworkCore.Update.IUpdateEntry>());

        // Act
        var result = StorePurchaseTransactionUseCaseTestsHelper.InvokeIsUniqueConstraintViolation(dbUpdateException);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsUniqueConstraintViolation_WithSqliteErrorCode19_ShouldReturnTrue()
    {
        // Arrange
        var sqliteException = new SqliteException("constraint violation", 19);
        var dbUpdateException = new DbUpdateException("Database update failed", sqliteException, Array.Empty<Microsoft.EntityFrameworkCore.Update.IUpdateEntry>());

        // Act
        var result = StorePurchaseTransactionUseCaseTestsHelper.InvokeIsUniqueConstraintViolation(dbUpdateException);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsUniqueConstraintViolation_WithUniqueKeywordInMessage_ShouldReturnTrue()
    {
        // Arrange
        var innerException = new Exception("unique constraint violation occurred");
        var dbUpdateException = new DbUpdateException("Database update failed", innerException, Array.Empty<Microsoft.EntityFrameworkCore.Update.IUpdateEntry>());

        // Act
        var result = StorePurchaseTransactionUseCaseTestsHelper.InvokeIsUniqueConstraintViolation(dbUpdateException);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsUniqueConstraintViolation_WithConstraintKeywordInMessage_ShouldReturnTrue()
    {
        // Arrange
        var innerException = new Exception("constraint violation occurred");
        var dbUpdateException = new DbUpdateException("Database update failed", innerException, Array.Empty<Microsoft.EntityFrameworkCore.Update.IUpdateEntry>());

        // Act
        var result = StorePurchaseTransactionUseCaseTestsHelper.InvokeIsUniqueConstraintViolation(dbUpdateException);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsUniqueConstraintViolation_WithIdempotencyKeywordInMessage_ShouldReturnTrue()
    {
        // Arrange
        var innerException = new Exception("idempotency key violation occurred");
        var dbUpdateException = new DbUpdateException("Database update failed", innerException, Array.Empty<Microsoft.EntityFrameworkCore.Update.IUpdateEntry>());

        // Act
        var result = StorePurchaseTransactionUseCaseTestsHelper.InvokeIsUniqueConstraintViolation(dbUpdateException);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsUniqueConstraintViolation_WithUnrelatedException_ShouldReturnFalse()
    {
        // Arrange
        var innerException = new Exception("some other database error");
        var dbUpdateException = new DbUpdateException("Database update failed", innerException, Array.Empty<Microsoft.EntityFrameworkCore.Update.IUpdateEntry>());

        // Act
        var result = StorePurchaseTransactionUseCaseTestsHelper.InvokeIsUniqueConstraintViolation(dbUpdateException);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsUniqueConstraintViolation_WithNullInnerException_ShouldReturnFalse()
    {
        // Arrange
        var dbUpdateException = new DbUpdateException("Database update failed", null, Array.Empty<Microsoft.EntityFrameworkCore.Update.IUpdateEntry>());

        // Act
        var result = StorePurchaseTransactionUseCaseTestsHelper.InvokeIsUniqueConstraintViolation(dbUpdateException);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsUniqueConstraintViolation_WithNullMessage_ShouldReturnFalse()
    {
        // Arrange
        var innerException = new Exception(null!);
        var dbUpdateException = new DbUpdateException("Database update failed", innerException);

        // Act
        var result = StorePurchaseTransactionUseCaseTestsHelper.InvokeIsUniqueConstraintViolation(dbUpdateException);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsUniqueConstraintViolation_WithEmptyMessage_ShouldReturnFalse()
    {
        // Arrange
        var innerException = new Exception("");
        var dbUpdateException = new DbUpdateException("Database update failed", innerException);

        // Act
        var result = StorePurchaseTransactionUseCaseTestsHelper.InvokeIsUniqueConstraintViolation(dbUpdateException);

        // Assert
        Assert.False(result);
    }
}

// Helper class to access private method for testing
public static class StorePurchaseTransactionUseCaseTestsHelper
{
    public static bool InvokeIsUniqueConstraintViolation(DbUpdateException ex)
    {
        // Use reflection to call the private method
        var method = typeof(StorePurchaseTransactionUseCase)
            .GetMethod("IsUniqueConstraintViolation", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        
        return (bool)method!.Invoke(null, new object[] { ex })!;
    }
}
