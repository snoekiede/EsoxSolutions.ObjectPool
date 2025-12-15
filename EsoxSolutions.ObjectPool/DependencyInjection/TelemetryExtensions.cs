using EsoxSolutions.ObjectPool.Interfaces;
using EsoxSolutions.ObjectPool.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EsoxSolutions.ObjectPool.DependencyInjection;

/// <summary>
/// Extension methods for registering OpenTelemetry metrics with object pools
/// </summary>
public static class TelemetryExtensions
{
    /// <summary>
    /// Adds OpenTelemetry metrics for the specified object pool
    /// </summary>
    /// <typeparam name="T">The type of object in the pool</typeparam>
    /// <param name="services">The service collection</param>
    /// <param name="meterName">Optional meter name (default: "EsoxSolutions.ObjectPool")</param>
    /// <param name="poolName">Optional pool name for tagging</param>
    /// <returns>The service collection for chaining</returns>
    /// <example>
    /// <code>
    /// services.AddObjectPool&lt;HttpClient&gt;(builder => ...)
    ///     .AddObjectPoolMetrics&lt;HttpClient&gt;("http-client-pool");
    /// </code>
    /// </example>
    public static IServiceCollection AddObjectPoolMetrics<T>(
        this IServiceCollection services,
        string? meterName = null,
        string? poolName = null) where T : class
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(sp =>
        {
            var pool = sp.GetRequiredService<IObjectPool<T>>();
            return new ObjectPoolMeter<T>(pool, meterName, poolName);
        });

        return services;
    }

    /// <summary>
    /// Adds OpenTelemetry metrics for multiple object pools
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configure">Configuration action for registering metrics</param>
    /// <returns>The service collection for chaining</returns>
    /// <example>
    /// <code>
    /// services.AddObjectPoolsWithMetrics(metrics =>
    /// {
    ///     metrics.AddMetrics&lt;HttpClient&gt;("http-client-pool");
    ///     metrics.AddMetrics&lt;DbConnection&gt;("database-pool");
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddObjectPoolsWithMetrics(
        this IServiceCollection services,
        Action<ObjectPoolMetricsBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new ObjectPoolMetricsBuilder(services);
        configure(builder);

        return services;
    }
}

/// <summary>
/// Builder for configuring OpenTelemetry metrics for multiple pools
/// </summary>
public class ObjectPoolMetricsBuilder
{
    private readonly IServiceCollection _services;

    internal ObjectPoolMetricsBuilder(IServiceCollection services)
    {
        _services = services;
    }

    /// <summary>
    /// Adds metrics for a specific pool type
    /// </summary>
    /// <typeparam name="T">The type of object in the pool</typeparam>
    /// <param name="poolName">Optional pool name for tagging</param>
    /// <param name="meterName">Optional meter name</param>
    /// <returns>The builder for chaining</returns>
    public ObjectPoolMetricsBuilder AddMetrics<T>(
        string? poolName = null,
        string? meterName = null) where T : class
    {
        _services.AddObjectPoolMetrics<T>(meterName, poolName);
        return this;
    }
}
