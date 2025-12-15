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
            // Try to get a recent exchange rate to verify API connectivity
            var rate = await _treasuryApiClient.GetExchangeRateAsync("USD", DateTime.UtcNow.AddDays(-1), cancellationToken);
            
            if (rate.HasValue)
            {
                return HealthCheckResult.Healthy("Treasury API is responding correctly");
            }
            else
            {
                return HealthCheckResult.Degraded("Treasury API responded but no data available");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Treasury API health check failed");
            return HealthCheckResult.Unhealthy("Treasury API is unavailable", ex);
        }
    }
}
