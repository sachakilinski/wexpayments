using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Wex.CorporatePayments.Application.Configuration;
using Wex.CorporatePayments.Application.Exceptions;
using Wex.CorporatePayments.Application.DTOs;
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
        
        // For historical dates, use bucket caching
        var cacheKey = $"exchange_rates_bucket_{currency}";
        var cachedRates = await GetRatesBucketFromCacheAsync(cacheKey, cancellationToken);
        
        if (cachedRates != null)
        {
            // Find rate using LINQ - exact date match or most recent prior date
            var rate = FindRateInBucket(cachedRates, requestedDate);
            if (rate.HasValue)
            {
                _logger.LogDebug("Found exchange rate {Rate} for {Currency} on {Date} in bucket cache", 
                    rate.Value, currency, requestedDate);
                return rate.Value;
            }
        }

        // Cache miss or insufficient data - fetch from API
        var sixMonthsAgo = today.AddMonths(-ApplicationConstants.ExchangeRate.FallbackMonths);
        var startDate = requestedDate < sixMonthsAgo ? requestedDate : sixMonthsAgo;
        
        _logger.LogInformation("Fetching exchange rates bucket for {Currency} from {StartDate}", currency, startDate);
        var ratesFromApi = await _treasuryApiClient.GetExchangeRatesRangeAsync(currency, startDate, cancellationToken);
        
        if (!ratesFromApi.Any())
        {
            throw new ExchangeRateUnavailableException(currency, requestedDate);
        }

        // Cache the entire bucket
        await SetRatesBucketCacheAsync(cacheKey, ratesFromApi, cancellationToken);
        
        // Find rate in the newly fetched data
        var finalRate = FindRateInBucket(ratesFromApi, requestedDate);
        if (!finalRate.HasValue)
        {
            throw new ExchangeRateUnavailableException(currency, requestedDate);
        }
        
        _logger.LogInformation("Retrieved and cached exchange rate bucket for {Currency} with {Count} rates", 
            currency, ratesFromApi.Count);
        
        return finalRate.Value;
    }

    private decimal? FindRateInBucket(List<ExchangeRateDto> rates, DateTime requestedDate)
    {
        // Try exact date match first
        var exactMatch = rates.FirstOrDefault(r => r.Date == requestedDate);
        if (exactMatch != null)
        {
            return exactMatch.Rate;
        }
        
        // Find most recent rate before the requested date
        var priorRate = rates
            .Where(r => r.Date < requestedDate)
            .OrderByDescending(r => r.Date)
            .FirstOrDefault();
            
        return priorRate?.Rate;
    }

    private async Task<List<ExchangeRateDto>?> GetRatesBucketFromCacheAsync(string cacheKey, CancellationToken cancellationToken)
    {
        try
        {
            var cachedData = await _cache.GetStringAsync(cacheKey, cancellationToken);
            if (string.IsNullOrEmpty(cachedData))
                return null;

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            
            return JsonSerializer.Deserialize<List<ExchangeRateDto>>(cachedData, options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving exchange rates bucket from cache for key {Key}", cacheKey);
            return null;
        }
    }

    private async Task SetRatesBucketCacheAsync(string cacheKey, List<ExchangeRateDto> rates, CancellationToken cancellationToken)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            
            var serializedData = JsonSerializer.Serialize(rates, options);
            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24) // Cache bucket for 24 hours
            };
            
            await _cache.SetStringAsync(cacheKey, serializedData, cacheOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error caching exchange rates bucket for key {Key}", cacheKey);
            // Don't throw - caching failures shouldn't break the main functionality
        }
    }
}
