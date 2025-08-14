using EsoxSolutions.ObjectPool.Exceptions;
using EsoxSolutions.ObjectPool.Interfaces;
using EsoxSolutions.ObjectPool.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

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
                throw new ArgumentException($"Initial objects count ({initialObjects.Count}) exceeds maximum pool size ({this.Configuration.MaxPoolSize})");
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

            T obj;
            Logger?.LogDebug("Attempting to get object from pool. Available: {Count}", AvailableObjects.Count);

            if (this.ActiveObjects.Count >= Configuration.MaxActiveObjects)
            {
                throw new InvalidOperationException($"Maximum active objects limit ({Configuration.MaxActiveObjects}) reached");
            }

            if (!this.AvailableObjects.TryPop(out obj!))
            {
                statistics.PoolEmptyCount++;
                Logger?.LogWarning("Pool is empty, no objects available");
                throw new NoObjectsInPoolException("No objects available");
            }
            this.ActiveObjects.TryAdd(obj, 0);

            statistics.TotalObjectsRetrieved++;
            statistics.CurrentActiveObjects = this.ActiveObjects.Count;
            statistics.CurrentAvailableObjects = this.AvailableObjects.Count;
            if (statistics.CurrentActiveObjects > statistics.PeakActiveObjects)
            {
                statistics.PeakActiveObjects = statistics.CurrentActiveObjects;
            }

            Logger?.LogDebug("Object retrieved from pool. Active: {Active}, Available: {Available}",
                ActiveObjects.Count, AvailableObjects.Count);

            return new PoolModel<T>(obj, this);
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

            if (!this.AvailableObjects.TryPop(out var obj))
            {
                statistics.PoolEmptyCount++;
                Logger?.LogDebug("Cannot get object: pool is empty");
                poolModel = null;
                return false;
            }

            this.ActiveObjects.TryAdd(obj, 0);

            statistics.TotalObjectsRetrieved++;
            statistics.CurrentActiveObjects = this.ActiveObjects.Count;
            statistics.CurrentAvailableObjects = this.AvailableObjects.Count;
            if (statistics.CurrentActiveObjects > statistics.PeakActiveObjects)
            {
                statistics.PeakActiveObjects = statistics.CurrentActiveObjects;
            }

            poolModel = new PoolModel<T>(obj, this);
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
                Logger?.LogWarning("Attempted to return object that was not in active objects list");
                throw new NoObjectsInPoolException("Object not in pool");
            }

            // Validate object if configured
            if (Configuration is {ValidateOnReturn: true, ValidationFunction: not null})
            {
                if (!Configuration.ValidationFunction(unwrapped!))
                {
                    Logger?.LogWarning("Object failed validation on return, not adding back to pool");
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
                Logger?.LogDebug("Pool at maximum size, discarding returned object");
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
        public int AvailableObjectCount {
            get
            {
                return this.AvailableObjects.Count;
            }
        }

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
                
                return hasAvailableObjects && notOverCapacity && utilizationPct < 95.0;
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
                    ["TotalRetrieved"] = statistics.TotalObjectsRetrieved,
                    ["TotalReturned"] = statistics.TotalObjectsReturned,
                    ["PeakActive"] = statistics.PeakActiveObjects,
                    ["PoolEmptyEvents"] = statistics.PoolEmptyCount,
                    ["CurrentActive"] = ActiveObjects.Count,
                    ["CurrentAvailable"] = AvailableObjects.Count
                }
            };

            // Check for warning conditions
            if (AvailableObjects.Count == 0)
            {
                status.Warnings.Add("Pool has no available objects");
                status.WarningCount++;
            }

            if (status.UtilizationPercentage > 80.0)
            {
                status.Warnings.Add($"High utilization: {status.UtilizationPercentage:F1}%");
                status.WarningCount++;
            }

            if (statistics.PoolEmptyCount > 0)
            {
                status.Warnings.Add($"Pool has been empty {statistics.PoolEmptyCount} times");
                status.WarningCount++;
            }

            status.IsHealthy = IsHealthy;
            status.HealthMessage = status.IsHealthy ? "Pool is healthy" : 
                $"Pool has {status.WarningCount} warning(s): {string.Join(", ", status.Warnings)}";

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
                    Logger?.LogDebug("Successfully retrieved object asynchronously");
                    return poolModel!;
                }

                // Wait a short time before trying again
                await Task.Delay(10, cancellationToken);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                Logger?.LogDebug("Async object retrieval cancelled");
                cancellationToken.ThrowIfCancellationRequested();
            }

            Logger?.LogWarning("Timeout waiting for object from pool after {Timeout}", effectiveTimeout);
            throw new TimeoutException($"Timeout waiting for object from pool after {effectiveTimeout}");
        }

        #region IPoolMetrics Implementation

        /// <summary>
        /// Export metrics with tags/labels for dimensional monitoring
        /// </summary>
        public Dictionary<string, object> ExportMetrics(Dictionary<string, string>? tags = null)
        {
            var metrics = new Dictionary<string, object>
            {
                ["pool_objects_retrieved_total"] = statistics.TotalObjectsRetrieved,
                ["pool_objects_returned_total"] = statistics.TotalObjectsReturned,
                ["pool_objects_active_current"] = ActiveObjects.Count,
                ["pool_objects_available_current"] = AvailableObjects.Count,
                ["pool_objects_active_peak"] = statistics.PeakActiveObjects,
                ["pool_empty_events_total"] = statistics.PoolEmptyCount,
                ["pool_utilization_percentage"] = UtilizationPercentage,
                ["pool_health_status"] = IsHealthy ? 1 : 0,
                ["pool_max_size"] = Configuration.MaxPoolSize == int.MaxValue ? -1 : Configuration.MaxPoolSize,
                ["pool_max_active"] = Configuration.MaxActiveObjects == int.MaxValue ? -1 : Configuration.MaxActiveObjects,
                ["pool_statistics_start_time"] = statistics.StatisticsStartTime,
                ["pool_uptime_seconds"] = (DateTime.UtcNow - statistics.StatisticsStartTime).TotalSeconds
            };

            if (tags != null)
            {
                foreach (var tag in tags)
                {
                    metrics[$"tag_{tag.Key}"] = tag.Value;
                }
            }

            return metrics;
        }

        /// <summary>
        /// Reset metrics counters (useful for testing or periodic resets)
        /// </summary>
        public void ResetMetrics()
        {
            Logger?.LogInformation("Resetting pool metrics");
            statistics = new PoolStatistics
            {
                CurrentActiveObjects = ActiveObjects.Count,
                CurrentAvailableObjects = AvailableObjects.Count,
                PeakActiveObjects = ActiveObjects.Count
            };
        }

        /// <summary>
        /// Get metrics in Prometheus format
        /// </summary>
        public string ExportPrometheusMetrics(string metricPrefix = "objectpool")
        {
            var metrics = ExportMetrics();
            var lines = new List<string>();

            foreach (var metric in metrics)
            {
                var name = $"{metricPrefix}_{metric.Key}";
                var value = metric.Value;
                
                // Add metric documentation
                lines.Add($"# HELP {name} {GetMetricDescription(metric.Key)}");
                lines.Add($"# TYPE {name} {GetMetricType(metric.Key)}");
                lines.Add($"{name} {value}");
                lines.Add("");
            }

            return string.Join("\n", lines);
        }

        private static string GetMetricDescription(string metricKey)
        {
            return metricKey switch
            {
                "pool_objects_retrieved_total" => "Total number of objects retrieved from the pool",
                "pool_objects_returned_total" => "Total number of objects returned to the pool",
                "pool_objects_active_current" => "Current number of active objects",
                "pool_objects_available_current" => "Current number of available objects in the pool",
                "pool_objects_active_peak" => "Peak number of active objects",
                "pool_empty_events_total" => "Total number of times the pool was empty when requested",
                "pool_utilization_percentage" => "Pool utilization as a percentage",
                "pool_health_status" => "Pool health status (1=healthy, 0=unhealthy)",
                "pool_uptime_seconds" => "Pool uptime in seconds",
                _ => "Pool metric"
            };
        }

        private static string GetMetricType(string metricKey)
        {
            return metricKey switch
            {
                "pool_objects_retrieved_total" => "counter",
                "pool_objects_returned_total" => "counter",
                "pool_empty_events_total" => "counter",
                "pool_uptime_seconds" => "counter",
                _ => "gauge"
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
