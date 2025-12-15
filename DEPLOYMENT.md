# Production Deployment Guide - Version 4.0.0

## Prerequisites

### Framework Requirements
- .NET 8.0, .NET 9.0 or .NET 10.0
- Microsoft.Extensions.Logging.Abstractions 8.0.0+
- Microsoft.Extensions.DependencyInjection.Abstractions 8.0.0+
- Microsoft.Extensions.Diagnostics.HealthChecks 8.0.0+ (for health checks)
- System.Diagnostics.DiagnosticSource 8.0.0+ (for OpenTelemetry)

### Recommended Monitoring Stack
- Application Performance Monitoring (APM) tool
- Structured logging system (Serilog, NLog, etc.)
- Metrics collection (Prometheus, OpenTelemetry, Application Insights)
- Health check endpoint monitoring

## Configuration

### Basic Production Configuration
```csharp
services.AddDynamicObjectPool<MyResource>(
    sp => CreateResource(),
    config =>
    {
        config.MaxPoolSize = Environment.ProcessorCount * 25;
        config.MaxActiveObjects = Environment.ProcessorCount * 10;
        config.DefaultTimeout = TimeSpan.FromSeconds(30);
        config.ValidateOnReturn = true;
        config.ValidationFunction = ValidateObject;
    })
    .WithAutoWarmup(50) // Pre-populate pool
    .WithEviction(
        timeToLive: TimeSpan.FromHours(4),
        idleTimeout: TimeSpan.FromMinutes(30))
    .WithCircuitBreaker(
        failureThreshold: 5,
        openDuration: TimeSpan.FromSeconds(30));
```

### High-Load Configuration
```csharp
services.AddDynamicObjectPool<MyResource>(
    sp => CreateResource(),
    config =>
    {
        config.MaxPoolSize = 1000;
        config.MaxActiveObjects = 500;
        config.DefaultTimeout = TimeSpan.FromSeconds(5);
        config.ValidateOnReturn = false; // Disable for performance
    })
    .WithAutoWarmupPercentage(80) // Pre-populate 80% capacity
    .WithIdleTimeout(TimeSpan.FromMinutes(15));
```

### Multi-Tenant Configuration
```csharp
services.AddScopedObjectPool<TenantResource>(
    (sp, scope) => CreateResourceForTenant(scope.TenantId),
    config =>
    {
        config.MaxPoolSize = 100;
        config.MaxActiveObjects = 50;
    });
```

## Monitoring Setup

### Health Checks Integration (v4.0)
```csharp
// In ASP.NET Core Startup/Program.cs
services.AddHealthChecks()
    .AddObjectPoolHealthCheck<HttpClient>("http-client-pool", 
        warningUtilization: 70.0,
        criticalUtilization: 90.0)
    .AddObjectPoolHealthCheck<DbConnection>("database-pool",
        warningUtilization: 80.0,
        criticalUtilization: 95.0);

// Configure health check endpoint
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});
```

### OpenTelemetry Metrics Integration (v4.0)
```csharp
// Register OpenTelemetry metrics
services.AddObjectPoolMetrics<HttpClient>("http-client-pool");
services.AddObjectPoolMetrics<DbConnection>("database-pool");

services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics
        .AddMeter("EsoxSolutions.ObjectPool")
        .AddPrometheusExporter()
        .AddOtlpExporter());

// Configure Prometheus endpoint
app.MapPrometheusScrapingEndpoint();
```

**Available Metrics:**
- `objectpool.objects.active` - Current active objects (gauge)
- `objectpool.objects.available` - Current available objects (gauge)
- `objectpool.utilization` - Pool utilization ratio 0.0-1.0 (gauge)
- `objectpool.health.status` - Health status 1=healthy, 0=unhealthy (gauge)
- `objectpool.objects.retrieved` - Total objects retrieved (counter)
- `objectpool.objects.returned` - Total objects returned (counter)
- `objectpool.events.empty` - Pool empty events (counter)
- `objectpool.operation.duration` - Operation duration (histogram)

### Legacy Prometheus Exporter
You can still export pool metrics in Prometheus exposition format using the built-in helper:

```csharp
// Use the interface default/extension
var prometheusText = ((IPoolMetrics)pool).ExportMetricsPrometheus();

// Or use the convenience method on concrete pools
var prometheusText = pool.ExportMetricsPrometheus();

// With optional tags as labels
var tags = new Dictionary<string, string> { 
    ["service"] = "order-service", 
    ["env"] = "prod" 
};
var prometheusText = pool.ExportMetricsPrometheus(tags);
```

### Lifecycle Hooks for Monitoring (v4.0)
```csharp
services.AddDynamicObjectPool<MyResource>(
    sp => CreateResource(),
    config => config.MaxPoolSize = 100)
    .WithLifecycleHooks(hooks =>
    {
        hooks.OnCreate = resource =>
        {
            logger.LogInformation("Resource created: {Id}", resource.Id);
            metrics.IncrementCounter("resource_created_total");
        };
        
        hooks.OnAcquire = resource =>
        {
            metrics.RecordGauge("active_resources", pool.Statistics.CurrentActiveObjects);
        };
        
        hooks.OnReturn = resource =>
        {
            var duration = DateTime.UtcNow - resource.AcquiredAt;
            metrics.RecordHistogram("resource_usage_duration", duration.TotalSeconds);
        };
        
        hooks.OnEvict = (resource, reason) =>
        {
            logger.LogWarning("Resource evicted: {Id}, Reason: {Reason}", 
                resource.Id, reason);
        };
    });
```

## Performance Tuning

### Capacity Planning
- **MaxPoolSize**: Set to 2-3x expected peak concurrent usage
- **MaxActiveObjects**: Set to expected peak concurrent usage
- **Warm-up**: Pre-populate 50-75% of capacity for instant availability
- **Eviction**: Configure TTL based on resource refresh requirements
- **Circuit Breaker**: Set thresholds based on acceptable failure rates

### Memory Management
- Monitor pool statistics for optimal sizing
- Use `Statistics.PeakActiveObjects` for capacity planning
- Monitor `Statistics.PoolEmptyCount` for undersizing detection
- Use eviction to prevent memory leaks from stale objects

### Warm-up Strategies
```csharp
// Quick startup - fixed number
.WithAutoWarmup(50)

// Percentage-based - scales with MaxPoolSize
.WithAutoWarmupPercentage(75)

// Manual warm-up for complex scenarios
var warmer = sp.GetRequiredService<IObjectPoolWarmer<MyResource>>();
await warmer.WarmUpAsync(targetSize: 100, cancellationToken);
```

## Kubernetes Deployment

### Health Check Configuration
```yaml
apiVersion: v1
kind: Pod
metadata:
  name: myapp
spec:
  containers:
  - name: myapp
    image: myapp:latest
    livenessProbe:
      httpGet:
        path: /health
        port: 8080
      initialDelaySeconds: 30
      periodSeconds: 10
    readinessProbe:
      httpGet:
        path: /health/ready
        port: 8080
      initialDelaySeconds: 5
      periodSeconds: 5
```

### Prometheus Integration
```yaml
apiVersion: v1
kind: Service
metadata:
  name: myapp
  annotations:
    prometheus.io/scrape: "true"
    prometheus.io/port: "8080"
    prometheus.io/path: "/metrics"
spec:
  ports:
  - port: 8080
    targetPort: 8080
```

## Circuit Breaker Configuration (v4.0)

### Basic Circuit Breaker
```csharp
.WithCircuitBreaker(
    failureThreshold: 5,           // Open after 5 failures
    openDuration: TimeSpan.FromSeconds(30), // Stay open for 30s
    halfOpenAttempts: 3)          // Test with 3 attempts
```

### Advanced Circuit Breaker with Custom Policy
```csharp
.WithCircuitBreaker(config =>
{
    config.FailureThreshold = 10;
    config.OpenDuration = TimeSpan.FromMinutes(1);
    config.HalfOpenAttempts = 5;
    config.OnCircuitOpen = () => 
        logger.LogError("Circuit breaker opened!");
    config.OnCircuitClose = () => 
        logger.LogInformation("Circuit breaker closed");
});
```

## Eviction Configuration (v4.0)

### Time-to-Live (TTL)
```csharp
.WithEviction(
    timeToLive: TimeSpan.FromHours(2),  // Max object lifetime
    enableBackgroundEviction: true)     // Periodic cleanup
```

### Idle Timeout
```csharp
.WithIdleTimeout(TimeSpan.FromMinutes(15)) // Evict after 15 min idle
```

### Custom Eviction Policy
```csharp
.WithCustomEviction(
    predicate: (obj, metadata) => 
        metadata.AccessCount > 1000 || 
        obj.IsStale(),
    checkInterval: TimeSpan.FromMinutes(5))
```

## Scoped Pools for Multi-Tenancy (v4.0)

### Tenant-Based Pools
```csharp
services.AddTenantScopedObjectPool<DbConnection>(
    (sp, tenantId) => CreateConnectionForTenant(tenantId));

// In your service
public class TenantService
{
    private readonly ScopedPoolManager<DbConnection> _poolManager;
    
    public TenantService(ScopedPoolManager<DbConnection> poolManager)
    {
        _poolManager = poolManager;
    }
    
    public async Task ProcessAsync(string tenantId)
    {
        var scope = PoolScope.FromTenant(tenantId);
        using var connection = _poolManager.GetObjectForScope(scope);
        // Connection is from tenant-specific pool
    }
}
```

### Ambient Scope
```csharp
// Set ambient scope
using (AmbientPoolScope.BeginScope(PoolScope.FromTenant("tenant1")))
{
    // All pool operations use tenant1's pool
    using var resource = poolManager.GetObject();
}
```

## Troubleshooting

### Common Issues

#### 1. Pool Exhaustion
**Symptoms**: Frequent `TimeoutException` or `NoObjectsInPoolException`
**Solutions**:
- Increase `MaxActiveObjects` or `MaxPoolSize`
- Implement warm-up to pre-populate pool
- Check for objects not being returned (memory leaks)
- Monitor eviction statistics

#### 2. High Latency
**Symptoms**: Slow `GetObject()` or `GetObjectAsync()` calls
**Solutions**:
- Reduce `DefaultTimeout`
- Increase pool size
- Use warm-up to eliminate cold-start
- Check circuit breaker status

#### 3. Memory Leaks
**Symptoms**: Increasing memory usage over time
**Solutions**:
- Ensure objects implement `IDisposable` correctly
- Configure eviction policies (TTL/idle timeout)
- Monitor eviction statistics
- Check lifecycle hooks for proper cleanup

#### 4. Validation Errors
**Symptoms**: Objects rejected on return
**Solutions**:
- Check `ValidationFunction` logic
- Use `OnValidationFailed` lifecycle hook for diagnostics
- Monitor validation failure metrics

#### 5. Circuit Breaker Tripping
**Symptoms**: `CircuitBreakerOpenException` thrown frequently
**Solutions**:
- Review circuit breaker thresholds
- Check underlying resource health
- Monitor circuit breaker statistics
- Adjust `FailureThreshold` and `OpenDuration`

### Diagnostic Commands

```csharp
// Check current pool state
var stats = pool.Statistics;
logger.LogInformation(
    "Pool Stats: Active={Active}, Available={Available}, Peak={Peak}, EmptyCount={Empty}", 
    stats.CurrentActiveObjects, 
    stats.CurrentAvailableObjects, 
    stats.PeakActiveObjects,
    stats.PoolEmptyCount);

// Get eviction statistics (v4.0)
if (pool is DynamicObjectPool<T> dynamicPool)
{
    var evictionStats = dynamicPool.GetEvictionStatistics();
    if (evictionStats != null)
    {
        logger.LogInformation(
            "Eviction Stats: Evicted={Evicted}, Active={Active}", 
            evictionStats.TotalEvicted,
            evictionStats.ActiveObjects);
    }
}

// Get circuit breaker statistics (v4.0)
var cbStats = dynamicPool.GetCircuitBreakerStatistics();
if (cbStats != null)
{
    logger.LogInformation(
        "Circuit Breaker: State={State}, Failures={Failures}", 
        cbStats.State,
        cbStats.FailureCount);
}

// Get lifecycle hook statistics (v4.0)
var lifecycleStats = dynamicPool.GetLifecycleHookStatistics();
if (lifecycleStats != null)
{
    logger.LogInformation(
        "Lifecycle: Creates={Creates}, Acquires={Acquires}, Returns={Returns}", 
        lifecycleStats.CreateCalls,
        lifecycleStats.AcquireCalls,
        lifecycleStats.ReturnCalls);
}

// Get scoped pool statistics (v4.0)
if (poolManager is ScopedPoolManager<T> scopedManager)
{
    var scopedStats = scopedManager.GetStatistics();
    logger.LogInformation(
        "Scoped Pools: Active={Active}, Total={Total}, Peak={Peak}", 
        scopedStats.ActiveScopes,
        scopedStats.TotalScopesCreated,
        scopedStats.PeakScopes);
}

// Export all metrics
var metrics = pool.ExportMetrics();
logger.LogInformation("Pool Metrics: {@Metrics}", metrics);
```

### Health Check Endpoint Monitoring

Monitor the `/health` endpoint for pool status:
```bash
curl http://localhost:8080/health
```

Expected response:
```json
{
  "status": "Healthy",
  "duration": "00:00:00.0123456",
  "entries": {
    "http-client-pool": {
      "data": {
        "utilization_percentage": 45.5,
        "available_objects": 55,
        "active_objects": 45
      },
      "status": "Healthy"
    }
  }
}
```

### Prometheus Metrics Queries

Useful Prometheus queries for monitoring:

```promql
# Pool utilization
objectpool_utilization{pool_name="http-client-pool"}

# Objects retrieved rate
rate(objectpool_objects_retrieved_total[5m])

# Pool empty events
rate(objectpool_events_empty_total[5m])

# Operation duration 95th percentile
histogram_quantile(0.95, objectpool_operation_duration_bucket)

# Health status (should be 1)
objectpool_health_status{pool_name="database-pool"}
```

## Best Practices

1. **Always use dependency injection** for production deployments
2. **Enable health checks** for monitoring and alerting
3. **Configure warm-up** to eliminate cold-start latency
4. **Set appropriate eviction policies** to prevent resource exhaustion
5. **Use circuit breakers** for external resource pools
6. **Implement lifecycle hooks** for custom monitoring and cleanup
7. **Monitor OpenTelemetry metrics** in your observability platform
8. **Use scoped pools** for multi-tenant applications
9. **Test capacity planning** under expected load
10. **Configure proper timeouts** based on SLAs

## Version

This deployment guide is for **EsoxSolutions.ObjectPool v4.0.0**