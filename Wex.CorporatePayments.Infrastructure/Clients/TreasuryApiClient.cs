using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace Wex.CorporatePayments.Infrastructure.Clients;

public interface ITreasuryApiClient
{
    Task<decimal?> GetExchangeRateAsync(string currency, DateTime date, CancellationToken cancellationToken = default);
}

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
            .OrResult(msg => msg.StatusCode >= 500 || msg.StatusCode == System.Net.HttpStatusCode.RequestTimeout)
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
            .OrResult(msg => msg.StatusCode >= 500)
            .CircuitBreakerAsync(
                exceptionsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(30),
                onBreak: (exception, duration) =>
                {
                    _logger.LogError(
                        "Circuit breaker opened for {Duration}s due to {Reason}",
                        duration.TotalSeconds,
                        exception.Exception?.Message ?? exception.Result?.StatusCode.ToString());
                },
                onReset: () => _logger.LogInformation("Circuit breaker reset"),
                onHalfOpen: () => _logger.LogInformation("Circuit breaker half-open"));
    }

    public async Task<decimal?> GetExchangeRateAsync(string currency, DateTime date, CancellationToken cancellationToken = default)
    {
        try
        {
            // Treasury API expects date in YYYY-MM format for monthly rates
            var dateParam = date.ToString("yyyy-MM");
            
            // Build the request URL for Treasury API
            var requestUrl = $"/services/api/fiscal_service/v1/accounting/od/rates_of_exchange" +
                           $"?fields=country_currency_desc,exchange_rate,record_date" +
                           $"&filter=record_date:gte:{dateParam}-01" +
                           $"&filter=country_currency_desc:eq:{currency}" +
                           $"&sort=-record_date" +
                           $"&page[size]=1";

            var response = await _circuitBreakerPolicy.ExecuteAsync(() =>
                _retryPolicy.ExecuteAsync(() =>
                    _httpClient.GetAsync(requestUrl, cancellationToken)));

            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var treasuryResponse = JsonSerializer.Deserialize<TreasuryApiResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            var rate = treasuryResponse?.Data?.FirstOrDefault()?.ExchangeRate;
            
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
}

// DTOs for Treasury API response
public class TreasuryApiResponse
{
    public List<TreasuryRateData> Data { get; set; } = new();
}

public class TreasuryRateData
{
    public string CountryCurrencyDesc { get; set; } = string.Empty;
    public decimal ExchangeRate { get; set; }
    public string RecordDate { get; set; } = string.Empty;
}
