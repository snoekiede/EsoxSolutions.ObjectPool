namespace EsoxSolutions.ObjectPool.Models
{
    /// <summary>
    /// Statistics for an object pool. All counter mutations are thread-safe via <see cref="Interlocked"/>.
    /// </summary>
    public class PoolStatistics
    {
        private long _totalObjectsRetrieved;
        private long _totalObjectsReturned;
        private long _poolEmptyCount;
        private int _peakActiveObjects;

        /// <summary>
        /// Total number of objects retrieved from the pool
        /// </summary>
        public long TotalObjectsRetrieved => Interlocked.Read(ref _totalObjectsRetrieved);

        /// <summary>
        /// Total number of objects returned to the pool
        /// </summary>
        public long TotalObjectsReturned => Interlocked.Read(ref _totalObjectsReturned);

        /// <summary>
        /// Current number of active objects (snapshot; updated after every acquire/return)
        /// </summary>
        public int CurrentActiveObjects { get; set; }

        /// <summary>
        /// Current number of available objects (snapshot; updated after every acquire/return)
        /// </summary>
        public int CurrentAvailableObjects { get; set; }

        /// <summary>
        /// Peak number of active objects ever observed
        /// </summary>
        public int PeakActiveObjects => Volatile.Read(ref _peakActiveObjects);

        /// <summary>
        /// Number of times the pool was empty when an object was requested
        /// </summary>
        public long PoolEmptyCount => Interlocked.Read(ref _poolEmptyCount);

        /// <summary>
        /// Time when statistics collection started
        /// </summary>
        public DateTime StatisticsStartTime { get; private set; }

        /// <summary>
        /// Constructor
        /// </summary>
        public PoolStatistics()
        {
            StatisticsStartTime = DateTime.UtcNow;
        }

        // ── Thread-safe mutation helpers (internal use only) ──────────────────

        internal void IncrementRetrieved() => Interlocked.Increment(ref _totalObjectsRetrieved);

        internal void IncrementReturned() => Interlocked.Increment(ref _totalObjectsReturned);

        internal void IncrementPoolEmpty() => Interlocked.Increment(ref _poolEmptyCount);

        /// <summary>
        /// Updates <see cref="PeakActiveObjects"/> if <paramref name="current"/> exceeds the stored peak.
        /// Uses a CAS loop so the update is atomic even under concurrent calls.
        /// </summary>
        internal void UpdatePeakIfHigher(int current)
        {
            int snapshot;
            do
            {
                snapshot = _peakActiveObjects;
                if (current <= snapshot) return;
            } while (Interlocked.CompareExchange(ref _peakActiveObjects, current, snapshot) != snapshot);
        }

        /// <summary>
        /// Resets all counters atomically (used by <c>ResetMetrics</c>).
        /// </summary>
        internal void Reset(int currentActive, int currentAvailable)
        {
            Interlocked.Exchange(ref _totalObjectsRetrieved, 0);
            Interlocked.Exchange(ref _totalObjectsReturned, 0);
            Interlocked.Exchange(ref _poolEmptyCount, 0);
            Volatile.Write(ref _peakActiveObjects, currentActive);
            CurrentActiveObjects = currentActive;
            CurrentAvailableObjects = currentAvailable;
            StatisticsStartTime = DateTime.UtcNow;
        }
    }
}
