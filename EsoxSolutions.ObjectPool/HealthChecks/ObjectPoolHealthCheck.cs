using EsoxSolutions.ObjectPool.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace EsoxSolutions.ObjectPool.HealthChecks;

/// <summary>
/// Health check for monitoring object pool status
/// </summary>
/// <typeparam name="T">The type of object in the pool</typeparam>
public class ObjectPoolHealthCheck<T> : IHealthCheck where T : class
{
    private readonly IObjectPool<T> _pool;
    private readonly ObjectPoolHealthCheckOptions _options;

    /// <summary>
    /// Creates a new instance of ObjectPoolHealthCheck
    /// </summary>
    /// <param name="pool">The object pool to monitor</param>
    /// <param name="options">Health check options</param>
    public ObjectPoolHealthCheck(IObjectPool<T> pool, ObjectPoolHealthCheckOptions? options = null)
    {
        _pool = pool ?? throw new ArgumentNullException(nameof(pool));
        _options = options ?? new ObjectPoolHealthCheckOptions();
    }

    /// <summary>
    /// Checks the health of the object pool
    /// </summary>
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (_pool is not IPoolHealth poolHealth)
            {
                return Task.FromResult(HealthCheckResult.Healthy(
                    "Pool does not implement IPoolHealth",
                    new Dictionary<string, object>
                    {
                        ["available"] = _pool.AvailableObjectCount
                    }));
            }

            var health = poolHealth.GetHealthStatus();
            var utilization = poolHealth.UtilizationPercentage;

            var data = new Dictionary<string, object>
            {
                ["utilization_percentage"] = utilization,
                ["available_objects"] = health.Diagnostics["CurrentAvailable"],
                ["active_objects"] = health.Diagnostics["CurrentActive"],
                ["peak_active"] = health.Diagnostics["PeakActive"],
                ["total_retrieved"] = health.Diagnostics["TotalRetrieved"],
                ["total_returned"] = health.Diagnostics["TotalReturned"],
                ["pool_empty_events"] = health.Diagnostics["PoolEmptyEvents"],
                ["last_checked"] = health.LastChecked
            };

            // Determine health status based on thresholds
            if (!health.IsHealthy || utilization >= _options.UnhealthyUtilizationThreshold)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy(
                    health.HealthMessage ?? "Pool is unhealthy",
                    data: data));
            }

            if (utilization >= _options.DegradedUtilizationThreshold || health.WarningCount > 0)
            {
                return Task.FromResult(HealthCheckResult.Degraded(
                    health.HealthMessage ?? $"Pool is degraded: {string.Join(", ", health.Warnings)}",
                    data: data));
            }

            return Task.FromResult(HealthCheckResult.Healthy(
                "Pool is healthy",
                data));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "Error checking pool health",
                ex,
                new Dictionary<string, object>
                {
                    ["error"] = ex.Message
                }));
        }
    }
}

/// <summary>
/// Options for configuring object pool health checks
/// </summary>
public class ObjectPoolHealthCheckOptions
{
    /// <summary>
    /// Utilization percentage threshold for degraded status (default: 75%)
    /// </summary>
    public double DegradedUtilizationThreshold { get; set; } = 75.0;

    /// <summary>
    /// Utilization percentage threshold for unhealthy status (default: 95%)
    /// </summary>
    public double UnhealthyUtilizationThreshold { get; set; } = 95.0;

    /// <summary>
    /// Optional custom health check function
    /// </summary>
    public Func<IPoolHealth, HealthCheckResult>? CustomHealthCheck { get; set; }
}
