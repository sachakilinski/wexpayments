using Microsoft.Extensions.Diagnostics.HealthChecks;
using Wex.CorporatePayments.Application.Interfaces;

namespace Wex.CorporatePayments.Api.Health;

public class TreasuryApiHealthCheck : IHealthCheck
{
    private readonly ITreasuryApiClient _treasuryApiClient;
    private readonly ILogger<TreasuryApiHealthCheck> _logger;

    public TreasuryApiHealthCheck(ITreasuryApiClient treasuryApiClient, ILogger<TreasuryApiHealthCheck> logger)
    {
        _treasuryApiClient = treasuryApiClient;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // Use a shorter timeout for health checks to avoid long waits
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5)); // 5 second timeout for health check
            
            // Try to get a recent exchange rate to verify API connectivity
            // Use a historical date that's guaranteed to have data (2015)
            var testDate = new DateTime(2015, 01, 01);
            var rate = await _treasuryApiClient.GetExchangeRateAsync("Canada-Dollar", testDate, cts.Token);
            
            if (rate.HasValue)
            {
                return HealthCheckResult.Healthy("Treasury API is responding correctly");
            }
            else
            {
                return HealthCheckResult.Degraded("Treasury API responded but no data available");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Treasury API health check timed out");
            return HealthCheckResult.Degraded("Treasury API health check timed out");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Treasury API health check failed");
            return HealthCheckResult.Unhealthy("Treasury API is unavailable", ex);
        }
    }
}
