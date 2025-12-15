using System.Collections.Concurrent;
using EsoxSolutions.ObjectPool.Interfaces;
using EsoxSolutions.ObjectPool.Models;
using Microsoft.Extensions.Logging;

namespace EsoxSolutions.ObjectPool.Scoping;

/// <summary>
/// Manages multiple object pools scoped by tenant, user, or context
/// </summary>
/// <typeparam name="T">The type of object in the pools</typeparam>
public class ScopedPoolManager<T> : IDisposable where T : class
{
    private readonly ConcurrentDictionary<PoolScope, IObjectPool<T>> _scopedPools = new();
    private readonly Func<PoolScope, IObjectPool<T>> _poolFactory;
    private readonly ScopedPoolConfiguration _configuration;
    private readonly ILogger? _logger;
    private readonly Timer? _cleanupTimer;
    private readonly ScopedPoolStatistics _statistics = new();
    private readonly ConcurrentDictionary<PoolScope, DateTime> _lastAccessTimes = new();
    private bool _disposed;

    /// <summary>
    /// Creates a new scoped pool manager
    /// </summary>
    /// <param name="poolFactory">Factory function to create pools for each scope</param>
    /// <param name="configuration">Configuration for scoped pools</param>
    /// <param name="logger">Optional logger</param>
    public ScopedPoolManager(
        Func<PoolScope, IObjectPool<T>> poolFactory,
        ScopedPoolConfiguration? configuration = null,
        ILogger? logger = null)
    {
        _poolFactory = poolFactory ?? throw new ArgumentNullException(nameof(poolFactory));
        _configuration = configuration ?? new ScopedPoolConfiguration();
        _logger = logger;

        if (_configuration.EnableAutomaticCleanup)
        {
            _cleanupTimer = new Timer(
                PerformCleanup,
                null,
                _configuration.CleanupInterval,
                _configuration.CleanupInterval);
        }

        _logger?.LogInformation("Scoped pool manager initialized with strategy: {Strategy}", 
            _configuration.ResolutionStrategy);
    }

    /// <summary>
    /// Gets the pool for the current scope
    /// </summary>
    public IObjectPool<T> GetPool()
    {
        var scope = ResolveCurrentScope();
        return GetPoolForScope(scope);
    }

    /// <summary>
    /// Gets the pool for a specific scope
    /// </summary>
    public IObjectPool<T> GetPoolForScope(PoolScope scope)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ScopedPoolManager<>));

        _lastAccessTimes[scope] = DateTime.UtcNow;

        var pool = _scopedPools.GetOrAdd(scope, s =>
        {
            _logger?.LogInformation("Creating new pool for scope: {Scope}", s);
            
            var newPool = _poolFactory(s);
            
            _statistics.TotalScopesCreated++;
            // ActiveScopes will be updated after GetOrAdd completes
            
            _configuration.OnScopeCreated?.Invoke(s);

            // Check if we're exceeding max scopes
            if (_scopedPools.Count > _configuration.MaxScopes)
            {
                _logger?.LogWarning("Exceeded max scopes ({Max}), triggering cleanup", 
                    _configuration.MaxScopes);
                Task.Run(() => PerformCleanup(null));
            }

            return newPool;
        });

        // Update statistics after pool is added
        _statistics.ActiveScopes = _scopedPools.Count;
        if (_statistics.ActiveScopes > _statistics.PeakScopes)
        {
            _statistics.PeakScopes = _statistics.ActiveScopes;
        }

        // Track access
        _statistics.ScopeAccessCounts.TryAdd(scope.Id, 0);
        _statistics.ScopeAccessCounts[scope.Id]++;

        return pool;
    }

    /// <summary>
    /// Gets an object from the current scope's pool
    /// </summary>
    public PoolModel<T> GetObject()
    {
        var pool = GetPool();
        return pool.GetObject();
    }

    /// <summary>
    /// Gets an object from a specific scope's pool
    /// </summary>
    public PoolModel<T> GetObjectForScope(PoolScope scope)
    {
        var pool = GetPoolForScope(scope);
        return pool.GetObject();
    }

    /// <summary>
    /// Resolves the current scope based on configuration
    /// </summary>
    private PoolScope ResolveCurrentScope()
    {
        return _configuration.ResolutionStrategy switch
        {
            ScopeResolutionStrategy.Ambient => ResolveAmbientScope(),
            ScopeResolutionStrategy.Custom => ResolveCustomScope(),
            ScopeResolutionStrategy.DependencyInjection => ResolveDIScope(),
            ScopeResolutionStrategy.HttpContext => ResolveHttpContextScope(),
            _ => throw new NotSupportedException($"Resolution strategy {_configuration.ResolutionStrategy} is not supported")
        };
    }

    private PoolScope ResolveAmbientScope()
    {
        var scope = AmbientPoolScope.Current;
        if (scope == null)
        {
            _logger?.LogWarning("No ambient scope found, creating default scope");
            scope = new PoolScope("default");
        }
        return scope;
    }

    private PoolScope ResolveCustomScope()
    {
        if (_configuration.CustomScopeResolver == null)
        {
            throw new InvalidOperationException("Custom scope resolver not configured");
        }
        return _configuration.CustomScopeResolver();
    }

    private PoolScope ResolveDIScope()
    {
        // This would typically use IHttpContextAccessor or similar
        // For now, fall back to ambient
        return ResolveAmbientScope();
    }

    private PoolScope ResolveHttpContextScope()
    {
        // This would typically use IHttpContextAccessor
        // For now, fall back to ambient
        return ResolveAmbientScope();
    }

    /// <summary>
    /// Performs cleanup of inactive scopes
    /// </summary>
    private void PerformCleanup(object? state)
    {
        if (_disposed) return;

        try
        {
            var now = DateTime.UtcNow;
            var scopesToRemove = new List<PoolScope>();

            foreach (var kvp in _lastAccessTimes)
            {
                var idle = now - kvp.Value;
                if (idle >= _configuration.ScopeIdleTimeout)
                {
                    scopesToRemove.Add(kvp.Key);
                }
            }

            foreach (var scope in scopesToRemove)
            {
                if (_scopedPools.TryRemove(scope, out var pool))
                {
                    _logger?.LogInformation("Cleaning up inactive scope: {Scope}", scope);

                    if (_configuration.DisposePoolsOnCleanup && pool is IDisposable disposable)
                    {
                        try
                        {
                            disposable.Dispose();
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "Error disposing pool for scope: {Scope}", scope);
                        }
                    }

                    _lastAccessTimes.TryRemove(scope, out _);
                    _statistics.ScopesCleanedUp++;
                    _statistics.ActiveScopes = _scopedPools.Count;

                    _configuration.OnScopeDisposed?.Invoke(scope);
                }
            }

            _statistics.LastCleanup = now;

            if (scopesToRemove.Count > 0)
            {
                _logger?.LogInformation("Cleaned up {Count} inactive scopes", scopesToRemove.Count);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during scope cleanup");
        }
    }

    /// <summary>
    /// Manually removes a specific scope
    /// </summary>
    public bool RemoveScope(PoolScope scope)
    {
        if (_scopedPools.TryRemove(scope, out var pool))
        {
            _logger?.LogInformation("Manually removing scope: {Scope}", scope);

            if (_configuration.DisposePoolsOnCleanup && pool is IDisposable disposable)
            {
                disposable.Dispose();
            }

            _lastAccessTimes.TryRemove(scope, out _);
            _statistics.ScopesCleanedUp++;
            _statistics.ActiveScopes = _scopedPools.Count;

            _configuration.OnScopeDisposed?.Invoke(scope);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Gets all active scopes
    /// </summary>
    public IEnumerable<PoolScope> GetActiveScopes()
    {
        return _scopedPools.Keys.ToList();
    }

    /// <summary>
    /// Gets statistics for all scopes
    /// </summary>
    public ScopedPoolStatistics GetStatistics()
    {
        _statistics.ActiveScopes = _scopedPools.Count;

        // Calculate average objects per scope
        long totalObjects = 0;
        foreach (var pool in _scopedPools.Values)
        {
            if (pool is IPoolMetrics metricsPool)
            {
                var metrics = metricsPool.ExportMetrics();
                if (metrics.TryGetValue("objectpool_active_current", out var active))
                {
                    totalObjects += Convert.ToInt64(active);
                }
                if (metrics.TryGetValue("objectpool_available_current", out var available))
                {
                    totalObjects += Convert.ToInt64(available);
                }
            }
        }

        _statistics.TotalObjects = totalObjects;
        _statistics.AverageObjectsPerScope = _scopedPools.Count > 0
            ? (double)totalObjects / _scopedPools.Count
            : 0;

        return _statistics;
    }

    /// <summary>
    /// Gets statistics for a specific scope
    /// </summary>
    public Dictionary<string, object>? GetScopeStatistics(PoolScope scope)
    {
        if (_scopedPools.TryGetValue(scope, out var pool) && pool is IPoolMetrics metricsPool)
        {
            var stats = metricsPool.ExportMetrics();
            stats["scope_id"] = scope.Id;
            if (scope.TenantId != null) stats["tenant_id"] = scope.TenantId;
            if (scope.UserId != null) stats["user_id"] = scope.UserId;
            if (_lastAccessTimes.TryGetValue(scope, out var lastAccess))
            {
                stats["last_access"] = lastAccess;
                stats["idle_seconds"] = (DateTime.UtcNow - lastAccess).TotalSeconds;
            }
            return stats;
        }

        return null;
    }

    /// <summary>
    /// Manually triggers cleanup
    /// </summary>
    public void TriggerCleanup()
    {
        PerformCleanup(null);
    }

    /// <summary>
    /// Disposes all pools and resources
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        _cleanupTimer?.Dispose();

        foreach (var kvp in _scopedPools)
        {
            if (kvp.Value is IDisposable disposable)
            {
                try
                {
                    disposable.Dispose();
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error disposing pool for scope: {Scope}", kvp.Key);
                }
            }
        }

        _scopedPools.Clear();
        _lastAccessTimes.Clear();
        _disposed = true;

        _logger?.LogInformation("Scoped pool manager disposed");
    }
}
