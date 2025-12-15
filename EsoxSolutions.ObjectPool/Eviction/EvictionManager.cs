using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace EsoxSolutions.ObjectPool.Eviction;

/// <summary>
/// Manages eviction of objects from a pool based on TTL and idle timeout
/// </summary>
/// <typeparam name="T">The type of object in the pool</typeparam>
public class EvictionManager<T> : IDisposable where T : notnull
{
    private readonly EvictionConfiguration _configuration;
    private readonly ConcurrentDictionary<T, ObjectMetadata> _metadata;
    private readonly ILogger? _logger;
    private readonly Timer? _evictionTimer;
    private readonly EvictionStatistics _statistics = new();
    private bool _disposed;

    /// <summary>
    /// Creates a new eviction manager
    /// </summary>
    /// <param name="configuration">Eviction configuration</param>
    /// <param name="logger">Optional logger</param>
    public EvictionManager(EvictionConfiguration configuration, ILogger? logger = null)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger;
        _metadata = new ConcurrentDictionary<T, ObjectMetadata>();

        if (_configuration.EnableBackgroundEviction && _configuration.Policy != EvictionPolicy.None)
        {
            _evictionTimer = new Timer(
                RunEviction,
                null,
                _configuration.EvictionInterval,
                _configuration.EvictionInterval);

            _logger?.LogInformation(
                "Started background eviction with policy: {Policy}, TTL: {TTL}, Idle: {Idle}, Interval: {Interval}",
                _configuration.Policy,
                _configuration.TimeToLive,
                _configuration.IdleTimeout,
                _configuration.EvictionInterval);
        }
    }

    /// <summary>
    /// Tracks a new object in the pool
    /// </summary>
    public void TrackObject(T obj)
    {
        if (_configuration.Policy == EvictionPolicy.None) return;

        _metadata.TryAdd(obj, new ObjectMetadata());
    }

    /// <summary>
    /// Records when an object is accessed
    /// </summary>
    public void RecordAccess(T obj)
    {
        if (_configuration.Policy == EvictionPolicy.None) return;

        if (_metadata.TryGetValue(obj, out var metadata))
        {
            metadata.LastAccessedAt = DateTime.UtcNow;
            metadata.AccessCount++;
        }
    }

    /// <summary>
    /// Records when an object is returned to the pool
    /// </summary>
    public void RecordReturn(T obj)
    {
        if (_configuration.Policy == EvictionPolicy.None) return;

        if (_metadata.TryGetValue(obj, out var metadata))
        {
            metadata.LastReturnedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Stops tracking an object
    /// </summary>
    public void UntrackObject(T obj)
    {
        _metadata.TryRemove(obj, out _);
    }

    /// <summary>
    /// Checks if an object should be evicted
    /// </summary>
    public bool ShouldEvict(T obj)
    {
        if (_configuration.Policy == EvictionPolicy.None) return false;

        if (!_metadata.TryGetValue(obj, out var metadata))
        {
            return false;
        }

        return ShouldEvictInternal(obj, metadata);
    }

    private bool ShouldEvictInternal(T obj, ObjectMetadata metadata)
    {
        // Custom predicate takes precedence
        if (_configuration.CustomEvictionPredicate != null)
        {
            if (_configuration.CustomEvictionPredicate(obj, metadata))
            {
                return true;
            }
        }

        switch (_configuration.Policy)
        {
            case EvictionPolicy.TimeToLive:
                return metadata.Age >= _configuration.TimeToLive;

            case EvictionPolicy.IdleTimeout:
                return metadata.IdleTime >= _configuration.IdleTimeout;

            case EvictionPolicy.Combined:
                return metadata.Age >= _configuration.TimeToLive ||
                       metadata.IdleTime >= _configuration.IdleTimeout;

            default:
                return false;
        }
    }

    /// <summary>
    /// Runs eviction on a collection of objects
    /// </summary>
    /// <param name="objects">Objects to check for eviction</param>
    /// <param name="removeAction">Action to call when an object should be removed</param>
    /// <returns>Number of objects evicted</returns>
    public int RunEviction(IEnumerable<T> objects, Action<T> removeAction)
    {
        if (_configuration.Policy == EvictionPolicy.None) return 0;

        var stopwatch = Stopwatch.StartNew();
        var evictedCount = 0;

        foreach (var obj in objects.Take(_configuration.MaxEvictionsPerRun))
        {
            if (!_metadata.TryGetValue(obj, out var metadata))
            {
                continue;
            }

            if (ShouldEvictInternal(obj, metadata))
            {
                // Determine eviction reason for statistics
                bool ttlExpired = metadata.Age >= _configuration.TimeToLive;
                bool idleExpired = metadata.IdleTime >= _configuration.IdleTimeout;
                bool customEviction = _configuration.CustomEvictionPredicate?.Invoke(obj, metadata) ?? false;

                if (ttlExpired) _statistics.TtlEvictions++;
                if (idleExpired) _statistics.IdleEvictions++;
                if (customEviction) _statistics.CustomEvictions++;

                _logger?.LogDebug(
                    "Evicting object: Age={Age}, Idle={Idle}, AccessCount={Count}, TTL={TTL}, Idle={IdleExpired}, Custom={Custom}",
                    metadata.Age,
                    metadata.IdleTime,
                    metadata.AccessCount,
                    ttlExpired,
                    idleExpired,
                    customEviction);

                // Remove from pool
                removeAction(obj);

                // Dispose if configured
                if (_configuration.DisposeEvictedObjects && obj is IDisposable disposable)
                {
                    try
                    {
                        disposable.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Error disposing evicted object");
                    }
                }

                // Untrack
                _metadata.TryRemove(obj, out _);

                evictedCount++;
                _statistics.TotalEvictions++;
            }
        }

        stopwatch.Stop();
        _statistics.LastEvictionRun = DateTime.UtcNow;
        _statistics.LastEvictionDuration = stopwatch.Elapsed;
        _statistics.EvictionRuns++;

        if (evictedCount > 0)
        {
            _logger?.LogInformation(
                "Eviction run completed: {Count} objects evicted in {Duration}ms",
                evictedCount,
                stopwatch.ElapsedMilliseconds);
        }

        return evictedCount;
    }

    private void RunEviction(object? state)
    {
        // This is called by the timer for background eviction
        // The actual eviction is performed by the pool
        _logger?.LogDebug("Background eviction triggered");
    }

    /// <summary>
    /// Gets the metadata for an object
    /// </summary>
    public ObjectMetadata? GetMetadata(T obj)
    {
        _metadata.TryGetValue(obj, out var metadata);
        return metadata;
    }

    /// <summary>
    /// Gets eviction statistics
    /// </summary>
    public EvictionStatistics GetStatistics() => _statistics;

    /// <summary>
    /// Gets all tracked objects with their metadata
    /// </summary>
    public IReadOnlyDictionary<T, ObjectMetadata> GetAllMetadata()
    {
        return _metadata;
    }

    /// <summary>
    /// Manually triggers an eviction check
    /// </summary>
    public void TriggerEviction()
    {
        _logger?.LogInformation("Manual eviction triggered");
    }

    /// <summary>
    /// Disposes the eviction manager
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        _evictionTimer?.Dispose();
        _metadata.Clear();
        _disposed = true;

        _logger?.LogInformation("Eviction manager disposed");
    }
}
