namespace Wex.CorporatePayments.Application.DTOs;

public class ExchangeRateDto
{
    public string Currency { get; set; } = string.Empty;
    public decimal Rate { get; set; }
    public DateTime Date { get; set; }
    public DateTime RecordDate { get; set; }
}
