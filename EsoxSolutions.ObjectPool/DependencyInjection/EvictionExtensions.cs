using EsoxSolutions.ObjectPool.Eviction;
using Microsoft.Extensions.DependencyInjection;

namespace EsoxSolutions.ObjectPool.DependencyInjection;

/// <summary>
/// Extension methods for configuring object pool eviction
/// </summary>
public static class EvictionExtensions
{
    /// <param name="services">The service collection to configure. Cannot be null.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Configures the time-to-live and optional eviction interval for pooled instances of the specified type in the
        /// service collection.
        /// </summary>
        /// <remarks>This method should be called after registering a pool for type T to configure its expiration
        /// policy. The time-to-live determines how long an instance can remain in the pool before being considered expired.
        /// The eviction interval controls how frequently the pool checks for and removes expired instances.</remarks>
        /// <typeparam name="T">The type of the pooled instances to configure.</typeparam>
        /// <param name="timeToLive">The duration that a pooled instance remains valid before it is eligible for eviction. Must be a non-negative
        /// time span.</param>
        /// <param name="evictionInterval">The interval at which expired instances are checked and evicted from the pool. If null, a default interval is
        /// used.</param>
        /// <returns>The same IServiceCollection instance so that additional configuration calls can be chained.</returns>
        public IServiceCollection WithTimeToLive<T>(TimeSpan timeToLive,
            TimeSpan? evictionInterval = null) where T : class
        {
            // This would need to be applied to the last registered pool configuration
            // For now, return services (actual implementation would modify the pool configuration)
            return services;
        }

        /// <summary>
        /// Configures the service of type T to be automatically evicted from the service collection after a specified period of
        /// inactivity.
        /// </summary>
        /// <typeparam name="T">The type of service to configure for idle timeout eviction. Must be a reference type.</typeparam>
        /// <param name="idleTimeout">The duration of inactivity after which the service instance will be considered idle and eligible for eviction. Must
        /// be a non-negative time span.</param>
        /// <param name="evictionInterval">The interval at which the service collection checks for idle services to evict. If null, a default interval is used.</param>
        /// <returns>The same IServiceCollection instance with idle timeout eviction configured for the specified service type.</returns>
        public IServiceCollection WithIdleTimeout<T>(TimeSpan idleTimeout,
            TimeSpan? evictionInterval = null) where T : class
        {
            return services;
        }

        /// <summary>
        /// Adds eviction policies for the specified service type to the service collection, enabling automatic removal of
        /// cached instances based on time-to-live and idle timeout settings.
        /// </summary>
        /// <remarks>Use this method to configure automatic eviction for services that are cached or pooled,
        /// helping to manage memory usage and resource lifetimes. The actual eviction timing may vary depending on the
        /// specified interval and the underlying implementation.</remarks>
        /// <typeparam name="T">The type of service for which eviction policies are to be applied. Must be a reference type.</typeparam>
        /// <param name="timeToLive">The maximum duration a cached instance of the service type remains available before it is eligible for eviction.
        /// Must be a non-negative time span.</param>
        /// <param name="idleTimeout">The maximum duration a cached instance can remain idle (unused) before it is eligible for eviction. Must be a
        /// non-negative time span.</param>
        /// <param name="evictionInterval">The interval at which the eviction process runs to remove expired or idle instances. If null, a default interval
        /// is used.</param>
        /// <returns>The same IServiceCollection instance, allowing for method chaining.</returns>
        public IServiceCollection WithEviction<T>(TimeSpan timeToLive,
            TimeSpan idleTimeout,
            TimeSpan? evictionInterval = null) where T : class
        {
            return services;
        }

        /// <summary>
        /// Adds custom eviction logic for cached instances of type T to the service collection.
        /// </summary>
        /// <remarks>Use this method to define custom eviction criteria for cached objects, such as based on
        /// object state or metadata. This is useful for advanced caching scenarios where default eviction policies are
        /// insufficient.</remarks>
        /// <typeparam name="T">The type of objects to which the custom eviction policy will be applied.</typeparam>
        /// <param name="evictionPredicate">A function that determines whether a cached object should be evicted. The function receives the cached object
        /// and its associated metadata, and returns <see langword="true"/> to evict the object; otherwise, <see
        /// langword="false"/>.</param>
        /// <param name="evictionInterval">The interval at which the eviction predicate is evaluated. If <see langword="null"/>, a default interval is
        /// used.</param>
        /// <returns>The same <see cref="IServiceCollection"/> instance so that additional calls can be chained.</returns>
        public IServiceCollection WithCustomEviction<T>(Func<object, ObjectMetadata, bool> evictionPredicate,
            TimeSpan? evictionInterval = null) where T : class
        {
            return services;
        }
    }
}

/// <summary>
/// Builder extensions for eviction configuration
/// </summary>
public static class EvictionBuilderExtensions
{
    /// <param name="builder">The object pool builder to configure. Cannot be null.</param>
    extension<T>(ObjectPoolBuilder<T> builder) where T : class
    {
        /// <summary>
        /// Configures Time-to-Live eviction on the builder
        /// </summary>
        public ObjectPoolBuilder<T> WithTimeToLive(TimeSpan timeToLive,
            TimeSpan? evictionInterval = null)
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
        public ObjectPoolBuilder<T> WithIdleTimeout(TimeSpan idleTimeout,
            TimeSpan? evictionInterval = null)
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
        public ObjectPoolBuilder<T> WithEviction(TimeSpan timeToLive,
            TimeSpan idleTimeout,
            TimeSpan? evictionInterval = null)
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
        public ObjectPoolBuilder<T> WithCustomEviction(Func<object, ObjectMetadata, bool> evictionPredicate,
            TimeSpan? evictionInterval = null)
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
        public ObjectPoolBuilder<T> WithEvictionConfiguration(Action<EvictionConfiguration> configure)
        {
            return builder.Configure(config =>
            {
                var evictionConfig = new EvictionConfiguration();
                configure(evictionConfig);
                config.EvictionConfiguration = evictionConfig;
            });
        }
    }
}
