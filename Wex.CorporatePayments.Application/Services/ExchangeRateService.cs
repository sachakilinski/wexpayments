using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Wex.CorporatePayments.Application.Configuration;
using Wex.CorporatePayments.Application.Exceptions;
using Wex.CorporatePayments.Infrastructure.Clients;

namespace Wex.CorporatePayments.Application.Services;

public interface IExchangeRateService
{
    Task<decimal> GetExchangeRateAsync(string currency, DateTime date, CancellationToken cancellationToken = default);
}

public class ExchangeRateService : IExchangeRateService
{
    private readonly ITreasuryApiClient _treasuryApiClient;
    private readonly IDistributedCache _cache;
    private readonly ILogger<ExchangeRateService> _logger;

    public ExchangeRateService(
        ITreasuryApiClient treasuryApiClient,
        IDistributedCache cache,
        ILogger<ExchangeRateService> logger)
    {
        _treasuryApiClient = treasuryApiClient;
        _cache = cache;
        _logger = logger;
    }

    public async Task<decimal> GetExchangeRateAsync(string currency, DateTime date, CancellationToken cancellationToken = default)
    {
        // Check cache first
        var cacheKey = $"exchange_rate_{currency}_{date:yyyy-MM-dd}";
        var cachedRate = await GetFromCacheAsync(cacheKey, cancellationToken);
        
        if (cachedRate.HasValue)
        {
            _logger.LogDebug("Retrieved exchange rate {Rate} for {Currency} on {Date} from cache", 
                cachedRate.Value, currency, date);
            return cachedRate.Value;
        }

        // Try to get rate from Treasury API with 6-month window
        var rate = await GetRateFromApiWithFallback(currency, date, cancellationToken);
        
        if (!rate.HasValue)
        {
            throw new ExchangeRateUnavailableException(currency, date);
        }

        // Cache the result for configured hours
        await SetCacheAsync(cacheKey, rate.Value, TimeSpan.FromHours(ApplicationConstants.ExchangeRate.CacheExpirationHours), cancellationToken);
        
        _logger.LogInformation("Retrieved and cached exchange rate {Rate} for {Currency} on {Date}", 
            rate.Value, currency, date);
        
        return rate.Value;
    }

    private async Task<decimal?> GetRateFromApiWithFallback(string currency, DateTime date, CancellationToken cancellationToken)
    {
        // Try to get rate for the exact date first
        var rate = await _treasuryApiClient.GetExchangeRateAsync(currency, date, cancellationToken);
        if (rate.HasValue)
        {
            return rate.Value;
        }

        // If not found, try previous dates within configured months
        var fallbackMonthsAgo = date.AddMonths(-ApplicationConstants.ExchangeRate.FallbackMonths);
        var currentDate = date.AddDays(-1); // Start from yesterday

        while (currentDate >= fallbackMonthsAgo)
        {
            rate = await _treasuryApiClient.GetExchangeRateAsync(currency, currentDate, cancellationToken);
            if (rate.HasValue)
            {
                _logger.LogInformation("Found exchange rate {Rate} for {Currency} on {Date} (requested date: {RequestedDate})", 
                    rate.Value, currency, currentDate, date);
                return rate.Value;
            }

            currentDate = currentDate.AddDays(-1);
        }

        _logger.LogWarning("No exchange rate found for {Currency} within {Months} months of {Date}", currency, ApplicationConstants.ExchangeRate.FallbackMonths, date);
        return null;
    }

    private async Task<decimal?> GetFromCacheAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            var cachedData = await _cache.GetStringAsync(key, cancellationToken);
            if (string.IsNullOrEmpty(cachedData))
                return null;

            return JsonSerializer.Deserialize<decimal>(cachedData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving exchange rate from cache for key {Key}", key);
            return null;
        }
    }

    private async Task SetCacheAsync(string key, decimal value, TimeSpan expiration, CancellationToken cancellationToken)
    {
        try
        {
            var serializedData = JsonSerializer.Serialize(value);
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration
            };
            
            await _cache.SetStringAsync(key, serializedData, options, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error caching exchange rate for key {Key}", key);
            // Don't throw - caching failures shouldn't break the main functionality
        }
    }
}
