# EsoxSolutions.ObjectPool

## Overview

EsoxSolutions.ObjectPool is a high-performance, thread-safe object pool for .NET 8+, .NET 9 and .NET 10. It supports automatic return of objects, async operations, performance metrics, flexible configuration, **first-class dependency injection support**, and **ASP.NET Core Health Checks integration**. Useful for pooling expensive resources like database connections, network clients, or reusable buffers.

## What's New in Version 4.1.0

### New Features in 4.1.0
- **Pooling Policies** - Choose retrieval strategies (LIFO, FIFO, Priority, LRU, Round-Robin)
- **IAsyncDisposable Support** - Proper async cleanup for modern .NET resources
- **Async Validation** - Validate objects asynchronously on return to pool
- **Enhanced AOT Support** - Native AOT and trimming compatibility
- **Package Validation** - NuGet package validation enabled
- **SourceLink** - Step-through debugging support

### Complete Production-Ready Suite (4.0.0)
- **First-class ASP.NET Core support** with fluent configuration API
- **ASP.NET Core Health Checks integration** for production monitoring
- **OpenTelemetry metrics** with native `System.Diagnostics.Metrics` support
- **Pool warm-up/pre-population** to eliminate cold-start latency
- **Eviction / Time-to-Live (TTL)** support for automatic stale object removal
- **Circuit Breaker pattern** for protecting against cascading failures
- **Lifecycle Hooks** for custom object lifecycle management
- **Scoped Pools** for multi-tenancy and per-tenant/user isolation
- **Builder pattern** for easy pool setup
- **Multiple pool registration** with `AddObjectPools()`
- **Service provider integration** for factory methods
- **Kubernetes-ready** with liveness and readiness probe support
- **Prometheus, Grafana, Azure Monitor, AWS CloudWatch** integration
- See [DEPENDENCY_INJECTION.md](DEPENDENCY_INJECTION.md) for complete guide

### Previous Updates (v3.0)

#### Performance & Reliability
- **20-40% faster** queryable pool operations with optimized `TryGetObject(query)` implementation
- **Critical bug fix**: Eliminated race condition in `DynamicObjectPool` that could cause object creation failures under high concurrency
- **Thread-safe disposal**: Improved `PoolModel<T>` disposal pattern using modern atomic operations

#### Modern C# 14 Features
- **Collection expressions**: Cleaner initialization syntax (`[1, 2, 3]` instead of `new List<int> { 1, 2, 3 }`)
- **Primary constructors**: Simplified class declarations
- **ArgumentNullException.ThrowIfNull**: Modern null checking patterns
- **Sealed classes**: Better performance optimization opportunities

#### Technical Improvements
- **Early exit optimization**: Query operations exit immediately when match found
- **Reduced allocations**: Eliminated redundant snapshots and LINQ overhead
- **Better statistics**: More accurate tracking under concurrent load

#### Quality Assurance
- **100% test success rate**: All 186 tests passing
- **Stress tested**: Verified with 500 concurrent threads on 100 objects
- **Production ready**: Comprehensive validation across .NET 8, 9, and 10

## Features

- **Pooling Policies** - LIFO, FIFO, Priority, LRU, Round-Robin retrieval strategies
- **IAsyncDisposable** - Proper async cleanup for database connections, gRPC channels, etc.
- **Async Validation** - Asynchronously validate objects on return (health checks, ping tests)
- **Dependency Injection** - First-class ASP.NET Core and Generic Host support
- **Health Checks** - ASP.NET Core Health Checks integration for monitoring
- **OpenTelemetry Metrics** - Native observability with System.Diagnostics.Metrics API
- **Pool Warm-up** - Pre-population for zero cold-start latency
- **Eviction / TTL** - Automatic removal of stale objects based on time-to-live or idle timeout
- **Circuit Breaker** - Protect against cascading failures with automatic recovery
- **Lifecycle Hooks** - Execute custom logic at object creation, acquisition, return, and disposal
- **Scoped Pools** - Multi-tenancy support with per-tenant/user/context pool isolation
- **Thread-safe object pooling** with lock-free concurrent operations
- **Automatic return of objects** via IDisposable pattern
- **Async support** with `GetObjectAsync`, `TryGetObjectAsync`, timeout and cancellation
- **Queryable pools** for finding objects matching predicates
- **Dynamic pools** with factory methods for on-demand object creation
- **Health monitoring** with real-time status and utilization metrics
- **Prometheus metrics** exportable format with tags/labels
- **Pool configuration** for max size, active objects, validation, and timeouts
- **Try* methods** for non-throwing retrieval patterns
- **High-performance** with O(1) get/return operations
- **AOT Compatible** - Native AOT and trimming support

## Quick Start

### With Pooling Policies & Async Disposal (New in 4.1.0!)

```csharp
using EsoxSolutions.ObjectPool.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Database connection pool with FIFO policy and async validation
builder.Services.AddObjectPool<DatabaseConnection>(builder => builder
    .WithFactory(() => new DatabaseConnection(connectionString))
    .WithFifoPolicy()  // Fair distribution, prevents connection aging
    .WithAsyncValidation(async conn =>
    {
        // Verify connection is still alive
        if (conn.State != ConnectionState.Open)
            await conn.OpenAsync();

        await conn.ExecuteAsync("SELECT 1"); // Ping test
        return true;
    })
    .WithAsyncDisposal(true)  // Proper async cleanup
    .WithMaxSize(50));

// HTTP client pool with Round-Robin policy
builder.Services.AddObjectPool<HttpClient>(builder => builder
    .WithFactory(() => new HttpClient())
    .WithRoundRobinPolicy()  // Load balancing
    .WithMaxSize(100));

// Multi-tenant pool with Priority policy
builder.Services.AddObjectPool<TenantConnection>(builder => builder
    .WithFactory(() => new TenantConnection())
    .WithPriorityPolicy(conn => conn.TenantTier switch
    {
        TenantTier.Premium => 10,
        TenantTier.Standard => 5,
        TenantTier.Free => 1,
        _ => 0
    })
    .WithMaxSize(100));

var app = builder.Build();
app.Run();
```

**New Pooling Policies:**
- **LIFO** (default): Best performance, cache locality
- **FIFO**: Fair scheduling, prevents aging
- **Priority**: QoS, multi-tenant prioritization
- **LRU**: Prevents staleness, keep-alive
- **Round-Robin**: Load balancing, even distribution

**IAsyncDisposable Support:**
- Automatic async disposal of database connections
- Proper cleanup for gRPC channels
- Async validation for health checks
- No blocking on I/O operations

See [POOLING_POLICIES.md](docs/POOLING_POLICIES.md) and [ASYNC_OPERATIONS.md](docs/ASYNC_OPERATIONS.md) for details.

### With Dependency Injection, Health Checks, OpenTelemetry & Warm-up (Recommended)

```csharp
using EsoxSolutions.ObjectPool.DependencyInjection;
using EsoxSolutions.ObjectPool.HealthChecks;
using OpenTelemetry.Metrics;

var builder = WebApplication.CreateBuilder(args);

// Register pools with warm-up
builder.Services.AddDynamicObjectPool<HttpClient>(
    sp => new HttpClient(),
    config =>
    {
        config.MaxPoolSize = 100;
        config.MaxActiveObjects = 50;
    })
    .WithAutoWarmup<HttpClient>(50); // Pre-create 50 objects on startup

builder.Services.AddDynamicObjectPool<DbConnection>(
    sp => new SqlConnection(connectionString),
    config => config.MaxPoolSize = 50)
    .WithAutoWarmupPercentage<DbConnection>(75); // Pre-create 75% of capacity

// Register health checks
builder.Services.AddHealthChecks()
    .AddObjectPoolHealthCheck<HttpClient>("http-client-pool")
    .AddObjectPoolHealthCheck<DbConnection>("database-pool");

// Register OpenTelemetry metrics
builder.Services.AddObjectPoolMetrics<HttpClient>("http-client-pool");
builder.Services.AddObjectPoolMetrics<DbConnection>("database-pool");

builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics
        .AddMeter("EsoxSolutions.ObjectPool")
        .AddPrometheusExporter());

var app = builder.Build();

// Add endpoints
app.MapHealthChecks("/health");
app.MapPrometheusScrapingEndpoint();

app.Run();
```

**Benefits:**
- **Zero cold-start latency** - Objects pre-created during startup
- **Immediate availability** - First request served instantly
- **Configurable warm-up** - Target size or percentage of capacity
- **Async warm-up** - Non-blocking startup
- **Progress tracking** - Monitor warm-up status and duration

**Health Check Response:**
```json
{
  "status": "Healthy",
  "entries": {
    "http-client-pool": {
      "status": "Healthy",
      "data": {
        "utilization_percentage": 25.0,
        "available_objects": 75,
        "active_objects": 25,
        "total_retrieved": 1523,
        "total_returned": 1498
      }
    }
  }
}
```

**OpenTelemetry Metrics Available:**
- `objectpool.objects.active` - Current active objects
- `objectpool.objects.available` - Current available objects
- `objectpool.utilization` - Pool utilization ratio (0.0-1.0)
- `objectpool.health.status` - Health status (1=healthy, 0=unhealthy)
- `objectpool.objects.retrieved` - Total objects retrieved (counter)
- `objectpool.objects.returned` - Total objects returned (counter)
- `objectpool.events.empty` - Pool empty events (counter)
- `objectpool.operation.duration` - Operation duration histogram

### With Dependency Injection, Health Checks, OpenTelemetry, Warm-up & Eviction (Recommended)

```csharp
using EsoxSolutions.ObjectPool.DependencyInjection;
using EsoxSolutions.ObjectPool.HealthChecks;
using OpenTelemetry.Metrics;

var builder = WebApplication.CreateBuilder(args);

// Register pools with warm-up and eviction
builder.Services.AddDynamicObjectPool<HttpClient>(
    sp => new HttpClient(),
    config =>
    {
        config.MaxPoolSize = 100;
        config.MaxActiveObjects = 50;
    })
    .WithAutoWarmup<HttpClient>(50) // Pre-create 50 objects on startup
    .WithEviction(
        timeToLive: TimeSpan.FromHours(1),      // Objects expire after 1 hour
        idleTimeout: TimeSpan.FromMinutes(10)); // or 10 minutes idle

builder.Services.AddDynamicObjectPool<DbConnection>(
    sp => new SqlConnection(connectionString),
    config => config.MaxPoolSize = 50)
    .WithAutoWarmupPercentage<DbConnection>(75)
    .WithIdleTimeout(TimeSpan.FromMinutes(5)); // Evict connections idle for 5 minutes

// Register health checks
builder.Services.AddHealthChecks()
    .AddObjectPoolHealthCheck<HttpClient>("http-client-pool")
    .AddObjectPoolHealthCheck<DbConnection>("database-pool");

// Register OpenTelemetry metrics
builder.Services.AddObjectPoolMetrics<HttpClient>("http-client-pool");
builder.Services.AddObjectPoolMetrics<DbConnection>("database-pool");

builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics
        .AddMeter("EsoxSolutions.ObjectPool")
        .AddPrometheusExporter());

var app = builder.Build();

// Add endpoints
app.MapHealthChecks("/health");
app.MapPrometheusScrapingEndpoint();

app.Run();
```

**Benefits:**
- **Zero cold-start latency** - Objects pre-created during startup
- **Automatic cleanup** - Stale objects removed automatically
- **Resource efficiency** - No memory leaks from expired objects
- **Configurable policies** - TTL, idle timeout, or custom eviction logic
- **Immediate availability** - First request served instantly
- **Progress tracking** - Monitor warm-up and eviction status

### In Your Service

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

## New in 4.1.0: Pooling Policies & Async Support

### Pooling Policies

Choose the right retrieval strategy for your use case:

```csharp
// LIFO (Default) - Best performance, cache locality
services.AddObjectPool<Resource>(b => b
    .WithFactory(() => new Resource())
    .WithLifoPolicy());  // or omit - LIFO is default

// FIFO - Fair scheduling, prevents object aging
services.AddObjectPool<Connection>(b => b
    .WithFactory(() => new Connection())
    .WithFifoPolicy());

// Priority - Multi-tenant QoS
services.AddObjectPool<TenantResource>(b => b
    .WithFactory(() => new TenantResource())
    .WithPriorityPolicy(r => r.Priority));

// LRU - Prevents staleness, keep-alive
services.AddObjectPool<GrpcChannel>(b => b
    .WithFactory(() => CreateChannel())
    .WithLeastRecentlyUsedPolicy());

// Round-Robin - Load balancing
services.AddObjectPool<ServiceClient>(b => b
    .WithFactory(() => new ServiceClient())
    .WithRoundRobinPolicy());
```

**When to use each policy:**
- **LIFO**: General purpose, maximum performance
- **FIFO**: Database connections (prevent idle timeouts)
- **Priority**: Multi-tenant apps with SLA tiers
- **LRU**: Long-lived connections needing keep-alive
- **Round-Robin**: Multiple endpoints, load balancing

### IAsyncDisposable Support

Proper async cleanup for modern .NET resources:

```csharp
// Define async-disposable resource
public class DatabaseConnection : IAsyncDisposable
{
    private SqlConnection _connection;

    public async ValueTask DisposeAsync()
    {
        await _connection.CloseAsync();
        await _connection.DisposeAsync();
    }
}

// Pool with async disposal
services.AddObjectPool<DatabaseConnection>(b => b
    .WithFactory(() => new DatabaseConnection(connString))
    .WithAsyncDisposal(true)  // enabled by default
    .WithMaxSize(50));

// Async cleanup on disposal
await using var pool = serviceProvider.GetRequiredService<IObjectPool<DatabaseConnection>>();
// All connections disposed asynchronously when pool is disposed
```

### Async Validation

Validate objects asynchronously on return:

```csharp
services.AddObjectPool<HttpClient>(b => b
    .WithFactory(() => new HttpClient())
    .WithAsyncValidation(async client =>
    {
        try
        {
            // Verify connection is still alive
            var response = await client.GetAsync("https://api.example.com/health");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;  // Invalid - will be removed from pool
        }
    })
    .WithMaxSize(50));

// Use with async return
var pooled = pool.GetObject();
try
{
    await pooled.Unwrap().GetAsync("https://api.example.com/data");
}
finally
{
    await pool.ReturnObjectAsync(pooled);  // Async validation runs here
}
```

**See [POOLING_POLICIES.md](docs/POOLING_POLICIES.md) and [ASYNC_OPERATIONS.md](docs/ASYNC_OPERATIONS.md) for comprehensive examples including:**
- Policy comparison and use cases
- Multi-tenant priority pooling
- gRPC channel pooling with async disposal
- Database connection health checks
- Custom policy implementation
- Performance characteristics
- Best practices

**See [DEPENDENCY_INJECTION.md](DEPENDENCY_INJECTION.md) for comprehensive examples including:**
- ASP.NET Core Health Checks setup
- OpenTelemetry metrics integration
- Pool warm-up and pre-population strategies
- Eviction / TTL configuration (time-to-live, idle timeout)
- Circuit Breaker pattern implementation
- Lifecycle Hooks for custom object management
- Scoped Pools for multi-tenancy
- Prometheus, Grafana, Azure Monitor integration
- Kubernetes liveness/readiness probes
- Custom health check thresholds
- Database connection pooling
- HTTP client pooling
- Multi-tenant scenarios
- Configuration best practices

### Direct Instantiation

### PoolModel
A generic wrapper for pooled objects. Use `Unwrap()` to access the value.

```csharp
var pool = new ObjectPool<int>([1, 2, 3]);  // Collection expression syntax
using (var model = pool.GetObject())
{
    var value = model.Unwrap();
    Console.WriteLine(value);
}
```

### ObjectPool
Administers a fixed set of objects. Throws if pool is empty.

```csharp
var initialObjects = new List<int> { 1, 2, 3 };
var pool = new ObjectPool<int>(initialObjects);
using (var model = pool.GetObject())
{
    Console.WriteLine(model.Unwrap());
}
```

#### Async Usage
```csharp
using (var model = await pool.GetObjectAsync())
{
    Console.WriteLine(model.Unwrap());
}
```

#### Try Methods
```csharp
if (pool.TryGetObject(out var model))
{
    using (model)
    {
        Console.WriteLine(model.Unwrap());
    }
}
```

#### Pool Configuration
```csharp
var config = new PoolConfiguration {
    MaxPoolSize = 5,
    MaxActiveObjects = 3,
    ValidateOnReturn = true,
    ValidationFunction = obj => obj != null
};
var pool = new ObjectPool<int>(initialObjects, config);
```

### QueryableObjectPool
Query for objects matching a predicate. **Now 20-40% faster!**

```csharp
var pool = new QueryableObjectPool<int>([1, 2, 3]);
using (var model = pool.GetObject(x => x == 2))
{
    Console.WriteLine(model.Unwrap());
}
```

### DynamicObjectPool
Creates objects on the fly using a factory method. **Race condition fixed in v3.0!**

```csharp
var pool = new DynamicObjectPool<int>(() => 42);
using (var model = pool.GetObject())
{
    Console.WriteLine(model.Unwrap());
}

// With initial objects and factory for scaling
var pool = new DynamicObjectPool<Car>(
    () => new Car("Ford", "Dynamic"), 
    initialCars
);
```

### Prometheus Exporter
Export pool metrics in Prometheus exposition format.

```csharp
// Using interface default/extension
var prometheusText = ((IPoolMetrics)pool).ExportMetricsPrometheus();

// Or using concrete pool convenience method
var prometheusText = pool.ExportMetricsPrometheus();

// With optional tags as labels
var tags = new Dictionary<string, string> { 
    ["service"] = "order-service", 
    ["env"] = "prod" 
};
var prometheusTextWithLabels = pool.ExportMetricsPrometheus(tags);
```

The exporter emits `HELP`/`TYPE` lines and converts string metrics into `*_info` gauge metrics with a `value` label.

### Health Monitoring & Metrics

```csharp
var health = pool.GetHealthStatus();
Console.WriteLine($"Healthy: {health.IsHealthy}, Warnings: {health.WarningCount}");

var metrics = pool.ExportMetrics();
foreach (var kv in metrics)
    Console.WriteLine($"{kv.Key}: {kv.Value}");
```

## Performance Characteristics

| Operation | Complexity | Notes |
|-----------|-----------|-------|
| `GetObject()` | O(1) | Constant time pop from concurrent stack |
| `ReturnObject()` | O(1) | Constant time push to concurrent stack |
| `GetObject(query)` | O(n) worst case | Optimized with early exit and bulk operations |
| `TryGetObject()` | O(1) | Non-throwing variant |

## Thread-Safety

All pool operations are thread-safe using lock-free `ConcurrentStack<T>` and `ConcurrentDictionary<T, byte>`:
- Tested with 500 concurrent threads
- Race condition free
- No blocking locks in hot paths
- Atomic operations for critical sections

## Version History

### 4.1.0 (Current) - January 2025
- **Pooling Policies**: LIFO, FIFO, Priority, LRU, Round-Robin retrieval strategies
  - Fluent API: `.WithFifoPolicy()`, `.WithPriorityPolicy()`, etc.
  - 5 built-in policies, custom policy support via `IPoolingPolicy<T>`
  - 28 new tests for policy implementations
- **IAsyncDisposable Support**: Proper async cleanup for modern resources
  - `ObjectPool<T>` and `QueryableObjectPool<T>` implement `IAsyncDisposable`
  - Automatic async disposal of pooled objects
  - Smart fallback to sync disposal when needed
  - 44 new tests for async operations
- **Async Validation**: Validate objects asynchronously on return
  - `.WithAsyncValidation()` for health checks, ping tests
  - `ReturnObjectAsync()` method added to `IObjectPool<T>`
  - Perfect for database connections, HTTP clients, gRPC channels
- **Enhanced AOT Support**: Native AOT and trimming compatibility
- **Package Quality**: SourceLink, package validation, code analyzers
- **230+ tests passing** (100% success rate)
  - Original 186 tests + 44 async tests + 28 policy tests

### 4.0.0 - December 2024
- **Complete Production-Ready Suite**: All enterprise features integrated and tested
- **Dependency Injection**: First-class ASP.NET Core and Generic Host support
- **Health Checks**: ASP.NET Core Health Checks integration with custom thresholds
- **OpenTelemetry**: Native metrics using System.Diagnostics.Metrics API
- **Pool Warm-up**: Pre-population for zero cold-start latency
- **Eviction / TTL**: Automatic stale object removal with configurable policies
- **Circuit Breaker**: Protection against cascading failures with automatic recovery
- **Lifecycle Hooks**: Custom object lifecycle management at all stages
- **Scoped Pools**: Multi-tenancy with per-tenant/user/context pool isolation
- **Monitoring Integration**: Native Prometheus, Grafana, Azure Monitor, AWS CloudWatch support
- **Fluent API**: Builder pattern for intuitive pool configuration
- **Service Integration**: Factory methods with dependency injection support
- **Kubernetes Ready**: Liveness and readiness probe support
- Comprehensive documentation for all features
- **186/186 tests passing** (100% success rate)
  - 83 original core tests
  - 12 dependency injection tests
  - 9 health check tests
  - 11 OpenTelemetry tests
  - 16 warm-up tests
  - 11 eviction tests
  - 16 circuit breaker tests
  - 12 lifecycle hooks tests
  - 16 scoped pools tests


### 3.1.0 - January 2025
- Individual feature releases leading to v4.0.0
- Various improvements and bug fixes

### 3.0.0 - November 2024
- Added support for .NET 10
- 20-40% performance improvement for queryable pool operations
- **Critical fix**: Eliminated race condition in `DynamicObjectPool` under high concurrency
- Modern C# 14 patterns: collection expressions, primary constructors, sealed classes
- 100% test pass rate (83/83 tests)
- Added Prometheus metrics exporter
- Production-ready certification

### 2.1.0
- Added PoolConfiguration for flexible pool behavior
- Improved health checks with utilization tracking
- Fixed async disposal for queryable pool
- Enhanced thread-safety for all pools

### 2.0.0
- Async support with `GetObjectAsync`
- Performance metrics and exporters
- Try* methods for safe retrieval
- Improved performance

### 1.1.5
- Improved thread-safety
- Dynamic pool throws if no match

### 1.1.3
- Added DynamicObjectPool

### 1.1.2
- Improved thread-safety

### 1.1.1
- Added QueryableObjectPool

## Documentation

- **[Pooling Policies Guide](docs/POOLING_POLICIES.md)** - NEW! LIFO, FIFO, Priority, LRU, Round-Robin strategies
- **[Async Operations Guide](docs/ASYNC_OPERATIONS.md)** - NEW! IAsyncDisposable and async validation
- **[Dependency Injection & Health Checks Guide](DEPENDENCY_INJECTION.md)** - Complete DI and health check integration guide
- **[Deployment Guide](DEPLOYMENT.md)** - Production deployment best practices
- **[Warm-up Implementation](WARMUP_IMPLEMENTATION.md)** - Pool warm-up strategies and examples
- **[Eviction Implementation](EVICTION_IMPLEMENTATION.md)** - TTL and eviction policies guide
- **[Circuit Breaker Implementation](CIRCUIT_BREAKER_IMPLEMENTATION.md)** - Circuit breaker pattern guide
- **[Lifecycle Hooks Implementation](LIFECYCLE_HOOKS_IMPLEMENTATION.md)** - Custom lifecycle management
- **[Examples](examples/)** - Code samples and use cases

## Production Use

This library is production-ready and suitable for:
- High-traffic web applications (ASP.NET Core)
- Microservices architectures
- Cloud deployments (Azure, AWS, Kubernetes)
- Container orchestration (Docker, Kubernetes)
- Enterprise systems with multi-tenancy
- Real-time applications
- Database connection pooling
- Network client pooling
- Mission-critical applications requiring circuit breaker protection
- Applications requiring automatic resource cleanup (eviction)
- Systems needing comprehensive observability (OpenTelemetry)

See [DEPLOYMENT.md](DEPLOYMENT.md) for production deployment guidance.

## Contributing

Contributions are welcome! Please ensure:
- All tests pass (`dotnet test`)
- Code follows existing patterns
- New features include tests
- Documentation is updated

## License

MIT License - See [LICENSE.txt](LICENSE.txt) for details.

---

For bug reports or suggestions, contact [info@esoxsolutions.nl](mailto:info@esoxsolutions.nl)

---

## Disclaimer

**Use of this software is at your own risk. The author(s) of EsoxSolutions.ObjectPool are not liable for any damages, losses, or other consequences resulting from the use, misuse, or inability to use this software.**

