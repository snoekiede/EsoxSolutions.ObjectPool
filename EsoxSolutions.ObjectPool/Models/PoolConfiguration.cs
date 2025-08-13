namespace EsoxSolutions.ObjectPool.Models
{
    /// <summary>
    /// Configuration options for object pools
    /// </summary>
    public class PoolConfiguration
    {
        /// <summary>
        /// Maximum number of objects the pool can contain (default: unlimited)
        /// </summary>
        public int MaxPoolSize { get; set; } = int.MaxValue;

        /// <summary>
        /// Maximum number of objects that can be active at once (default: unlimited)
        /// </summary>
        public int MaxActiveObjects { get; set; } = int.MaxValue;

        /// <summary>
        /// Timeout for async operations (default: 30 seconds)
        /// </summary>
        public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Whether to validate objects when returned to pool
        /// </summary>
        public bool ValidateOnReturn { get; set; } = false;

        /// <summary>
        /// Optional validation function for returned objects
        /// </summary>
        public Func<object, bool>? ValidationFunction { get; set; }

        /// <summary>
        /// Whether to enable detailed statistics collection (may impact performance)
        /// </summary>
        public bool EnableDetailedStatistics { get; set; } = true;

        /// <summary>
        /// How often to clean up expired objects (default: 5 minutes)
        /// </summary>
        public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Maximum age for objects in the pool before they're considered stale
        /// </summary>
        public TimeSpan? MaxObjectAge { get; set; }
    }
}
