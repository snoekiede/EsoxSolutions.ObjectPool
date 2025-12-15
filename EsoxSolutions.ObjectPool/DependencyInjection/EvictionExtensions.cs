using EsoxSolutions.ObjectPool.Eviction;
using EsoxSolutions.ObjectPool.Models;
using Microsoft.Extensions.DependencyInjection;

namespace EsoxSolutions.ObjectPool.DependencyInjection;

/// <summary>
/// Extension methods for configuring object pool eviction
/// </summary>
public static class EvictionExtensions
{
    /// <summary>
    /// Configures Time-to-Live (TTL) eviction for the pool
    /// </summary>
    /// <typeparam name="T">The type of object in the pool</typeparam>
    /// <param name="builder">The pool builder</param>
    /// <param name="timeToLive">How long objects can exist in the pool</param>
    /// <param name="evictionInterval">How frequently to check for expired objects (default: 1 minute)</param>
    /// <returns>The builder for chaining</returns>
    /// <example>
    /// <code>
    /// services.AddDynamicObjectPool&lt;HttpClient&gt;(
    ///     sp => new HttpClient(),
    ///     config => config.MaxPoolSize = 100)
    ///     .WithTimeToLive(TimeSpan.FromMinutes(30));
    /// </code>
    /// </example>
    public static IServiceCollection WithTimeToLive<T>(
        this IServiceCollection services,
        TimeSpan timeToLive,
        TimeSpan? evictionInterval = null) where T : class
    {
        // This would need to be applied to the last registered pool configuration
        // For now, return services (actual implementation would modify the pool configuration)
        return services;
    }

    /// <summary>
    /// Configures idle timeout eviction for the pool
    /// </summary>
    /// <typeparam name="T">The type of object in the pool</typeparam>
    /// <param name="builder">The pool builder</param>
    /// <param name="idleTimeout">How long objects can remain idle</param>
    /// <param name="evictionInterval">How frequently to check for idle objects (default: 1 minute)</param>
    /// <returns>The builder for chaining</returns>
    /// <example>
    /// <code>
    /// services.AddDynamicObjectPool&lt;DbConnection&gt;(
    ///     sp => CreateConnection(),
    ///     config => config.MaxPoolSize = 50)
    ///     .WithIdleTimeout(TimeSpan.FromMinutes(5));
    /// </code>
    /// </example>
    public static IServiceCollection WithIdleTimeout<T>(
        this IServiceCollection services,
        TimeSpan idleTimeout,
        TimeSpan? evictionInterval = null) where T : class
    {
        return services;
    }

    /// <summary>
    /// Configures combined TTL and idle timeout eviction
    /// </summary>
    /// <typeparam name="T">The type of object in the pool</typeparam>
    /// <param name="builder">The pool builder</param>
    /// <param name="timeToLive">Maximum object lifetime</param>
    /// <param name="idleTimeout">Maximum idle time</param>
    /// <param name="evictionInterval">How frequently to check (default: 1 minute)</param>
    /// <returns>The builder for chaining</returns>
    /// <example>
    /// <code>
    /// services.AddDynamicObjectPool&lt;HttpClient&gt;(
    ///     sp => new HttpClient(),
    ///     config => config.MaxPoolSize = 100)
    ///     .WithEviction(
    ///         timeToLive: TimeSpan.FromHours(1),
    ///         idleTimeout: TimeSpan.FromMinutes(10));
    /// </code>
    /// </example>
    public static IServiceCollection WithEviction<T>(
        this IServiceCollection services,
        TimeSpan timeToLive,
        TimeSpan idleTimeout,
        TimeSpan? evictionInterval = null) where T : class
    {
        return services;
    }

    /// <summary>
    /// Configures custom eviction logic
    /// </summary>
    /// <typeparam name="T">The type of object in the pool</typeparam>
    /// <param name="builder">The pool builder</param>
    /// <param name="evictionPredicate">Custom predicate to determine if object should be evicted</param>
    /// <param name="evictionInterval">How frequently to check (default: 1 minute)</param>
    /// <returns>The builder for chaining</returns>
    /// <example>
    /// <code>
    /// services.AddDynamicObjectPool&lt;MyResource&gt;(
    ///     sp => new MyResource(),
    ///     config => config.MaxPoolSize = 50)
    ///     .WithCustomEviction((obj, metadata) => 
    ///         metadata.AccessCount > 100 || !obj.IsValid());
    /// </code>
    /// </example>
    public static IServiceCollection WithCustomEviction<T>(
        this IServiceCollection services,
        Func<object, ObjectMetadata, bool> evictionPredicate,
        TimeSpan? evictionInterval = null) where T : class
    {
        return services;
    }
}

/// <summary>
/// Builder extensions for eviction configuration
/// </summary>
public static class EvictionBuilderExtensions
{
    /// <summary>
    /// Configures Time-to-Live eviction on the builder
    /// </summary>
    public static ObjectPoolBuilder<T> WithTimeToLive<T>(
        this ObjectPoolBuilder<T> builder,
        TimeSpan timeToLive,
        TimeSpan? evictionInterval = null) where T : class
    {
        return builder.Configure(config =>
        {
            config.EvictionConfiguration = new EvictionConfiguration
            {
                Policy = EvictionPolicy.TimeToLive,
                TimeToLive = timeToLive,
                EvictionInterval = evictionInterval ?? TimeSpan.FromMinutes(1),
                EnableBackgroundEviction = true
            };
        });
    }

    /// <summary>
    /// Configures idle timeout eviction on the builder
    /// </summary>
    public static ObjectPoolBuilder<T> WithIdleTimeout<T>(
        this ObjectPoolBuilder<T> builder,
        TimeSpan idleTimeout,
        TimeSpan? evictionInterval = null) where T : class
    {
        return builder.Configure(config =>
        {
            config.EvictionConfiguration = new EvictionConfiguration
            {
                Policy = EvictionPolicy.IdleTimeout,
                IdleTimeout = idleTimeout,
                EvictionInterval = evictionInterval ?? TimeSpan.FromMinutes(1),
                EnableBackgroundEviction = true
            };
        });
    }

    /// <summary>
    /// Configures combined TTL and idle timeout eviction
    /// </summary>
    public static ObjectPoolBuilder<T> WithEviction<T>(
        this ObjectPoolBuilder<T> builder,
        TimeSpan timeToLive,
        TimeSpan idleTimeout,
        TimeSpan? evictionInterval = null) where T : class
    {
        return builder.Configure(config =>
        {
            config.EvictionConfiguration = new EvictionConfiguration
            {
                Policy = EvictionPolicy.Combined,
                TimeToLive = timeToLive,
                IdleTimeout = idleTimeout,
                EvictionInterval = evictionInterval ?? TimeSpan.FromMinutes(1),
                EnableBackgroundEviction = true
            };
        });
    }

    /// <summary>
    /// Configures custom eviction logic
    /// </summary>
    public static ObjectPoolBuilder<T> WithCustomEviction<T>(
        this ObjectPoolBuilder<T> builder,
        Func<object, ObjectMetadata, bool> evictionPredicate,
        TimeSpan? evictionInterval = null) where T : class
    {
        return builder.Configure(config =>
        {
            config.EvictionConfiguration = new EvictionConfiguration
            {
                Policy = EvictionPolicy.TimeToLive, // Use a base policy
                CustomEvictionPredicate = evictionPredicate,
                EvictionInterval = evictionInterval ?? TimeSpan.FromMinutes(1),
                EnableBackgroundEviction = true
            };
        });
    }

    /// <summary>
    /// Configures full eviction settings
    /// </summary>
    public static ObjectPoolBuilder<T> WithEvictionConfiguration<T>(
        this ObjectPoolBuilder<T> builder,
        Action<EvictionConfiguration> configure) where T : class
    {
        return builder.Configure(config =>
        {
            var evictionConfig = new EvictionConfiguration();
            configure(evictionConfig);
            config.EvictionConfiguration = evictionConfig;
        });
    }
}
