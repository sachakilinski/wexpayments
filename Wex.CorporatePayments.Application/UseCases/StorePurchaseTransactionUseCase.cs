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
        // Normalize empty string idempotency keys to null
        var normalizedIdempotencyKey = string.IsNullOrEmpty(command.IdempotencyKey) ? null : command.IdempotencyKey;
        
        // For null idempotency keys, generate a unique GUID to avoid SQLite NULL uniqueness issues
        var effectiveIdempotencyKey = normalizedIdempotencyKey ?? $"no-key-{Guid.NewGuid()}";
        
        // Check for idempotency if key is provided
        if (normalizedIdempotencyKey != null)
        {
            var existingPurchase = await _purchaseRepository.GetByIdempotencyKeyAsync(effectiveIdempotencyKey, cancellationToken);
            if (existingPurchase != null)
            {
                // Throw exception for controller to handle and return 409 Conflict
                throw new IdempotencyConflictException(effectiveIdempotencyKey);
            }
        }

        // Create new purchase entity
        var originalAmount = Money.Create(command.Amount, "USD");
        var purchase = new Purchase(
            command.Description,
            command.TransactionDate,
            originalAmount,
            effectiveIdempotencyKey
        );

        try
        {
            // Persist the purchase
            await _purchaseRepository.AddAsync(purchase, cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // Handle unique constraint violation on IdempotencyKey
            // This can happen in race conditions between check and insert
            // Try to find the existing purchase and return its ID
            var existingPurchase = await _purchaseRepository.GetByIdempotencyKeyAsync(effectiveIdempotencyKey, cancellationToken);
            if (existingPurchase != null)
            {
                return existingPurchase.Id;
            }
            // If we still can't find it, throw the original exception
            throw new IdempotencyConflictException(effectiveIdempotencyKey, ex);
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
