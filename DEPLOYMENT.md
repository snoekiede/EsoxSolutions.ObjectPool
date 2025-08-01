# Production Deployment Guide

## Prerequisites

### Framework Requirements
- .NET 8.0 or .NET 9.0
- Microsoft.Extensions.Logging.Abstractions 8.0.0+
- Microsoft.Extensions.DependencyInjection.Abstractions 8.0.0+

### Recommended Monitoring Stack
- Application Performance Monitoring (APM) tool
- Structured logging system (Serilog, NLog, etc.)
- Metrics collection (Prometheus, Application Insights)

## Configuration

### Basic Production Configuration
```csharp
var config = new PoolConfiguration
{
    MaxPoolSize = Environment.ProcessorCount * 25,
    MaxActiveObjects = Environment.ProcessorCount * 10,
    DefaultTimeout = TimeSpan.FromSeconds(30),
    ValidateOnReturn = true,
    ValidationFunction = ValidateObject
};
```

### High-Load Configuration
```csharp
var config = new PoolConfiguration
{
    MaxPoolSize = 1000,
    MaxActiveObjects = 500,
    DefaultTimeout = TimeSpan.FromSeconds(5),
    ValidateOnReturn = false, // Disable for performance
    ValidationFunction = null
};
```

## Monitoring Setup

### Health Checks Integration
```csharp
// In ASP.NET Core
services.AddHealthChecks()
    .AddCheck<PoolHealthCheck>("objectpool");

public class PoolHealthCheck : IHealthCheck
{
    private readonly ObjectPool<MyClass> pool;
    
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, 
        CancellationToken cancellationToken = default)
    {
        var health = pool.GetHealthStatus();
        return health.IsHealthy 
            ? HealthCheckResult.Healthy(health.HealthMessage)
            : HealthCheckResult.Unhealthy(health.HealthMessage);
    }
}
```

### Metrics Collection
```csharp
// Background service for metrics collection
public class PoolMetricsCollector : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var metrics = pool.ExportMetrics();
            await PublishMetrics(metrics);
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
```

## Performance Tuning

### Capacity Planning
- **MaxPoolSize**: Set to 2-3x expected peak concurrent usage
- **MaxActiveObjects**: Set to expected peak concurrent usage
- **Validation**: Disable in high-throughput scenarios

### Memory Management
- Monitor pool statistics for optimal sizing
- Use `Statistics.PeakActiveObjects` for capacity planning
- Monitor `Statistics.PoolEmptyCount` for undersizing detection

## Troubleshooting

### Common Issues
1. **Pool Exhaustion**: Increase MaxActiveObjects or MaxPoolSize
2. **High Latency**: Reduce DefaultTimeout or increase pool size
3. **Memory Leaks**: Ensure objects implement IDisposable correctly
4. **Validation Errors**: Check ValidationFunction logic

### Diagnostic Commands
```csharp
// Check current pool state
var stats = pool.Statistics;
logger.LogInformation("Pool Stats: Active={Active}, Available={Available}, Peak={Peak}", 
    stats.CurrentActiveObjects, stats.CurrentAvailableObjects, stats.PeakActiveObjects);

// Export all metrics
var metrics = pool.ExportMetrics();
logger.LogInformation("Pool Metrics: {@Metrics}", metrics);
```