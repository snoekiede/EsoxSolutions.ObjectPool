# Fix for WithAutoWarmup DI Error - Version 4.0.0

## Problem

When using `WithAutoWarmup()` or `WithAutoWarmupPercentage()` extension methods in ASP.NET applications, users were getting this error:

```
System.InvalidOperationException: 'No service for type 'EsoxSolutions.ObjectPool.Warmup.IObjectPoolWarmer`1[System.Net.Http.HttpClient]' has been registered.'
```

## Root Cause

When `AddDynamicObjectPool<T>()` registered a pool, it only registered the `IObjectPool<T>` interface. The `WithAutoWarmup` and `WithAutoWarmupPercentage` extensions require `IObjectPoolWarmer<T>` to be registered in the DI container, but this interface was not being registered.

## Solution

### Code Changes

**File: `EsoxSolutions.ObjectPool/DependencyInjection/ServiceCollectionExtensions.cs`**

1. **Added using directive** for `EsoxSolutions.ObjectPool.Warmup` namespace
2. **Modified `AddDynamicObjectPool` method** to register the pool as both interfaces:

```csharp
public IServiceCollection AddDynamicObjectPool<T>(
    Func<IServiceProvider, T> factory,
    Action<PoolConfiguration>? configure = null) where T : class
{
    // Register the concrete DynamicObjectPool<T> first
    services.TryAddSingleton<DynamicObjectPool<T>>(sp =>
    {
        var config = new PoolConfiguration();
        configure?.Invoke(config);
        var logger = sp.GetService<ILogger<ObjectPool<T>>>();
        T PoolFactory() => factory(sp);
        return new DynamicObjectPool<T>(PoolFactory, [], config, logger);
    });

    // Register as IObjectPool<T>
    services.TryAddSingleton<IObjectPool<T>>(sp => 
        sp.GetRequiredService<DynamicObjectPool<T>>());

    // Register as IObjectPoolWarmer<T> for warm-up support
    services.TryAddSingleton<IObjectPoolWarmer<T>>(sp => 
        sp.GetRequiredService<DynamicObjectPool<T>>());

    return services;
}
```

This ensures:
- ? `DynamicObjectPool<T>` is created once (singleton)
- ? `IObjectPool<T>` resolves to the same instance
- ? `IObjectPoolWarmer<T>` resolves to the same instance
- ? All interfaces point to the same pool object

### Correct Usage

When using warm-up extensions, you **must specify the type parameter explicitly**:

```csharp
// ? CORRECT - with explicit type parameter
builder.Services.AddDynamicObjectPool<HttpClient>(
    sp => new HttpClient(),
    config => config.MaxPoolSize = 100)
    .WithAutoWarmup<HttpClient>(50);

// ? WRONG - type cannot be inferred
builder.Services.AddDynamicObjectPool<HttpClient>(
    sp => new HttpClient(),
    config => config.MaxPoolSize = 100)
    .WithAutoWarmup(50); // Error: type arguments cannot be inferred
```

## Test Coverage

Added comprehensive integration tests in `WarmupDIIntegrationTests.cs`:

1. **AddDynamicObjectPool_RegistersIObjectPoolWarmer** - Verifies `IObjectPoolWarmer<T>` is registered
2. **WithAutoWarmup_WarmsUpPoolOnStartup** - Tests automatic warm-up on startup
3. **WithAutoWarmupPercentage_WarmsUpPoolOnStartup** - Tests percentage-based warm-up
4. **CanResolveAllPoolInterfaces** - Verifies all interfaces resolve to same instance

## Complete Working Example

```csharp
using EsoxSolutions.ObjectPool.DependencyInjection;
using EsoxSolutions.ObjectPool.HealthChecks;
using OpenTelemetry.Metrics;

var builder = WebApplication.CreateBuilder(args);

// Register HTTP client pool with warm-up
builder.Services.AddDynamicObjectPool<HttpClient>(
    sp => new HttpClient { Timeout = TimeSpan.FromSeconds(30) },
    config =>
    {
        config.MaxPoolSize = 100;
        config.MaxActiveObjects = 50;
    })
    .WithAutoWarmup<HttpClient>(25); // Pre-create 25 HTTP clients

// Register database connection pool with percentage warm-up
builder.Services.AddDynamicObjectPool<SqlConnection>(
    sp =>
    {
        var connString = builder.Configuration.GetConnectionString("DefaultConnection");
        var conn = new SqlConnection(connString);
        conn.Open();
        return conn;
    },
    config =>
    {
        config.MaxPoolSize = 50;
        config.MaxActiveObjects = 30;
        config.ValidateOnReturn = true;
        config.ValidationFunction = obj => 
            ((SqlConnection)obj).State == ConnectionState.Open;
    })
    .WithAutoWarmupPercentage<SqlConnection>(80); // Pre-create 80% of capacity

// Add health checks
builder.Services.AddHealthChecks()
    .AddObjectPoolHealthCheck<HttpClient>("http-client-pool")
    .AddObjectPoolHealthCheck<SqlConnection>("database-pool");

// Add OpenTelemetry metrics
builder.Services.AddObjectPoolMetrics<HttpClient>("http-client-pool");
builder.Services.AddObjectPoolMetrics<SqlConnection>("database-pool");

builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics
        .AddMeter("EsoxSolutions.ObjectPool")
        .AddPrometheusExporter());

var app = builder.Build();

app.MapHealthChecks("/health");
app.MapPrometheusScrapingEndpoint();

// Use the pool in your services
app.MapGet("/api/data", async (IObjectPool<HttpClient> clientPool) =>
{
    using var pooledClient = clientPool.GetObject();
    var client = pooledClient.Unwrap();
    var response = await client.GetStringAsync("https://api.example.com/data");
    return Results.Ok(response);
});

app.Run();
```

## Why Explicit Type Parameters?

The warm-up extensions are defined as:

```csharp
extension(IServiceCollection services)
{
    public IServiceCollection WithAutoWarmup<T>(int targetSize) where T : class
    {
        services.AddHostedService(sp => 
            new PoolWarmupHostedService<T>(
                sp.GetRequiredService<IObjectPoolWarmer<T>>(),
                // ...
            ));
        return services;
    }
}
```

Since `AddDynamicObjectPool<T>` returns `IServiceCollection` (which has no type information), C# cannot infer the type parameter `T` for the chained `WithAutoWarmup` call. Therefore, you must explicitly specify it: `.WithAutoWarmup<HttpClient>(50)`.

## Alternative Pattern

If you prefer not to use explicit type parameters, you can manually register warm-up:

```csharp
// Register pool
builder.Services.AddDynamicObjectPool<HttpClient>(
    sp => new HttpClient(),
    config => config.MaxPoolSize = 100);

// Manually configure warm-up
builder.Services.AddHostedService(sp =>
{
    var warmer = sp.GetRequiredService<IObjectPoolWarmer<HttpClient>>();
    var logger = sp.GetService<ILogger<PoolWarmupHostedService<HttpClient>>>();
    return new PoolWarmupHostedService<HttpClient>(warmer, logger, targetSize: 50, null);
});
```

## Breaking Changes

None - this is a bug fix that enables the intended functionality. Code that wasn't working before will now work correctly.

## Migration Guide

If you were using warm-up before and it was failing:

**Before** (Not working):
```csharp
services.AddDynamicObjectPool<HttpClient>(sp => new HttpClient())
    .WithAutoWarmup(50); // Error!
```

**After** (Working):
```csharp
services.AddDynamicObjectPool<HttpClient>(sp => new HttpClient())
    .WithAutoWarmup<HttpClient>(50); // ?
```

## Test Results

```
? All 190 tests passing (100% success rate)
   - 83 original core tests
   - 12 dependency injection tests
   - 9 health check tests
   - 11 OpenTelemetry tests
   - 16 warm-up tests
   - 11 eviction tests
   - 16 circuit breaker tests
   - 12 lifecycle hooks tests
   - 16 scoped pools tests
   - 4 NEW warm-up DI integration tests ?
```

## Related Documentation

- [WARMUP_IMPLEMENTATION.md](WARMUP_IMPLEMENTATION.md) - Detailed warm-up feature guide
- [DEPENDENCY_INJECTION.md](DEPENDENCY_INJECTION.md) - Complete DI integration guide
- [README.md](README.md) - Main documentation with examples

---

**Issue Fixed in Version 4.0.0** ?

The `WithAutoWarmup` and `WithAutoWarmupPercentage` extensions now work correctly when used with `AddDynamicObjectPool`. Remember to always specify the type parameter explicitly when calling these methods.
