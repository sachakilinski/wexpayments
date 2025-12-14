using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Wex.CorporatePayments.Application.Exceptions;
using Wex.CorporatePayments.Application.Services;
using Wex.CorporatePayments.Application.Interfaces;
using Wex.CorporatePayments.Application.DTOs;

namespace Wex.CorporatePayments.Tests.Application.Services;

public class ExchangeRateServiceTests
{
    private readonly Mock<ITreasuryApiClient> _treasuryApiClientMock;
    private readonly Mock<IDistributedCache> _cacheMock;
    private readonly Mock<ILogger<ExchangeRateService>> _loggerMock;
    private readonly ExchangeRateService _exchangeRateService;

    public ExchangeRateServiceTests()
    {
        _treasuryApiClientMock = new Mock<ITreasuryApiClient>();
        _cacheMock = new Mock<IDistributedCache>();
        _loggerMock = new Mock<ILogger<ExchangeRateService>>();

        _exchangeRateService = new ExchangeRateService(
            _treasuryApiClientMock.Object,
            _cacheMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task GetExchangeRateAsync_WithCachedRate_ShouldReturnFromCache()
    {
        // Arrange
        var currency = "BRL";
        var date = new DateTime(2023, 12, 15);
        var cachedRate = 5.25m;
        var cacheKey = $"exchange_rates_bucket_{currency}";
        
        // Create cached rates list
        var cachedRates = new List<ExchangeRateDto>
        {
            new() { Date = date, Rate = cachedRate }
        };
        var serializedRates = System.Text.Json.JsonSerializer.Serialize(cachedRates);

        _cacheMock
            .Setup(c => c.GetStringAsync(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(serializedRates);

        // Act
        var result = await _exchangeRateService.GetExchangeRateAsync(currency, date);

        // Assert
        Assert.Equal(cachedRate, result);
        _treasuryApiClientMock.Verify(c => c.GetExchangeRateAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        _cacheMock.Verify(c => c.SetStringAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetExchangeRateAsync_WithExactDateRate_ShouldReturnAndCache()
    {
        // Arrange
        var currency = "BRL";
        var date = new DateTime(2023, 12, 15);
        var rate = 5.25m;
        var cacheKey = $"exchange_rates_bucket_{currency}";

        _cacheMock
            .Setup(c => c.GetStringAsync(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var apiRates = new List<ExchangeRateDto>
        {
            new() { Date = date, Rate = rate }
        };

        _treasuryApiClientMock
            .Setup(c => c.GetExchangeRatesRangeAsync(currency, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiRates);

        // Act
        var result = await _exchangeRateService.GetExchangeRateAsync(currency, date);

        // Assert
        Assert.Equal(rate, result);
        _treasuryApiClientMock.Verify(c => c.GetExchangeRatesRangeAsync(currency, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        _cacheMock.Verify(c => c.SetStringAsync(cacheKey, It.IsAny<string>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetExchangeRateAsync_WithFallbackToPreviousDate_ShouldReturnAndCache()
    {
        // Arrange
        var currency = "BRL";
        var date = new DateTime(2023, 12, 15);
        var fallbackDate = new DateTime(2023, 12, 14);
        var rate = 5.25m;
        var cacheKey = $"exchange_rates_bucket_{currency}";

        _cacheMock
            .Setup(c => c.GetStringAsync(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        // API returns rates with fallback date
        var apiRates = new List<ExchangeRateDto>
        {
            new() { Date = fallbackDate, Rate = rate }
        };

        _treasuryApiClientMock
            .Setup(c => c.GetExchangeRatesRangeAsync(currency, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiRates);

        // Act
        var result = await _exchangeRateService.GetExchangeRateAsync(currency, date);

        // Assert
        Assert.Equal(rate, result);
        _treasuryApiClientMock.Verify(c => c.GetExchangeRatesRangeAsync(currency, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        _cacheMock.Verify(c => c.SetStringAsync(cacheKey, It.IsAny<string>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetExchangeRateAsync_WithMultipleFallbackDates_ShouldFindFirstAvailable()
    {
        // Arrange
        var currency = "BRL";
        var date = new DateTime(2023, 12, 15);
        var firstFallbackDate = new DateTime(2023, 12, 10);
        var rate = 5.25m;
        var cacheKey = $"exchange_rates_bucket_{currency}";

        _cacheMock
            .Setup(c => c.GetStringAsync(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        // API returns rates with multiple dates, first available should be used
        var apiRates = new List<ExchangeRateDto>
        {
            new() { Date = firstFallbackDate, Rate = rate },
            new() { Date = new DateTime(2023, 12, 8), Rate = 5.30m }
        };

        _treasuryApiClientMock
            .Setup(c => c.GetExchangeRatesRangeAsync(currency, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiRates);

        // Act
        var result = await _exchangeRateService.GetExchangeRateAsync(currency, date);

        // Assert
        Assert.Equal(rate, result);
        _treasuryApiClientMock.Verify(c => c.GetExchangeRatesRangeAsync(currency, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        _cacheMock.Verify(c => c.SetStringAsync(cacheKey, It.IsAny<string>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetExchangeRateAsync_WhenNoRateFoundWithinSixMonths_ShouldThrowException()
    {
        // Arrange
        var currency = "BRL";
        var date = new DateTime(2023, 12, 15);
        var cacheKey = $"exchange_rates_bucket_{currency}";

        _cacheMock
            .Setup(c => c.GetStringAsync(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        // API returns empty rates list
        var apiRates = new List<ExchangeRateDto>();

        _treasuryApiClientMock
            .Setup(c => c.GetExchangeRatesRangeAsync(currency, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiRates);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ExchangeRateUnavailableException>(
            () => _exchangeRateService.GetExchangeRateAsync(currency, date));

        Assert.Equal(currency, exception.Currency);
        Assert.Equal(date, exception.Date);
        _treasuryApiClientMock.Verify(c => c.GetExchangeRatesRangeAsync(currency, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        _cacheMock.Verify(c => c.SetStringAsync(cacheKey, It.IsAny<string>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetExchangeRateAsync_WhenRateIsExactlySixMonthsOld_ShouldReturnRate()
    {
        // Arrange
        var currency = "BRL";
        var date = new DateTime(2023, 12, 15);
        var sixMonthsAgo = date.AddMonths(-6);
        var rate = 5.25m;
        var cacheKey = $"exchange_rates_bucket_{currency}";

        _cacheMock
            .Setup(c => c.GetStringAsync(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        // API returns rates with 6-month-old rate
        var apiRates = new List<ExchangeRateDto>
        {
            new() { Date = sixMonthsAgo, Rate = rate }
        };

        _treasuryApiClientMock
            .Setup(c => c.GetExchangeRatesRangeAsync(currency, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiRates);

        // Act
        var result = await _exchangeRateService.GetExchangeRateAsync(currency, date);

        // Assert
        Assert.Equal(rate, result);
        _treasuryApiClientMock.Verify(c => c.GetExchangeRatesRangeAsync(currency, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        _cacheMock.Verify(c => c.SetStringAsync(cacheKey, It.IsAny<string>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetExchangeRateAsync_WhenRateIsOlderThanSixMonths_ShouldThrowException()
    {
        // Arrange
        var currency = "BRL";
        var date = new DateTime(2023, 12, 15);
        var cacheKey = $"exchange_rates_bucket_{currency}";

        _cacheMock
            .Setup(c => c.GetStringAsync(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        // API returns empty rates list (no rates within 6 months)
        var apiRates = new List<ExchangeRateDto>();

        _treasuryApiClientMock
            .Setup(c => c.GetExchangeRatesRangeAsync(currency, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiRates);

        // Act & Assert
        await Assert.ThrowsAsync<ExchangeRateUnavailableException>(
            () => _exchangeRateService.GetExchangeRateAsync(currency, date));

        _treasuryApiClientMock.Verify(c => c.GetExchangeRatesRangeAsync(currency, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        _cacheMock.Verify(c => c.SetStringAsync(cacheKey, It.IsAny<string>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetExchangeRateAsync_WhenCacheFails_ShouldStillWork()
    {
        // Arrange
        var currency = "BRL";
        var date = new DateTime(2023, 12, 15);
        var rate = 5.25m;
        var cacheKey = $"exchange_rates_bucket_{currency}";

        // Cache throws exception on get
        _cacheMock
            .Setup(c => c.GetStringAsync(cacheKey, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Cache error"));

        var apiRates = new List<ExchangeRateDto>
        {
            new() { Date = date, Rate = rate }
        };

        _treasuryApiClientMock
            .Setup(c => c.GetExchangeRatesRangeAsync(currency, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiRates);

        // Act
        var result = await _exchangeRateService.GetExchangeRateAsync(currency, date);

        // Assert
        Assert.Equal(rate, result);
        _treasuryApiClientMock.Verify(c => c.GetExchangeRatesRangeAsync(currency, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetExchangeRateAsync_WhenCacheSetFails_ShouldStillReturnRate()
    {
        // Arrange
        var currency = "BRL";
        var date = new DateTime(2023, 12, 15);
        var rate = 5.25m;
        var cacheKey = $"exchange_rates_bucket_{currency}";

        _cacheMock
            .Setup(c => c.GetStringAsync(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var apiRates = new List<ExchangeRateDto>
        {
            new() { Date = date, Rate = rate }
        };

        _treasuryApiClientMock
            .Setup(c => c.GetExchangeRatesRangeAsync(currency, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiRates);

        // Cache throws exception on set
        _cacheMock
            .Setup(c => c.SetStringAsync(cacheKey, It.IsAny<string>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Cache set error"));

        // Act
        var result = await _exchangeRateService.GetExchangeRateAsync(currency, date);

        // Assert
        Assert.Equal(rate, result);
        _treasuryApiClientMock.Verify(c => c.GetExchangeRatesRangeAsync(currency, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("USD")]
    [InlineData("EUR")]
    [InlineData("JPY")]
    [InlineData("GBP")]
    public async Task GetExchangeRateAsync_WithDifferentCurrencies_ShouldWorkCorrectly(string currency)
    {
        // Arrange
        var date = new DateTime(2023, 12, 15);
        var rate = 1.25m;
        var cacheKey = $"exchange_rates_bucket_{currency}";

        _cacheMock
            .Setup(c => c.GetStringAsync(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var apiRates = new List<ExchangeRateDto>
        {
            new() { Date = date, Rate = rate }
        };

        _treasuryApiClientMock
            .Setup(c => c.GetExchangeRatesRangeAsync(currency, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiRates);

        // Act
        var result = await _exchangeRateService.GetExchangeRateAsync(currency, date);

        // Assert
        Assert.Equal(rate, result);
        _treasuryApiClientMock.Verify(c => c.GetExchangeRatesRangeAsync(currency, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
