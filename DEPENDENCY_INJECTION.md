# Dependency Injection Integration

## Overview

EsoxSolutions.ObjectPool v3.1 includes first-class support for ASP.NET Core and Generic Host dependency injection, making it easy to register and configure object pools in your applications.

## Installation

```bash
dotnet add package EsoxSolutions.ObjectPool
```

The package includes `Microsoft.Extensions.DependencyInjection.Abstractions` for DI support.

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

builder.Services.AddScoped<WeatherService>();

var app = builder.Build();

app.MapGet("/weather/{city}", async (string city, WeatherService service) =>
{
    var weather = await service.GetWeatherAsync(city);
    return Results.Ok(weather);
});

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

## Health Monitoring

The pools support health monitoring which can be integrated with ASP.NET Core health checks:

```csharp
// Check pool health
var pool = serviceProvider.GetRequiredService<IObjectPool<HttpClient>>();
var health = ((IPoolHealth)pool).GetHealthStatus();

if (!health.IsHealthy)
{
    _logger.LogWarning("Pool unhealthy: {Message}", health.HealthMessage);
}

// Get metrics
var metrics = ((IPoolMetrics)pool).ExportMetrics();
foreach (var (key, value) in metrics)
{
    _logger.LogInformation("{Key}: {Value}", key, value);
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

## Performance Tips

1. **Pre-populate pools** for warm-start scenarios
2. **Use validation sparingly** - it adds overhead
3. **Size pools appropriately** - too small causes contention, too large wastes memory
4. **Monitor metrics** in production to tune configuration
5. **Use `TryGetObject`** in high-throughput scenarios to avoid exceptions

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

- [Main README](../README.md)
- [Deployment Guide](../DEPLOYMENT.md)
- [Performance Benchmarks](BENCHMARKS.md)
