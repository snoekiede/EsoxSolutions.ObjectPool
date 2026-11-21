using EsoxSolutions.ObjectPool.HealthChecks;
using EsoxSolutions.ObjectPool.Interfaces;
using EsoxSolutions.ObjectPool.Models;
using EsoxSolutions.ObjectPool.Pools;
using EsoxSolutions.ObjectPool.Scoping;
using EsoxSolutions.ObjectPool.Warmup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace EsoxSolutions.ObjectPool.DependencyInjection;

internal sealed record PoolConfigurationModifier<T>(Action<PoolConfiguration> Configure) where T : class;

internal sealed record RegisteredPoolHealthCheckDescriptor(
    Type PoolObjectType,
    Action<IHealthChecksBuilder, HealthStatus?, IEnumerable<string>?> Register);

/// <summary>
/// Extension methods for registering object pools with dependency injection
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <param name="services">The service collection</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Adds an object pool to the service collection with fluent configuration
        /// </summary>
        /// <typeparam name="T">The type of object to pool</typeparam>
        /// <param name="configure">Configuration action for the pool builder</param>
        /// <returns>The service collection for chaining</returns>
        public IServiceCollection AddObjectPool<T>(Action<ObjectPoolBuilder<T>> configure) where T : class
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configure);

            var builder = new ObjectPoolBuilder<T>();
            configure(builder);

            services.TryAddSingleton<IObjectPool<T>>(sp =>
            {
                var logger = sp.GetService<ILogger<ObjectPool<T>>>();
                var modifiers = sp.GetServices<PoolConfigurationModifier<T>>();
                return builder.Build(logger, modifiers);
            });

            if (builder.TypeOfPool == PoolType.Dynamic)
            {
                services.TryAddSingleton<IObjectPoolWarmer<T>>(sp =>
                    (IObjectPoolWarmer<T>)sp.GetRequiredService<IObjectPool<T>>());
            }

            if (builder.ScopedConfiguration != null)
            {
                var scopedConfig = builder.ScopedConfiguration;
                services.TryAddSingleton(scopedConfig);
                services.TryAddSingleton<ScopedPoolManager<T>>(sp =>
                {
                    var logger = sp.GetService<ILogger<ScopedPoolManager<T>>>();
                    var modifiers = sp.GetServices<PoolConfigurationModifier<T>>().ToList();
                    return new ScopedPoolManager<T>(
                        _ => builder.Build(sp.GetService<ILogger<ObjectPool<T>>>(), modifiers),
                        scopedConfig,
                        logger);
                });
            }

            RegisterHealthCheckDescriptor<T>(services, isQueryable: false);

            if (builder.HealthChecksEnabled)
            {
                services.AddHealthChecks().AddObjectPoolHealthCheck<T>();
            }

            return services;
        }

        /// <summary>
        /// Adds a dynamic object pool to the service collection
        /// </summary>
        /// <typeparam name="T">The type of object to pool</typeparam>
        /// <param name="factory">Factory method that uses IServiceProvider for dependencies</param>
        /// <param name="configure">Optional configuration action</param>
        /// <returns>The service collection for chaining</returns>
        public IServiceCollection AddDynamicObjectPool<T>(Func<IServiceProvider, T> factory,
            Action<PoolConfiguration>? configure = null) where T : class
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(factory);

            services.TryAddSingleton<DynamicObjectPool<T>>(sp =>
            {
                var config = new PoolConfiguration<T>();
                configure?.Invoke(config);
                foreach (var modifier in sp.GetServices<PoolConfigurationModifier<T>>())
                {
                    modifier.Configure(config);
                }

                var logger = sp.GetService<ILogger<ObjectPool<T>>>();
            
                // Create factory that doesn't need IServiceProvider after initial setup
                T PoolFactory() => factory(sp);

                return new DynamicObjectPool<T>(PoolFactory, [], config, logger);
            });

            // Register as IObjectPool<T>
            services.TryAddSingleton<IObjectPool<T>>(sp => sp.GetRequiredService<DynamicObjectPool<T>>());

            // Register as IObjectPoolWarmer<T> for warm-up support
            services.TryAddSingleton<IObjectPoolWarmer<T>>(sp => sp.GetRequiredService<DynamicObjectPool<T>>());

            RegisterHealthCheckDescriptor<T>(services, isQueryable: false);

            return services;
        }

        /// <summary>
        /// Adds a queryable object pool to the service collection
        /// </summary>
        /// <typeparam name="T">The type of object to pool</typeparam>
        /// <param name="configure">Configuration action for the pool builder</param>
        /// <returns>The service collection for chaining</returns>
        public IServiceCollection AddQueryableObjectPool<T>(Action<ObjectPoolBuilder<T>> configure) where T : class
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configure);

            var builder = new ObjectPoolBuilder<T>();
            configure(builder);

            services.TryAddSingleton<IQueryableObjectPool<T>>(sp =>
            {
                var logger = sp.GetService<ILogger<QueryableObjectPool<T>>>();
                var modifiers = sp.GetServices<PoolConfigurationModifier<T>>();
                var pool = builder.Build(logger, modifiers);
            
                return pool as IQueryableObjectPool<T> 
                       ?? throw new InvalidOperationException("Pool is not queryable. Use AsQueryable() in configuration.");
            });

            RegisterHealthCheckDescriptor<T>(services, isQueryable: true);

            if (builder.HealthChecksEnabled)
            {
                services.AddHealthChecks().AddQueryablePoolHealthCheck<T>();
            }

            return services;
        }

        /// <summary>
        /// Adds an object pool with pre-created initial objects
        /// </summary>
        /// <typeparam name="T">The type of object to pool</typeparam>
        /// <param name="initialObjects">Initial objects to add to the pool</param>
        /// <param name="configure">Optional configuration action</param>
        /// <returns>The service collection for chaining</returns>
        public IServiceCollection AddObjectPoolWithObjects<T>(IEnumerable<T> initialObjects,
            Action<PoolConfiguration>? configure = null) where T : class
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(initialObjects);

            var objectList = initialObjects.ToList();
        
            services.TryAddSingleton<IObjectPool<T>>(sp =>
            {
                var config = new PoolConfiguration<T>();
                configure?.Invoke(config);
                foreach (var modifier in sp.GetServices<PoolConfigurationModifier<T>>())
                {
                    modifier.Configure(config);
                }

                var logger = sp.GetService<ILogger<ObjectPool<T>>>();
                return new ObjectPool<T>(objectList, config, logger);
            });

            RegisterHealthCheckDescriptor<T>(services, isQueryable: false);

            return services;
        }

        /// <summary>
        /// Adds multiple object pools for different types
        /// </summary>
        /// <param name="configure">Configuration action for multiple pools</param>
        /// <returns>The service collection for chaining</returns>
        public IServiceCollection AddObjectPools(Action<ObjectPoolCollectionBuilder> configure)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configure);

            var collectionBuilder = new ObjectPoolCollectionBuilder(services);
            configure(collectionBuilder);

            return services;
        }
    }

    private static void RegisterHealthCheckDescriptor<T>(IServiceCollection services, bool isQueryable) where T : class
    {
        if (services.Any(sd =>
                sd.ServiceType == typeof(RegisteredPoolHealthCheckDescriptor) &&
                sd.ImplementationInstance is RegisteredPoolHealthCheckDescriptor existing &&
                existing.PoolObjectType == typeof(T)))
        {
            return;
        }

        services.AddSingleton(new RegisteredPoolHealthCheckDescriptor(
            typeof(T),
            (hcBuilder, failureStatus, tags) =>
            {
                if (isQueryable)
                {
                    hcBuilder.AddQueryablePoolHealthCheck<T>(failureStatus: failureStatus, tags: tags);
                }
                else
                {
                    hcBuilder.AddObjectPoolHealthCheck<T>(failureStatus: failureStatus, tags: tags);
                }
            }));
    }
}

/// <summary>
/// Builder for configuring multiple object pools
/// </summary>
public class ObjectPoolCollectionBuilder
{
    private readonly IServiceCollection _services;

    internal ObjectPoolCollectionBuilder(IServiceCollection services)
    {
        _services = services;
    }

    /// <summary>
    /// Adds an object pool for a specific type
    /// </summary>
    public ObjectPoolCollectionBuilder AddPool<T>(Action<ObjectPoolBuilder<T>> configure) where T : class
    {
        _services.AddObjectPool(configure);
        return this;
    }

    /// <summary>
    /// Adds a dynamic object pool for a specific type
    /// </summary>
    public ObjectPoolCollectionBuilder AddDynamicPool<T>(
        Func<IServiceProvider, T> factory,
        Action<PoolConfiguration>? configure = null) where T : class
    {
        _services.AddDynamicObjectPool(factory, configure);
        return this;
    }

    /// <summary>
    /// Adds a queryable object pool for a specific type
    /// </summary>
    public ObjectPoolCollectionBuilder AddQueryablePool<T>(Action<ObjectPoolBuilder<T>> configure) where T : class
    {
        _services.AddQueryableObjectPool(configure);
        return this;
    }
}
