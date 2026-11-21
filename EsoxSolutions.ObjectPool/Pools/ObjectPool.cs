using EsoxSolutions.ObjectPool.Exceptions;
using EsoxSolutions.ObjectPool.Interfaces;
using EsoxSolutions.ObjectPool.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using EsoxSolutions.ObjectPool.Constants;
using EsoxSolutions.ObjectPool.Metrics;
using EsoxSolutions.ObjectPool.Infrastructure;
using EsoxSolutions.ObjectPool.Policies;

namespace EsoxSolutions.ObjectPool.Pools
{
    /// <summary>
    /// A threadsafe generic object pool
    /// </summary>
    /// <typeparam name="T">The type of object to be stored in the object pool</typeparam>
    public class ObjectPool<T> : IObjectPool<T>, IPoolHealth, IPoolMetrics, IDisposable, IAsyncDisposable where T : notnull
    {
        /// <summary>
        /// The pooling policy managing storage and retrieval order of available objects
        /// </summary>
        protected readonly IPoolingPolicy<T> PoolingPolicy;
        
        /// <summary>
        /// A concurrent dictionary of active objects for efficient O(1) lookups
        /// </summary>
        protected ConcurrentDictionary<T, byte> ActiveObjects;

        /// <summary>
        /// Atomically limits the number of active objects, including objects being created.
        /// </summary>
        protected readonly SemaphoreSlim ActiveObjectSlots;

        /// <summary>
        /// Pool statistics
        /// </summary>
        protected PoolStatistics statistics = new();

        /// <summary>
        /// Pool configuration
        /// </summary>
        protected readonly PoolConfiguration Configuration;

        /// <summary>
        /// Logger instance
        /// </summary>
        protected readonly ILogger? Logger;

        /// <summary>
        /// Signals waiting callers that an object or active slot has become available.
        /// </summary>
        protected readonly SemaphoreSlim _availabilitySignal = new(0, int.MaxValue);

        /// <summary>
        /// Flag to track if the pool has been disposed. Volatile ensures cross-thread visibility.
        /// </summary>
        protected volatile bool Disposed;
        
        /// <summary>
        /// Constructor for the object pool
        /// </summary>
        /// <param name="initialObjects">The list of initialized objects. The number of available objects does not change during the lifetime of the object-pool.</param>
        public ObjectPool(List<T> initialObjects) : this(initialObjects, new PoolConfiguration(), (ILogger?)null)
        {
        }

        /// <summary>
        /// Constructor for the object pool with configuration
        /// </summary>
        /// <param name="initialObjects">The list of initialized objects</param>
        /// <param name="configuration">Pool configuration options</param>
        /// <param name="logger">Logger instance</param>
        public ObjectPool(List<T> initialObjects, PoolConfiguration? configuration, ILogger<ObjectPool<T>>? logger = null)
            : this(initialObjects, configuration, (ILogger?)logger)
        {
        }

        /// <summary>
        /// Protected constructor allowing derived pools to supply their own typed logger
        /// </summary>
        /// <param name="initialObjects">The list of initialized objects</param>
        /// <param name="configuration">Pool configuration options</param>
        /// <param name="logger">Logger instance</param>
        protected ObjectPool(List<T> initialObjects, PoolConfiguration? configuration, ILogger? logger)
        {
            ArgumentNullException.ThrowIfNull(initialObjects);

            this.Configuration = configuration ?? new PoolConfiguration();
            this.Logger = logger;

            if (initialObjects.Count > this.Configuration.MaxPoolSize)
            {
                throw new ArgumentException(string.Format(PoolConstants.Messages.InitialObjectsExceedMaxFormat, 
                    initialObjects.Count, this.Configuration.MaxPoolSize));
            }

            this.ActiveObjects = new ConcurrentDictionary<T, byte>(ReferenceOrValueEqualityComparer<T>.Instance);
            this.ActiveObjectSlots = new SemaphoreSlim(this.Configuration.MaxActiveObjects, this.Configuration.MaxActiveObjects);

            var prioritySelector = ResolvePrioritySelector(this.Configuration);
            this.PoolingPolicy = PoolingPolicyFactory.Create(this.Configuration.PoolingPolicyType, prioritySelector);

            foreach (var obj in initialObjects)
            {
                this.PoolingPolicy.Add(obj);
            }

            logger?.LogInformation(PoolConstants.Messages.ObjectpoolCreatedWithInitialcountObjectsMaxpoolsizeMaxactive,
                initialObjects.Count, this.Configuration.MaxPoolSize, this.Configuration.MaxActiveObjects);
        }

        private static Func<T, int>? ResolvePrioritySelector(PoolConfiguration configuration)
        {
            if (configuration.PrioritySelector is Func<T, int> typedSelector)
            {
                return typedSelector;
            }

            if (configuration.PrioritySelector is Func<object, int> objectSelector)
            {
                return item => objectSelector(item);
            }

            return null;
        }

        /// <summary>
        /// Returns an object from the pool. If no objects are available, an exception is thrown.
        /// </summary>
        /// <returns>A PoolModel object</returns>
        /// <exception cref="NoObjectsInPoolException">Raised when no object could be found</exception>
        public virtual PoolModel<T> GetObject()
        {
            if (Disposed) throw new ObjectDisposedException(GetType().Name);

            Logger?.LogDebug(PoolConstants.Messages.AttemptingToGetObjectFromPoolAvailableCount, PoolingPolicy.Count);

            if (!this.ActiveObjectSlots.Wait(0))
            {
                Logger?.LogWarning(PoolConstants.Messages.MaxActiveLimitFormat, Configuration.MaxActiveObjects);
                throw new InvalidOperationException(string.Format(PoolConstants.Messages.MaxActiveLimitFormat, 
                    Configuration.MaxActiveObjects));
            }

            if (!this.PoolingPolicy.TryTake(out var result) || result is null)
            {
                this.ActiveObjectSlots.Release();
                statistics.IncrementPoolEmpty();
                Logger?.LogWarning(PoolConstants.Messages.PoolEmpty);
                throw new NoObjectsInPoolException(PoolConstants.Messages.NoObjectsAvailable);
            }

            if (!this.ActiveObjects.TryAdd(result, 0))
            {
                this.PoolingPolicy.Add(result);
                this.ActiveObjectSlots.Release();
                throw new InvalidOperationException("The object could not be registered as active.");
            }

            UpdateStatisticsOnRetrieve();

            Logger?.LogDebug(PoolConstants.Messages.ObjectRetrievedFromPoolActiveAvailable,
                ActiveObjects.Count, PoolingPolicy.Count);

            return new PoolModel<T>(result, this);
        }

        /// <summary>
        /// Try to get an object from the pool without throwing an exception
        /// </summary>
        /// <param name="poolModel">The pool model if successful</param>
        /// <returns>True if an object was retrieved, false otherwise</returns>
        public virtual bool TryGetObject(out PoolModel<T>? poolModel)
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

            if (!this.PoolingPolicy.TryTake(out var result) || result is null)
            {
                this.ActiveObjectSlots.Release();
                statistics.IncrementPoolEmpty();
                Logger?.LogDebug(PoolConstants.Messages.NoAvailableObjects);
                poolModel = null;
                return false;
            }

            if (!this.ActiveObjects.TryAdd(result, 0))
            {
                this.PoolingPolicy.Add(result);
                this.ActiveObjectSlots.Release();
                poolModel = null;
                return false;
            }

            UpdateStatisticsOnRetrieve();

            poolModel = new PoolModel<T>(result, this);
            Logger?.LogDebug(PoolConstants.Messages.ObjectRetrievedSuccessfullyActiveAvailable,
                ActiveObjects.Count, PoolingPolicy.Count);
            return true;
        }

        /// <summary>
        /// Returns an object to the pool. If the object is not in the pool, an exception is thrown.
        /// </summary>
        /// <param name="obj">The object to be returned</param>
        /// <exception cref="NoObjectsInPoolException">Raised if the object was not in the active objects list</exception>
        public virtual void ReturnObject(PoolModel<T> obj)
        {
            ArgumentNullException.ThrowIfNull(obj);
            if (Disposed) throw new ObjectDisposedException(GetType().Name);

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
                    DisposeObject(unwrapped);
                    _availabilitySignal.Release();
                    UpdateStatisticsOnReturn();
                    return;
                }

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
        /// Asynchronously returns an object to the pool with async validation support
        /// </summary>
        /// <param name="obj">The object to be returned</param>
        /// <exception cref="NoObjectsInPoolException">Raised if the object was not in the active objects list</exception>
        public virtual async ValueTask ReturnObjectAsync(PoolModel<T> obj)
        {
            ArgumentNullException.ThrowIfNull(obj);
            if (Disposed) throw new ObjectDisposedException(GetType().Name);

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
                // Fall back to sync validation if no async validation
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

                // Check if we're exceeding pool size limit
                if (this.PoolingPolicy.Count >= Configuration.MaxPoolSize)
                {
                    Logger?.LogDebug(PoolConstants.Messages.PoolAtMaxSize);
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
        /// Updates pool statistics after an object is retrieved
        /// </summary>
        protected void UpdateStatisticsOnRetrieve()
        {
            statistics.IncrementRetrieved();
            statistics.CurrentActiveObjects = this.ActiveObjects.Count;
            statistics.CurrentAvailableObjects = this.PoolingPolicy.Count;
            statistics.UpdatePeakIfHigher(statistics.CurrentActiveObjects);
        }

        /// <summary>
        /// Updates pool statistics after an object is returned
        /// </summary>
        protected void UpdateStatisticsOnReturn()
        {
            statistics.IncrementReturned();
            statistics.CurrentActiveObjects = this.ActiveObjects.Count;
            statistics.CurrentAvailableObjects = this.PoolingPolicy.Count;
        }

        /// <summary>
        /// Gets the number of available objects in the pool
        /// </summary>
        public int AvailableObjectCount => PoolingPolicy.Count;

        /// <summary>
        /// Gets the current pool statistics
        /// </summary>
        public PoolStatistics Statistics
        {
            get
            {
                statistics.CurrentActiveObjects = this.ActiveObjects.Count;
                statistics.CurrentAvailableObjects = this.PoolingPolicy.Count;
                return statistics;
            }
        }
        
        #region IPoolHealth Implementation

        /// <summary>
        /// Checks if the pool is healthy
        /// </summary>
        public bool IsHealthy
        {
            get
            {
                var utilizationPct = UtilizationPercentage;
                var hasAvailableObjects = PoolingPolicy.Count > 0;
                var notOverCapacity = ActiveObjects.Count < Configuration.MaxActiveObjects;
                
                return hasAvailableObjects && notOverCapacity && utilizationPct < PoolConstants.Thresholds.CriticalUtilizationThreshold;
            }
        }

        /// <summary>
        /// Gets the utilization percentage of the pool
        /// </summary>
        public double UtilizationPercentage
        {
            get
            {
                var totalCapacity = Math.Min(Configuration.MaxActiveObjects, Configuration.MaxPoolSize);
                if (totalCapacity == int.MaxValue) return 0.0; // Unlimited capacity
                
                return (double)ActiveObjects.Count / totalCapacity * 100.0;
            }
        }

        /// <summary>
        /// Gets health status with details
        /// </summary>
        public PoolHealthStatus GetHealthStatus()
        {
            var availableCount = PoolingPolicy.Count;
            var activeCount = ActiveObjects.Count;
            var status = new PoolHealthStatus
            {
                UtilizationPercentage = UtilizationPercentage,
                LastChecked = DateTime.UtcNow,
                Diagnostics =
                {
                    [PoolConstants.Diagnostics.TotalRetrieved] = statistics.TotalObjectsRetrieved,
                    [PoolConstants.Diagnostics.TotalReturned] = statistics.TotalObjectsReturned,
                    [PoolConstants.Diagnostics.PeakActive] = statistics.PeakActiveObjects,
                    [PoolConstants.Diagnostics.PoolEmptyEvents] = statistics.PoolEmptyCount,
                    [PoolConstants.Diagnostics.CurrentActive] = activeCount,
                    [PoolConstants.Diagnostics.CurrentAvailable] = availableCount
                }
            };

            // Check for warning conditions
            if (availableCount == 0)
            {
                status.Warnings.Add(PoolConstants.Messages.NoAvailableObjects);
                status.WarningCount++;
            }

            if (status.UtilizationPercentage > PoolConstants.Thresholds.HighUtilizationThreshold)
            {
                status.Warnings.Add(string.Format(PoolConstants.Messages.HighUtilizationFormat, status.UtilizationPercentage));
                status.WarningCount++;
            }

            if (statistics.PoolEmptyCount > 0)
            {
                status.Warnings.Add(string.Format(PoolConstants.Messages.EmptyCountWarningFormat, statistics.PoolEmptyCount));
                status.WarningCount++;
            }

            status.IsHealthy = IsHealthy;
            status.HealthMessage = status.IsHealthy ? PoolConstants.Messages.PoolHealthy : 
                string.Format(PoolConstants.Messages.PoolWarningsFormat, status.WarningCount, string.Join(", ", status.Warnings));

            return status;
        }

        #endregion

        /// <summary>
        /// Asynchronously get an object from the pool
        /// </summary>
        /// <param name="timeout">Maximum time to wait for an object</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A poolmodel</returns>
        public virtual async Task<PoolModel<T>> GetObjectAsync(TimeSpan timeout = default, CancellationToken cancellationToken = default)
        {
            if (Disposed) throw new ObjectDisposedException(GetType().Name);

            var effectiveTimeout = timeout == TimeSpan.Zero ? Configuration.DefaultTimeout : timeout;

            Logger?.LogDebug(PoolConstants.Messages.StartingAsyncObjectRetrievalWithTimeout, effectiveTimeout);

            cancellationToken.ThrowIfCancellationRequested();

            // Try once without waiting before entering the semaphore loop.
            if (TryGetObject(out var poolModel))
            {
                Logger?.LogDebug(PoolConstants.Messages.AsyncRetrievalSuccess);
                return poolModel!;
            }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(effectiveTimeout);

            while (true)
            {
                bool signalled;
                try
                {
                    signalled = await _availabilitySignal
                        .WaitAsync(effectiveTimeout, timeoutCts.Token)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        Logger?.LogDebug(PoolConstants.Messages.AsyncRetrievalCancelled);
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                    // Inner timeout CTS fired.
                    Logger?.LogWarning(PoolConstants.Messages.TimeoutWaitingFormat, effectiveTimeout);
                    throw new TimeoutException(string.Format(PoolConstants.Messages.TimeoutWaitingFormat, effectiveTimeout));
                }

                if (!signalled)
                {
                    Logger?.LogWarning(PoolConstants.Messages.TimeoutWaitingFormat, effectiveTimeout);
                    throw new TimeoutException(string.Format(PoolConstants.Messages.TimeoutWaitingFormat, effectiveTimeout));
                }

                cancellationToken.ThrowIfCancellationRequested();

                if (TryGetObject(out poolModel))
                {
                    Logger?.LogDebug(PoolConstants.Messages.AsyncRetrievalSuccess);
                    return poolModel!;
                }
                // Another thread consumed the object; loop and wait again.
            }
        }

        #region IPoolMetrics Implementation

        /// <summary>
        /// Export metrics with tags/labels for dimensional monitoring
        /// </summary>
        public virtual Dictionary<string, object> ExportMetrics(Dictionary<string, string>? tags = null)
        {
            var metrics = new Dictionary<string, object>
            {
                [PoolConstants.Metrics.RetrievedTotal] = statistics.TotalObjectsRetrieved,
                [PoolConstants.Metrics.ReturnedTotal] = statistics.TotalObjectsReturned,
                [PoolConstants.Metrics.ActiveCurrent] = ActiveObjects.Count,
                [PoolConstants.Metrics.AvailableCurrent] = PoolingPolicy.Count,
                [PoolConstants.Metrics.ActivePeak] = statistics.PeakActiveObjects,
                [PoolConstants.Metrics.EmptyEventsTotal] = statistics.PoolEmptyCount,
                [PoolConstants.Metrics.UtilizationPercentage] = UtilizationPercentage,
                [PoolConstants.Metrics.HealthStatus] = IsHealthy ? 1 : 0,
                [PoolConstants.Metrics.MaxSize] = Configuration.MaxPoolSize == int.MaxValue ? -1 : Configuration.MaxPoolSize,
                [PoolConstants.Metrics.MaxActive] = Configuration.MaxActiveObjects == int.MaxValue ? -1 : Configuration.MaxActiveObjects,
                [PoolConstants.Metrics.StartTime] = statistics.StatisticsStartTime,
                [PoolConstants.Metrics.UptimeSeconds] = (DateTime.UtcNow - statistics.StatisticsStartTime).TotalSeconds
            };

            if (tags != null)
            {
                foreach (var tag in tags)
                {
                    metrics[$"{PoolConstants.Metrics.TagPrefix}{tag.Key}"] = tag.Value;
                }
            }

            return metrics;
        }

        /// <summary>
        /// Convenience method to export metrics in Prometheus exposition format directly from the pool.
        /// </summary>
        /// <param name="tags">Optional tags to include as labels.</param>
        /// <returns>Prometheus exposition formatted text.</returns>
        public string ExportMetricsPrometheus(Dictionary<string, string>? tags = null)
        {
            return PrometheusExporter.ExportMetricsPrometheus(this, tags);
        }

        /// <summary>
        /// Reset metrics counters (useful for testing or periodic resets)
        /// </summary>
        public void ResetMetrics()
        {
            Logger?.LogInformation(PoolConstants.Messages.ResettingMetrics);
            statistics.Reset(ActiveObjects.Count, PoolingPolicy.Count);
        }

        #endregion

        #region IDisposable and IAsyncDisposable Implementation

        /// <summary>
        /// Asynchronously disposes the object pool and all pooled objects.
        /// Prefers IAsyncDisposable over IDisposable when available.
        /// </summary>
        public virtual async ValueTask DisposeAsync()
        {
            if (Disposed)
                return;

            Disposed = true;

            Logger?.LogInformation("Asynchronously disposing ObjectPool with {Active} active objects and {Available} available objects",
                ActiveObjects.Count, PoolingPolicy.Count);

            // Dispose available objects
            foreach (var obj in PoolingPolicy.GetAll())
            {
                await DisposeObjectAsync(obj).ConfigureAwait(false);
            }

            // Dispose active objects
            foreach (var obj in ActiveObjects.Keys)
            {
                await DisposeObjectAsync(obj).ConfigureAwait(false);
            }

            PoolingPolicy.Clear();
            ActiveObjects.Clear();

            _availabilitySignal.Dispose();
            ActiveObjectSlots.Dispose();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Helper method to synchronously dispose an object if it implements IDisposable
        /// </summary>
        protected virtual void DisposeObject(T obj)
        {
            try
            {
                if (obj is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Logger?.LogWarning(ex, "Error disposing object of type {Type}", typeof(T).Name);
            }
        }

        /// <summary>
        /// Helper method to dispose an object, preferring async disposal if available
        /// </summary>
        protected virtual async ValueTask DisposeObjectAsync(T obj)
        {
            try
            {
                if (obj is IAsyncDisposable asyncDisposable)
                {
                    await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                }
                else if (obj is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Logger?.LogWarning(ex, "Error disposing object of type {Type}", typeof(T).Name);
            }
        }

        /// <summary>
        /// Dispose the pool and clean up resources
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected dispose method
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!Disposed && disposing)
            {
                Disposed = true;

                Logger?.LogInformation("Disposing ObjectPool with {Active} active objects and {Available} available objects", 
                    ActiveObjects.Count, PoolingPolicy.Count);

                foreach (var obj in PoolingPolicy.GetAll())
                {
                    DisposeObject(obj);
                }

                foreach (var obj in ActiveObjects.Keys)
                {
                    DisposeObject(obj);
                }

                PoolingPolicy.Clear();
                ActiveObjects.Clear();

                _availabilitySignal.Dispose();
                ActiveObjectSlots.Dispose();
            }
        }

        #endregion
    }
}
