using Microsoft.EntityFrameworkCore;
using Wex.CorporatePayments.Application.Commands;
using Wex.CorporatePayments.Application.Exceptions;
using Wex.CorporatePayments.Application.Interfaces;
using Wex.CorporatePayments.Domain.Entities;
using Wex.CorporatePayments.Domain.ValueObjects;

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
        var originalAmount = Money.Create(command.Amount, command.Currency);
        var purchase = new Purchase(
            command.Description,
            command.TransactionDate,
            originalAmount,
            command.IdempotencyKey
        );

        try
        {
            // Persist the purchase
            await _purchaseRepository.AddAsync(purchase, cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // Handle unique constraint violation on IdempotencyKey
            throw new IdempotencyConflictException(command.IdempotencyKey!, ex);
        }

        return purchase.Id;
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        // Check for SQLite unique constraint violation
        var innerException = ex.InnerException;
        
        // SQLite unique constraint violation error codes
        if (innerException is Microsoft.Data.Sqlite.SqliteException sqliteEx)
        {
            // SQLite error code 19 = constraint violation
            // SQLite error code 2067 = UNIQUE constraint failed
            return sqliteEx.SqliteErrorCode == 19 || sqliteEx.SqliteErrorCode == 2067;
        }

        // Generic check for constraint violation messages
        var message = innerException?.Message?.ToLowerInvariant();
        return message?.Contains("unique") == true || 
               message?.Contains("constraint") == true ||
               message?.Contains("idempotency") == true;
    }
}
