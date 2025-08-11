# EsoxSolutions.ObjectPool

## Overview

EsoxSolutions.ObjectPool is a high-performance, thread-safe object pool for .NET 8+ and .NET 9. It supports automatic return of objects, async operations, health monitoring, performance metrics, and flexible configuration. Useful for pooling expensive resources like database connections, network clients, or reusable buffers.

## Features
    
- Thread-safe object pooling
- Automatic return of objects (via IDisposable)
- Async support (`GetObjectAsync`, `TryGetObjectAsync`)
- Queryable and dynamic pools
- Health monitoring and status
- Performance metrics (exportable, Prometheus format)
- Pool configuration (max size, validation, etc.)
- Try* methods for safe retrieval
- Multithreaded and high-performance (Stack/HashSet)

## Usage

### PoolModel
A generic wrapper for pooled objects. Use `Unwrap()` to access the value.

```csharp
var pool = new ObjectPool<int>(new List<int> { 1, 2, 3 });
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
Query for objects matching a predicate.

```csharp
var pool = new QueryableObjectPool<int>(new List<int> { 1, 2, 3 });
using (var model = pool.GetObject(x => x == 2))
{
    Console.WriteLine(model.Unwrap());
}
```

### DynamicObjectPool
Creates objects on the fly using a factory method.

```csharp
var pool = new DynamicObjectPool<int>(() => 42);
using (var model = pool.GetObject())
{
    Console.WriteLine(model.Unwrap());
}
```

### Health Monitoring & Metrics

```csharp
var health = pool.GetHealthStatus();
Console.WriteLine($"Healthy: {health.IsHealthy}, Warnings: {health.WarningCount}");

var metrics = pool.ExportMetrics();
foreach (var kv in metrics)
    Console.WriteLine($"{kv.Key}: {kv.Value}");

var prometheus = pool.ExportPrometheusMetrics();
Console.WriteLine(prometheus);
```

## Version history:
* 2.0.0: Async support, performance metrics, Try* methods, Prometheus metrics, improved performance
* 1.1.5: Improved thread-safety, dynamic pool throws if no match
* 1.1.3: Added DynamicObjectPool
* 1.1.2: Improved threadsafety
* 1.1.1: Added QueryableObjectPool

## Future work
- Timeout/disposal for idle objects
- More advanced health checks
- Integration with dependency injection

---
For bug reports or suggestions, contact [info@esoxsolutions.nl](info@esoxsolutions.nl)

