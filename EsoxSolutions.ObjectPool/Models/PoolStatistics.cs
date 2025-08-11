namespace EsoxSolutions.ObjectPool.Models
{
    /// <summary>
    /// Statistics for an object pool
    /// </summary>
    public class PoolStatistics
    {
        /// <summary>
        /// Total number of objects retrieved from the pool
        /// </summary>
        public long TotalObjectsRetrieved { get; set; }

        /// <summary>
        /// Total number of objects returned to the pool
        /// </summary>
        public long TotalObjectsReturned { get; set; }

        /// <summary>
        /// Current number of active objects
        /// </summary>
        public int CurrentActiveObjects { get; set; }

        /// <summary>
        /// Current number of available objects
        /// </summary>
        public int CurrentAvailableObjects { get; set; }

        /// <summary>
        /// Peak number of active objects
        /// </summary>
        public int PeakActiveObjects { get; set; }

        /// <summary>
        /// Number of times the pool was empty when requested
        /// </summary>
        public long PoolEmptyCount { get; set; }

        /// <summary>
        /// Time when statistics collection started
        /// </summary>
        public DateTime StatisticsStartTime { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        public PoolStatistics()
        {
            StatisticsStartTime = DateTime.UtcNow;
        }
    }
}
