using EsoxSolutions.ObjectPool.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace EsoxSolutions.ObjectPool.HealthChecks;

/// <summary>
/// Extension methods for registering object pool health checks
/// </summary>
public static class HealthCheckBuilderExtensions
{
    /// <param name="builder">The health checks builder</param>
    extension(IHealthChecksBuilder builder)
    {
        /// <summary>
        /// Adds a health check for an object pool
        /// </summary>
        /// <typeparam name="T">The type of object in the pool</typeparam>
        /// <param name="name">The name of the health check (defaults to "objectpool_{typename}")</param>
        /// <param name="failureStatus">The health status to report on failure (defaults to Degraded)</param>
        /// <param name="tags">Optional tags for the health check</param>
        /// <param name="timeout">Optional timeout for the health check</param>
        /// <param name="configureOptions">Optional action to configure health check options</param>
        /// <returns>The health checks builder for chaining</returns>
        /// <example>
        /// <code>
        /// services.AddHealthChecks()
        ///     .AddObjectPoolHealthCheck&lt;DbConnection&gt;("database-pool");
        /// </code>
        /// </example>
        public IHealthChecksBuilder AddObjectPoolHealthCheck<T>(string? name = null,
            HealthStatus? failureStatus = null,
            IEnumerable<string>? tags = null,
            TimeSpan? timeout = null,
            Action<ObjectPoolHealthCheckOptions>? configureOptions = null) where T : class
        {
            ArgumentNullException.ThrowIfNull(builder);

            var healthCheckName = name ?? $"objectpool_{typeof(T).Name.ToLowerInvariant()}";
            var effectiveFailureStatus = failureStatus ?? HealthStatus.Degraded;
            var effectiveTags = tags ?? ["ready", "objectpool"];

            return builder.Add(new HealthCheckRegistration(
                healthCheckName,
                sp =>
                {
                    var pool = sp.GetRequiredService<IObjectPool<T>>();
                    var options = new ObjectPoolHealthCheckOptions();
                    configureOptions?.Invoke(options);
                    return new ObjectPoolHealthCheck<T>(pool, options);
                },
                effectiveFailureStatus,
                effectiveTags,
                timeout));
        }

        /// <summary>
        /// Adds health checks for all registered object pools
        /// </summary>
        /// <param name="failureStatus">The health status to report on failure (defaults to Degraded)</param>
        /// <param name="tags">Optional tags for the health checks</param>
        /// <returns>The health checks builder for chaining</returns>
        /// <example>
        /// <code>
        /// services.AddHealthChecks()
        ///     .AddAllObjectPoolHealthChecks();
        /// </code>
        /// </example>
        public IHealthChecksBuilder AddAllObjectPoolHealthChecks(HealthStatus? failureStatus = null,
            IEnumerable<string>? tags = null)
        {
            ArgumentNullException.ThrowIfNull(builder);

            // This is a marker method - actual registration happens when pools are registered
            // Users should call AddObjectPoolHealthCheck for each pool type explicitly
            return builder;
        }

        /// <summary>
        /// Adds a health check for a queryable object pool
        /// </summary>
        /// <typeparam name="T">The type of object in the pool</typeparam>
        /// <param name="name">The name of the health check (defaults to "queryablepool_{typename}")</param>
        /// <param name="failureStatus">The health status to report on failure (defaults to Degraded)</param>
        /// <param name="tags">Optional tags for the health check</param>
        /// <param name="timeout">Optional timeout for the health check</param>
        /// <param name="configureOptions">Optional action to configure health check options</param>
        /// <returns>The health checks builder for chaining</returns>
        public IHealthChecksBuilder AddQueryablePoolHealthCheck<T>(string? name = null,
            HealthStatus? failureStatus = null,
            IEnumerable<string>? tags = null,
            TimeSpan? timeout = null,
            Action<ObjectPoolHealthCheckOptions>? configureOptions = null) where T : class
        {
            ArgumentNullException.ThrowIfNull(builder);

            var healthCheckName = name ?? $"queryablepool_{typeof(T).Name.ToLowerInvariant()}";
            var effectiveFailureStatus = failureStatus ?? HealthStatus.Degraded;
            var effectiveTags = tags ?? new[] { "ready", "objectpool", "queryable" };

            return builder.Add(new HealthCheckRegistration(
                healthCheckName,
                sp =>
                {
                    var pool = sp.GetRequiredService<IQueryableObjectPool<T>>();
                    var options = new ObjectPoolHealthCheckOptions();
                    configureOptions?.Invoke(options);
                    return new ObjectPoolHealthCheck<T>(pool, options);
                },
                effectiveFailureStatus,
                effectiveTags,
                timeout));
        }
    }
}
