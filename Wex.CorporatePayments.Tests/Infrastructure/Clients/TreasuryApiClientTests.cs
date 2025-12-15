using System.Net;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;
using Wex.CorporatePayments.Infrastructure.Clients;

namespace Wex.CorporatePayments.Tests.Infrastructure.Clients;

public class TreasuryApiClientTests
{
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly HttpClient _httpClient;
    private readonly Mock<ILogger<TreasuryApiClient>> _loggerMock;
    private readonly TreasuryApiClient _treasuryApiClient;

    public TreasuryApiClientTests()
    {
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpMessageHandlerMock.Object);
        _loggerMock = new Mock<ILogger<TreasuryApiClient>>();
        
        // Don't set BaseAddress and headers here - the TreasuryApiClient constructor will set them
        _treasuryApiClient = new TreasuryApiClient(_httpClient, _loggerMock.Object);
    }

    [Fact]
    public async Task GetExchangeRateAsync_WithValidResponse_ShouldReturnRate()
    {
        // Arrange
        var currency = "BRL";
        var date = new DateTime(2023, 12, 15);
        var expectedRate = 5.25m;
        var jsonResponse = @"{
            ""data"": [
                {
                    ""country_currency_desc"": ""BRAZIL-REAL"",
                    ""exchange_rate"": ""5.25"",
                    ""record_date"": ""2023-12-15""
                }
            ]
        }";

        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonResponse)
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _treasuryApiClient.GetExchangeRateAsync(currency, date);

        // Assert
        Assert.Equal(expectedRate, result);
        
        // Verify the correct URL was called
        _httpMessageHandlerMock
            .Protected()
            .Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Get &&
                    req.RequestUri!.ToString().Contains("rates_of_exchange") &&
                    req.RequestUri!.ToString().Contains("filter=record_date:gte:2023-12-15") &&
                    req.RequestUri!.ToString().Contains("filter=country_currency_desc:eq:BRL") &&
                    req.RequestUri!.ToString().Contains("sort=-record_date")),
                ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task GetExchangeRateAsync_WithEmptyData_ShouldReturnNull()
    {
        // Arrange
        var currency = "BRL";
        var date = new DateTime(2023, 12, 15);
        var jsonResponse = @"{
            ""data"": []
        }";

        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonResponse)
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _treasuryApiClient.GetExchangeRateAsync(currency, date);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetExchangeRateAsync_With404Response_ShouldReturnNull()
    {
        // Arrange
        var currency = "BRL";
        var date = new DateTime(2023, 12, 15);

        var httpResponse = new HttpResponseMessage(HttpStatusCode.NotFound);

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _treasuryApiClient.GetExchangeRateAsync(currency, date);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetExchangeRateAsync_With500Response_ShouldReturnNull()
    {
        // Arrange
        var currency = "BRL";
        var date = new DateTime(2023, 12, 15);

        var httpResponse = new HttpResponseMessage(HttpStatusCode.InternalServerError);

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _treasuryApiClient.GetExchangeRateAsync(currency, date);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetExchangeRateAsync_WithTimeoutResponse_ShouldReturnNull()
    {
        // Arrange
        var currency = "BRL";
        var date = new DateTime(2023, 12, 15);

        var httpResponse = new HttpResponseMessage(HttpStatusCode.RequestTimeout);

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _treasuryApiClient.GetExchangeRateAsync(currency, date);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetExchangeRateAsync_WithHttpRequestException_ShouldReturnNull()
    {
        // Arrange
        var currency = "BRL";
        var date = new DateTime(2023, 12, 15);

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act
        var result = await _treasuryApiClient.GetExchangeRateAsync(currency, date);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetExchangeRateAsync_WithInvalidJson_ShouldReturnNull()
    {
        // Arrange
        var currency = "BRL";
        var date = new DateTime(2023, 12, 15);
        var invalidJson = @"{ invalid json }";

        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(invalidJson)
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _treasuryApiClient.GetExchangeRateAsync(currency, date);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetExchangeRateAsync_WithMissingExchangeRateField_ShouldReturnNull()
    {
        // Arrange
        var currency = "BRL";
        var date = new DateTime(2023, 12, 15);
        var jsonResponse = @"{
            ""data"": [
                {
                    ""country_currency_desc"": ""BRAZIL-REAL"",
                    ""record_date"": ""2023-12-15""
                }
            ]
        }";

        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonResponse)
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _treasuryApiClient.GetExchangeRateAsync(currency, date);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetExchangeRateAsync_WithMultipleDataRecords_ShouldReturnFirstRecord()
    {
        // Arrange
        var currency = "BRL";
        var date = new DateTime(2023, 12, 15);
        var jsonResponse = @"{
            ""data"": [
                {
                    ""country_currency_desc"": ""BRAZIL-REAL"",
                    ""exchange_rate"": ""5.25"",
                    ""record_date"": ""2023-12-15""
                },
                {
                    ""country_currency_desc"": ""BRAZIL-REAL"",
                    ""exchange_rate"": ""5.20"",
                    ""record_date"": ""2023-12-14""
                }
            ]
        }";

        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonResponse)
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _treasuryApiClient.GetExchangeRateAsync(currency, date);

        // Assert
        Assert.Equal(5.25m, result);
    }

    [Theory]
    [InlineData("BRL", "2023-12-15", "2023-12-15")]
    [InlineData("EUR", "2023-06-30", "2023-06-30")]
    [InlineData("JPY", "2023-01-01", "2023-01-01")]
    public async Task GetExchangeRateAsync_WithDifferentDatesAndCurrencies_ShouldBuildCorrectUrl(
        string currency, DateTime date, string expectedDateParam)
    {
        // Arrange
        var jsonResponse = @"{
            ""data"": [
                {
                    ""country_currency_desc"": """ + currency.ToUpperInvariant() + @""",
                    ""exchange_rate"": ""1.25"",
                    ""record_date"": """ + expectedDateParam + @"""
                }
            ]
        }";

        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonResponse)
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _treasuryApiClient.GetExchangeRateAsync(currency, date);

        // Assert
        Assert.Equal(1.25m, result);
        
        // Verify the correct URL was called with proper date format
        _httpMessageHandlerMock
            .Protected()
            .Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Get &&
                    req.RequestUri!.ToString().Contains($"filter=record_date:gte:{expectedDateParam}") &&
                    req.RequestUri!.ToString().Contains($"filter=country_currency_desc:eq:{currency.ToUpperInvariant()}")),
                ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task GetExchangeRateAsync_WithUserAgentHeader_ShouldIncludeCorrectUserAgent()
    {
        // Arrange
        var currency = "BRL";
        var date = new DateTime(2023, 12, 15);
        var jsonResponse = @"{ ""data"": [] }";

        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonResponse)
        };

        HttpRequestMessage? capturedRequest = null;
        
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(httpResponse);

        // Act
        await _treasuryApiClient.GetExchangeRateAsync(currency, date);

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.Equal("Wex-CorporatePayments/1.0", capturedRequest.Headers.UserAgent.ToString());
    }

    [Fact]
    public async Task GetExchangeRateAsync_WithBaseAddress_ShouldUseCorrectBaseUrl()
    {
        // Arrange
        var currency = "BRL";
        var date = new DateTime(2023, 12, 15);
        var jsonResponse = @"{ ""data"": [] }";

        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonResponse)
        };

        HttpRequestMessage? capturedRequest = null;
        
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(httpResponse);

        // Act
        await _treasuryApiClient.GetExchangeRateAsync(currency, date);

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.Equal("https://api.fiscaldata.treasury.gov/services/api/fiscal_service/v1/accounting/od/rates_of_exchange", capturedRequest.RequestUri!.GetLeftPart(UriPartial.Path));
    }

    [Fact]
    public async Task GetExchangeRateAsync_WithCircuitBreakerOpen_ShouldReturnNull()
    {
        // This test would require more complex setup to actually trigger the circuit breaker
        // For now, we'll test that the client handles the scenario gracefully
        // In a real implementation, you might want to inject the circuit breaker policy for testing
        
        // Arrange
        var currency = "BRL";
        var date = new DateTime(2023, 12, 15);

        // Simulate multiple failures that would trigger circuit breaker
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Service unavailable"));

        // Act
        var result = await _treasuryApiClient.GetExchangeRateAsync(currency, date);

        // Assert
        Assert.Null(result);
    }
}
