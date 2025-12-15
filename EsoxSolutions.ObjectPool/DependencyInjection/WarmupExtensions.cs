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
    /// <param name="services">The service collection</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Configures a pool to warm up automatically on application startup
        /// </summary>
        /// <typeparam name="T">The type of object in the pool</typeparam>
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
        public IServiceCollection WithAutoWarmup<T>(int targetSize) where T : class
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
        public IServiceCollection WithAutoWarmupPercentage<T>(double targetPercentage) where T : class
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
        public IServiceCollection ConfigurePoolWarmup(Action<PoolWarmupBuilder> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);

            var builder = new PoolWarmupBuilder(services);
            configure(builder);

            return services;
        }
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
internal class PoolWarmupHostedService<T>(
    IObjectPoolWarmer<T> warmer,
    ILogger<PoolWarmupHostedService<T>>? logger,
    int? targetSize,
    double? targetPercentage)
    : IHostedService
    where T : class
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            logger?.LogInformation("Starting pool warm-up for {PoolType}", typeof(T).Name);

            if (targetSize.HasValue)
            {
                await warmer.WarmUpAsync(targetSize.Value, cancellationToken);
            }
            else if (targetPercentage.HasValue)
            {
                await warmer.WarmUpToPercentageAsync(targetPercentage.Value, cancellationToken);
            }

            var status = warmer.GetWarmupStatus();
            logger?.LogInformation(
                "Pool warm-up completed for {PoolType}: {Created} objects in {Duration}ms",
                typeof(T).Name,
                status.ObjectsCreated,
                status.WarmupDuration.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error during pool warm-up for {PoolType}", typeof(T).Name);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
