# Production Features Sample

This sample demonstrates the new production-ready features of the ObjectPool library.

## Features Demonstrated

### 1. Configuration Options
```csharp
var config = new PoolConfiguration
{
    MaxPoolSize = 10,
    MaxActiveObjects = 5,
    DefaultTimeout = TimeSpan.FromSeconds(10),
    ValidateOnReturn = true,
    ValidationFunction = obj => obj.IsValid()
};

var pool = new ObjectPool<MyClass>(initialObjects, config, logger);
```

### 2. Health Monitoring
```csharp
// Check pool health
if (!pool.IsHealthy)
{
    var status = pool.GetHealthStatus();
    logger.LogWarning("Pool unhealthy: {Message}", status.HealthMessage);
    
    // Check diagnostics
    foreach (var diagnostic in status.Diagnostics)
    {
        logger.LogInformation("{Key}: {Value}", diagnostic.Key, diagnostic.Value);
    }
}
```

### 3. Metrics Export
```csharp
// Export metrics for monitoring systems
var metrics = pool.ExportMetrics();
await metricsCollector.RecordAsync(metrics);

// Export in Prometheus format
var prometheusMetrics = pool.ExportPrometheusMetrics("myapp_objectpool");
await File.WriteAllTextAsync("/metrics/objectpool.prom", prometheusMetrics);
```

### 4. Logging Integration
```csharp
using var loggerFactory = LoggerFactory.Create(builder => 
    builder.AddConsole().SetMinimumLevel(LogLevel.Debug));

var logger = loggerFactory.CreateLogger<ObjectPool<MyClass>>();
var pool = new ObjectPool<MyClass>(initialObjects, config, logger);

// All pool operations are now logged
using var obj = pool.GetObject(); // Logs: "Object retrieved from pool. Active: 1, Available: 4"
```

### 5. Production Limits
```csharp
var config = new PoolConfiguration
{
    MaxActiveObjects = 100,      // Prevent resource exhaustion
    MaxPoolSize = 50,            // Limit memory usage
    ValidateOnReturn = true,     // Ensure object integrity
    ValidationFunction = obj => obj.IsHealthy()
};
```

### 6. Async with Timeout
```csharp
try
{
    // Uses configured default timeout
    using var obj = await pool.GetObjectAsync();
    
    // Or specify custom timeout
    using var obj2 = await pool.GetObjectAsync(TimeSpan.FromSeconds(5), cancellationToken);
}
catch (TimeoutException)
{
    logger.LogError("Pool timeout - consider increasing capacity");
}
```

## Integration with ASP.NET Core

### Startup Configuration
```csharp
// In Program.cs or Startup.cs
services.AddSingleton<PoolConfiguration>(new PoolConfiguration
{
    MaxPoolSize = 100,
    MaxActiveObjects = 50,
    DefaultTimeout = TimeSpan.FromSeconds(30)
});

services.AddSingleton<IObjectPool<DatabaseConnection>>(provider =>
{
    var config = provider.GetRequiredService<PoolConfiguration>();
    var logger = provider.GetRequiredService<ILogger<ObjectPool<DatabaseConnection>>>();
    var connections = CreateInitialConnections();
    return new ObjectPool<DatabaseConnection>(connections, config, logger);
});
```

### Health Check Integration
```csharp
services.AddHealthChecks()
    .AddCheck<ObjectPoolHealthCheck>("objectpool");

public class ObjectPoolHealthCheck : IHealthCheck
{
    private readonly IObjectPool<DatabaseConnection> pool;

    public ObjectPoolHealthCheck(IObjectPool<DatabaseConnection> pool)
    {
        this.pool = pool;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var status = pool.GetHealthStatus();
        
        return Task.FromResult(status.IsHealthy 
            ? HealthCheckResult.Healthy(status.HealthMessage, status.Diagnostics)
            : HealthCheckResult.Unhealthy(status.HealthMessage, data: status.Diagnostics));
    }
}
```

### Metrics Integration (Prometheus)
```csharp
// In a background service
public class MetricsExportService : BackgroundService
{
    private readonly IObjectPool<DatabaseConnection> pool;
    private readonly ILogger<MetricsExportService> logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var metrics = pool.ExportMetrics();
                await ExportToPrometheus(metrics);
                await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to export pool metrics");
            }
        }
    }
}
```
