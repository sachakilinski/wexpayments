using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Wex.CorporatePayments.Application.DTOs;
using Wex.CorporatePayments.Application.Interfaces;
using System.Text.Json.Serialization;

namespace Wex.CorporatePayments.Infrastructure.Clients;

public class TreasuryApiClient : ITreasuryApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TreasuryApiClient> _logger;
    private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;
    private readonly AsyncCircuitBreakerPolicy<HttpResponseMessage> _circuitBreakerPolicy;

    public TreasuryApiClient(HttpClient httpClient, ILogger<TreasuryApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        
        // Configure base URL for Treasury API
        _httpClient.BaseAddress = new Uri("https://api.fiscaldata.treasury.gov");
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Wex-CorporatePayments/1.0");

        // Configure retry policy with exponential backoff
        _retryPolicy = Policy<HttpResponseMessage>
            .Handle<HttpRequestException>()
            .OrResult(msg => (int)msg.StatusCode >= 500 || msg.StatusCode == System.Net.HttpStatusCode.RequestTimeout)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryAttempt, context) =>
                {
                    _logger.LogWarning(
                        "Retry {RetryAttempt} after {Delay}s due to {Reason}",
                        retryAttempt,
                        timespan.TotalSeconds,
                        outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString());
                });

        // Configure circuit breaker policy
        _circuitBreakerPolicy = Policy<HttpResponseMessage>
            .Handle<HttpRequestException>()
            .OrResult(msg => (int)msg.StatusCode >= 500)
            .AdvancedCircuitBreakerAsync(
                failureThreshold: 0.5,
                samplingDuration: TimeSpan.FromSeconds(30),
                minimumThroughput: 5,
                durationOfBreak: TimeSpan.FromSeconds(30));
    }

    public async Task<decimal?> GetExchangeRateAsync(string currency, DateTime date, CancellationToken cancellationToken = default)
    {
        try
        {
            // Treasury API expects date in YYYY-MM-DD format for daily rates
            var dateParam = date.ToString("yyyy-MM-dd");
            
            // Build the request URL for Treasury API
            var requestUrl = $"/services/api/fiscal_service/v1/accounting/od/rates_of_exchange" +
                           $"?fields=country_currency_desc,exchange_rate,record_date" +
                           $"&filter=record_date:gte:{dateParam}" +
                           $"&filter=country_currency_desc:eq:{currency}" +
                           $"&sort=-record_date" +
                           $"&page[size]=1";

            var fullUrl = $"{_httpClient.BaseAddress}{requestUrl}";
            _logger.LogInformation("Calling Treasury API: {FullUrl}", fullUrl);

            var response = await _circuitBreakerPolicy.ExecuteAsync(() =>
                _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync(requestUrl, cancellationToken)));

            _logger.LogInformation("Treasury API response status: {StatusCode}", response.StatusCode);

            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogInformation("Treasury API response content: {Content}", content);

            var treasuryResponse = JsonSerializer.Deserialize<TreasuryApiResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            // Parse the exchange rate from string to decimal
            decimal? rate = null;
            var firstRecord = treasuryResponse?.Data?.FirstOrDefault();
            if (firstRecord != null && !string.IsNullOrEmpty(firstRecord.ExchangeRate))
            {
                if (decimal.TryParse(firstRecord.ExchangeRate, out var parsedRate))
                {
                    rate = parsedRate;
                }
            }
            
            if (rate.HasValue)
            {
                _logger.LogInformation("Retrieved exchange rate {Rate} for {Currency} on {Date}", rate.Value, currency, date);
            }
            else
            {
                _logger.LogWarning("No exchange rate found for {Currency} on {Date}", currency, date);
            }

            return rate;
        }
        catch (BrokenCircuitException)
        {
            _logger.LogError("Circuit breaker is open - Treasury API unavailable");
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error when calling Treasury API for {Currency} on {Date}", currency, date);
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error parsing Treasury API response for {Currency} on {Date}", currency, date);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error when getting exchange rate for {Currency} on {Date}", currency, date);
            return null;
        }
    }

    public async Task<List<ExchangeRateDto>> GetExchangeRatesRangeAsync(string currency, DateTime startDate, CancellationToken cancellationToken = default)
    {
        try
        {
            // Build the request URL for Treasury API range query
            var startDateParam = startDate.ToString("yyyy-MM-dd");
            var requestUrl = $"/services/api/fiscal_service/v1/accounting/od/rates_of_exchange" +
                           $"?fields=country_currency_desc,exchange_rate,record_date" +
                           $"&filter=country_currency_desc:eq:{currency}" +
                           $"&filter=record_date:gte:{startDateParam}" +
                           $"&sort=-record_date" +
                           $"&page[size]=1000"; // Get up to 1000 records

            var response = await _circuitBreakerPolicy.ExecuteAsync(() =>
                _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync(requestUrl, cancellationToken)));

            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var treasuryResponse = JsonSerializer.Deserialize<TreasuryApiResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            var rates = treasuryResponse?.Data?
                .Where(d => !string.IsNullOrEmpty(d.CountryCurrencyDesc) && !string.IsNullOrEmpty(d.ExchangeRate))
                .Select(d => new ExchangeRateDto
                {
                    Currency = d.CountryCurrencyDesc,
                    Rate = decimal.TryParse(d.ExchangeRate, out var parsedRate) ? parsedRate : 0,
                    Date = DateTime.Parse(d.RecordDate),
                    RecordDate = DateTime.Parse(d.RecordDate)
                })
                .ToList() ?? new List<ExchangeRateDto>();

            _logger.LogInformation("Retrieved {Count} exchange rates for {Currency} from {StartDate}", 
                rates.Count, currency, startDate);

            return rates;
        }
        catch (BrokenCircuitException)
        {
            _logger.LogError("Circuit breaker is open - Treasury API unavailable");
            return new List<ExchangeRateDto>();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error when calling Treasury API for {Currency} range from {StartDate}", currency, startDate);
            return new List<ExchangeRateDto>();
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error parsing Treasury API response for {Currency} range from {StartDate}", currency, startDate);
            return new List<ExchangeRateDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error when getting exchange rates range for {Currency} from {StartDate}", currency, startDate);
            return new List<ExchangeRateDto>();
        }
    }
}

// DTOs for Treasury API response
public class TreasuryApiResponse
{
    [JsonPropertyName("data")]
    public List<TreasuryRateData> Data { get; set; } = new();
}

public class TreasuryRateData
{
    [JsonPropertyName("country_currency_desc")]
    public string CountryCurrencyDesc { get; set; } = string.Empty;
    
    [JsonPropertyName("exchange_rate")]
    public string ExchangeRate { get; set; } = string.Empty;
    
    [JsonPropertyName("record_date")]
    public string RecordDate { get; set; } = string.Empty;
}
