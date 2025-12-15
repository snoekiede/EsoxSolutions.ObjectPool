using EsoxSolutions.ObjectPool.Interfaces;
using EsoxSolutions.ObjectPool.Models;
using EsoxSolutions.ObjectPool.Pools;
using Microsoft.Extensions.Logging;

namespace EsoxSolutions.ObjectPool.DependencyInjection;

/// <summary>
/// Builder for configuring object pools in dependency injection
/// </summary>
/// <typeparam name="T">The type of object to pool</typeparam>
public class ObjectPoolBuilder<T> where T : class
{
    private readonly List<T> _initialObjects = [];
    private Func<T>? _factory;
    private readonly PoolConfiguration _configuration = new();
    private bool _enableHealthChecks;
    private PoolType _poolType = PoolType.Standard;

    /// <summary>
    /// Sets the initial objects for the pool
    /// </summary>
    public ObjectPoolBuilder<T> WithInitialObjects(IEnumerable<T> objects)
    {
        ArgumentNullException.ThrowIfNull(objects);
        _initialObjects.AddRange(objects);
        return this;
    }

    /// <summary>
    /// Sets the initial objects for the pool
    /// </summary>
    public ObjectPoolBuilder<T> WithInitialObjects(params T[] objects)
    {
        ArgumentNullException.ThrowIfNull(objects);
        _initialObjects.AddRange(objects);
        return this;
    }

    /// <summary>
    /// Sets a factory method for creating objects dynamically
    /// </summary>
    public ObjectPoolBuilder<T> WithFactory(Func<T> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;
        _poolType = PoolType.Dynamic;
        return this;
    }

    /// <summary>
    /// Sets the maximum pool size
    /// </summary>
    public ObjectPoolBuilder<T> WithMaxSize(int maxSize)
    {
        if (maxSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxSize), "Max size must be greater than 0");
        
        _configuration.MaxPoolSize = maxSize;
        return this;
    }

    /// <summary>
    /// Sets the maximum number of active objects
    /// </summary>
    public ObjectPoolBuilder<T> WithMaxActiveObjects(int maxActive)
    {
        if (maxActive <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxActive), "Max active must be greater than 0");
        
        _configuration.MaxActiveObjects = maxActive;
        return this;
    }

    /// <summary>
    /// Sets the default timeout for async operations
    /// </summary>
    public ObjectPoolBuilder<T> WithDefaultTimeout(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be greater than zero");
        
        _configuration.DefaultTimeout = timeout;
        return this;
    }

    /// <summary>
    /// Configures validation on object return
    /// </summary>
    public ObjectPoolBuilder<T> WithValidation(Func<T, bool> validationFunction)
    {
        ArgumentNullException.ThrowIfNull(validationFunction);
        
        _configuration.ValidateOnReturn = true;
        _configuration.ValidationFunction = obj => validationFunction((T)obj);
        return this;
    }

    /// <summary>
    /// Enables health checks for the pool
    /// </summary>
    public ObjectPoolBuilder<T> WithHealthChecks()
    {
        _enableHealthChecks = true;
        return this;
    }

    /// <summary>
    /// Uses a queryable pool implementation
    /// </summary>
    public ObjectPoolBuilder<T> AsQueryable()
    {
        _poolType = PoolType.Queryable;
        return this;
    }

    /// <summary>
    /// Configures the pool with a configuration action
    /// </summary>
    public ObjectPoolBuilder<T> Configure(Action<PoolConfiguration> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        configure(_configuration);
        return this;
    }

    /// <summary>
    /// Builds the configured object pool
    /// </summary>
    internal IObjectPool<T> Build(ILogger? logger)
    {
        return _poolType switch
        {
            PoolType.Standard => new ObjectPool<T>(_initialObjects, _configuration, logger as ILogger<ObjectPool<T>>),
            PoolType.Dynamic when _factory != null => new DynamicObjectPool<T>(_factory, _initialObjects, _configuration, logger as ILogger<ObjectPool<T>>),
            PoolType.Queryable => new QueryableObjectPool<T>(_initialObjects, _configuration, logger as ILogger<QueryableObjectPool<T>>),
            _ => throw new InvalidOperationException("Invalid pool configuration")
        };
    }

    /// <summary>
    /// Gets whether health checks are enabled
    /// </summary>
    internal bool HealthChecksEnabled => _enableHealthChecks;
}

/// <summary>
/// Type of pool to create
/// </summary>
internal enum PoolType
{
    Standard,
    Dynamic,
    Queryable
}
