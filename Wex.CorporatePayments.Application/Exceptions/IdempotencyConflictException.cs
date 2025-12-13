namespace Wex.CorporatePayments.Application.Exceptions;

public class IdempotencyConflictException : Exception
{
    public string IdempotencyKey { get; }

    public IdempotencyConflictException(string idempotencyKey)
        : base($"Duplicate request detected for idempotency key: {idempotencyKey}")
    {
        IdempotencyKey = idempotencyKey;
    }

    public IdempotencyConflictException(string idempotencyKey, Exception innerException)
        : base($"Duplicate request detected for idempotency key: {idempotencyKey}", innerException)
    {
        IdempotencyKey = idempotencyKey;
    }
}
