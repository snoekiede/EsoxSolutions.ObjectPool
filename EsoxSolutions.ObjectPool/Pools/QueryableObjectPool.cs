using EsoxSolutions.ObjectPool.Constants;
using EsoxSolutions.ObjectPool.Exceptions;
using EsoxSolutions.ObjectPool.Interfaces;
using EsoxSolutions.ObjectPool.Models;
using EsoxSolutions.ObjectPool.Metrics;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using EsoxSolutions.ObjectPool.Infrastructure;

namespace EsoxSolutions.ObjectPool.Pools
{
    /// <summary>
    /// Queryable object pool
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class QueryableObjectPool<T> : IQueryableObjectPool<T>, IPoolHealth, IPoolMetrics, IDisposable where T: notnull
    {
        /// <summary>
        /// A concurrent stack of available objects for efficient O(1) operations
        /// </summary>
        protected ConcurrentStack<T> AvailableObjects;
        
        /// <summary>
        /// A concurrent dictionary of active objects for efficient O(1) lookups
        /// </summary>
        protected ConcurrentDictionary<T, byte> ActiveObjects;

        /// <summary>
        /// Atomically limits the number of active objects.
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
        /// Signals waiting callers that an object has been returned to the pool.
        /// </summary>
        private readonly SemaphoreSlim _availabilitySignal = new(0, int.MaxValue);

        /// <summary>
        /// Flag to track if the pool has been disposed. Volatile ensures cross-thread visibility.
        /// </summary>
        protected volatile bool Disposed;

        /// <summary>
        /// The constructor for the queryable object pool
        /// </summary>
        /// <param name="initialObjects">the list of initial objects</param>
        public QueryableObjectPool(List<T> initialObjects) : this(initialObjects, new PoolConfiguration())
        {
        }

        /// <summary>
        /// Constructor for the queryable object pool with configuration and logging
        /// </summary>
        /// <param name="initialObjects">The list of initialized objects</param>
        /// <param name="configuration">Pool configuration options</param>
        /// <param name="logger">Logger instance</param>
        public QueryableObjectPool(List<T> initialObjects, PoolConfiguration? configuration, ILogger<QueryableObjectPool<T>>? logger = null)
        {
            this.Configuration = configuration ?? new PoolConfiguration();
            this.Logger = logger;
            this.ActiveObjects = new ConcurrentDictionary<T, byte>(ReferenceOrValueEqualityComparer<T>.Instance);
            this.ActiveObjectSlots = new SemaphoreSlim(this.Configuration.MaxActiveObjects, this.Configuration.MaxActiveObjects);
            // Initialize the concurrent stack from the provided initial list
            this.AvailableObjects = new ConcurrentStack<T>(initialObjects);
            this.Disposed = false;
            
            if (initialObjects.Count > this.Configuration.MaxPoolSize)
            {
                throw new ArgumentException(string.Format(PoolConstants.Messages.InitialObjectsExceedMaxFormat, 
                    initialObjects.Count, this.Configuration.MaxPoolSize));
            }
            
            logger?.LogInformation(PoolConstants.Messages.ObjectpoolCreatedWithInitialcountObjectsMaxpoolsizeMaxactive,
                initialObjects.Count, this.Configuration.MaxPoolSize, this.Configuration.MaxActiveObjects);
        }

        /// <summary>
        /// Gets the number of available objects in the pool
        /// </summary>
        public int AvailableObjectCount => this.AvailableObjects.Count;

        /// <summary>
        /// Gets the current pool statistics
        /// </summary>
        public PoolStatistics Statistics
        {
            get
            {
                statistics.CurrentActiveObjects = this.ActiveObjects.Count;
                statistics.CurrentAvailableObjects = this.AvailableObjects.Count;
                return statistics;
            }
        }

        /// <summary>
        /// Returns an object from the pool. If no objects are available, an exception is thrown.
        /// </summary>
        /// <returns>A PoolModel object</returns>
        /// <exception cref="NoObjectsInPoolException">Raised when no object could be found</exception>
        public virtual PoolModel<T> GetObject()
        {
            if (Disposed) throw new ObjectDisposedException(nameof(QueryableObjectPool<>));
            
            Logger?.LogDebug(PoolConstants.Messages.AttemptingToGetObjectFromPoolAvailableCount, AvailableObjects.Count);
            
            if (!this.ActiveObjectSlots.Wait(0))
            {
                Logger?.LogWarning(PoolConstants.Messages.MaxActiveLimitFormat, Configuration.MaxActiveObjects);
                throw new InvalidOperationException(string.Format(PoolConstants.Messages.MaxActiveLimitFormat, 
                    Configuration.MaxActiveObjects));
            }
            
            if (!this.AvailableObjects.TryPop(out var result))
            {
                this.ActiveObjectSlots.Release();
                statistics.IncrementPoolEmpty();
                Logger?.LogWarning(PoolConstants.Messages.PoolEmpty);
                throw new NoObjectsInPoolException(PoolConstants.Messages.NoObjectsAvailable);
            }

            if (!this.ActiveObjects.TryAdd(result, 0))
            {
                this.AvailableObjects.Push(result);
                this.ActiveObjectSlots.Release();
                throw new InvalidOperationException("The object could not be registered as active.");
            }

            statistics.IncrementRetrieved();
            statistics.CurrentActiveObjects = this.ActiveObjects.Count;
            statistics.CurrentAvailableObjects = this.AvailableObjects.Count;
            statistics.UpdatePeakIfHigher(statistics.CurrentActiveObjects);

            Logger?.LogDebug(PoolConstants.Messages.ObjectRetrievedFromPoolActiveAvailable,
                ActiveObjects.Count, AvailableObjects.Count);

            return new PoolModel<T>(result, this);
        }

        /// <summary>
        /// Try to get an object from the pool without throwing an exception
        /// </summary>
        /// <param name="poolModel">The pool model if successful</param>
        /// <returns>True if an object was retrieved, false otherwise</returns>
        public bool TryGetObject(out PoolModel<T>? poolModel)
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
            
            if (!this.AvailableObjects.TryPop(out var result))
            {
                this.ActiveObjectSlots.Release();
                statistics.IncrementPoolEmpty();
                Logger?.LogDebug(PoolConstants.Messages.NoAvailableObjects);
                poolModel = null;
                return false;
            }

            if (!this.ActiveObjects.TryAdd(result, 0))
            {
                this.AvailableObjects.Push(result);
                this.ActiveObjectSlots.Release();
                poolModel = null;
                return false;
            }

            statistics.IncrementRetrieved();
            statistics.CurrentActiveObjects = this.ActiveObjects.Count;
            statistics.CurrentAvailableObjects = this.AvailableObjects.Count;
            statistics.UpdatePeakIfHigher(statistics.CurrentActiveObjects);

            poolModel = new PoolModel<T>(result, this);
            Logger?.LogDebug(PoolConstants.Messages.ObjectRetrievedSuccessfullyActiveAvailable,
                ActiveObjects.Count, AvailableObjects.Count);
            return true;
        }

        /// <summary>
        /// Returns an object to the pool. If the object is not in the pool, an exception is thrown.
        /// </summary>
        /// <param name="obj">The object to be returned</param>
        /// <exception cref="NoObjectsInPoolException">Raised if the object was not in the active objects list</exception>
        public void ReturnObject(PoolModel<T> obj)
        {
            if (Disposed) throw new ObjectDisposedException(nameof(QueryableObjectPool<>));

            var unwrapped = obj.Unwrap();

            // Use TryRemove directly to avoid ContainsKey + TryRemove TOCTOU race.
            if (!this.ActiveObjects.TryRemove(unwrapped, out _))
            {
                Logger?.LogWarning(PoolConstants.Messages.ObjectNotInActiveList);
                throw new NoObjectsInPoolException(PoolConstants.Messages.ObjectNotInPool);
            }

            this.ActiveObjectSlots.Release();

            // Validate object if configured
            if (Configuration is {ValidateOnReturn: true, ValidationFunction: not null})
            {
                bool valid;
                try { valid = Configuration.ValidationFunction(unwrapped); }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    Logger?.LogError(ex, "Validation function threw; discarding object");
                    valid = false;
                }

                if (!valid)
                {
                    Logger?.LogWarning(PoolConstants.Messages.ValidationFailed);
                    statistics.IncrementReturned();
                    statistics.CurrentActiveObjects = this.ActiveObjects.Count;
                    statistics.CurrentAvailableObjects = this.AvailableObjects.Count;
                    return;
                }
            }

            // Check if we're exceeding pool size limit
            if (this.AvailableObjects.Count >= Configuration.MaxPoolSize)
            {
                Logger?.LogDebug(PoolConstants.Messages.PoolAtMaxSize);
                statistics.IncrementReturned();
                statistics.CurrentActiveObjects = this.ActiveObjects.Count;
                statistics.CurrentAvailableObjects = this.AvailableObjects.Count;
                return;
            }

            this.AvailableObjects.Push(unwrapped);
            _availabilitySignal.Release();

            statistics.IncrementReturned();
            statistics.CurrentActiveObjects = this.ActiveObjects.Count;
            statistics.CurrentAvailableObjects = this.AvailableObjects.Count;

            Logger?.LogDebug(PoolConstants.Messages.ObjectReturnedToPoolActiveAvailable,
                ActiveObjects.Count, AvailableObjects.Count);
        }

        /// <summary>
        /// Asynchronously returns an object to the pool with async validation support
        /// </summary>
        /// <param name="obj">The object to be returned</param>
        public async ValueTask ReturnObjectAsync(PoolModel<T> obj)
        {
            if (Disposed) throw new ObjectDisposedException(nameof(QueryableObjectPool<>));

            var unwrapped = obj.Unwrap();

            // Use TryRemove directly to avoid ContainsKey + TryRemove TOCTOU race.
            if (!this.ActiveObjects.TryRemove(unwrapped, out _))
            {
                Logger?.LogWarning(PoolConstants.Messages.ObjectNotInActiveList);
                throw new NoObjectsInPoolException(PoolConstants.Messages.ObjectNotInPool);
            }

            this.ActiveObjectSlots.Release();

            // Async validation takes precedence
            if (Configuration is { ValidateOnReturn: true, AsyncValidationFunction: not null })
            {
                bool isValid;
                try { isValid = await Configuration.AsyncValidationFunction(unwrapped).ConfigureAwait(false); }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    Logger?.LogError(ex, "Async validation function threw; discarding object");
                    isValid = false;
                }

                if (!isValid)
                {
                    Logger?.LogWarning(PoolConstants.Messages.ValidationFailed);
                    statistics.IncrementReturned();
                    statistics.CurrentActiveObjects = this.ActiveObjects.Count;
                    statistics.CurrentAvailableObjects = this.AvailableObjects.Count;
                    return;
                }
            }
            // Fall back to sync validation if no async validation
            else if (Configuration is { ValidateOnReturn: true, ValidationFunction: not null })
            {
                bool valid;
                try { valid = Configuration.ValidationFunction(unwrapped); }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    Logger?.LogError(ex, "Validation function threw; discarding object");
                    valid = false;
                }

                if (!valid)
                {
                    Logger?.LogWarning(PoolConstants.Messages.ValidationFailed);
                    statistics.IncrementReturned();
                    statistics.CurrentActiveObjects = this.ActiveObjects.Count;
                    statistics.CurrentAvailableObjects = this.AvailableObjects.Count;
                    return;
                }
            }

            // Check if we're exceeding pool size limit
            if (this.AvailableObjects.Count >= Configuration.MaxPoolSize)
            {
                Logger?.LogDebug(PoolConstants.Messages.PoolAtMaxSize);
                statistics.IncrementReturned();
                statistics.CurrentActiveObjects = this.ActiveObjects.Count;
                statistics.CurrentAvailableObjects = this.AvailableObjects.Count;
                return;
            }

            this.AvailableObjects.Push(unwrapped);
            _availabilitySignal.Release();

            statistics.IncrementReturned();
            statistics.CurrentActiveObjects = this.ActiveObjects.Count;
            statistics.CurrentAvailableObjects = this.AvailableObjects.Count;

            Logger?.LogDebug(PoolConstants.Messages.ObjectReturnedToPoolActiveAvailable,
                ActiveObjects.Count, AvailableObjects.Count);
        }

        /// <summary>
        /// Get objects from the pool which match the query. If no objects are available, an exception is thrown.
        /// </summary>
        /// <param name="query">the query to be performed</param>
        /// <returns>an object from the pool</returns>
        /// <exception cref="NoObjectsInPoolException">Thrown when no objects could be found</exception>
        public PoolModel<T> GetObject(Func<T, bool> query)
        {
            if (Disposed) throw new ObjectDisposedException(nameof(QueryableObjectPool<>));

            Logger?.LogDebug(PoolConstants.Messages.AttemptingToGetObjectFromPoolUsingQueryAvailableCount, AvailableObjects.Count);
            
            if (!this.ActiveObjectSlots.Wait(0))
            {
                Logger?.LogWarning(PoolConstants.Messages.MaxActiveLimitFormat, Configuration.MaxActiveObjects);
                throw new InvalidOperationException(string.Format(PoolConstants.Messages.MaxActiveLimitFormat, 
                    Configuration.MaxActiveObjects));
            }
            
            // Create a snapshot of available objects
            var availableObjects = this.AvailableObjects.ToArray();
            
            // Find a matching object in the snapshot
            var matchingObject = availableObjects.FirstOrDefault(query);
            if (matchingObject == null || EqualityComparer<T>.Default.Equals(matchingObject, default))
            {
                statistics.IncrementPoolEmpty();
                Logger?.LogWarning(PoolConstants.Messages.NoObjectsInPoolMatchingYourQuery);
                throw new NoObjectsInPoolException(PoolConstants.Messages.NoObjectsInPoolMatchingYourQuery);
            }

            // Try to find and remove a matching object from the available objects
            bool foundMatch = false;
            T foundObject = default!;
            
            // Create a temporary stack to hold non-matching objects
            var tempStack = new ConcurrentStack<T>();
            
            // Pop items from available stack until we find a match or empty the stack
            while (!foundMatch && this.AvailableObjects.TryPop(out var item))
            {
                if (!foundMatch && query(item))
                {
                    // Found a matching object
                    foundMatch = true;
                    foundObject = item;
                }
                else
                {
                    // Not a match, push to temp stack
                    tempStack.Push(item);
                }
            }
            
            // Push all the non-matching items back to the available stack
            foreach (var item in tempStack)
            {
                this.AvailableObjects.Push(item);
            }
            
            if (!foundMatch)
            {
                this.ActiveObjectSlots.Release();
                // No matching object was available (might have been taken by another thread)
                statistics.IncrementPoolEmpty();
                Logger?.LogWarning(PoolConstants.Messages.NoObjectsInPoolMatchingYourQuery);
                throw new NoObjectsInPoolException(PoolConstants.Messages.NoObjectsInPoolMatchingYourQuery);
            }

            // Add to active objects and return
            if (!this.ActiveObjects.TryAdd(foundObject, 0))
            {
                this.AvailableObjects.Push(foundObject);
                this.ActiveObjectSlots.Release();
                throw new InvalidOperationException("The object could not be registered as active.");
            }

            statistics.IncrementRetrieved();
            statistics.CurrentActiveObjects = this.ActiveObjects.Count;
            statistics.CurrentAvailableObjects = this.AvailableObjects.Count;
            statistics.UpdatePeakIfHigher(statistics.CurrentActiveObjects);

            Logger?.LogDebug(PoolConstants.Messages.ObjectMatchingQueryRetrievedFromPoolActiveAvailable, 
                ActiveObjects.Count, AvailableObjects.Count);

            return new PoolModel<T>(foundObject, this);
        }

        /// <summary>
        /// Try to get an object from the pool that matches the query without throwing an exception
        /// </summary>
        /// <param name="query">The query to be performed</param>
        /// <param name="poolModel">The pool model if successful</param>
        /// <returns>True if a matching object was retrieved, false otherwise</returns>
        public bool TryGetObject(Func<T, bool> query, out PoolModel<T>? poolModel)
        {
            poolModel = null;
            
            if (Disposed)
            {
                return false;
            }
            
            if (!this.ActiveObjectSlots.Wait(0))
            {
                Logger?.LogDebug(PoolConstants.Messages.CannotGetObjectActiveObjectsLimitMaxactiveReached, Configuration.MaxActiveObjects);
                return false;
            }
            
            // Try to find and remove a matching object directly from the stack
            T? foundObject = default;
            bool foundMatch = false;
            
            // Create a temporary stack to hold non-matching objects
            var tempStack = new ConcurrentStack<T>();
            
            // Pop items from available stack until we find a match or empty the stack
            while (!foundMatch && this.AvailableObjects.TryPop(out var item))
            {
                if (query(item))
                {
                    // Found a matching object
                    foundMatch = true;
                    foundObject = item;
                    break; // Exit loop immediately
                }
                
                // Not a match, push to temp stack
                tempStack.Push(item);
            }
            
            // Push all the non-matching items back to the available stack in bulk
            if (!tempStack.IsEmpty)
            {
                // More efficient: Push range instead of individual pushes
                this.AvailableObjects.PushRange(tempStack.ToArray());
            }
            
            if (!foundMatch)
            {
                this.ActiveObjectSlots.Release();
                Logger?.LogDebug(PoolConstants.Messages.NoMatchingObjectAvailableRaceConditionTakenByAnotherThread);
                return false;
            }
            
            // Add to active objects and return
            if (!this.ActiveObjects.TryAdd(foundObject!, 0))
            {
                this.AvailableObjects.Push(foundObject!);
                this.ActiveObjectSlots.Release();
                return false;
            }

            // Update statistics atomically
            statistics.IncrementRetrieved();
            statistics.CurrentActiveObjects = this.ActiveObjects.Count;
            statistics.CurrentAvailableObjects = this.AvailableObjects.Count;
            statistics.UpdatePeakIfHigher(statistics.CurrentActiveObjects);

            poolModel = new PoolModel<T>(foundObject!, this);
            Logger?.LogDebug(PoolConstants.Messages.ObjectMatchingQueryRetrievedSuccessfullyActiveAvailable, 
                ActiveObjects.Count, AvailableObjects.Count);
            return true;
        }

        /// <summary>
        /// Asynchronously get an object from the pool
        /// </summary>
        /// <param name="timeout">Maximum time to wait for an object</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A poolmodel</returns>
        public async Task<PoolModel<T>> GetObjectAsync(TimeSpan timeout = default, CancellationToken cancellationToken = default)
        {
            if (Disposed) throw new ObjectDisposedException(nameof(QueryableObjectPool<>));

            var effectiveTimeout = timeout == TimeSpan.Zero ? Configuration.DefaultTimeout : timeout;

            Logger?.LogDebug(PoolConstants.Messages.StartingAsyncObjectRetrievalWithTimeout, effectiveTimeout);

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
            }
        }
        
        /// <summary>
        /// Asynchronously get an object from the pool that matches the query
        /// </summary>
        /// <param name="query">The query to be performed</param>
        /// <param name="timeout">Maximum time to wait for an object</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A poolmodel</returns>
        public async Task<PoolModel<T>> GetObjectAsync(Func<T, bool> query, TimeSpan timeout = default, CancellationToken cancellationToken = default)
        {
            if (Disposed) throw new ObjectDisposedException(nameof(QueryableObjectPool<>));

            var effectiveTimeout = timeout == TimeSpan.Zero ? Configuration.DefaultTimeout : timeout;

            Logger?.LogDebug(PoolConstants.Messages.StartingAsyncObjectRetrievalWithQueryAndTimeout, effectiveTimeout);

            if (TryGetObject(query, out var poolModel))
            {
                Logger?.LogDebug(PoolConstants.Messages.SuccessfullyRetrievedObjectWithQueryAsynchronously);
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
                    Logger?.LogWarning(PoolConstants.Messages.TimeoutWaitingForObjectMatchingQueryFromPoolAfter, effectiveTimeout);
                    throw new TimeoutException(string.Format(PoolConstants.Messages.TimeoutWaitingFormat, effectiveTimeout));
                }

                if (!signalled)
                {
                    Logger?.LogWarning(PoolConstants.Messages.TimeoutWaitingForObjectMatchingQueryFromPoolAfter, effectiveTimeout);
                    throw new TimeoutException(string.Format(PoolConstants.Messages.TimeoutWaitingFormat, effectiveTimeout));
                }

                cancellationToken.ThrowIfCancellationRequested();

                if (TryGetObject(query, out poolModel))
                {
                    Logger?.LogDebug(PoolConstants.Messages.SuccessfullyRetrievedObjectWithQueryAsynchronously);
                    return poolModel!;
                }
                // A non-matching object was returned; put the signal back for the next waiter.
                _availabilitySignal.Release();
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
                var hasAvailableObjects = AvailableObjects.Count > 0;
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
                    [PoolConstants.Diagnostics.CurrentActive] = ActiveObjects.Count,
                    [PoolConstants.Diagnostics.CurrentAvailable] = AvailableObjects.Count
                }
            };

            // Check for warning conditions
            if (AvailableObjects.Count == 0)
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

        #region IPoolMetrics Implementation

        /// <summary>
        /// Export metrics with tags/labels for dimensional monitoring
        /// </summary>
        public Dictionary<string, object> ExportMetrics(Dictionary<string, string>? tags = null)
        {
            var metrics = new Dictionary<string, object>
            {
                [PoolConstants.Metrics.RetrievedTotal] = statistics.TotalObjectsRetrieved,
                [PoolConstants.Metrics.ReturnedTotal] = statistics.TotalObjectsReturned,
                [PoolConstants.Metrics.ActiveCurrent] = ActiveObjects.Count,
                [PoolConstants.Metrics.AvailableCurrent] = AvailableObjects.Count,
                [PoolConstants.Metrics.ActivePeak] = statistics.PeakActiveObjects,
                [PoolConstants.Metrics.EmptyEventsTotal] = statistics.PoolEmptyCount,
                [PoolConstants.Metrics.UtilizationPercentage] = UtilizationPercentage,
                [PoolConstants.Metrics.HealthStatus] = IsHealthy ? 1 : 0,
                [PoolConstants.Metrics.MaxSize] = Configuration.MaxPoolSize == int.MaxValue ? -1 : Configuration.MaxPoolSize,
                [PoolConstants.Metrics.MaxActive] = Configuration.MaxActiveObjects == int.MaxValue ? -1 : Configuration.MaxActiveObjects,
                [PoolConstants.Metrics.StartTime] = statistics.StatisticsStartTime,
                [PoolConstants.Metrics.UptimeSeconds] = (DateTime.UtcNow - statistics.StatisticsStartTime).TotalSeconds,
                // Add a metric to indicate this is a queryable pool
                ["pool_type"] = "queryable"
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
            statistics.Reset(ActiveObjects.Count, AvailableObjects.Count);
        }

        /// <summary>
        /// Gets the metric type for the specified key
        /// </summary>
        /// <param name="metricKey">The metric key</param>
        /// <returns>The metric type</returns>
        private static string GetMetricType(string metricKey)
        {
            return metricKey switch
            {
                PoolConstants.Metrics.RetrievedTotal => PoolConstants.MetricTypes.Counter,
                PoolConstants.Metrics.ReturnedTotal => PoolConstants.MetricTypes.Counter,
                PoolConstants.Metrics.EmptyEventsTotal => PoolConstants.MetricTypes.Counter,
                PoolConstants.Metrics.UptimeSeconds => PoolConstants.MetricTypes.Counter,
                _ => PoolConstants.MetricTypes.Gauge
            };
        }

        /// <summary>
        /// Gets the metric description for the specified key
        /// </summary>
        /// <param name="metricKey">The metric key</param>
        /// <returns>The metric description</returns>
        private static string GetMetricDescription(string metricKey)
        {
            return metricKey switch
            {
                PoolConstants.Metrics.RetrievedTotal => "Total number of objects retrieved from the pool",
                PoolConstants.Metrics.ReturnedTotal => "Total number of objects returned to the pool",
                PoolConstants.Metrics.ActiveCurrent => "Current number of active objects",
                PoolConstants.Metrics.AvailableCurrent => "Current number of available objects in the pool",
                PoolConstants.Metrics.ActivePeak => "Peak number of active objects",
                PoolConstants.Metrics.EmptyEventsTotal => "Total number of times the pool was empty when requested",
                PoolConstants.Metrics.UtilizationPercentage => "Pool utilization as a percentage",
                PoolConstants.Metrics.HealthStatus => "Pool health status (1=healthy, 0=unhealthy)",
                PoolConstants.Metrics.UptimeSeconds => "Pool uptime in seconds",
                "pool_type" => "Type of pool (queryable)",
                _ => "Pool metric"
            };
        }

        #endregion
        
        #region IDisposable Implementation

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
                Logger?.LogInformation(PoolConstants.Messages.DisposingQueryableobjectpoolWithActiveActiveObjectsAndAvailableAvailableObjects, 
                    ActiveObjects.Count, AvailableObjects.Count);

                foreach (var obj in AvailableObjects)
                {
                    if (obj is IDisposable disposableObj)
                    {
                        disposableObj.Dispose();
                    }
                }

                foreach (var obj in ActiveObjects.Keys)
                {
                    if (obj is IDisposable disposableObj)
                    {
                        disposableObj.Dispose();
                    }
                }

                AvailableObjects.Clear();
                ActiveObjects.Clear();

                _availabilitySignal.Dispose();
                ActiveObjectSlots.Dispose();
                Disposed = true;
            }
        }

        #endregion
    }
}

