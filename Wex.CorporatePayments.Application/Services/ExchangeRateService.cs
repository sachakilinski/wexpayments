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
        var today = DateTime.Today;
        var requestedDate = date.Date;
        
        // d0: No cache, always fetch from API
        if (requestedDate == today)
        {
            _logger.LogInformation("Fetching current day rate for {Currency} on {Date} (no cache)", currency, requestedDate);
            var rate = await _treasuryApiClient.GetExchangeRateAsync(currency, requestedDate, cancellationToken);
            
            if (!rate.HasValue)
            {
                throw new ExchangeRateUnavailableException(currency, requestedDate);
            }
            
            return rate.Value;
        }
        
        // For historical dates, check cache first
        var cacheKey = $"exchange_rate_{currency}_{requestedDate:yyyy-MM-dd}";
        var cachedRate = await GetFromCacheAsync(cacheKey, cancellationToken);
        
        if (cachedRate.HasValue)
        {
            _logger.LogDebug("Retrieved exchange rate {Rate} for {Currency} on {Date} from cache", 
                cachedRate.Value, currency, requestedDate);
            return cachedRate.Value;
        }

        // d-1 and older: Try 6-month cache lookup before HTTP
        var cachedRateFromHistory = await TryGetFromHistoricalCacheAsync(currency, requestedDate, cancellationToken);
        if (cachedRateFromHistory.HasValue)
        {
            return cachedRateFromHistory.Value;
        }

        // Try to get rate from Treasury API with 6-month window
        var rate = await GetRateFromApiWithFallback(currency, requestedDate, cancellationToken);
        
        if (!rate.HasValue)
        {
            throw new ExchangeRateUnavailableException(currency, requestedDate);
        }

        // Cache with appropriate expiration based on date
        var cacheExpiration = GetCacheExpiration(requestedDate, today);
        await SetCacheAsync(cacheKey, rate.Value, cacheExpiration, cancellationToken);
        
        _logger.LogInformation("Retrieved and cached exchange rate {Rate} for {Currency} on {Date} with expiration {Expiration}", 
            rate.Value, currency, requestedDate, cacheExpiration);
        
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

    private async Task<decimal?> TryGetFromHistoricalCacheAsync(string currency, DateTime requestedDate, CancellationToken cancellationToken)
    {
        var today = DateTime.Today;
        var sixMonthsAgo = today.AddMonths(-ApplicationConstants.ExchangeRate.FallbackMonths);
        
        // Only search in cache if requested date is within 6 months
        if (requestedDate < sixMonthsAgo)
        {
            return null;
        }
        
        // Search cache for the last 6 months
        var currentDate = today.AddDays(-1); // Start from yesterday
        while (currentDate >= sixMonthsAgo)
        {
            var cacheKey = $"exchange_rate_{currency}_{currentDate:yyyy-MM-dd}";
            var cachedRate = await GetFromCacheAsync(cacheKey, cancellationToken);
            
            if (cachedRate.HasValue)
            {
                _logger.LogInformation("Found historical rate {Rate} for {Currency} on {CachedDate} for requested date {RequestedDate}", 
                    cachedRate.Value, currency, currentDate, requestedDate);
                return cachedRate.Value;
            }
            
            currentDate = currentDate.AddDays(-1);
        }
        
        return null;
    }

    private TimeSpan GetCacheExpiration(DateTime requestedDate, DateTime today)
    {
        var daysDifference = (today - requestedDate).Days;
        
        // d-1 and older: Eternal cache
        if (daysDifference >= 1)
        {
            return TimeSpan.MaxValue; // Eternal cache
        }
        
        // Future dates or d0: No cache (this shouldn't reach here due to earlier logic)
        return TimeSpan.Zero;
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
