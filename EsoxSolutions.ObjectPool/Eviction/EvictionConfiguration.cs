namespace EsoxSolutions.ObjectPool.Eviction;

/// <summary>
/// Eviction policy for pool objects
/// </summary>
public enum EvictionPolicy
{
    /// <summary>
    /// No eviction - objects remain in pool indefinitely
    /// </summary>
    None = 0,

    /// <summary>
    /// Time-based eviction - objects expire after a specified time
    /// </summary>
    TimeToLive = 1,

    /// <summary>
    /// Idle-based eviction - objects expire after being idle for a specified time
    /// </summary>
    IdleTimeout = 2,

    /// <summary>
    /// Combined TTL and idle timeout
    /// </summary>
    Combined = 3
}

/// <summary>
/// Configuration for object eviction
/// </summary>
public class EvictionConfiguration
{
    /// <summary>
    /// Eviction policy to use
    /// </summary>
    public EvictionPolicy Policy { get; set; } = EvictionPolicy.None;

    /// <summary>
    /// Time-to-live for objects (how long they can exist in the pool)
    /// </summary>
    public TimeSpan TimeToLive { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Idle timeout (how long an object can remain unused)
    /// </summary>
    public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// How frequently to run the eviction check
    /// </summary>
    public TimeSpan EvictionInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Whether to run eviction on a background thread
    /// </summary>
    public bool EnableBackgroundEviction { get; set; } = true;

    /// <summary>
    /// Maximum number of objects to evict per run
    /// </summary>
    public int MaxEvictionsPerRun { get; set; } = int.MaxValue;

    /// <summary>
    /// Optional custom eviction predicate
    /// </summary>
    public Func<object, ObjectMetadata, bool>? CustomEvictionPredicate { get; set; }

    /// <summary>
    /// Whether to dispose evicted objects if they implement IDisposable
    /// </summary>
    public bool DisposeEvictedObjects { get; set; } = true;
}

/// <summary>
/// Metadata for tracking object lifecycle in the pool
/// </summary>
public class ObjectMetadata
{
    /// <summary>
    /// When the object was created/added to the pool
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the object was last retrieved from the pool
    /// </summary>
    public DateTime? LastAccessedAt { get; set; }

    /// <summary>
    /// When the object was last returned to the pool
    /// </summary>
    public DateTime? LastReturnedAt { get; set; }

    /// <summary>
    /// Number of times the object has been retrieved
    /// </summary>
    public int AccessCount { get; set; }

    /// <summary>
    /// Custom metadata tags
    /// </summary>
    public Dictionary<string, object> Tags { get; set; } = [];

    /// <summary>
    /// Gets the age of the object (time since creation)
    /// </summary>
    public TimeSpan Age => DateTime.UtcNow - CreatedAt;

    /// <summary>
    /// Gets the idle time (time since last access)
    /// </summary>
    public TimeSpan IdleTime => LastAccessedAt.HasValue 
        ? DateTime.UtcNow - LastAccessedAt.Value 
        : Age;

    /// <summary>
    /// Whether the object has ever been accessed
    /// </summary>
    public bool HasBeenAccessed => LastAccessedAt.HasValue;
}

/// <summary>
/// Statistics about eviction operations
/// </summary>
public class EvictionStatistics
{
    /// <summary>
    /// Total number of objects evicted
    /// </summary>
    public long TotalEvictions { get; set; }

    /// <summary>
    /// Number of evictions due to TTL expiration
    /// </summary>
    public long TtlEvictions { get; set; }

    /// <summary>
    /// Number of evictions due to idle timeout
    /// </summary>
    public long IdleEvictions { get; set; }

    /// <summary>
    /// Number of evictions due to custom predicate
    /// </summary>
    public long CustomEvictions { get; set; }

    /// <summary>
    /// When the last eviction run occurred
    /// </summary>
    public DateTime? LastEvictionRun { get; set; }

    /// <summary>
    /// Duration of the last eviction run
    /// </summary>
    public TimeSpan LastEvictionDuration { get; set; }

    /// <summary>
    /// Number of eviction runs
    /// </summary>
    public long EvictionRuns { get; set; }

    /// <summary>
    /// Average evictions per run
    /// </summary>
    public double AverageEvictionsPerRun => EvictionRuns > 0 
        ? (double)TotalEvictions / EvictionRuns 
        : 0;
}
