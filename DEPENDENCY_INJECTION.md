# Dependency Injection Integration - Version 4.0.0

## Overview

EsoxSolutions.ObjectPool v4.0.0 includes first-class support for ASP.NET Core and Generic Host dependency injection, making it easy to register and configure object pools in your applications. The library also includes full integration with ASP.NET Core Health Checks, OpenTelemetry metrics, pool warm-up, eviction/TTL, circuit breaker, lifecycle hooks, and scoped pools for production monitoring.

## Installation

```bash
dotnet add package EsoxSolutions.ObjectPool
```

The package includes:
- `Microsoft.Extensions.DependencyInjection.Abstractions` for DI support
- `Microsoft.Extensions.Diagnostics.HealthChecks` for health check integration
- `System.Diagnostics.DiagnosticSource` for OpenTelemetry metrics
- Complete support for warm-up, eviction, circuit breaker, lifecycle hooks, and scoped pools

## Quick Start

### Basic Registration

```csharp
using EsoxSolutions.ObjectPool.DependencyInjection;

// In Program.cs or Startup.cs
services.AddObjectPool<HttpClient>(builder => builder
    .WithFactory(() => new HttpClient())
    .WithMaxSize(100)
    .WithMaxActiveObjects(50));
```

### Using the Pool

```csharp
public class MyService
{
    private readonly IObjectPool<HttpClient> _clientPool;
    
    public MyService(IObjectPool<HttpClient> clientPool)
    {
        _clientPool = clientPool;
    }
    
    public async Task<string> FetchDataAsync(string url)
    {
        using var pooledClient = _clientPool.GetObject();
        var client = pooledClient.Unwrap();
        return await client.GetStringAsync(url);
    }
}
```

## ASP.NET Core Health Checks Integration

### Basic Health Check Setup

```csharp
using EsoxSolutions.ObjectPool.DependencyInjection;
using EsoxSolutions.ObjectPool.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// Register pools
builder.Services.AddObjectPool<HttpClient>(b => b
    .WithFactory(() => new HttpClient())
    .WithMaxSize(100));

builder.Services.AddObjectPool<DbConnection>(b => b
    .WithFactory(() => new SqlConnection(connectionString))
    .WithMaxSize(50));

// Register health checks for pools
builder.Services.AddHealthChecks()
    .AddObjectPoolHealthCheck<HttpClient>("http-client-pool")
    .AddObjectPoolHealthCheck<DbConnection>("database-pool");

var app = builder.Build();

// Add health check endpoints
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.Run();
```

### Health Check Response Example

```json
{
  "status": "Healthy",
  "totalDuration": "00:00:00.0123456",
  "entries": {
    "http-client-pool": {
      "status": "Healthy",
      "description": "Pool is healthy",
      "data": {
        "utilization_percentage": 25.0,
        "available_objects": 75,
        "active_objects": 25,
        "peak_active": 32,
        "total_retrieved": 1523,
        "total_returned": 1498,
        "pool_empty_events": 0,
        "last_checked": "2025-01-15T10:30:00Z"
      }
    },
    "database-pool": {
      "status": "Degraded",
      "description": "Pool is degraded: High utilization: 82.5%",
      "data": {
        "utilization_percentage": 82.5,
        "available_objects": 8,
        "active_objects": 42,
        "peak_active": 48,
        "total_retrieved": 5231,
        "total_returned": 5189,
        "pool_empty_events": 3,
        "last_checked": "2025-01-15T10:30:00Z"
      }
    }
  }
}
```

### Custom Health Check Thresholds

```csharp
builder.Services.AddHealthChecks()
    .AddObjectPoolHealthCheck<DbConnection>(
        "database-pool",
        configureOptions: options =>
        {
            options.DegradedUtilizationThreshold = 70.0;  // Default: 75%
            options.UnhealthyUtilizationThreshold = 90.0; // Default: 95%
        });
```

### With Custom Tags and Failure Status

```csharp
builder.Services.AddHealthChecks()
    .AddObjectPoolHealthCheck<HttpClient>(
        "http-client-pool",
        failureStatus: HealthStatus.Degraded,  // Report as Degraded instead of Unhealthy
        tags: new[] { "ready", "live", "critical" },
        timeout: TimeSpan.FromSeconds(5));
```

### Queryable Pool Health Checks

```csharp
builder.Services.AddQueryableObjectPool<Car>(b => b
    .AsQueryable()
    .WithInitialObjects(cars));

builder.Services.AddHealthChecks()
    .AddQueryablePoolHealthCheck<Car>("car-pool");
```

### Multiple Pools with Different Endpoints

```csharp
// Register health checks with different tags
builder.Services.AddHealthChecks()
    .AddObjectPoolHealthCheck<HttpClient>(
        "http-client-pool",
        tags: new[] { "ready" })
    .AddObjectPoolHealthCheck<DbConnection>(
        "database-pool",
        tags: new[] { "ready", "live" })
    .AddObjectPoolHealthCheck<CacheConnection>(
        "cache-pool",
        tags: new[] { "live" });

// Map different endpoints
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
});
```

### Integration with Kubernetes

```csharp
var builder = WebApplication.CreateBuilder(args);

// ... register pools and health checks ...

var app = builder.Build();

// Liveness probe - basic application health
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
});

// Readiness probe - ready to accept traffic
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var json = JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                data = e.Value.Data
            })
        });
        await context.Response.WriteAsync(json);
    }
});

app.Run();
```

#### Kubernetes Deployment YAML

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: myapp
spec:
  template:
    spec:
      containers:
      - name: myapp
        image: myapp:latest
        ports:
        - containerPort: 8080
        livenessProbe:
          httpGet:
            path: /health/live
            port: 8080
          initialDelaySeconds: 10
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /health/ready
            port: 8080
          initialDelaySeconds: 5
          periodSeconds: 5
```

### Health Check Dashboard

```csharp
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// ... register pools and health checks ...

// Add Health Checks UI
builder.Services.AddHealthChecksUI()
    .AddInMemoryStorage();

var app = builder.Build();

// Detailed health check endpoint with UI response writer
app.MapHealthChecks("/healthz", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

// Health Checks UI
app.MapHealthChecksUI(options => options.UIPath = "/health-ui");

app.Run();
```

### Monitoring Pool Health Programmatically

```csharp
public class PoolMonitoringService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PoolMonitoringService> _logger;
    
    public PoolMonitoringService(
        IServiceProvider serviceProvider,
        ILogger<PoolMonitoringService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _serviceProvider.CreateScope();
            var healthCheckService = scope.ServiceProvider
                .GetRequiredService<HealthCheckService>();
            
            var result = await healthCheckService.CheckHealthAsync(stoppingToken);
            
            if (result.Status != HealthStatus.Healthy)
            {
                _logger.LogWarning(
                    "Pool health check failed: {Status}. Details: {@Entries}",
                    result.Status,
                    result.Entries);
            }
            
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}

// Register the service
builder.Services.AddHostedService<PoolMonitoringService>();
```

## OpenTelemetry Metrics Integration

### Overview

The library includes native OpenTelemetry metrics support using the `System.Diagnostics.Metrics` API, providing seamless integration with modern observability platforms like Prometheus, Grafana, Azure Monitor, AWS X-Ray, and Datadog.

### Basic Setup

```csharp
using EsoxSolutions.ObjectPool.DependencyInjection;
using OpenTelemetry.Metrics;

var builder = WebApplication.CreateBuilder(args);

// Register pools
builder.Services.AddObjectPool<HttpClient>(b => b
    .WithFactory(() => new HttpClient())
    .WithMaxSize(100));

// Register OpenTelemetry metrics
builder.Services.AddObjectPoolMetrics<HttpClient>(poolName: "http-client-pool");

// Configure OpenTelemetry
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics
        .AddMeter("EsoxSolutions.ObjectPool") // Subscribe to pool metrics
        .AddPrometheusExporter());             // Export to Prometheus

var app = builder.Build();
app.Run();
```

### Available Metrics

The pool exports the following OpenTelemetry metrics:

| Metric Name | Type | Unit | Description |
|-------------|------|------|-------------|
| `objectpool.objects.active` | ObservableGauge | `{objects}` | Current number of active (checked out) objects |
| `objectpool.objects.available` | ObservableGauge | `{objects}` | Current number of available objects in pool |
| `objectpool.utilization` | ObservableGauge | `1` (ratio) | Pool utilization as a ratio (0.0 to 1.0) |
| `objectpool.health.status` | ObservableGauge | `1` | Health status (1=healthy, 0=unhealthy) |
| `objectpool.objects.retrieved` | Counter | `{objects}` | Total objects retrieved from pool |
| `objectpool.objects.returned` | Counter | `{objects}` | Total objects returned to pool |
| `objectpool.events.empty` | Counter | `{events}` | Times pool was empty when requested |
| `objectpool.operation.duration` | Histogram | `ms` | Duration of pool operations |

### Multiple Pools with Metrics

```csharp
services.AddObjectPools(pools =>
{
    pools.AddPool<HttpClient>(b => b.WithFactory(() => new HttpClient()));
    pools.AddPool<DbConnection>(b => b.WithFactory(() => new SqlConnection(cs)));
});

// Register metrics for all pools
services.AddObjectPoolsWithMetrics(metrics =>
{
    metrics.AddMetrics<HttpClient>("http-client-pool");
    metrics.AddMetrics<DbConnection>("database-pool");
});
```

### With Prometheus Export

```csharp
using OpenTelemetry.Metrics;

var builder = WebApplication.CreateBuilder(args);

// Register pools and metrics
builder.Services.AddObjectPool<HttpClient>(b => b
    .WithFactory(() => new HttpClient())
    .WithMaxSize(100));

builder.Services.AddObjectPoolMetrics<HttpClient>("http-pool");

// Configure OpenTelemetry with Prometheus
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics
        .AddMeter("EsoxSolutions.ObjectPool")
        .AddPrometheusExporter());

var app = builder.Build();

// Prometheus scrape endpoint
app.MapPrometheusScrapingEndpoint();

app.Run();
```

### Example Prometheus Queries

```promql
# Pool utilization
objectpool_utilization{pool_name="http-client-pool"}

# Active objects over time
rate(objectpool_objects_active[5m])

# Pool empty events
increase(objectpool_events_empty[1h])

# 95th percentile operation duration
histogram_quantile(0.95, rate(objectpool_operation_duration_bucket[5m]))

# Alert when utilization exceeds 80%
objectpool_utilization > 0.8
```

### Grafana Dashboard Example

```json
{
  "dashboard": {
    "title": "Object Pool Metrics",
    "panels": [
      {
        "title": "Pool Utilization",
        "targets": [
          {
            "expr": "objectpool_utilization{pool_name=\"$pool\"} * 100",
            "legendFormat": "{{pool_name}}"
          }
        ],
        "type": "graph"
      },
      {
        "title": "Active vs Available Objects",
        "targets": [
          {
            "expr": "objectpool_objects_active{pool_name=\"$pool\"}",
            "legendFormat": "Active"
          },
          {
            "expr": "objectpool_objects_available{pool_name=\"$pool\"}",
            "legendFormat": "Available"
          }
        ],
        "type": "graph"
      },
      {
        "title": "Operation Duration (p95)",
        "targets": [
          {
            "expr": "histogram_quantile(0.95, rate(objectpool_operation_duration_bucket[5m]))",
            "legendFormat": "p95"
          }
        ],
        "type": "graph"
      }
    ]
  }
}
```

### Azure Monitor Integration

```csharp
using Azure.Monitor.OpenTelemetry.Exporter;

builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics
        .AddMeter("EsoxSolutions.ObjectPool")
        .AddAzureMonitorMetricExporter(options =>
        {
            options.ConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
        }));
```

### AWS CloudWatch Integration

```csharp
using OpenTelemetry.Exporter.OpenTelemetryProtocol;

builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics
        .AddMeter("EsoxSolutions.ObjectPool")
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri("https://otlp.nr-data.net");
            options.Headers = $"api-key={Environment.GetEnvironmentVariable("NEW_RELIC_LICENSE_KEY")}";
        }));
```

### Custom Instrumentation

```csharp
using EsoxSolutions.ObjectPool.Telemetry;

public class CustomService
{
    private readonly IObjectPool<HttpClient> _pool;
    private readonly ObjectPoolMeter<HttpClient> _meter;
    
    public CustomService(
        IObjectPool<HttpClient> pool,
        ObjectPoolMeter<HttpClient> meter)
    {
        _pool = pool;
        _meter = meter;
    }
    
    public async Task<string> FetchDataAsync(string url)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            using var pooled = _pool.GetObject();
            _meter.RecordRetrieval(success: true, durationMs: stopwatch.Elapsed.TotalMilliseconds);
            
            var client = pooled.Unwrap();
            return await client.GetStringAsync(url);
        }
        catch
        {
            _meter.RecordEmptyEvent();
            throw;
        }
        finally
        {
            stopwatch.Stop();
        }
    }
}
```

### Metric Tags and Dimensions

All metrics include the following tags/dimensions:

- `pool.name`: The configured pool name
- `pool.type`: The type of objects in the pool (e.g., "HttpClient")
- `success`: For counter metrics, indicates operation success (true/false)
- `operation`: For histogram metrics, indicates operation type ("retrieve", "return")

### Real-time Monitoring with MeterListener

```csharp
using System.Diagnostics.Metrics;

public class PoolMonitoringService : BackgroundService
{
    private MeterListener? _listener;
    
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _listener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == "EsoxSolutions.ObjectPool")
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            }
        };
        
        _listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) =>
        {
            Console.WriteLine($"{instrument.Name}: {measurement:F2}");
        });
        
        _listener.Start();
        return Task.CompletedTask;
    }
    
    public override void Dispose()
    {
        _listener?.Dispose();
        base.Dispose();
    }
}
```

### Alerting Examples

#### Prometheus AlertManager

```yaml
groups:
  - name: objectpool_alerts
    rules:
      - alert: HighPoolUtilization
        expr: objectpool_utilization > 0.85
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "High pool utilization detected"
          description: "Pool {{ $labels.pool_name }} utilization is {{ $value }}%"
      
      - alert: PoolEmptyEvents
        expr: increase(objectpool_events_empty[5m]) > 10
        labels:
          severity: critical
        annotations:
          summary: "Pool empty events detected"
          description: "Pool {{ $labels.pool_name }} was empty {{ $value }} times in the last 5 minutes"
      
      - alert: PoolUnhealthy
        expr: objectpool_health_status == 0
        for: 2m
        labels:
          severity: critical
        annotations:
          summary: "Pool is unhealthy"
          description: "Pool {{ $labels.pool_name }} is reporting unhealthy status"
```

### Best Practices for OpenTelemetry

1. **Use meaningful pool names** for easy identification in dashboards
2. **Configure appropriate scrape intervals** (typically 15-60 seconds)
3. **Set up alerts** for high utilization and empty events
4. **Monitor operation duration** to identify performance issues
5. **Use tags/dimensions** for filtering and grouping in queries
6. **Export to multiple backends** for redundancy
7. **Set resource attributes** to identify the application instance

### Complete Example with All Observability Features

```csharp
using EsoxSolutions.ObjectPool.DependencyInjection;
using EsoxSolutions.ObjectPool.HealthChecks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

var builder = WebApplication.CreateBuilder(args);

// Register pools
builder.Services.AddObjectPool<HttpClient>(b => b
    .WithFactory(() => new HttpClient())
    .WithMaxSize(100)
    .WithMaxActiveObjects(50));

// Health Checks
builder.Services.AddHealthChecks()
    .AddObjectPoolHealthCheck<HttpClient>("http-client-pool");

// OpenTelemetry Metrics
builder.Services.AddObjectPoolMetrics<HttpClient>("http-client-pool");

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService("MyApplication", serviceVersion: "1.0.0"))
    .WithMetrics(metrics => metrics
        .AddMeter("EsoxSolutions.ObjectPool")
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddPrometheusExporter()
        .AddOtlpExporter());

var app = builder.Build();

// Endpoints
app.MapHealthChecks("/health");
app.MapPrometheusScrapingEndpoint();

app.Run();
```

## Configuration Options

### Standard Object Pool

```csharp
services.AddObjectPool<MyClass>(builder => builder
    .WithInitialObjects(initialList)           // Pre-populate pool
    .WithMaxSize(100)                          // Maximum pool size
    .WithMaxActiveObjects(50)                  // Max concurrent active objects
    .WithDefaultTimeout(TimeSpan.FromSeconds(30))  // Async operation timeout
    .WithValidation(obj => obj.IsValid())      // Validate on return
    .WithHealthChecks());                      // Enable health monitoring
```

### Dynamic Object Pool

Creates objects on-demand using a factory:

```csharp
services.AddDynamicObjectPool<DbConnection>(
    sp => sp.GetRequiredService<IDbConnectionFactory>().Create(),
    config =>
    {
        config.MaxPoolSize = 50;
        config.MaxActiveObjects = 25;
    });
```

### Queryable Object Pool

Supports querying for specific objects:

```csharp
services.AddQueryableObjectPool<Car>(builder => builder
    .AsQueryable()
    .WithInitialObjects(carList)
    .WithMaxSize(100));

// Usage
using var pooledCar = carPool.GetObject(c => c.Make == "Ford");
```

### Simple Registration with Initial Objects

```csharp
var initialConnections = CreateConnections();
services.AddObjectPoolWithObjects(initialConnections, config =>
{
    config.MaxActiveObjects = 10;
});
```

## Advanced Scenarios

### Multiple Pools

Register different pool types for different classes:

```csharp
services.AddObjectPools(pools =>
{
    pools.AddPool<HttpClient>(builder => builder
        .WithFactory(() => new HttpClient())
        .WithMaxSize(100));
    
    pools.AddPool<DbConnection>(builder => builder
        .WithFactory(() => new SqlConnection(connectionString))
        .WithMaxSize(50)
        .WithValidation(conn => conn.State != ConnectionState.Broken));
    
    pools.AddQueryablePool<Car>(builder => builder
        .AsQueryable()
        .WithInitialObjects(carList));
});
```

### With Configuration Object

```csharp
services.AddObjectPool<MyClass>(builder => builder
    .WithFactory(() => new MyClass())
    .Configure(config =>
    {
        config.MaxPoolSize = 100;
        config.MaxActiveObjects = 50;
        config.DefaultTimeout = TimeSpan.FromMinutes(1);
        config.ValidateOnReturn = true;
        config.ValidationFunction = obj => ((MyClass)obj).IsHealthy;
    }));
```

### Database Connection Pool Example

```csharp
// Registration
services.AddDynamicObjectPool<SqlConnection>(
    sp =>
    {
        var config = sp.GetRequiredService<IConfiguration>();
        var connString = config.GetConnectionString("DefaultConnection");
        var conn = new SqlConnection(connString);
        conn.Open();
        return conn;
    },
    config =>
    {
        config.MaxPoolSize = 100;
        config.MaxActiveObjects = 50;
        config.ValidateOnReturn = true;
        config.ValidationFunction = obj => 
        {
            var conn = (SqlConnection)obj;
            return conn.State == ConnectionState.Open;
        };
    });

// Add health check
services.AddHealthChecks()
    .AddObjectPoolHealthCheck<SqlConnection>("database-connection-pool");

// Usage in a service
public class DataService
{
    private readonly IObjectPool<SqlConnection> _connPool;
    
    public DataService(IObjectPool<SqlConnection> connPool)
    {
        _connPool = connPool;
    }
    
    public async Task<List<User>> GetUsersAsync()
    {
        using var pooledConn = _connPool.GetObject();
        var conn = pooledConn.Unwrap();
        
        using var cmd = new SqlCommand("SELECT * FROM Users", conn);
        using var reader = await cmd.ExecuteReaderAsync();
        
        var users = new List<User>();
        while (await reader.ReadAsync())
        {
            users.Add(new User 
            { 
                Id = reader.GetInt32(0),
                Name = reader.GetString(1)
            });
        }
        return users;
    }
}
```

### HTTP Client Pool Example

```csharp
// Registration with custom configuration
services.AddObjectPool<HttpClient>(builder => builder
    .WithFactory(() => new HttpClient 
    { 
        Timeout = TimeSpan.FromSeconds(30),
        DefaultRequestHeaders = 
        {
            { "User-Agent", "MyApp/1.0" }
        }
    })
    .WithMaxSize(50)
    .WithMaxActiveObjects(25)
    .WithValidation(client => client.DefaultRequestHeaders != null));

// Add health check
services.AddHealthChecks()
    .AddObjectPoolHealthCheck<HttpClient>("http-client-pool");

// Usage in a service
public class ApiService
{
    private readonly IObjectPool<HttpClient> _httpClientPool;
    private readonly ILogger<ApiService> _logger;
    
    public ApiService(
        IObjectPool<HttpClient> httpClientPool,
        ILogger<ApiService> logger)
    {
        _httpClientPool = httpClientPool;
        _logger = logger;
    }
    
    public async Task<WeatherData> GetWeatherAsync(string city)
    {
        using var pooledClient = _httpClientPool.GetObject();
        var client = pooledClient.Unwrap();
        
        var response = await client.GetAsync($"api/weather/{city}");
        response.EnsureSuccessStatusCode();
        
        return await response.Content.ReadFromJsonAsync<WeatherData>();
    }
}
```

### Multi-Tenant Scenario

```csharp
// Register a pool per tenant
public class TenantPoolManager
{
    private readonly ConcurrentDictionary<string, IObjectPool<DbConnection>> _pools = new();
    private readonly IServiceProvider _serviceProvider;
    
    public TenantPoolManager(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }
    
    public IObjectPool<DbConnection> GetPoolForTenant(string tenantId)
    {
        return _pools.GetOrAdd(tenantId, tid =>
        {
            var connString = GetConnectionStringForTenant(tid);
            
            // Create a new pool for this tenant
            var builder = new ObjectPoolBuilder<DbConnection>();
            builder.WithFactory(() => new SqlConnection(connString))
                   .WithMaxSize(20);
                   
            var logger = _serviceProvider.GetService<ILogger<ObjectPool<DbConnection>>>();
            return builder.Build(logger);
        });
    }
}
```

## ASP.NET Core Integration

### Minimal API Example

```csharp
var builder = WebApplication.CreateBuilder(args);

// Register pools
builder.Services.AddObjectPool<HttpClient>(b => b
    .WithFactory(() => new HttpClient())
    .WithMaxSize(100));

// Register health checks
builder.Services.AddHealthChecks()
    .AddObjectPoolHealthCheck<HttpClient>();

builder.Services.AddScoped<WeatherService>();

var app = builder.Build();

app.MapGet("/weather/{city}", async (string city, WeatherService service) =>
{
    var weather = await service.GetWeatherAsync(city);
    return Results.Ok(weather);
});

app.MapHealthChecks("/health");

app.Run();
```

### MVC Controller Example

```csharp
[ApiController]
[Route("api/[controller]")]
public class DataController : ControllerBase
{
    private readonly IObjectPool<DbConnection> _connectionPool;
    private readonly ILogger<DataController> _logger;
    
    public DataController(
        IObjectPool<DbConnection> connectionPool,
        ILogger<DataController> logger)
    {
        _connectionPool = connectionPool;
        _logger = logger;
    }
    
    [HttpGet]
    public async Task<IActionResult> GetData()
    {
        using var pooledConn = await _connectionPool.GetObjectAsync(
            TimeSpan.FromSeconds(5));
            
        var conn = pooledConn.Unwrap();
        
        // Use connection...
        
        return Ok(data);
    }
}
```

## Best Practices

### 1. Pool Sizing

```csharp
// Rule of thumb: ProcessorCount * factor
var maxSize = Environment.ProcessorCount * 25;
var maxActive = Environment.ProcessorCount * 10;

services.AddObjectPool<MyClass>(builder => builder
    .WithFactory(() => new MyClass())
    .WithMaxSize(maxSize)
    .WithMaxActiveObjects(maxActive));
```

### 2. Validation

```csharp
services.AddObjectPool<DbConnection>(builder => builder
    .WithFactory(() => CreateConnection())
    .WithValidation(conn => 
    {
        // Only return healthy connections to the pool
        var sqlConn = conn as SqlConnection;
        return sqlConn?.State == ConnectionState.Open;
    }));
```

### 3. Timeout Configuration

```csharp
services.AddObjectPool<MyClass>(builder => builder
    .WithFactory(() => new MyClass())
    .WithDefaultTimeout(TimeSpan.FromSeconds(30)));

// Usage with async
using var obj = await pool.GetObjectAsync(); // Uses configured timeout
```

### 4. Logging Integration

Pools automatically integrate with `ILogger<T>`:

```csharp
services.AddLogging(builder => 
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Debug);
});

services.AddObjectPool<MyClass>(builder => builder
    .WithFactory(() => new MyClass()));
    
// Logs will be automatically generated for:
// - Object retrieval
// - Object return
// - Validation failures
// - Pool empty events
```

### 5. Health Check Best Practices

- **Use appropriate tags** for liveness vs. readiness probes
- **Set custom thresholds** based on your application's requirements
- **Monitor health check metrics** in production
- **Configure timeouts** to prevent hanging health checks
- **Include pool health in overall application health**

## Performance Tips

1. **Pre-populate pools** for warm-start scenarios
2. **Use validation sparingly** - it adds overhead
3. **Size pools appropriately** - too small causes contention, too large wastes memory
4. **Monitor metrics** in production to tune configuration
5. **Use `TryGetObject`** in high-throughput scenarios to avoid exceptions
6. **Enable health checks** for production monitoring and alerting

## Migration from Manual Management

### Before (Manual)

```csharp
public class MyService
{
    private readonly Queue<HttpClient> _clients = new();
    private readonly SemaphoreSlim _semaphore = new(10);
    
    public async Task DoWorkAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            var client = _clients.Dequeue();
            // Use client...
            _clients.Enqueue(client);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
```

### After (With Pool)

```csharp
public class MyService
{
    private readonly IObjectPool<HttpClient> _clientPool;
    
    public MyService(IObjectPool<HttpClient> clientPool)
    {
        _clientPool = clientPool;
    }
    
    public async Task DoWorkAsync()
    {
        using var pooledClient = _clientPool.GetObject();
        var client = pooledClient.Unwrap();
        // Use client - automatically returned on dispose
    }
}
```

## See Also

- [Main README](README.md)
- [Deployment Guide](DEPLOYMENT.md)
- [ASP.NET Core Health Checks Documentation](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks)
