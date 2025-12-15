using EsoxSolutions.ObjectPool.Constants;
using EsoxSolutions.ObjectPool.Exceptions;
using EsoxSolutions.ObjectPool.Models;
using EsoxSolutions.ObjectPool.Warmup;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace EsoxSolutions.ObjectPool.Pools
{
    /// <summary>
    /// Dynamic object pool that can create new objects using a factory method when the pool is empty
    /// </summary>
    /// <typeparam name="T">the type to be stored in the object pool</typeparam>
    public class DynamicObjectPool<T> : ObjectPool<T>, IObjectPoolWarmer<T> where T:class
    {
        /// <summary>
        /// The factory method to be used to create new objects
        /// </summary>
        private readonly Func<T>? _factory;

        /// <summary>
        /// Warm-up status tracking
        /// </summary>
        private WarmupStatus _warmupStatus = new();

        /// <summary>
        /// The constructor for the queryable object pool
        /// </summary>
        /// <param name="initialObjects">the initial objects</param>
        public DynamicObjectPool(List<T> initialObjects) : base(initialObjects)
        {
        }

        /// <summary>
        /// The constructor for the dynamic object pool
        /// </summary>
        /// <param name="factory">creation function for new objects</param>
        public DynamicObjectPool(Func<T> factory) : base([])
        {
            this._factory = factory;
        }

        /// <summary>
        /// The constructor for the dynamic object pool
        /// </summary>
        /// <param name="factory">creation function for new objects</param>
        /// <param name="initialObjects">list of initial objects</param>
        public DynamicObjectPool(Func<T> factory, List<T> initialObjects) : base(initialObjects)
        {
            this._factory = factory;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DynamicObjectPool{T}"/> class with a specified factory method,
        /// an initial collection of objects, optional pool configuration, and an optional logger.
        /// </summary>
        /// <remarks>The <paramref name="factory"/> parameter is required and must not be null. The
        /// <paramref name="initialObjects"/> list can be empty, but it must not be null. If <paramref
        /// name="configuration"/> is provided, it will override the default pool behavior. The logger can be used to
        /// monitor pool activity, such as object creation and disposal.</remarks>
        /// <param name="factory">A function that creates new instances of the pooled object. This function is invoked when the pool needs to
        /// create additional objects.</param>
        /// <param name="initialObjects">A list of pre-created objects to populate the pool initially. These objects will be available for reuse
        /// immediately.</param>
        /// <param name="configuration">Optional configuration settings for the object pool, such as maximum pool size and eviction policies. If
        /// null, default settings are used.</param>
        /// <param name="logger">An optional logger instance for logging pool-related events. If null, no logging will be performed.</param>
        public DynamicObjectPool(Func<T> factory,List<T> initialObjects, PoolConfiguration? configuration, ILogger<ObjectPool<T>>? logger = null): base(initialObjects, configuration, logger)
        {
            this._factory = factory;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DynamicObjectPool{T}"/> class with the specified factory
        /// method, configuration, and optional logger.
        /// </summary>
        /// <param name="factory">A function that creates new instances of the pooled object. This function is called when the pool needs to
        /// allocate a new object.</param>
        /// <param name="configuration">An optional <see cref="PoolConfiguration"/> object that specifies the settings for the object pool, such as
        /// maximum size and eviction policies. If null, default settings are used.</param>
        /// <param name="logger">An optional <see cref="ILogger{TCategoryName}"/> instance used to log diagnostic information about the
        /// pool's behavior. If null, no logging is performed.</param>
        public DynamicObjectPool(Func<T> factory, PoolConfiguration? configuration, ILogger<ObjectPool<T>>? logger = null) : base([], configuration, logger)
        {
            this._factory = factory;
        }
        
        /// <summary>
        /// Returns an object from the pool. If no objects are available, an exception is thrown.
        /// </summary>
        /// <returns>A PoolModel wrapping the pooled object</returns>
        /// <exception cref="NoObjectsInPoolException">Thrown if no object could be found and no factory is available</exception>
        /// <exception cref="UnableToCreateObjectException">Thrown if the factory fails to create an object or no factory exists</exception>
        public override PoolModel<T> GetObject()
        {
            if (Disposed) throw new ObjectDisposedException(nameof(DynamicObjectPool<>));

            // Check max active objects limit
            if (this.ActiveObjects.Count >= Configuration.MaxActiveObjects)
            {
                throw new InvalidOperationException(string.Format(PoolConstants.Messages.MaxActiveLimitFormat, 
                    Configuration.MaxActiveObjects));
            }

            // Try to get an existing object first
            if (this.AvailableObjects.TryPop(out var result))
            {
                this.ActiveObjects.TryAdd(result, 0);
                statistics.TotalObjectsRetrieved++;
                statistics.CurrentActiveObjects = this.ActiveObjects.Count;
                statistics.CurrentAvailableObjects = this.AvailableObjects.Count;
                if (statistics.CurrentActiveObjects > statistics.PeakActiveObjects)
                {
                    statistics.PeakActiveObjects = statistics.CurrentActiveObjects;
                }
                return new PoolModel<T>(result, this);
            }

            // No objects available - check if we have a factory
            if (this._factory == null)
            {
                // No factory available to create new objects
                statistics.PoolEmptyCount++;
                Logger?.LogWarning(PoolConstants.Messages.CannotCreateObject);
                throw new UnableToCreateObjectException(PoolConstants.Messages.CannotCreateObject);
            }

            // Try to create a new object using the factory
            T? newObject;
            try
            {
                newObject = this._factory.Invoke();
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, PoolConstants.Messages.CannotCreateObject);
                throw new UnableToCreateObjectException(PoolConstants.Messages.CannotCreateObject, ex);
            }

            if (newObject == null)
            {
                throw new UnableToCreateObjectException(PoolConstants.Messages.CannotCreateObject);
            }

            // Add directly to active objects without pushing to available first
            this.ActiveObjects.TryAdd(newObject, 0);
            statistics.TotalObjectsRetrieved++;
            statistics.CurrentActiveObjects = this.ActiveObjects.Count;
            statistics.CurrentAvailableObjects = this.AvailableObjects.Count;
            if (statistics.CurrentActiveObjects > statistics.PeakActiveObjects)
            {
                statistics.PeakActiveObjects = statistics.CurrentActiveObjects;
            }
            
            Logger?.LogDebug("Created new object dynamically. Active: {Active}, Available: {Available}", 
                ActiveObjects.Count, AvailableObjects.Count);
            
            return new PoolModel<T>(newObject, this);
        }

        #region IObjectPoolWarmer Implementation

        /// <summary>
        /// Warms up the pool by pre-creating objects to the target size
        /// </summary>
        public async Task WarmUpAsync(int targetSize, CancellationToken cancellationToken = default)
        {
            if (_factory == null)
            {
                Logger?.LogWarning("Cannot warm up pool: no factory method provided");
                _warmupStatus.Errors.Add("No factory method available");
                return;
            }

            var stopwatch = Stopwatch.StartNew();
            var currentSize = AvailableObjects.Count;
            var objectsToCreate = Math.Min(targetSize - currentSize, Configuration.MaxPoolSize - currentSize);

            if (objectsToCreate <= 0)
            {
                Logger?.LogInformation("Pool already at target size: {CurrentSize}/{TargetSize}", currentSize, targetSize);
                _warmupStatus.IsWarmedUp = true;
                _warmupStatus.ObjectsCreated = 0;
                _warmupStatus.TargetSize = targetSize;
                return;
            }

            _warmupStatus = new WarmupStatus
            {
                TargetSize = targetSize,
                ObjectsCreated = 0
            };

            Logger?.LogInformation("Starting pool warm-up: creating {Count} objects", objectsToCreate);

            var batchSize = Math.Min(Environment.ProcessorCount * 2, objectsToCreate);
            var tasks = new List<Task<T?>>();

            try
            {
                for (int i = 0; i < objectsToCreate && !cancellationToken.IsCancellationRequested; i++)
                {
                    tasks.Add(Task.Run(() =>
                    {
                        try
                        {
                            return _factory.Invoke();
                        }
                        catch (Exception ex)
                        {
                            Logger?.LogError(ex, "Error creating object during warm-up");
                            _warmupStatus.Errors.Add($"Factory error: {ex.Message}");
                            return null;
                        }
                    }, cancellationToken));

                    // Process in batches to avoid overwhelming the system
                    if (tasks.Count >= batchSize || i == objectsToCreate - 1)
                    {
                        var results = await Task.WhenAll(tasks);
                        
                        foreach (var obj in results.Where(o => o != null))
                        {
                            AvailableObjects.Push(obj!);
                            _warmupStatus.ObjectsCreated++;
                        }

                        tasks.Clear();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Logger?.LogInformation("Pool warm-up cancelled after creating {Created} objects", _warmupStatus.ObjectsCreated);
            }

            stopwatch.Stop();
            _warmupStatus.WarmupDuration = stopwatch.Elapsed;
            _warmupStatus.IsWarmedUp = true;
            _warmupStatus.CompletedAt = DateTime.UtcNow;

            Logger?.LogInformation(
                "Pool warm-up completed: created {Created}/{Target} objects in {Duration}ms",
                _warmupStatus.ObjectsCreated,
                targetSize,
                stopwatch.ElapsedMilliseconds);
        }

        /// <summary>
        /// Warms up the pool to a percentage of maximum capacity
        /// </summary>
        public async Task WarmUpToPercentageAsync(double targetPercentage, CancellationToken cancellationToken = default)
        {
            if (targetPercentage < 0 || targetPercentage > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(targetPercentage), "Target percentage must be between 0 and 100");
            }

            var maxSize = Configuration.MaxPoolSize == int.MaxValue 
                ? 100  // Default to 100 if unlimited
                : Configuration.MaxPoolSize;

            var targetSize = (int)Math.Ceiling(maxSize * (targetPercentage / 100.0));
            await WarmUpAsync(targetSize, cancellationToken);
        }

        /// <summary>
        /// Gets the current warm-up status
        /// </summary>
        public WarmupStatus GetWarmupStatus()
        {
            return _warmupStatus;
        }

        #endregion
    }
}
