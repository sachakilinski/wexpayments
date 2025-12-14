namespace Wex.CorporatePayments.Application.Configuration;

public static class ApplicationConstants
{
    public static class Currency
    {
        public const string Default = "BRL";
        public const string USD = "USD";
        public const string EUR = "EUR";
        public const string JPY = "JPY";
        public const string GBP = "GBP";
    }

    public static class ExchangeRate
    {
        public const int CacheExpirationHours = 1;
        public const int FallbackMonths = 6;
        public const int HistoricalCacheDays = 30; // d-1 to 6 months
        public const int EternalCacheDays = -1; // d-1 and older (no expiration)
    }

    public static class Validation
    {
        public const int MaxDecimalPlaces = 2;
        public const decimal MinAmount = 0.01m;
    }

    public static class ErrorCodes
    {
        public const string ValidationFailed = "VALIDATION_ERROR";
        public const string IdempotencyConflict = "IDEMPOTENCY_CONFLICT";
        public const string PurchaseNotFound = "PURCHASE_NOT_FOUND";
        public const string ExchangeRateUnavailable = "EXCHANGE_RATE_UNAVAILABLE";
        public const string UnexpectedError = "UNEXPECTED_ERROR";
    }
}
