using System.Diagnostics.Metrics;
using EsoxSolutions.ObjectPool.Interfaces;

namespace EsoxSolutions.ObjectPool.Telemetry;

/// <summary>
/// OpenTelemetry metrics provider for object pools
/// </summary>
/// <typeparam name="T">The type of object in the pool</typeparam>
public sealed class ObjectPoolMeter<T> : IDisposable where T : class
{
    private readonly Meter _meter;
    private readonly IObjectPool<T> _pool;
    private readonly string _poolName;
    
    private ObservableGauge<int>? _activeObjectsGauge;
    private ObservableGauge<int>? _availableObjectsGauge;
    private ObservableGauge<double>? _utilizationGauge;
    private ObservableGauge<int>? _healthStatusGauge;
    private Counter<long>? _retrievedCounter;
    private Counter<long>? _returnedCounter;
    private Counter<long>? _emptyEventsCounter;
    private Histogram<double>? _operationDurationHistogram;
    private bool _disposed;

    /// <summary>
    /// Creates a new OpenTelemetry meter for the object pool
    /// </summary>
    /// <param name="pool">The object pool to monitor</param>
    /// <param name="meterName">The meter name (default: "EsoxSolutions.ObjectPool")</param>
    /// <param name="poolName">Optional pool name for tagging</param>
    /// <param name="version">Meter version</param>
    public ObjectPoolMeter(
        IObjectPool<T> pool, 
        string? meterName = null,
        string? poolName = null,
        string? version = null)
    {
        ArgumentNullException.ThrowIfNull(pool);
        
        _pool = pool;
        _poolName = poolName ?? typeof(T).Name;
        _meter = new Meter(
            meterName ?? "EsoxSolutions.ObjectPool", 
            version ?? "3.1.0");
        
        InitializeMetrics();
    }

    private void InitializeMetrics()
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new("pool.name", _poolName),
            new("pool.type", typeof(T).Name)
        };

        // Gauges for current state
        _activeObjectsGauge = _meter.CreateObservableGauge(
            "objectpool.objects.active",
            () => new Measurement<int>(
                GetActiveCount(),
                tags),
            unit: "{objects}",
            description: "Number of objects currently checked out from the pool");

        _availableObjectsGauge = _meter.CreateObservableGauge(
            "objectpool.objects.available",
            () => new Measurement<int>(
                _pool.AvailableObjectCount,
                tags),
            unit: "{objects}",
            description: "Number of objects available in the pool");

        _utilizationGauge = _meter.CreateObservableGauge(
            "objectpool.utilization",
            () => new Measurement<double>(
                GetUtilizationPercentage() / 100.0,
                tags),
            unit: "1",
            description: "Pool utilization as a ratio (0.0 to 1.0)");

        _healthStatusGauge = _meter.CreateObservableGauge(
            "objectpool.health.status",
            () => new Measurement<int>(
                GetHealthStatus(),
                tags),
            unit: "1",
            description: "Pool health status (1=healthy, 0=unhealthy)");

        // Counters for operations
        _retrievedCounter = _meter.CreateCounter<long>(
            "objectpool.objects.retrieved",
            unit: "{objects}",
            description: "Total number of objects retrieved from the pool");

        _returnedCounter = _meter.CreateCounter<long>(
            "objectpool.objects.returned",
            unit: "{objects}",
            description: "Total number of objects returned to the pool");

        _emptyEventsCounter = _meter.CreateCounter<long>(
            "objectpool.events.empty",
            unit: "{events}",
            description: "Total number of times the pool was empty when requested");

        // Histogram for operation duration
        _operationDurationHistogram = _meter.CreateHistogram<double>(
            "objectpool.operation.duration",
            unit: "ms",
            description: "Duration of pool operations");
    }

    /// <summary>
    /// Records a retrieval operation
    /// </summary>
    /// <param name="success">Whether the operation succeeded</param>
    /// <param name="durationMs">Operation duration in milliseconds</param>
    public void RecordRetrieval(bool success = true, double? durationMs = null)
    {
        if (_disposed) return;

        _retrievedCounter?.Add(1, 
            new KeyValuePair<string, object?>("pool.name", _poolName),
            new KeyValuePair<string, object?>("success", success));

        if (durationMs.HasValue)
        {
            _operationDurationHistogram?.Record(durationMs.Value,
                new KeyValuePair<string, object?>("pool.name", _poolName),
                new KeyValuePair<string, object?>("operation", "retrieve"));
        }
    }

    /// <summary>
    /// Records a return operation
    /// </summary>
    /// <param name="success">Whether the operation succeeded</param>
    /// <param name="durationMs">Operation duration in milliseconds</param>
    public void RecordReturn(bool success = true, double? durationMs = null)
    {
        if (_disposed) return;

        _returnedCounter?.Add(1,
            new KeyValuePair<string, object?>("pool.name", _poolName),
            new KeyValuePair<string, object?>("success", success));

        if (durationMs.HasValue)
        {
            _operationDurationHistogram?.Record(durationMs.Value,
                new KeyValuePair<string, object?>("pool.name", _poolName),
                new KeyValuePair<string, object?>("operation", "return"));
        }
    }

    /// <summary>
    /// Records a pool empty event
    /// </summary>
    public void RecordEmptyEvent()
    {
        if (_disposed) return;

        _emptyEventsCounter?.Add(1,
            new KeyValuePair<string, object?>("pool.name", _poolName));
    }

    private int GetActiveCount()
    {
        if (_pool is IPoolMetrics poolMetrics)
        {
            var metrics = poolMetrics.ExportMetrics();
            if (metrics.TryGetValue("pool_objects_active_current", out var active))
            {
                return Convert.ToInt32(active);
            }
        }
        return 0;
    }

    private double GetUtilizationPercentage()
    {
        if (_pool is IPoolHealth poolHealth)
        {
            return poolHealth.UtilizationPercentage;
        }
        return 0.0;
    }

    private int GetHealthStatus()
    {
        if (_pool is IPoolHealth poolHealth)
        {
            return poolHealth.IsHealthy ? 1 : 0;
        }
        return 1; // Assume healthy if not implemented
    }

    /// <summary>
    /// Disposes the meter and releases resources
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        _meter?.Dispose();
        _disposed = true;
    }
}
