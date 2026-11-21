using EsoxSolutions.ObjectPool.CircuitBreaker;
using EsoxSolutions.ObjectPool.Constants;
using EsoxSolutions.ObjectPool.Eviction;
using EsoxSolutions.ObjectPool.Exceptions;
using EsoxSolutions.ObjectPool.Infrastructure;
using EsoxSolutions.ObjectPool.Lifecycle;
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
    public class DynamicObjectPool<T> : ObjectPool<T>, IObjectPoolWarmer<T> where T : class
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
        /// Eviction manager for TTL and idle timeout support
        /// </summary>
        private EvictionManager<T>? _evictionManager;

        /// <summary>
        /// Timer for periodic eviction checks
        /// </summary>
        private Timer? _evictionCheckTimer;

        /// <summary>
        /// Circuit breaker for protecting against cascading failures
        /// </summary>
        private CircuitBreaker.CircuitBreaker? _circuitBreaker;

        /// <summary>
        /// Lifecycle hook manager for object lifecycle events
        /// </summary>
        private LifecycleHookManager<T>? _lifecycleHookManager;

        /// <summary>
        /// The constructor for the dynamic object pool with initial objects
        /// </summary>
        /// <param name="initialObjects">the initial objects</param>
        public DynamicObjectPool(List<T> initialObjects) : base(initialObjects)
        {
            InitializeEviction();
            InitializeCircuitBreaker();
            InitializeLifecycleHooks();
        }

        /// <summary>
        /// The constructor for the dynamic object pool
        /// </summary>
        /// <param name="factory">creation function for new objects</param>
        public DynamicObjectPool(Func<T> factory) : base([])
        {
            this._factory = factory;
            InitializeEviction();
            InitializeCircuitBreaker();
            InitializeLifecycleHooks();
        }

        /// <summary>
        /// The constructor for the dynamic object pool
        /// </summary>
        /// <param name="factory">creation function for new objects</param>
        /// <param name="initialObjects">list of initial objects</param>
        public DynamicObjectPool(Func<T> factory, List<T> initialObjects) : base(initialObjects)
        {
            this._factory = factory;
            InitializeEviction();
            InitializeCircuitBreaker();
            InitializeLifecycleHooks();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DynamicObjectPool{T}"/> class with a specified factory method,
        /// an initial collection of objects, optional pool configuration, and an optional logger.
        /// </summary>
        /// <param name="factory">A function that creates new instances of the pooled object.</param>
        /// <param name="initialObjects">A list of pre-created objects to populate the pool initially.</param>
        /// <param name="configuration">Optional configuration settings for the object pool.</param>
        /// <param name="logger">An optional logger instance for logging pool-related events.</param>
        public DynamicObjectPool(Func<T> factory, List<T> initialObjects, PoolConfiguration? configuration, ILogger<ObjectPool<T>>? logger = null)
            : base(initialObjects, configuration, logger)
        {
            this._factory = factory;
            InitializeEviction();
            InitializeCircuitBreaker();
            InitializeLifecycleHooks();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DynamicObjectPool{T}"/> class with the specified factory
        /// method, configuration, and optional logger.
        /// </summary>
        /// <param name="factory">A function that creates new instances of the pooled object.</param>
        /// <param name="configuration">An optional <see cref="PoolConfiguration"/> object.</param>
        /// <param name="logger">An optional <see cref="ILogger{TCategoryName}"/> instance.</param>
        public DynamicObjectPool(Func<T> factory, PoolConfiguration? configuration, ILogger<ObjectPool<T>>? logger = null)
            : base([], configuration, logger)
        {
            this._factory = factory;
            InitializeEviction();
            InitializeCircuitBreaker();
            InitializeLifecycleHooks();
        }

        private void InitializeCircuitBreaker()
        {
            if (Configuration.CircuitBreakerConfiguration != null)
            {
                _circuitBreaker = new CircuitBreaker.CircuitBreaker(
                    Configuration.CircuitBreakerConfiguration,
                    Logger);

                Logger?.LogInformation(
                    "Circuit breaker enabled: FailureThreshold={Threshold}, OpenDuration={Duration}",
                    Configuration.CircuitBreakerConfiguration.FailureThreshold,
                    Configuration.CircuitBreakerConfiguration.OpenDuration);
            }
        }

        private void InitializeLifecycleHooks()
        {
            if (Configuration.LifecycleHooks is LifecycleHooks<T> hooks)
            {
                _lifecycleHookManager = new LifecycleHookManager<T>(
                    hooks,
                    Configuration.ContinueOnLifecycleHookError,
                    (ex, hookName) => Logger?.LogError(ex, "Error executing lifecycle hook: {HookName}", hookName));

                Logger?.LogInformation("Lifecycle hooks enabled");
            }
        }

        private void InitializeEviction()
        {
            if (Configuration.EvictionConfiguration != null && 
                Configuration.EvictionConfiguration.Policy != EvictionPolicy.None)
            {
                _evictionManager = new EvictionManager<T>(Configuration.EvictionConfiguration, Logger);

                // Track initial objects
                foreach (var obj in PoolingPolicy.GetAll())
                {
                    _evictionManager.TrackObject(obj);
                }

                // Set up periodic eviction checks
                if (Configuration.EvictionConfiguration.EnableBackgroundEviction)
                {
                    _evictionCheckTimer = new Timer(
                        PerformEvictionCheck,
                        null,
                        Configuration.EvictionConfiguration.EvictionInterval,
                        Configuration.EvictionConfiguration.EvictionInterval);

                    Logger?.LogInformation(
                        "Eviction enabled with policy: {Policy}, TTL: {TTL}, Idle: {Idle}",
                        Configuration.EvictionConfiguration.Policy,
                        Configuration.EvictionConfiguration.TimeToLive,
                        Configuration.EvictionConfiguration.IdleTimeout);
                }
            }
        }

        private void PerformEvictionCheck(object? state)
        {
            if (_evictionManager == null || Disposed) return;

            try
            {
                var objectsToCheck = PoolingPolicy.GetAll().ToArray();
                _evictionManager.RunEviction(
                    objectsToCheck,
                    (obj, reason) =>
                    {
                        var removed = PoolingPolicy.TryTake(
                            candidate => ReferenceOrValueEqualityComparer<T>.Instance.Equals(candidate, obj),
                            out _);

                        if (removed)
                        {
                            Logger?.LogDebug("Evicted object from pool");
                            _lifecycleHookManager?.ExecuteOnEvict(obj, reason);
                        }

                        return removed;
                    },
                    beforeDisposeAction: obj => _lifecycleHookManager?.ExecuteOnDispose(obj));
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Logger?.LogError(ex, "Error during eviction check");
            }
        }

        /// <summary>
        /// Returns an object from the pool. If no objects are available, an exception is thrown.
        /// </summary>
        /// <returns>A PoolModel wrapping the pooled object</returns>
        /// <exception cref="NoObjectsInPoolException">Thrown if no object could be found and no factory is available</exception>
        /// <exception cref="UnableToCreateObjectException">Thrown if the factory fails to create an object or no factory exists</exception>
        /// <exception cref="CircuitBreakerOpenException">Thrown if circuit breaker is open</exception>
        public override PoolModel<T> GetObject()
        {
            if (Disposed) throw new ObjectDisposedException(nameof(DynamicObjectPool<>));

            // Reserve active capacity before checking circuit breaker or consuming/creating an object
            if (!this.ActiveObjectSlots.Wait(0))
            {
                throw new InvalidOperationException(string.Format(PoolConstants.Messages.MaxActiveLimitFormat, 
                    Configuration.MaxActiveObjects));
            }

            try
            {
                if (_circuitBreaker != null)
                {
                    return _circuitBreaker.Execute(GetObjectWithReservedSlot);
                }

                return GetObjectWithReservedSlot();
            }
            catch
            {
                this.ActiveObjectSlots.Release();
                throw;
            }
        }

        /// <summary>
        /// Try to get an object from the dynamic pool (or create one via factory if empty) without throwing an exception
        /// </summary>
        /// <param name="poolModel">The pool model if successful</param>
        /// <returns>True if an object was retrieved or created, false otherwise</returns>
        public override bool TryGetObject(out PoolModel<T>? poolModel)
        {
            if (Disposed)
            {
                poolModel = null;
                return false;
            }

            if (!this.ActiveObjectSlots.Wait(0))
            {
                Logger?.LogDebug(PoolConstants.Messages.CannotGetObjectActiveObjectsLimitMaxactiveReached, Configuration.MaxActiveObjects);
                poolModel = null;
                return false;
            }

            bool succeeded = false;
            try
            {
                if (_circuitBreaker != null)
                {
                    succeeded = _circuitBreaker.TryExecute(GetObjectWithReservedSlot, out poolModel) && poolModel != null;
                    return succeeded;
                }

                poolModel = GetObjectWithReservedSlot();
                succeeded = true;
                return true;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                poolModel = null;
                return false;
            }
            finally
            {
                if (!succeeded)
                {
                    this.ActiveObjectSlots.Release();
                }
            }
        }

        private PoolModel<T> GetObjectWithReservedSlot()
        {
            // Try to get an existing non-expired object first
            while (this.PoolingPolicy.TryTake(out var result) && result != null)
            {
                // Check if object should be evicted
                if (_evictionManager != null && _evictionManager.TryGetEvictionReason(result, out var evictionReason))
                {
                    Logger?.LogDebug("Object expired, attempting to evict");
                    
                    // Execute eviction hook
                    _lifecycleHookManager?.ExecuteOnEvict(result, evictionReason);
                    
                    // Record inline eviction and untrack
                    _evictionManager.RecordInlineEviction(result, evictionReason);
                    
                    if (Configuration.EvictionConfiguration?.DisposeEvictedObjects == true)
                    {
                        DisposeObject(result);
                    }
                    
                    // Try next object
                    continue;
                }

                // Found a valid object
                _evictionManager?.RecordAccess(result);
                _lifecycleHookManager?.ExecuteOnAcquire(result);

                if (!this.ActiveObjects.TryAdd(result, 0))
                {
                    this.PoolingPolicy.Add(result);
                    throw new InvalidOperationException("The object could not be registered as active.");
                }

                UpdateStatisticsOnRetrieve();
                return new PoolModel<T>(result, this);
            }

            // No objects available - check if we have a factory
            if (this._factory == null)
            {
                statistics.IncrementPoolEmpty();
                Logger?.LogWarning(PoolConstants.Messages.CannotCreateObject);
                throw new UnableToCreateObjectException(PoolConstants.Messages.CannotCreateObject);
            }

            // Try to create a new object using the factory
            T? newObject;
            try
            {
                newObject = this._factory.Invoke();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Logger?.LogError(ex, PoolConstants.Messages.CannotCreateObject);
                throw new UnableToCreateObjectException(PoolConstants.Messages.CannotCreateObject, ex);
            }

            if (newObject == null)
            {
                throw new UnableToCreateObjectException(PoolConstants.Messages.CannotCreateObject);
            }

            // Execute create hook
            _lifecycleHookManager?.ExecuteOnCreate(newObject);

            // Track the new object for eviction
            _evictionManager?.TrackObject(newObject);
            _evictionManager?.RecordAccess(newObject);

            // Execute acquire hook
            _lifecycleHookManager?.ExecuteOnAcquire(newObject);

            // Add directly to active objects without pushing to available first
            if (!this.ActiveObjects.TryAdd(newObject, 0))
            {
                throw new InvalidOperationException("The object could not be registered as active.");
            }

            UpdateStatisticsOnRetrieve();
            
            Logger?.LogDebug("Created new object dynamically. Active: {Active}, Available: {Available}", 
                ActiveObjects.Count, PoolingPolicy.Count);
            
            return new PoolModel<T>(newObject, this);
        }

        /// <summary>
        /// Returns an object to the pool
        /// </summary>
        public override void ReturnObject(PoolModel<T> obj)
        {
            ArgumentNullException.ThrowIfNull(obj);
            if (Disposed) throw new ObjectDisposedException(nameof(DynamicObjectPool<>));

            var unwrapped = obj.Unwrap();

            try
            {
                // Use TryRemove directly to avoid ContainsKey + TryRemove TOCTOU race.
                if (!this.ActiveObjects.TryRemove(unwrapped, out _))
                {
                    Logger?.LogWarning(PoolConstants.Messages.ObjectNotInActiveList);
                    throw new NoObjectsInPoolException(PoolConstants.Messages.ObjectNotInPool);
                }

                this.ActiveObjectSlots.Release();

                // Execute return hook
                _lifecycleHookManager?.ExecuteOnReturn(unwrapped);

                // Validate object if configured
                if (Configuration.HasSyncValidator)
                {
                    bool valid;
                    try { valid = Configuration.ValidateSync(unwrapped); }
                    catch (Exception ex) when (ex is not OutOfMemoryException)
                    {
                        Logger?.LogError(ex, "Validation function threw; discarding object");
                        valid = false;
                    }

                    if (!valid)
                    {
                        Logger?.LogWarning(PoolConstants.Messages.ValidationFailed);
                        _lifecycleHookManager?.ExecuteOnValidationFailed(unwrapped);
                        _evictionManager?.UntrackObject(unwrapped);
                        DisposeObject(unwrapped);
                        _availabilitySignal.Release();
                        UpdateStatisticsOnReturn();
                        return;
                    }
                }

                // Check if we're exceeding pool size limit
                if (this.PoolingPolicy.Count >= Configuration.MaxPoolSize)
                {
                    Logger?.LogDebug(PoolConstants.Messages.PoolAtMaxSize);
                    _evictionManager?.UntrackObject(unwrapped);
                    DisposeObject(unwrapped);
                    _availabilitySignal.Release();
                    UpdateStatisticsOnReturn();
                    return;
                }

                // Record return for eviction tracking
                _evictionManager?.RecordReturn(unwrapped);

                this.PoolingPolicy.Add(unwrapped);
                _availabilitySignal.Release();

                UpdateStatisticsOnReturn();

                Logger?.LogDebug(PoolConstants.Messages.ObjectReturnedToPoolActiveAvailable,
                    ActiveObjects.Count, PoolingPolicy.Count);
            }
            finally
            {
                obj.MarkReturned();
            }
        }

        /// <summary>
        /// Asynchronously returns an object to the dynamic pool with async lifecycle hook and async validation support
        /// </summary>
        public override async ValueTask ReturnObjectAsync(PoolModel<T> obj)
        {
            ArgumentNullException.ThrowIfNull(obj);
            if (Disposed) throw new ObjectDisposedException(nameof(DynamicObjectPool<>));

            var unwrapped = obj.Unwrap();

            try
            {
                if (!this.ActiveObjects.TryRemove(unwrapped, out _))
                {
                    Logger?.LogWarning(PoolConstants.Messages.ObjectNotInActiveList);
                    throw new NoObjectsInPoolException(PoolConstants.Messages.ObjectNotInPool);
                }

                this.ActiveObjectSlots.Release();

                if (_lifecycleHookManager != null)
                {
                    await _lifecycleHookManager.ExecuteOnReturnAsync(unwrapped).ConfigureAwait(false);
                }

                // Async validation takes precedence
                if (Configuration.HasAsyncValidator)
                {
                    bool isValid;
                    try { isValid = await Configuration.ValidateAsync(unwrapped).ConfigureAwait(false); }
                    catch (Exception ex) when (ex is not OutOfMemoryException)
                    {
                        Logger?.LogError(ex, "Async validation function threw; discarding object");
                        isValid = false;
                    }

                    if (!isValid)
                    {
                        Logger?.LogWarning(PoolConstants.Messages.ValidationFailed);
                        _lifecycleHookManager?.ExecuteOnValidationFailed(unwrapped);
                        _evictionManager?.UntrackObject(unwrapped);
                        if (Configuration.UseAsyncDisposal)
                        {
                            await DisposeObjectAsync(unwrapped).ConfigureAwait(false);
                        }
                        else
                        {
                            DisposeObject(unwrapped);
                        }
                        _availabilitySignal.Release();
                        UpdateStatisticsOnReturn();
                        return;
                    }
                }
                else if (Configuration.HasSyncValidator)
                {
                    bool valid;
                    try { valid = Configuration.ValidateSync(unwrapped); }
                    catch (Exception ex) when (ex is not OutOfMemoryException)
                    {
                        Logger?.LogError(ex, "Validation function threw; discarding object");
                        valid = false;
                    }

                    if (!valid)
                    {
                        Logger?.LogWarning(PoolConstants.Messages.ValidationFailed);
                        _lifecycleHookManager?.ExecuteOnValidationFailed(unwrapped);
                        _evictionManager?.UntrackObject(unwrapped);
                        if (Configuration.UseAsyncDisposal)
                        {
                            await DisposeObjectAsync(unwrapped).ConfigureAwait(false);
                        }
                        else
                        {
                            DisposeObject(unwrapped);
                        }
                        _availabilitySignal.Release();
                        UpdateStatisticsOnReturn();
                        return;
                    }
                }

                if (this.PoolingPolicy.Count >= Configuration.MaxPoolSize)
                {
                    Logger?.LogDebug(PoolConstants.Messages.PoolAtMaxSize);
                    _evictionManager?.UntrackObject(unwrapped);
                    if (Configuration.UseAsyncDisposal)
                    {
                        await DisposeObjectAsync(unwrapped).ConfigureAwait(false);
                    }
                    else
                    {
                        DisposeObject(unwrapped);
                    }
                    _availabilitySignal.Release();
                    UpdateStatisticsOnReturn();
                    return;
                }

                _evictionManager?.RecordReturn(unwrapped);

                this.PoolingPolicy.Add(unwrapped);
                _availabilitySignal.Release();

                UpdateStatisticsOnReturn();

                Logger?.LogDebug(PoolConstants.Messages.ObjectReturnedToPoolActiveAvailable,
                    ActiveObjects.Count, PoolingPolicy.Count);
            }
            finally
            {
                obj.MarkReturned();
            }
        }

        /// <inheritdoc/>
        protected override void DisposeObject(T obj)
        {
            _lifecycleHookManager?.ExecuteOnDispose(obj);
            base.DisposeObject(obj);
        }

        /// <inheritdoc/>
        protected override async ValueTask DisposeObjectAsync(T obj)
        {
            if (_lifecycleHookManager != null)
            {
                await _lifecycleHookManager.ExecuteOnDisposeAsync(obj).ConfigureAwait(false);
            }
            await base.DisposeObjectAsync(obj).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets eviction statistics
        /// </summary>
        public EvictionStatistics? GetEvictionStatistics()
        {
            return _evictionManager?.GetStatistics();
        }

        /// <summary>
        /// Gets circuit breaker statistics
        /// </summary>
        public CircuitBreakerStatistics? GetCircuitBreakerStatistics()
        {
            return _circuitBreaker?.GetStatistics();
        }

        /// <summary>
        /// Gets lifecycle hook statistics
        /// </summary>
        public LifecycleHookStatistics? GetLifecycleHookStatistics()
        {
            return _lifecycleHookManager?.GetStatistics();
        }

        /// <summary>
        /// Manually triggers an eviction check
        /// </summary>
        public void TriggerEviction()
        {
            PerformEvictionCheck(null);
        }

        /// <summary>
        /// Manually resets the circuit breaker
        /// </summary>
        public void ResetCircuitBreaker()
        {
            _circuitBreaker?.Reset();
            Logger?.LogInformation("Circuit breaker manually reset");
        }

        /// <summary>
        /// Manually trips (opens) the circuit breaker
        /// </summary>
        public void TripCircuitBreaker()
        {
            _circuitBreaker?.Trip();
            Logger?.LogWarning("Circuit breaker manually tripped");
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
            var currentSize = PoolingPolicy.Count;
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
            var errorsLock = new object();

            try
            {
                for (int i = 0; i < objectsToCreate && !cancellationToken.IsCancellationRequested; i++)
                {
                    tasks.Add(Task.Run(() =>
                    {
                        try
                        {
                            // Use circuit breaker protection during warm-up
                            if (_circuitBreaker != null)
                            {
                                return _circuitBreaker.Execute(() => _factory.Invoke());
                            }
                            return _factory.Invoke();
                        }
                        catch (Exception ex) when (ex is not OutOfMemoryException)
                        {
                            Logger?.LogError(ex, "Error creating object during warm-up");
                            lock (errorsLock)
                            {
                                _warmupStatus.Errors.Add($"Factory error: {ex.Message}");
                            }
                            return null;
                        }
                    }, cancellationToken));

                    // Process in batches to avoid overwhelming the system
                    if (tasks.Count >= batchSize || i == objectsToCreate - 1)
                    {
                        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
                        
                        foreach (var newObj in results.Where(o => o != null))
                        {
                            if (_lifecycleHookManager != null)
                            {
                                await _lifecycleHookManager.ExecuteOnCreateAsync(newObj!).ConfigureAwait(false);
                            }
                            PoolingPolicy.Add(newObj!);
                            _evictionManager?.TrackObject(newObj!);
                            _availabilitySignal.Release();
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
            catch (CircuitBreakerOpenException ex)
            {
                Logger?.LogWarning("Pool warm-up interrupted by circuit breaker: {Message}", ex.Message);
                lock (errorsLock)
                {
                    _warmupStatus.Errors.Add($"Circuit breaker open: {ex.Message}");
                }
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
            await WarmUpAsync(targetSize, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the current warm-up status
        /// </summary>
        public WarmupStatus GetWarmupStatus()
        {
            return _warmupStatus;
        }

        #endregion

        /// <summary>
        /// Disposes the pool and releases resources
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing && !Disposed)
            {
                _evictionCheckTimer?.Dispose();
                _evictionManager?.Dispose();
                _circuitBreaker?.Dispose();
            }

            base.Dispose(disposing);
        }

        /// <summary>
        /// Asynchronously disposes the pool, including eviction timer and supporting services.
        /// </summary>
        public override async ValueTask DisposeAsync()
        {
            if (!Disposed)
            {
                _evictionCheckTimer?.Dispose();
                _evictionManager?.Dispose();
                _circuitBreaker?.Dispose();
            }

            await base.DisposeAsync().ConfigureAwait(false);
        }
    }
}
