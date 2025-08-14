﻿using EsoxSolutions.ObjectPool.Exceptions;
using EsoxSolutions.ObjectPool.Interfaces;
using EsoxSolutions.ObjectPool.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using EsoxSolutions.ObjectPool.Constants;

namespace EsoxSolutions.ObjectPool.Pools
{
    /// <summary>
    /// A threadsafe generic object pool
    /// </summary>
    /// <typeparam name="T">The type of object to be stored in the object pool</typeparam>
    public class ObjectPool<T> : IObjectPool<T>, IPoolHealth, IPoolMetrics, IDisposable where T : notnull
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
        /// Flag to track if the pool has been disposed
        /// </summary>
        protected bool Disposed;
        /// <summary>
        /// Constructor for the object pool
        /// </summary>
        /// <param name="initialObjects">The list of initialized objects. The number of available objects does not change during the lifetime of the object-pool.</param>
        public ObjectPool(List<T> initialObjects) : this(initialObjects, new PoolConfiguration())
        {
        }

        /// <summary>
        /// Constructor for the object pool with configuration
        /// </summary>
        /// <param name="initialObjects">The list of initialized objects</param>
        /// <param name="configuration">Pool configuration options</param>
        /// <param name="logger">Logger instance</param>
        public ObjectPool(List<T> initialObjects, PoolConfiguration? configuration, ILogger<ObjectPool<T>>? logger = null)
        {
            this.Configuration = configuration ?? new PoolConfiguration();
            this.Logger = logger;
            this.ActiveObjects = new ConcurrentDictionary<T, byte>();
            this.AvailableObjects = new ConcurrentStack<T>(initialObjects);

            if (initialObjects.Count > this.Configuration.MaxPoolSize)
            {
                throw new ArgumentException(string.Format(PoolConstants.Messages.InitialObjectsExceedMaxFormat, 
                    initialObjects.Count, this.Configuration.MaxPoolSize));
            }

            logger?.LogInformation("ObjectPool created with {InitialCount} objects, MaxPoolSize: {MaxPoolSize}, MaxActive: {MaxActive}",
                initialObjects.Count, this.Configuration.MaxPoolSize, this.Configuration.MaxActiveObjects);
        }

        /// <summary>
        /// Returns an object from the pool. If no objects are available, an exception is thrown.
        /// </summary>
        /// <returns>A PoolModel object</returns>
        /// <exception cref="NoObjectsInPoolException">Raised when no object could be found</exception>
        public virtual PoolModel<T> GetObject()
        {
            if (Disposed) throw new ObjectDisposedException(nameof(ObjectPool<T>));

            Logger?.LogDebug("Attempting to get object from pool. Available: {Count}", AvailableObjects.Count);

            if (this.ActiveObjects.Count >= Configuration.MaxActiveObjects)
            {
                throw new InvalidOperationException(string.Format(PoolConstants.Messages.MaxActiveLimitFormat, 
                    Configuration.MaxActiveObjects));
            }

            if (!this.AvailableObjects.TryPop(out var result))
            {
                statistics.PoolEmptyCount++;
                Logger?.LogWarning(PoolConstants.Messages.PoolEmpty);
                throw new NoObjectsInPoolException(PoolConstants.Messages.NoObjectsAvailable);
            }
            this.ActiveObjects.TryAdd(result, 0);

            statistics.TotalObjectsRetrieved++;
            statistics.CurrentActiveObjects = this.ActiveObjects.Count;
            statistics.CurrentAvailableObjects = this.AvailableObjects.Count;
            if (statistics.CurrentActiveObjects > statistics.PeakActiveObjects)
            {
                statistics.PeakActiveObjects = statistics.CurrentActiveObjects;
            }

            Logger?.LogDebug("Object retrieved from pool. Active: {Active}, Available: {Available}",
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

            if (this.ActiveObjects.Count >= Configuration.MaxActiveObjects)
            {
                Logger?.LogDebug("Cannot get object: active objects limit ({MaxActive}) reached", Configuration.MaxActiveObjects);
                poolModel = null;
                return false;
            }

            if (!this.AvailableObjects.TryPop(out var result))
            {
                statistics.PoolEmptyCount++;
                Logger?.LogDebug(PoolConstants.Messages.NoAvailableObjects);
                poolModel = null;
                return false;
            }

            this.ActiveObjects.TryAdd(result, 0);

            statistics.TotalObjectsRetrieved++;
            statistics.CurrentActiveObjects = this.ActiveObjects.Count;
            statistics.CurrentAvailableObjects = this.AvailableObjects.Count;
            if (statistics.CurrentActiveObjects > statistics.PeakActiveObjects)
            {
                statistics.PeakActiveObjects = statistics.CurrentActiveObjects;
            }

            poolModel = new PoolModel<T>(result, this);
            Logger?.LogDebug("Object retrieved successfully. Active: {Active}, Available: {Available}",
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
            if (Disposed) throw new ObjectDisposedException(nameof(ObjectPool<T>));

            var unwrapped = obj.Unwrap();
            if (!this.ActiveObjects.ContainsKey(unwrapped))
            {
                Logger?.LogWarning(PoolConstants.Messages.ObjectNotInActiveList);
                throw new NoObjectsInPoolException(PoolConstants.Messages.ObjectNotInPool);
            }

            // Validate object if configured
            if (Configuration is {ValidateOnReturn: true, ValidationFunction: not null})
            {
                if (!Configuration.ValidationFunction(unwrapped))
                {
                    Logger?.LogWarning(PoolConstants.Messages.ValidationFailed);
                    this.ActiveObjects.TryRemove(unwrapped, out _);
                    statistics.TotalObjectsReturned++;
                    statistics.CurrentActiveObjects = this.ActiveObjects.Count;
                    statistics.CurrentAvailableObjects = this.AvailableObjects.Count;
                    return;
                }
            }

            // Check if we're exceeding pool size limit
            if (this.AvailableObjects.Count >= Configuration.MaxPoolSize)
            {
                Logger?.LogDebug(PoolConstants.Messages.PoolAtMaxSize);
                this.ActiveObjects.TryRemove(unwrapped, out _);
                statistics.TotalObjectsReturned++;
                statistics.CurrentActiveObjects = this.ActiveObjects.Count;
                statistics.CurrentAvailableObjects = this.AvailableObjects.Count;
                return;
            }

            this.ActiveObjects.TryRemove(unwrapped, out _);
            this.AvailableObjects.Push(unwrapped);

            statistics.TotalObjectsReturned++;
            statistics.CurrentActiveObjects = this.ActiveObjects.Count;
            statistics.CurrentAvailableObjects = this.AvailableObjects.Count;

            Logger?.LogDebug("Object returned to pool. Active: {Active}, Available: {Available}",
                ActiveObjects.Count, AvailableObjects.Count);
        }

        /// <summary>
        /// Gets the number of available objects in the pool
        /// </summary>
        public int AvailableObjectCount => AvailableObjects.Count;

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

        /// <summary>
        /// Asynchronously get an object from the pool
        /// </summary>
        /// <param name="timeout">Maximum time to wait for an object</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A poolmodel</returns>
        public async Task<PoolModel<T>> GetObjectAsync(TimeSpan timeout = default, CancellationToken cancellationToken = default)
        {
            if (Disposed) throw new ObjectDisposedException(nameof(ObjectPool<T>));

            var effectiveTimeout = timeout == TimeSpan.Zero ? Configuration.DefaultTimeout : timeout;
            var deadline = DateTime.UtcNow.Add(effectiveTimeout);

            Logger?.LogDebug("Starting async object retrieval with timeout: {Timeout}", effectiveTimeout);

            while (!cancellationToken.IsCancellationRequested && DateTime.UtcNow < deadline)
            {
                if (TryGetObject(out var poolModel))
                {
                    Logger?.LogDebug(PoolConstants.Messages.AsyncRetrievalSuccess);
                    return poolModel!;
                }

                // Wait a short time before trying again
                await Task.Delay(PoolConstants.Thresholds.DefaultAsyncPollingDelayMs, cancellationToken);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                Logger?.LogDebug(PoolConstants.Messages.AsyncRetrievalCancelled);
                cancellationToken.ThrowIfCancellationRequested();
            }

            Logger?.LogWarning(PoolConstants.Messages.TimeoutWaitingFormat, effectiveTimeout);
            throw new TimeoutException(string.Format(PoolConstants.Messages.TimeoutWaitingFormat, effectiveTimeout));
        }

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
        /// Reset metrics counters (useful for testing or periodic resets)
        /// </summary>
        public void ResetMetrics()
        {
            Logger?.LogInformation(PoolConstants.Messages.ResettingMetrics);
            statistics = new PoolStatistics
            {
                CurrentActiveObjects = ActiveObjects.Count,
                CurrentAvailableObjects = AvailableObjects.Count,
                PeakActiveObjects = ActiveObjects.Count
            };
        }


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
                _ => "Pool metric"
            };
        }

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
                Logger?.LogInformation("Disposing ObjectPool with {Active} active objects and {Available} available objects", 
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

                Disposed = true;
            }
        }

        #endregion
    }
}
