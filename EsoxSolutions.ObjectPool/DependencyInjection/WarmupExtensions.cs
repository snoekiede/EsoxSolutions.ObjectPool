using EsoxSolutions.ObjectPool.Pools;
using EsoxSolutions.ObjectPool.Warmup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EsoxSolutions.ObjectPool.DependencyInjection;

/// <summary>
/// Extension methods for pool warm-up functionality
/// </summary>
public static class WarmupExtensions
{
    /// <summary>
    /// Configures a pool to warm up automatically on application startup
    /// </summary>
    /// <typeparam name="T">The type of object in the pool</typeparam>
    /// <param name="services">The service collection</param>
    /// <param name="targetSize">Target number of objects to pre-create</param>
    /// <returns>The service collection for chaining</returns>
    /// <example>
    /// <code>
    /// services.AddDynamicObjectPool&lt;HttpClient&gt;(
    ///     sp => new HttpClient(),
    ///     config => config.MaxPoolSize = 100)
    ///     .WithAutoWarmup(50); // Pre-create 50 objects on startup
    /// </code>
    /// </example>
    public static IServiceCollection WithAutoWarmup<T>(
        this IServiceCollection services,
        int targetSize) where T : class
    {
        services.AddHostedService(sp => 
            new PoolWarmupHostedService<T>(
                sp.GetRequiredService<IObjectPoolWarmer<T>>(),
                sp.GetService<ILogger<PoolWarmupHostedService<T>>>(),
                targetSize,
                targetPercentage: null));

        return services;
    }

    /// <summary>
    /// Configures a pool to warm up to a percentage of capacity on application startup
    /// </summary>
    /// <typeparam name="T">The type of object in the pool</typeparam>
    /// <param name="services">The service collection</param>
    /// <param name="targetPercentage">Target percentage (0-100) of maximum capacity</param>
    /// <returns>The service collection for chaining</returns>
    /// <example>
    /// <code>
    /// services.AddDynamicObjectPool&lt;DbConnection&gt;(
    ///     sp => CreateConnection(),
    ///     config => config.MaxPoolSize = 100)
    ///     .WithAutoWarmupPercentage(75); // Pre-create 75% of max capacity
    /// </code>
    /// </example>
    public static IServiceCollection WithAutoWarmupPercentage<T>(
        this IServiceCollection services,
        double targetPercentage) where T : class
    {
        if (targetPercentage < 0 || targetPercentage > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(targetPercentage), "Target percentage must be between 0 and 100");
        }

        services.AddHostedService(sp =>
            new PoolWarmupHostedService<T>(
                sp.GetRequiredService<IObjectPoolWarmer<T>>(),
                sp.GetService<ILogger<PoolWarmupHostedService<T>>>(),
                targetSize: null,
                targetPercentage));

        return services;
    }

    /// <summary>
    /// Configures multiple pools to warm up on application startup
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configure">Configuration action for warm-up</param>
    /// <returns>The service collection for chaining</returns>
    /// <example>
    /// <code>
    /// services.ConfigurePoolWarmup(warmup =>
    /// {
    ///     warmup.WarmupPool&lt;HttpClient&gt;(50);
    ///     warmup.WarmupPool&lt;DbConnection&gt;(percentage: 80);
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection ConfigurePoolWarmup(
        this IServiceCollection services,
        Action<PoolWarmupBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new PoolWarmupBuilder(services);
        configure(builder);

        return services;
    }
}

/// <summary>
/// Builder for configuring pool warm-up for multiple pools
/// </summary>
public class PoolWarmupBuilder
{
    private readonly IServiceCollection _services;

    internal PoolWarmupBuilder(IServiceCollection services)
    {
        _services = services;
    }

    /// <summary>
    /// Configures warm-up for a specific pool type
    /// </summary>
    /// <typeparam name="T">The type of object in the pool</typeparam>
    /// <param name="targetSize">Target number of objects to pre-create</param>
    /// <param name="percentage">Optional target percentage instead of absolute size</param>
    /// <returns>The builder for chaining</returns>
    public PoolWarmupBuilder WarmupPool<T>(int? targetSize = null, double? percentage = null) where T : class
    {
        if (targetSize.HasValue)
        {
            _services.WithAutoWarmup<T>(targetSize.Value);
        }
        else if (percentage.HasValue)
        {
            _services.WithAutoWarmupPercentage<T>(percentage.Value);
        }
        else
        {
            throw new ArgumentException("Either targetSize or percentage must be specified");
        }

        return this;
    }
}

/// <summary>
/// Hosted service that warms up pools on application startup
/// </summary>
internal class PoolWarmupHostedService<T> : IHostedService where T : class
{
    private readonly IObjectPoolWarmer<T> _warmer;
    private readonly ILogger<PoolWarmupHostedService<T>>? _logger;
    private readonly int? _targetSize;
    private readonly double? _targetPercentage;

    public PoolWarmupHostedService(
        IObjectPoolWarmer<T> warmer,
        ILogger<PoolWarmupHostedService<T>>? logger,
        int? targetSize,
        double? targetPercentage)
    {
        _warmer = warmer;
        _logger = logger;
        _targetSize = targetSize;
        _targetPercentage = targetPercentage;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger?.LogInformation("Starting pool warm-up for {PoolType}", typeof(T).Name);

            if (_targetSize.HasValue)
            {
                await _warmer.WarmUpAsync(_targetSize.Value, cancellationToken);
            }
            else if (_targetPercentage.HasValue)
            {
                await _warmer.WarmUpToPercentageAsync(_targetPercentage.Value, cancellationToken);
            }

            var status = _warmer.GetWarmupStatus();
            _logger?.LogInformation(
                "Pool warm-up completed for {PoolType}: {Created} objects in {Duration}ms",
                typeof(T).Name,
                status.ObjectsCreated,
                status.WarmupDuration.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during pool warm-up for {PoolType}", typeof(T).Name);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
