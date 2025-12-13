namespace Wex.CorporatePayments.Application.Exceptions;

public class ExchangeRateUnavailableException : Exception
{
    public string Currency { get; }
    public DateTime Date { get; }

    public ExchangeRateUnavailableException(string currency, DateTime date)
        : base($"Exchange rate not available for {currency} on {date:yyyy-MM-dd}")
    {
        Currency = currency;
        Date = date;
    }

    public ExchangeRateUnavailableException(string currency, DateTime date, Exception innerException)
        : base($"Exchange rate not available for {currency} on {date:yyyy-MM-dd}", innerException)
    {
        Currency = currency;
        Date = date;
    }
}
