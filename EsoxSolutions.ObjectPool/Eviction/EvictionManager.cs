using System.Collections.Concurrent;
using System.Diagnostics;
using EsoxSolutions.ObjectPool.Infrastructure;
using EsoxSolutions.ObjectPool.Lifecycle;
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
    private readonly EvictionStatistics _statistics = new();
    private readonly object _statsLock = new();
    private volatile bool _disposed;

    /// <summary>
    /// Creates a new eviction manager
    /// </summary>
    /// <param name="configuration">Eviction configuration</param>
    /// <param name="logger">Optional logger</param>
    public EvictionManager(EvictionConfiguration configuration, ILogger? logger = null)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger;
        _metadata = new ConcurrentDictionary<T, ObjectMetadata>(ReferenceOrValueEqualityComparer<T>.Instance);
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
            metadata.RecordAccess(DateTime.UtcNow);
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
            metadata.RecordReturn(DateTime.UtcNow);
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
        return TryGetEvictionReason(obj, out _);
    }

    /// <summary>
    /// Checks if an object should be evicted and outputs the reason
    /// </summary>
    internal bool TryGetEvictionReason(T obj, out EvictionReason reason)
    {
        reason = EvictionReason.TimeToLive;

        if (_configuration.Policy == EvictionPolicy.None) return false;

        if (!_metadata.TryGetValue(obj, out var metadata))
        {
            return false;
        }

        return TryGetEvictionReasonInternal(obj, metadata, out reason, out _, out _, out _);
    }

    private bool TryGetEvictionReasonInternal(
        T obj,
        ObjectMetadata metadata,
        out EvictionReason primaryReason,
        out bool ttlExpired,
        out bool idleExpired,
        out bool customEviction)
    {
        primaryReason = EvictionReason.TimeToLive;
        customEviction = _configuration.CustomEvictionPredicate?.Invoke(obj, metadata) ?? false;
        ttlExpired = (_configuration.Policy is EvictionPolicy.TimeToLive or EvictionPolicy.Combined) &&
                     metadata.Age >= _configuration.TimeToLive;
        idleExpired = (_configuration.Policy is EvictionPolicy.IdleTimeout or EvictionPolicy.Combined) &&
                      metadata.IdleTime >= _configuration.IdleTimeout;

        if (customEviction)
        {
            primaryReason = EvictionReason.CustomPredicate;
            return true;
        }

        switch (_configuration.Policy)
        {
            case EvictionPolicy.TimeToLive when ttlExpired:
                primaryReason = EvictionReason.TimeToLive;
                return true;

            case EvictionPolicy.IdleTimeout when idleExpired:
                primaryReason = EvictionReason.IdleTimeout;
                return true;

            case EvictionPolicy.Combined when ttlExpired || idleExpired:
                primaryReason = ttlExpired ? EvictionReason.TimeToLive : EvictionReason.IdleTimeout;
                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// Records an inline eviction that occurred during object acquisition
    /// </summary>
    internal void RecordInlineEviction(T obj, EvictionReason reason)
    {
        lock (_statsLock)
        {
            switch (reason)
            {
                case EvictionReason.TimeToLive:
                    _statistics.TtlEvictions++;
                    break;
                case EvictionReason.IdleTimeout:
                    _statistics.IdleEvictions++;
                    break;
                case EvictionReason.CustomPredicate:
                    _statistics.CustomEvictions++;
                    break;
            }

            _statistics.TotalEvictions++;
        }

        _metadata.TryRemove(obj, out _);
    }

    /// <summary>
    /// Runs eviction on a collection of objects
    /// </summary>
    /// <param name="objects">Objects to check for eviction</param>
    /// <param name="removeAction">Action to call when an object should be removed</param>
    /// <returns>Number of objects evicted</returns>
    public int RunEviction(IEnumerable<T> objects, Action<T> removeAction)
    {
        ArgumentNullException.ThrowIfNull(removeAction);
        return RunEviction(objects, (obj, _) =>
        {
            removeAction(obj);
            return true;
        });
    }

    /// <summary>
    /// Runs eviction on a collection of objects using a conditional removal callback
    /// </summary>
    internal int RunEviction(
        IEnumerable<T> objects,
        Func<T, EvictionReason, bool> tryRemoveFunc,
        Action<T>? beforeDisposeAction = null)
    {
        if (_configuration.Policy == EvictionPolicy.None) return 0;

        var stopwatch = Stopwatch.StartNew();
        var evictedCount = 0;

        foreach (var obj in objects)
        {
            if (evictedCount >= _configuration.MaxEvictionsPerRun)
            {
                break;
            }

            if (!_metadata.TryGetValue(obj, out var metadata))
            {
                continue;
            }

            if (TryGetEvictionReasonInternal(obj, metadata, out var reason, out var ttlExpired, out var idleExpired, out var customEviction))
            {
                _logger?.LogDebug(
                    "Evicting object: Age={Age}, Idle={Idle}, AccessCount={Count}, TTL={TTL}, Idle={IdleExpired}, Custom={Custom}",
                    metadata.Age,
                    metadata.IdleTime,
                    metadata.AccessCount,
                    ttlExpired,
                    idleExpired,
                    customEviction);

                // Only count and dispose if the pool confirmed the object was removed from the available collection
                if (!tryRemoveFunc(obj, reason))
                {
                    continue;
                }

                // Dispose if configured
                if (_configuration.DisposeEvictedObjects && obj is IDisposable disposable)
                {
                    try
                    {
                        beforeDisposeAction?.Invoke(obj);
                        disposable.Dispose();
                    }
                    catch (Exception ex) when (ex is not OutOfMemoryException)
                    {
                        _logger?.LogError(ex, "Error disposing evicted object");
                    }
                }

                // Untrack
                _metadata.TryRemove(obj, out _);

                evictedCount++;
                lock (_statsLock)
                {
                    if (ttlExpired) _statistics.TtlEvictions++;
                    if (idleExpired) _statistics.IdleEvictions++;
                    if (customEviction) _statistics.CustomEvictions++;
                    _statistics.TotalEvictions++;
                }
            }
        }

        stopwatch.Stop();
        lock (_statsLock)
        {
            _statistics.LastEvictionRun = DateTime.UtcNow;
            _statistics.LastEvictionDuration = stopwatch.Elapsed;
            _statistics.EvictionRuns++;
        }

        if (evictedCount > 0)
        {
            _logger?.LogInformation(
                "Eviction run completed: {Count} objects evicted in {Duration}ms",
                evictedCount,
                stopwatch.ElapsedMilliseconds);
        }

        return evictedCount;
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
    public EvictionStatistics GetStatistics()
    {
        lock (_statsLock)
        {
            return _statistics;
        }
    }

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

        _metadata.Clear();
        _disposed = true;

        _logger?.LogInformation("Eviction manager disposed");
    }
}
