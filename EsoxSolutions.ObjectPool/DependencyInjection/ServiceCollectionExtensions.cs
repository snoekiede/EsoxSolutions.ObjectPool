using EsoxSolutions.ObjectPool.Interfaces;
using EsoxSolutions.ObjectPool.Models;
using EsoxSolutions.ObjectPool.Pools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace EsoxSolutions.ObjectPool.DependencyInjection;

/// <summary>
/// Extension methods for registering object pools with dependency injection
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds an object pool to the service collection with fluent configuration
    /// </summary>
    /// <typeparam name="T">The type of object to pool</typeparam>
    /// <param name="services">The service collection</param>
    /// <param name="configure">Configuration action for the pool builder</param>
    /// <returns>The service collection for chaining</returns>
    /// <example>
    /// <code>
    /// services.AddObjectPool&lt;DbConnection&gt;(builder => builder
    ///     .WithFactory(() => new SqlConnection(connectionString))
    ///     .WithMaxSize(100)
    ///     .WithValidation(conn => conn.State != ConnectionState.Broken));
    /// </code>
    /// </example>
    public static IServiceCollection AddObjectPool<T>(
        this IServiceCollection services,
        Action<ObjectPoolBuilder<T>> configure) where T : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new ObjectPoolBuilder<T>();
        configure(builder);

        services.TryAddSingleton<IObjectPool<T>>(sp =>
        {
            var logger = sp.GetService<ILogger<ObjectPool<T>>>();
            return builder.Build(logger);
        });

        return services;
    }

    /// <summary>
    /// Adds a dynamic object pool to the service collection
    /// </summary>
    /// <typeparam name="T">The type of object to pool</typeparam>
    /// <param name="services">The service collection</param>
    /// <param name="factory">Factory method that uses IServiceProvider for dependencies</param>
    /// <param name="configure">Optional configuration action</param>
    /// <returns>The service collection for chaining</returns>
    /// <example>
    /// <code>
    /// services.AddDynamicObjectPool&lt;DbConnection&gt;(
    ///     sp => sp.GetRequiredService&lt;IDbConnectionFactory&gt;().Create(),
    ///     config => config.MaxPoolSize = 50);
    /// </code>
    /// </example>
    public static IServiceCollection AddDynamicObjectPool<T>(
        this IServiceCollection services,
        Func<IServiceProvider, T> factory,
        Action<PoolConfiguration>? configure = null) where T : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(factory);

        services.TryAddSingleton<IObjectPool<T>>(sp =>
        {
            var config = new PoolConfiguration();
            configure?.Invoke(config);

            var logger = sp.GetService<ILogger<ObjectPool<T>>>();
            
            // Create factory that doesn't need IServiceProvider after initial setup
            Func<T> poolFactory = () => factory(sp);
            
            return new DynamicObjectPool<T>(poolFactory, [], config, logger);
        });

        return services;
    }

    /// <summary>
    /// Adds a queryable object pool to the service collection
    /// </summary>
    /// <typeparam name="T">The type of object to pool</typeparam>
    /// <param name="services">The service collection</param>
    /// <param name="configure">Configuration action for the pool builder</param>
    /// <returns>The service collection for chaining</returns>
    /// <example>
    /// <code>
    /// services.AddQueryableObjectPool&lt;Car&gt;(builder => builder
    ///     .WithInitialObjects(initialCars)
    ///     .WithMaxSize(100));
    /// </code>
    /// </example>
    public static IServiceCollection AddQueryableObjectPool<T>(
        this IServiceCollection services,
        Action<ObjectPoolBuilder<T>> configure) where T : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new ObjectPoolBuilder<T>();
        configure(builder);

        services.TryAddSingleton<IQueryableObjectPool<T>>(sp =>
        {
            var logger = sp.GetService<ILogger<QueryableObjectPool<T>>>();
            var pool = builder.Build(logger);
            
            return pool as IQueryableObjectPool<T> 
                ?? throw new InvalidOperationException("Pool is not queryable. Use AsQueryable() in configuration.");
        });

        return services;
    }

    /// <summary>
    /// Adds an object pool with pre-created initial objects
    /// </summary>
    /// <typeparam name="T">The type of object to pool</typeparam>
    /// <param name="services">The service collection</param>
    /// <param name="initialObjects">Initial objects to add to the pool</param>
    /// <param name="configure">Optional configuration action</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddObjectPoolWithObjects<T>(
        this IServiceCollection services,
        IEnumerable<T> initialObjects,
        Action<PoolConfiguration>? configure = null) where T : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(initialObjects);

        var objectList = initialObjects.ToList();
        
        services.TryAddSingleton<IObjectPool<T>>(sp =>
        {
            var config = new PoolConfiguration();
            configure?.Invoke(config);

            var logger = sp.GetService<ILogger<ObjectPool<T>>>();
            return new ObjectPool<T>(objectList, config, logger);
        });

        return services;
    }

    /// <summary>
    /// Adds multiple object pools for different types
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configure">Configuration action for multiple pools</param>
    /// <returns>The service collection for chaining</returns>
    /// <example>
    /// <code>
    /// services.AddObjectPools(pools =>
    /// {
    ///     pools.AddPool&lt;DbConnection&gt;(builder => builder.WithFactory(() => new SqlConnection(cs)));
    ///     pools.AddPool&lt;HttpClient&gt;(builder => builder.WithFactory(() => new HttpClient()));
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddObjectPools(
        this IServiceCollection services,
        Action<ObjectPoolCollectionBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var collectionBuilder = new ObjectPoolCollectionBuilder(services);
        configure(collectionBuilder);

        return services;
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
