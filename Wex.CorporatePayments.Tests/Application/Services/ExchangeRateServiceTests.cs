using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Wex.CorporatePayments.Application.Exceptions;
using Wex.CorporatePayments.Application.Services;
using Wex.CorporatePayments.Infrastructure.Clients;

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
        var cacheKey = $"exchange_rate_{currency}_{date:yyyy-MM-dd}";

        _cacheMock
            .Setup(c => c.GetStringAsync(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedRate.ToString());

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
        var cacheKey = $"exchange_rate_{currency}_{date:yyyy-MM-dd}";

        _cacheMock
            .Setup(c => c.GetStringAsync(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        _treasuryApiClientMock
            .Setup(c => c.GetExchangeRateAsync(currency, date, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rate);

        // Act
        var result = await _exchangeRateService.GetExchangeRateAsync(currency, date);

        // Assert
        Assert.Equal(rate, result);
        _treasuryApiClientMock.Verify(c => c.GetExchangeRateAsync(currency, date, It.IsAny<CancellationToken>()), Times.Once);
        _cacheMock.Verify(c => c.SetStringAsync(cacheKey, rate.ToString(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetExchangeRateAsync_WithFallbackToPreviousDate_ShouldReturnAndCache()
    {
        // Arrange
        var currency = "BRL";
        var date = new DateTime(2023, 12, 15);
        var fallbackDate = new DateTime(2023, 12, 14);
        var rate = 5.25m;
        var cacheKey = $"exchange_rate_{currency}_{date:yyyy-MM-dd}";

        _cacheMock
            .Setup(c => c.GetStringAsync(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        // No rate on exact date
        _treasuryApiClientMock
            .Setup(c => c.GetExchangeRateAsync(currency, date, It.IsAny<CancellationToken>()))
            .ReturnsAsync((decimal?)null);

        // Rate found on fallback date
        _treasuryApiClientMock
            .Setup(c => c.GetExchangeRateAsync(currency, fallbackDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rate);

        // All other dates return null
        for (var d = date.AddDays(-2); d >= date.AddMonths(-6); d = d.AddDays(-1))
        {
            if (d != fallbackDate)
            {
                _treasuryApiClientMock
                    .Setup(c => c.GetExchangeRateAsync(currency, d, It.IsAny<CancellationToken>()))
                    .ReturnsAsync((decimal?)null);
            }
        }

        // Act
        var result = await _exchangeRateService.GetExchangeRateAsync(currency, date);

        // Assert
        Assert.Equal(rate, result);
        _treasuryApiClientMock.Verify(c => c.GetExchangeRateAsync(currency, date, It.IsAny<CancellationToken>()), Times.Once);
        _treasuryApiClientMock.Verify(c => c.GetExchangeRateAsync(currency, fallbackDate, It.IsAny<CancellationToken>()), Times.Once);
        _cacheMock.Verify(c => c.SetStringAsync(cacheKey, rate.ToString(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetExchangeRateAsync_WithMultipleFallbackDates_ShouldFindFirstAvailable()
    {
        // Arrange
        var currency = "BRL";
        var date = new DateTime(2023, 12, 15);
        var firstFallbackDate = new DateTime(2023, 12, 10);
        var secondFallbackDate = new DateTime(2023, 12, 8);
        var rate = 5.25m;
        var cacheKey = $"exchange_rate_{currency}_{date:yyyy-MM-dd}";

        _cacheMock
            .Setup(c => c.GetStringAsync(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        // No rate on exact date
        _treasuryApiClientMock
            .Setup(c => c.GetExchangeRateAsync(currency, date, It.IsAny<CancellationToken>()))
            .ReturnsAsync((decimal?)null);

        // No rate on first few fallback dates
        for (var d = date.AddDays(-1); d > firstFallbackDate; d = d.AddDays(-1))
        {
            _treasuryApiClientMock
                .Setup(c => c.GetExchangeRateAsync(currency, d, It.IsAny<CancellationToken>()))
                .ReturnsAsync((decimal?)null);
        }

        // Rate found on first fallback date
        _treasuryApiClientMock
            .Setup(c => c.GetExchangeRateAsync(currency, firstFallbackDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rate);

        // Act
        var result = await _exchangeRateService.GetExchangeRateAsync(currency, date);

        // Assert
        Assert.Equal(rate, result);
        _treasuryApiClientMock.Verify(c => c.GetExchangeRateAsync(currency, firstFallbackDate, It.IsAny<CancellationToken>()), Times.Once);
        _treasuryApiClientMock.Verify(c => c.GetExchangeRateAsync(currency, secondFallbackDate, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetExchangeRateAsync_WhenNoRateFoundWithinSixMonths_ShouldThrowException()
    {
        // Arrange
        var currency = "BRL";
        var date = new DateTime(2023, 12, 15);
        var sixMonthsAgo = date.AddMonths(-6);
        var cacheKey = $"exchange_rate_{currency}_{date:yyyy-MM-dd}";

        _cacheMock
            .Setup(c => c.GetStringAsync(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        // No rate found for any date within 6 months
        for (var d = date; d >= sixMonthsAgo; d = d.AddDays(-1))
        {
            _treasuryApiClientMock
                .Setup(c => c.GetExchangeRateAsync(currency, d, It.IsAny<CancellationToken>()))
                .ReturnsAsync((decimal?)null);
        }

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ExchangeRateUnavailableException>(
            () => _exchangeRateService.GetExchangeRateAsync(currency, date));

        Assert.Equal(currency, exception.Currency);
        Assert.Equal(date, exception.Date);
        
        // Verify it tried the exact date and all fallback dates
        _treasuryApiClientMock.Verify(c => c.GetExchangeRateAsync(currency, date, It.IsAny<CancellationToken>()), Times.Once);
        
        var expectedCallCount = 1; // exact date
        for (var d = date.AddDays(-1); d >= sixMonthsAgo; d = d.AddDays(-1))
        {
            expectedCallCount++;
            _treasuryApiClientMock.Verify(c => c.GetExchangeRateAsync(currency, d, It.IsAny<CancellationToken>()), Times.Once);
        }
    }

    [Fact]
    public async Task GetExchangeRateAsync_WhenRateIsExactlySixMonthsOld_ShouldReturnRate()
    {
        // Arrange
        var currency = "BRL";
        var date = new DateTime(2023, 12, 15);
        var sixMonthsAgo = date.AddMonths(-6);
        var rate = 5.25m;
        var cacheKey = $"exchange_rate_{currency}_{date:yyyy-MM-dd}";

        _cacheMock
            .Setup(c => c.GetStringAsync(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        // No rate on recent dates
        for (var d = date; d > sixMonthsAgo; d = d.AddDays(-1))
        {
            _treasuryApiClientMock
                .Setup(c => c.GetExchangeRateAsync(currency, d, It.IsAny<CancellationToken>()))
                .ReturnsAsync((decimal?)null);
        }

        // Rate found exactly 6 months ago
        _treasuryApiClientMock
            .Setup(c => c.GetExchangeRateAsync(currency, sixMonthsAgo, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rate);

        // Act
        var result = await _exchangeRateService.GetExchangeRateAsync(currency, date);

        // Assert
        Assert.Equal(rate, result);
        _treasuryApiClientMock.Verify(c => c.GetExchangeRateAsync(currency, sixMonthsAgo, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetExchangeRateAsync_WhenRateIsOlderThanSixMonths_ShouldThrowException()
    {
        // Arrange
        var currency = "BRL";
        var date = new DateTime(2023, 12, 15);
        var olderThanSixMonths = date.AddMonths(-6).AddDays(-1);
        var rate = 5.25m;
        var cacheKey = $"exchange_rate_{currency}_{date:yyyy-MM-dd}";

        _cacheMock
            .Setup(c => c.GetStringAsync(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        // No rate within 6 months
        for (var d = date; d >= date.AddMonths(-6); d = d.AddDays(-1))
        {
            _treasuryApiClientMock
                .Setup(c => c.GetExchangeRateAsync(currency, d, It.IsAny<CancellationToken>()))
                .ReturnsAsync((decimal?)null);
        }

        // Rate exists but is older than 6 months (should not be checked)
        _treasuryApiClientMock
            .Setup(c => c.GetExchangeRateAsync(currency, olderThanSixMonths, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rate);

        // Act & Assert
        await Assert.ThrowsAsync<ExchangeRateUnavailableException>(
            () => _exchangeRateService.GetExchangeRateAsync(currency, date));

        // Verify it never checked dates older than 6 months
        _treasuryApiClientMock.Verify(c => c.GetExchangeRateAsync(currency, olderThanSixMonths, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetExchangeRateAsync_WhenCacheFails_ShouldStillWork()
    {
        // Arrange
        var currency = "BRL";
        var date = new DateTime(2023, 12, 15);
        var rate = 5.25m;
        var cacheKey = $"exchange_rate_{currency}_{date:yyyy-MM-dd}";

        // Cache throws exception on get
        _cacheMock
            .Setup(c => c.GetStringAsync(cacheKey, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Cache error"));

        _treasuryApiClientMock
            .Setup(c => c.GetExchangeRateAsync(currency, date, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rate);

        // Act
        var result = await _exchangeRateService.GetExchangeRateAsync(currency, date);

        // Assert
        Assert.Equal(rate, result);
        _treasuryApiClientMock.Verify(c => c.GetExchangeRateAsync(currency, date, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetExchangeRateAsync_WhenCacheSetFails_ShouldStillReturnRate()
    {
        // Arrange
        var currency = "BRL";
        var date = new DateTime(2023, 12, 15);
        var rate = 5.25m;
        var cacheKey = $"exchange_rate_{currency}_{date:yyyy-MM-dd}";

        _cacheMock
            .Setup(c => c.GetStringAsync(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        _treasuryApiClientMock
            .Setup(c => c.GetExchangeRateAsync(currency, date, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rate);

        // Cache throws exception on set
        _cacheMock
            .Setup(c => c.SetStringAsync(cacheKey, rate.ToString(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Cache set error"));

        // Act
        var result = await _exchangeRateService.GetExchangeRateAsync(currency, date);

        // Assert
        Assert.Equal(rate, result);
        _treasuryApiClientMock.Verify(c => c.GetExchangeRateAsync(currency, date, It.IsAny<CancellationToken>()), Times.Once);
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
        var cacheKey = $"exchange_rate_{currency}_{date:yyyy-MM-dd}";

        _cacheMock
            .Setup(c => c.GetStringAsync(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        _treasuryApiClientMock
            .Setup(c => c.GetExchangeRateAsync(currency, date, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rate);

        // Act
        var result = await _exchangeRateService.GetExchangeRateAsync(currency, date);

        // Assert
        Assert.Equal(rate, result);
        _treasuryApiClientMock.Verify(c => c.GetExchangeRateAsync(currency, date, It.IsAny<CancellationToken>()), Times.Once);
    }
}
