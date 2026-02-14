# Async Disposal and Validation Support

EsoxSolutions.ObjectPool provides comprehensive support for asynchronous operations, including async disposal (`IAsyncDisposable`), async validation, and async lifecycle hooks. This is essential for modern .NET applications dealing with async resources like database connections, network streams, and cloud services.

## Features

- ✅ **IAsyncDisposable Support**: Pool implements `IAsyncDisposable` for proper async cleanup
- ✅ **Async Object Disposal**: Automatically calls `DisposeAsync()` on pooled objects
- ✅ **Async Validation**: Validate returned objects asynchronously
- ✅ **Async Lifecycle Hooks**: Execute async logic during object lifecycle events
- ✅ **Backward Compatible**: Works alongside synchronous operations

---

## IAsyncDisposable Support

### Pool-Level Async Disposal

The `ObjectPool<T>` class implements `IAsyncDisposable`, allowing proper async cleanup:

```csharp
await using var pool = new DynamicObjectPool<AsyncResource>(
    () => new AsyncResource(),
    new PoolConfiguration { MaxPoolSize = 10 });

// Use the pool...

// Automatic async disposal when leaving scope
```

### Object-Level Async Disposal

Pooled objects implementing `IAsyncDisposable` are automatically disposed asynchronously:

```csharp
public class DatabaseConnection : IAsyncDisposable
{
    private SqlConnection _connection;

    public async ValueTask DisposeAsync()
    {
        if (_connection != null)
        {
            await _connection.CloseAsync();
            await _connection.DisposeAsync();
        }
    }
}

// Create pool
var pool = new DynamicObjectPool<DatabaseConnection>(
    () => new DatabaseConnection(),
    new PoolConfiguration { UseAsyncDisposal = true });

// When disposing the pool, all connections are closed asynchronously
await pool.DisposeAsync();
```

### Disposal Priority

When an object implements both `IDisposable` and `IAsyncDisposable`, the async version is preferred:

```csharp
public class HybridResource : IDisposable, IAsyncDisposable
{
    public void Dispose()
    {
        // Synchronous cleanup
        Console.WriteLine("Sync dispose");
    }

    public async ValueTask DisposeAsync()
    {
        // Asynchronous cleanup (this will be called)
        await Task.Delay(10);
        Console.WriteLine("Async dispose");
    }
}

await pool.DisposeAsync(); // Calls DisposeAsync(), not Dispose()
```

---

## Async Validation

Validate returned objects asynchronously, perfect for checking network connectivity, database state, or API availability.

### Basic Async Validation

```csharp
services.AddObjectPool<HttpClient>(builder => builder
    .WithFactory(() => new HttpClient())
    .WithAsyncValidation(async client =>
    {
        try
        {
            // Verify the connection is still alive
            var response = await client.GetAsync("https://api.example.com/health");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    })
    .WithMaxSize(50));
```

### Database Connection Validation

```csharp
services.AddObjectPool<SqlConnection>(builder => builder
    .WithFactory(() => new SqlConnection(connectionString))
    .WithAsyncValidation(async connection =>
    {
        try
        {
            // Check if connection is still open and working
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            // Ping the database
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            await command.ExecuteScalarAsync();
            
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Connection validation failed: {ex.Message}");
            return false;
        }
    })
    .WithMaxSize(20));
```

### Task-Based Validation

You can use either `ValueTask<bool>` or `Task<bool>`:

```csharp
// ValueTask version (more efficient)
builder.WithAsyncValidation(async resource =>
{
    await Task.Delay(1);
    return await resource.IsHealthyAsync();
});

// Task version (also supported)
builder.WithAsyncValidation(async resource =>
{
    await Task.Delay(1);
    return await resource.CheckStatusAsync();
});
```

### Validation Priority

When both sync and async validation are configured, async validation takes precedence:

```csharp
var config = new PoolConfiguration
{
    ValidateOnReturn = true,
    
    // This will be ignored if AsyncValidationFunction is set
    ValidationFunction = obj => obj.IsValid(),
    
    // This will be used
    AsyncValidationFunction = async obj => await obj.IsValidAsync()
};
```

### Returning Objects with Async Validation

```csharp
var pool = GetPool<HttpClient>();

// Get an object
var pooledClient = pool.GetObject();
var client = pooledClient.Unwrap();

try
{
    // Use the client
    await client.GetAsync("https://api.example.com/data");
}
finally
{
    // Return with async validation
    await pool.ReturnObjectAsync(pooledClient);
}
```

---

## Async Lifecycle Hooks

Execute async operations during object lifecycle events.

### Available Async Hooks

```csharp
public class LifecycleHooks<T>
{
    Func<T, Task>? OnCreateAsync { get; set; }      // Object creation
    Func<T, Task>? OnAcquireAsync { get; set; }     // Object acquisition
    Func<T, Task>? OnReturnAsync { get; set; }      // Object return
    Func<T, Task>? OnDisposeAsync { get; set; }     // Object disposal
}
```

### Complete Example

```csharp
services.AddObjectPool<DatabaseConnection>(builder => builder
    .WithFactory(() => new DatabaseConnection(connectionString))
    .WithAsyncLifecycleHooks(hooks =>
    {
        hooks.OnCreateAsync = async conn =>
        {
            // Initialize connection asynchronously
            await conn.OpenAsync();
            await conn.ExecuteAsync("SET SESSION CHARACTERISTICS AS TRANSACTION READ WRITE");
        };

        hooks.OnAcquireAsync = async conn =>
        {
            // Prepare connection for use
            if (conn.State != ConnectionState.Open)
            {
                await conn.OpenAsync();
            }
        };

        hooks.OnReturnAsync = async conn =>
        {
            // Clean up before returning to pool
            await conn.ExecuteAsync("ROLLBACK"); // Rollback any uncommitted transactions
        };

        hooks.OnDisposeAsync = async conn =>
        {
            // Clean shutdown
            await conn.CloseAsync();
            await conn.DisposeAsync();
        };
    })
    .WithMaxSize(50));
```

### gRPC Channel Example

```csharp
services.AddObjectPool<GrpcChannel>(builder => builder
    .WithFactory(() => GrpcChannel.ForAddress("https://api.example.com"))
    .WithAsyncLifecycleHooks(hooks =>
    {
        hooks.OnCreateAsync = async channel =>
        {
            // Warm up the channel
            await channel.ConnectAsync();
        };

        hooks.OnAcquireAsync = async channel =>
        {
            // Verify channel state
            var state = channel.State;
            if (state == ConnectivityState.Shutdown || state == ConnectivityState.TransientFailure)
            {
                // Attempt to reconnect
                await channel.ConnectAsync();
            }
        };

        hooks.OnDisposeAsync = async channel =>
        {
            // Graceful shutdown
            await channel.ShutdownAsync();
        };
    })
    .WithMaxSize(10));
```

### Mixed Sync and Async Hooks

You can use both sync and async hooks together:

```csharp
.WithAsyncLifecycleHooks(hooks =>
{
    // Sync hook
    hooks.OnCreate = resource =>
    {
        resource.Id = Guid.NewGuid();
    };

    // Async hook
    hooks.OnCreateAsync = async resource =>
    {
        await resource.InitializeAsync();
    };

    // Both will execute
})
```

---

## Complete Real-World Example

### Multi-Tenant Database Connection Pool

```csharp
public class TenantDatabaseConnection : IAsyncDisposable
{
    public string TenantId { get; set; }
    public SqlConnection Connection { get; set; }
    public DateTime LastUsed { get; set; }

    public async ValueTask DisposeAsync()
    {
        if (Connection != null)
        {
            await Connection.CloseAsync();
            await Connection.DisposeAsync();
        }
    }

    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            if (Connection.State != ConnectionState.Open)
                return false;

            using var cmd = Connection.CreateCommand();
            cmd.CommandText = "SELECT 1";
            cmd.CommandTimeout = 5;
            await cmd.ExecuteScalarAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }
}

// Configuration
services.AddObjectPool<TenantDatabaseConnection>(builder => builder
    .WithFactory(() => new TenantDatabaseConnection
    {
        TenantId = GetCurrentTenantId(),
        Connection = new SqlConnection(GetTenantConnectionString()),
        LastUsed = DateTime.UtcNow
    })
    .WithAsyncValidation(async conn =>
    {
        // Validate connection is still healthy
        var isHealthy = await conn.IsHealthyAsync();
        
        // Check if connection is too old
        var age = DateTime.UtcNow - conn.LastUsed;
        if (age > TimeSpan.FromMinutes(30))
        {
            return false;
        }

        return isHealthy;
    })
    .WithAsyncLifecycleHooks(hooks =>
    {
        hooks.OnCreateAsync = async conn =>
        {
            await conn.Connection.OpenAsync();
            
            // Set tenant context
            using var cmd = conn.Connection.CreateCommand();
            cmd.CommandText = $"SET SESSION tenant_id = '{conn.TenantId}'";
            await cmd.ExecuteNonQueryAsync();
        };

        hooks.OnReturnAsync = async conn =>
        {
            // Update last used timestamp
            conn.LastUsed = DateTime.UtcNow;
            
            // Clear any pending transactions
            using var cmd = conn.Connection.CreateCommand();
            cmd.CommandText = "ROLLBACK";
            await cmd.ExecuteNonQueryAsync();
        };

        hooks.OnDisposeAsync = async conn =>
        {
            await conn.DisposeAsync();
        };
    })
    .WithAsyncDisposal(true)
    .WithMaxSize(100)
    .WithMaxActive(50)
    .WithEviction(eviction => eviction
        .WithTimeToLive(TimeSpan.FromMinutes(30))
        .WithIdleTimeout(TimeSpan.FromMinutes(5)))
    .WithCircuitBreaker(cb => cb
        .WithFailureThreshold(5)
        .WithOpenDuration(TimeSpan.FromSeconds(30)))
    .WithHealthCheck()
    .WithMetrics());
```

### Usage

```csharp
public class TenantService
{
    private readonly IObjectPool<TenantDatabaseConnection> _connectionPool;

    public TenantService(IObjectPool<TenantDatabaseConnection> connectionPool)
    {
        _connectionPool = connectionPool;
    }

    public async Task<List<Order>> GetOrdersAsync(string customerId)
    {
        // Get connection from pool
        var pooled = _connectionPool.GetObject();
        var conn = pooled.Unwrap();

        try
        {
            using var cmd = conn.Connection.CreateCommand();
            cmd.CommandText = "SELECT * FROM Orders WHERE CustomerId = @CustomerId";
            cmd.Parameters.AddWithValue("@CustomerId", customerId);

            var orders = new List<Order>();
            using var reader = await cmd.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                orders.Add(MapOrder(reader));
            }

            return orders;
        }
        finally
        {
            // Return with async validation
            await _connectionPool.ReturnObjectAsync(pooled);
        }
    }
}
```

---

## Configuration Reference

### UseAsyncDisposal

```csharp
var config = new PoolConfiguration
{
    UseAsyncDisposal = true  // Default: true
};
```

When `true`, objects implementing `IAsyncDisposable` are disposed asynchronously.

### AsyncValidationFunction

```csharp
var config = new PoolConfiguration
{
    ValidateOnReturn = true,
    AsyncValidationFunction = async obj => 
    {
        await Task.Delay(1);
        return ((MyResource)obj).IsValid;
    }
};
```

Async validation function that returns `ValueTask<bool>`.

---

## Best Practices

### 1. Prefer Async Disposal for I/O Resources

```csharp
✅ Good - Async disposal for network resources
public class ApiClient : IAsyncDisposable
{
    private HttpClient _client;

    public async ValueTask DisposeAsync()
    {
        await _client.DisposeAsync(); // Graceful shutdown
    }
}

❌ Bad - Blocking sync disposal
public class ApiClient : IDisposable
{
    private HttpClient _client;

    public void Dispose()
    {
        _client.Dispose(); // May block
    }
}
```

### 2. Use Async Validation for Network Checks

```csharp
✅ Good - Async validation
.WithAsyncValidation(async client =>
{
    try
    {
        await client.GetAsync("/health");
        return true;
    }
    catch { return false; }
})

❌ Bad - Blocking sync validation
.WithValidation(client =>
{
    return client.GetAsync("/health").Result.IsSuccessStatusCode; // Blocks!
})
```

### 3. Handle Validation Failures Gracefully

```csharp
.WithAsyncValidation(async resource =>
{
    try
    {
        return await resource.HealthCheckAsync();
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "Validation failed for resource");
        return false; // Invalid object will be removed from pool
    }
})
```

### 4. Use Timeout for Async Operations

```csharp
.WithAsyncValidation(async resource =>
{
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    try
    {
        await resource.PingAsync(cts.Token);
        return true;
    }
    catch (OperationCanceledException)
    {
        return false; // Timeout = invalid
    }
})
```

### 5. Leverage await using for Automatic Cleanup

```csharp
await using var serviceProvider = services.BuildServiceProvider();
var pool = serviceProvider.GetRequiredService<IObjectPool<AsyncResource>>();

// Use pool...

// Automatic async disposal of both provider and pool
```

---

## Performance Considerations

- **Async overhead**: Async operations have a small overhead; use for I/O-bound operations
- **Validation frequency**: Async validation runs on every return; optimize for fast checks
- **Disposal batching**: The pool disposes all objects concurrently for better performance
- **ValueTask**: The pool uses `ValueTask` instead of `Task` for better memory efficiency

---

## Migration Guide

### From Sync to Async

**Before**:
```csharp
using var pool = new ObjectPool<Resource>(resources);
var pooled = pool.GetObject();
// Use...
pool.ReturnObject(pooled);
pool.Dispose();
```

**After**:
```csharp
await using var pool = new ObjectPool<AsyncResource>(resources);
var pooled = pool.GetObject();
// Use...
await pool.ReturnObjectAsync(pooled);
await pool.DisposeAsync(); // or automatic with await using
```

### Adding Async Validation

**Before**:
```csharp
.WithValidation(resource => resource.IsValid)
```

**After**:
```csharp
.WithAsyncValidation(async resource => await resource.IsValidAsync())
```

---

## Troubleshooting

### Common Issues

**Issue**: "Deadlock when using async disposal"
**Solution**: Always use `await pool.DisposeAsync()`, never `pool.DisposeAsync().GetAwaiter().GetResult()`

**Issue**: "Validation failures not being logged"
**Solution**: Configure logging and check validation function exceptions

**Issue**: "Objects not being disposed asynchronously"
**Solution**: Ensure `UseAsyncDisposal = true` and objects implement `IAsyncDisposable`

---

## See Also

- [Lifecycle Hooks Documentation](LIFECYCLE_HOOKS.md)
- [Health Checks Integration](HEALTH_CHECKS.md)
- [Circuit Breaker Pattern](CIRCUIT_BREAKER.md)
