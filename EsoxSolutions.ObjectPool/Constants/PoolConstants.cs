namespace EsoxSolutions.ObjectPool.Constants
{
    /// <summary>
    /// Constants used throughout the ObjectPool library
    /// </summary>
    public static class PoolConstants
    {
        /// <summary>
        /// Metric names used in the object pool
        /// </summary>
        public static class Metrics
        {
            /// <summary>
            /// Total number of objects retrieved from the pool
            /// </summary>
            public const string RetrievedTotal = "pool_objects_retrieved_total";
            
            /// <summary>
            /// Total number of objects returned to the pool
            /// </summary>
            public const string ReturnedTotal = "pool_objects_returned_total";
            
            /// <summary>
            /// Current number of active objects
            /// </summary>
            public const string ActiveCurrent = "pool_objects_active_current";
            
            /// <summary>
            /// Current number of available objects in the pool
            /// </summary>
            public const string AvailableCurrent = "pool_objects_available_current";
            
            /// <summary>
            /// Peak number of active objects
            /// </summary>
            public const string ActivePeak = "pool_objects_active_peak";
            
            /// <summary>
            /// Total number of times the pool was empty when requested
            /// </summary>
            public const string EmptyEventsTotal = "pool_empty_events_total";
            
            /// <summary>
            /// Pool utilization as a percentage
            /// </summary>
            public const string UtilizationPercentage = "pool_utilization_percentage";
            
            /// <summary>
            /// Pool health status (1=healthy, 0=unhealthy)
            /// </summary>
            public const string HealthStatus = "pool_health_status";
            
            /// <summary>
            /// Maximum pool size
            /// </summary>
            public const string MaxSize = "pool_max_size";
            
            /// <summary>
            /// Maximum active objects
            /// </summary>
            public const string MaxActive = "pool_max_active";
            
            /// <summary>
            /// Pool statistics start time
            /// </summary>
            public const string StartTime = "pool_statistics_start_time";
            
            /// <summary>
            /// Pool uptime in seconds
            /// </summary>
            public const string UptimeSeconds = "pool_uptime_seconds";
            
            /// <summary>
            /// Prefix for tag metrics
            /// </summary>
            public const string TagPrefix = "tag_";
        }

        /// <summary>
        /// Health status diagnostic key names
        /// </summary>
        public static class Diagnostics
        {
            /// <summary>
            /// Total objects retrieved
            /// </summary>
            public const string TotalRetrieved = "TotalRetrieved";
            
            /// <summary>
            /// Total objects returned
            /// </summary>
            public const string TotalReturned = "TotalReturned";
            
            /// <summary>
            /// Peak active objects
            /// </summary>
            public const string PeakActive = "PeakActive";
            
            /// <summary>
            /// Pool empty events count
            /// </summary>
            public const string PoolEmptyEvents = "PoolEmptyEvents";
            
            /// <summary>
            /// Current active objects
            /// </summary>
            public const string CurrentActive = "CurrentActive";
            
            /// <summary>
            /// Current available objects
            /// </summary>
            public const string CurrentAvailable = "CurrentAvailable";
        }

        /// <summary>
        /// Message constants used throughout the library
        /// </summary>
        public static class Messages
        {
            /// <summary>
            /// Pool is empty message
            /// </summary>
            public const string PoolEmpty = "Pool is empty, no objects available";
            
            /// <summary>
            /// No objects available exception message
            /// </summary>
            public const string NoObjectsAvailable = "No objects available";
            
            /// <summary>
            /// Object not in pool exception message
            /// </summary>
            public const string ObjectNotInPool = "Object not in pool";
            
            /// <summary>
            /// Object failed validation message
            /// </summary>
            public const string ValidationFailed = "Object failed validation on return, not adding back to pool";
            
            /// <summary>
            /// Pool at maximum size message
            /// </summary>
            public const string PoolAtMaxSize = "Pool at maximum size, discarding returned object";
            
            /// <summary>
            /// Pool is healthy message
            /// </summary>
            public const string PoolHealthy = "Pool is healthy";
            
            /// <summary>
            /// Pool has warnings message format
            /// </summary>
            public const string PoolWarningsFormat = "Pool has {0} warning(s): {1}";
            
            /// <summary>
            /// High utilization warning format
            /// </summary>
            public const string HighUtilizationFormat = "High utilization: {0:F1}%";
            
            /// <summary>
            /// No available objects warning
            /// </summary>
            public const string NoAvailableObjects = "Pool has no available objects";
            
            /// <summary>
            /// Pool has been empty warning format
            /// </summary>
            public const string EmptyCountWarningFormat = "Pool has been empty {0} times";
            
            /// <summary>
            /// Maximum active objects limit reached format
            /// </summary>
            public const string MaxActiveLimitFormat = "Maximum active objects limit ({0}) reached";
            
            /// <summary>
            /// Attempted to return object not in active list
            /// </summary>
            public const string ObjectNotInActiveList = "Attempted to return object that was not in active objects list";
            
            /// <summary>
            /// Successfully retrieved object asynchronously
            /// </summary>
            public const string AsyncRetrievalSuccess = "Successfully retrieved object asynchronously";
            
            /// <summary>
            /// Async object retrieval cancelled
            /// </summary>
            public const string AsyncRetrievalCancelled = "Async object retrieval cancelled";
            
            /// <summary>
            /// Timeout waiting for object format
            /// </summary>
            public const string TimeoutWaitingFormat = "Timeout waiting for object from pool after {0}";
            
            /// <summary>
            /// Resetting pool metrics
            /// </summary>
            public const string ResettingMetrics = "Resetting pool metrics";
            
            /// <summary>
            /// Initial objects exceed maximum pool size format
            /// </summary>
            public const string InitialObjectsExceedMaxFormat = "Initial objects count ({0}) exceeds maximum pool size ({1})";
        }

        /// <summary>
        /// Metric types for Prometheus export
        /// </summary>
        public static class MetricTypes
        {
            /// <summary>
            /// Counter metric type
            /// </summary>
            public const string Counter = "counter";
            
            /// <summary>
            /// Gauge metric type
            /// </summary>
            public const string Gauge = "gauge";
        }

        /// <summary>
        /// Pool thresholds and limits
        /// </summary>
        public static class Thresholds
        {
            /// <summary>
            /// High utilization percentage threshold (for warnings)
            /// </summary>
            public const double HighUtilizationThreshold = 80.0;
            
            /// <summary>
            /// Critical utilization percentage threshold (for health check)
            /// </summary>
            public const double CriticalUtilizationThreshold = 95.0;
            
            /// <summary>
            /// Default async polling delay in milliseconds
            /// </summary>
            public const int DefaultAsyncPollingDelayMs = 10;
        }

    }
}