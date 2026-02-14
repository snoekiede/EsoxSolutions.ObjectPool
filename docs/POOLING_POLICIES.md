# Pooling Policies

Pooling policies determine how objects are stored in and retrieved from the object pool. Different policies are optimized for different use cases.

## Available Policies

### 1. LIFO (Last-In-First-Out) - Default

**Best for**: Cache locality, temporal locality patterns

Objects most recently returned to the pool are retrieved first. This is the default policy and provides the best performance for most scenarios.

```csharp
// Configuration
var config = new PoolConfiguration
{
    PoolingPolicyType = PoolingPolicyType.Lifo
};

// Dependency Injection
services.AddObjectPool<HttpClient>(builder => builder
    .WithFactory(() => new HttpClient())
    .WithLifoPolicy()
    .WithMaxSize(100));
```

**Advantages**:
- Best cache locality
- Optimal CPU cache utilization
- Highest performance (O(1) operations)

**Use cases**:
- HTTP connection pooling
- Database connection pooling
- General-purpose object pooling

---

### 2. FIFO (First-In-First-Out)

**Best for**: Fair scheduling, round-robin distribution, preventing object aging

Objects are retrieved in the order they were returned to the pool, ensuring all objects get equal usage.

```csharp
// Configuration
var config = new PoolConfiguration
{
    PoolingPolicyType = PoolingPolicyType.Fifo
};

// Dependency Injection
services.AddObjectPool<DatabaseConnection>(builder => builder
    .WithFactory(() => CreateDatabaseConnection())
    .WithFifoPolicy()
    .WithMaxSize(50));
```

**Advantages**:
- Fair object distribution
- Prevents object starvation
- Ensures all objects are exercised

**Use cases**:
- Connection keep-alive scenarios
- Load balancing across multiple backends
- Preventing connection staleness

---

### 3. Priority-based

**Best for**: Quality-of-service requirements, tenant-based prioritization, resource quality tiers

Objects with higher priority values are retrieved first. Requires a priority selector function.

```csharp
// Configuration
var config = new PoolConfiguration
{
    PoolingPolicyType = PoolingPolicyType.Priority,
    PrioritySelector = new Func<Connection, int>(conn => conn.QualityScore)
};

// Dependency Injection
services.AddObjectPool<PremiumConnection>(builder => builder
    .WithFactory(() => CreateConnection())
    .WithPriorityPolicy(conn => conn.Priority)
    .WithMaxSize(100));
```

**Advantages**:
- Quality-of-service support
- Tenant tier differentiation
- Resource quality optimization

**Use cases**:
- Multi-tenant applications with SLA tiers
- Connection pooling with quality metrics
- Resource pools with varying performance characteristics

---

### 4. Least Recently Used (LRU)

**Best for**: Preventing staleness, ensuring all objects get used, connection keep-alive

Objects that haven't been used for the longest time are retrieved first.

```csharp
// Configuration
var config = new PoolConfiguration
{
    PoolingPolicyType = PoolingPolicyType.LeastRecentlyUsed
};

// Dependency Injection
services.AddObjectPool<GrpcChannel>(builder => builder
    .WithFactory(() => CreateGrpcChannel())
    .WithLeastRecentlyUsedPolicy()
    .WithMaxSize(20));
```

**Advantages**:
- Prevents object staleness
- Natural keep-alive behavior
- Ensures all objects remain active

**Use cases**:
- Long-lived connections that need keep-alive
- Preventing timeout on idle connections
- WebSocket connection pooling

---

### 5. Round-Robin

**Best for**: Load balancing, even wear distribution, multi-endpoint pooling

Objects are retrieved in a circular fashion, ensuring even distribution of load.

```csharp
// Configuration
var config = new PoolConfiguration
{
    PoolingPolicyType = PoolingPolicyType.RoundRobin
};

// Dependency Injection
services.AddObjectPool<ServiceClient>(builder => builder
    .WithFactory(() => CreateServiceClient())
    .WithRoundRobinPolicy()
    .WithMaxSize(10));
```

**Advantages**:
- Perfect load distribution
- Even wear across all objects
- Simple and predictable behavior

**Use cases**:
- Multi-endpoint service clients
- Load balancing scenarios
- Hardware resource pooling (GPUs, specialized devices)

---

## Policy Comparison

| Policy | Retrieval Strategy | Performance | Use Case |
|--------|-------------------|-------------|----------|
| **LIFO** | Most recent first | ⭐⭐⭐⭐⭐ | General purpose |
| **FIFO** | Oldest first | ⭐⭐⭐⭐ | Fair distribution |
| **Priority** | Highest priority first | ⭐⭐⭐ | QoS tiers |
| **LRU** | Least recently used first | ⭐⭐⭐ | Keep-alive |
| **Round-Robin** | Circular distribution | ⭐⭐⭐⭐ | Load balancing |

---

## Complete Example: Multi-Tenant Application

```csharp
// Define connection with priority
public class TenantConnection
{
    public string TenantId { get; set; }
    public TenantTier Tier { get; set; }
    public int Priority => Tier switch
    {
        TenantTier.Premium => 10,
        TenantTier.Standard => 5,
        TenantTier.Free => 1,
        _ => 0
    };
}

public enum TenantTier
{
    Free,
    Standard,
    Premium
}

// Configure in Startup.cs
services.AddObjectPool<TenantConnection>(builder => builder
    .WithFactory(() => new TenantConnection())
    .WithPriorityPolicy(conn => conn.Priority)
    .WithMaxSize(100)
    .WithMaxActive(50)
    .WithWarmup(20)
    .WithEviction(eviction => eviction
        .WithTimeToLive(TimeSpan.FromMinutes(30))
        .WithIdleTimeout(TimeSpan.FromMinutes(5)))
    .WithCircuitBreaker(cb => cb
        .WithFailureThreshold(5)
        .WithOpenDuration(TimeSpan.FromSeconds(30)))
    .WithHealthCheck()
    .WithMetrics());

// Usage
public class TenantService
{
    private readonly IObjectPool<TenantConnection> _connectionPool;

    public TenantService(IObjectPool<TenantConnection> connectionPool)
    {
        _connectionPool = connectionPool;
    }

    public async Task<Result> ExecuteQueryAsync(string tenantId)
    {
        // Premium tenants get priority access
        using var pooled = await _connectionPool.GetObjectAsync();
        var connection = pooled.Unwrap();
        
        // Use connection...
        return await connection.ExecuteAsync();
    }
}
```

---

## Advanced: Custom Policy

You can create custom pooling policies by implementing `IPoolingPolicy<T>`:

```csharp
public class WeightedRandomPolicy<T> : IPoolingPolicy<T> where T : notnull
{
    private readonly List<(T item, double weight)> _items = new();
    private readonly Random _random = new();
    private readonly object _lock = new();

    public string PolicyName => "WeightedRandom";
    public int Count => _items.Count;

    public void Add(T item)
    {
        lock (_lock)
        {
            var weight = 1.0; // Could be computed based on item properties
            _items.Add((item, weight));
        }
    }

    public bool TryTake(out T? item)
    {
        lock (_lock)
        {
            if (_items.Count == 0)
            {
                item = default;
                return false;
            }

            var totalWeight = _items.Sum(x => x.weight);
            var randomValue = _random.NextDouble() * totalWeight;
            var cumulativeWeight = 0.0;

            for (int i = 0; i < _items.Count; i++)
            {
                cumulativeWeight += _items[i].weight;
                if (randomValue <= cumulativeWeight)
                {
                    item = _items[i].item;
                    _items.RemoveAt(i);
                    return true;
                }
            }

            item = _items[^1].item;
            _items.RemoveAt(_items.Count - 1);
            return true;
        }
    }

    public void Clear() => _items.Clear();
    public IEnumerable<T> GetAll() => _items.Select(x => x.item);
}
```

---

## Performance Considerations

### LIFO (Fastest)
- O(1) push and pop operations
- Excellent CPU cache locality
- Minimal memory allocations

### FIFO
- O(1) enqueue and dequeue operations
- Good memory efficiency
- Slightly more cache misses than LIFO

### Priority
- O(log n) enqueue and dequeue operations
- Requires locking for thread safety
- Higher memory overhead

### LRU
- O(n) for finding least recently used
- Timestamp tracking overhead
- Requires locking for thread safety

### Round-Robin
- O(1) operations
- Good memory efficiency
- Predictable behavior

---

## Choosing the Right Policy

**Use LIFO (default) when**:
- You want maximum performance
- Cache locality is important
- Objects are stateless or short-lived

**Use FIFO when**:
- You need fair distribution
- Preventing object staleness is critical
- Implementing keep-alive patterns

**Use Priority when**:
- You have QoS requirements
- Different resource quality tiers exist
- Tenant-based differentiation is needed

**Use LRU when**:
- Connections have keep-alive requirements
- You want to prevent idle timeouts
- Natural rotation of all objects is desired

**Use Round-Robin when**:
- Load balancing is the primary goal
- You have multiple equivalent endpoints
- Even wear distribution is critical

---

## Metrics and Monitoring

All policies expose the policy name in metrics:

```csharp
var metrics = pool.ExportMetrics();
Console.WriteLine($"Policy: {metrics["policy_name"]}");
Console.WriteLine($"Available: {metrics["available_current"]}");
Console.WriteLine($"Active: {metrics["active_current"]}");
```

Health checks also include policy information:

```csharp
var health = pool.GetHealthStatus();
Console.WriteLine($"Policy: {health.Diagnostics["policy_name"]}");
Console.WriteLine($"Health: {health.HealthMessage}");
```
