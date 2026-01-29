# Scoped Pools Implementation Guide - Version 4.0.0

## ? **Successfully Implemented!**

All 190 tests passing (100% success rate)
- 83 original core tests
- 12 dependency injection tests  
- 9 health check tests
- 11 OpenTelemetry tests
- 16 warm-up tests
- 11 eviction tests
- 16 circuit breaker tests
- 12 lifecycle hooks tests
- **16 scoped pools tests** ?
- 4 warm-up DI integration tests

---

## ?? **New Files Created**

### 1. **PoolScope.cs** - Scope Models and Configuration
- `PoolScope` class for identifying scopes (tenant, user, context)
- `ScopeResolutionStrategy` enum (Ambient, HttpContext, DI, Custom)
- `ScopedPoolConfiguration` for configuring scoped pool behavior
- `ScopedPoolStatistics` for tracking multi-tenant metrics
- `AmbientPoolScope` for AsyncLocal context-based scoping

### 2. **ScopedPoolManager.cs** - Core Scoped Pool Management
- Manages multiple pools per scope/tenant
- Automatic inactive scope cleanup
- Per-scope statistics tracking
- Configurable scope resolution strategies
- Thread-safe concurrent scope management

### 3. **ScopedPoolExtensions.cs** - DI Integration
- `AddScopedObjectPool<T>()` - Register scoped pool manager
- `AddTenantScopedObjectPool<T>()` - Tenant-specific pools
- `AddAmbientScopedObjectPool<T>()` - Ambient scope pools
- `AddCustomScopedObjectPool<T>()` - Custom scope resolution

### 4. **ScopedPoolTests.cs** - Comprehensive Test Suite
- 16 tests covering all scenarios
- Tenant/user/context scoping tested
- Cleanup and lifecycle verified
- DI integration validated

---

## ?? **Usage Examples**

### Basic Tenant-Based Scoped Pool

```csharp
using EsoxSolutions.ObjectPool.DependencyInjection;
using EsoxSolutions.ObjectPool.Scoping;

var builder = WebApplication.CreateBuilder(args);

// Register tenant-scoped pools
builder.Services.AddTenantScopedObjectPool<DbConnection>(
    (sp, tenantId) => 
    {
        var connString = GetConnectionStringForTenant(tenantId);
        return new SqlConnection(connString);
    });

var app = builder.Build();
app.Run();
```

### Usage in Your Service

```csharp
public class TenantDataService
{
    private readonly ScopedPoolManager<DbConnection> _poolManager;
    
    public TenantDataService(ScopedPoolManager<DbConnection> poolManager)
    {
        _poolManager = poolManager;
    }
    
    public async Task<List<Order>> GetOrdersAsync(string tenantId)
    {
        var scope = PoolScope.FromTenant(tenantId);
        using var connection = _poolManager.GetObjectForScope(scope);
        
        // Connection is from tenant-specific pool
        var conn = connection.Unwrap();
        // ... execute query ...
    }
}
```

### Ambient Scope Pattern

```csharp
// Register with ambient scope resolution
builder.Services.AddAmbientScopedObjectPool<HttpClient>(
    sp => new HttpClient());

// In middleware - set scope from HTTP headers
app.Use(async (context, next) =>
{
    var tenantId = context.Request.Headers["X-Tenant-Id"].FirstOrDefault();
    if (!string.IsNullOrEmpty(tenantId))
    {
        using (AmbientPoolScope.BeginScope(PoolScope.FromTenant(tenantId)))
        {
            await next();
        }
    }
    else
    {
        await next();
    }
});

// In your service - automatically uses correct scope
public class ApiService
{
    private readonly ScopedPoolManager<HttpClient> _poolManager;
    
    public async Task<string> CallApiAsync()
    {
        // Automatically resolves to current tenant's pool
        using var client = _poolManager.GetObject();
        // ...
    }
}
```

### Custom Scope Resolution

```csharp
// Register with custom scope resolver
builder.Services.AddCustomScopedObjectPool<ServiceClient>(
    (sp, scope) => new ServiceClient(scope.TenantId),
    scopeResolver: () =>
    {
        // Custom logic to resolve current scope
        var httpContext = GetCurrentHttpContext();
        var tenantId = httpContext.User.FindFirst("tenant_id")?.Value;
        var userId = httpContext.User.FindFirst("sub")?.Value;
        return new PoolScope($"user:{userId}", tenantId, userId);
    });
```

### Advanced Multi-Tenant Configuration

```csharp
builder.Services.AddScopedObjectPool<DbConnection>(
    (sp, scope) =>
    {
        var config = sp.GetRequiredService<IConfiguration>();
        var connString = config[$"ConnectionStrings:Tenant_{scope.TenantId}"];
        var connection = new SqlConnection(connString);
        connection.Open();
        return connection;
    },
    configurePool: config =>
    {
        config.MaxPoolSize = 50;
        config.MaxActiveObjects = 25;
        config.ValidateOnReturn = true;
        config.ValidationFunction = obj => 
            ((SqlConnection)obj).State == ConnectionState.Open;
    },
    configureScoping: scopeConfig =>
    {
        scopeConfig.MaxScopes = 100;
        scopeConfig.ScopeIdleTimeout = TimeSpan.FromMinutes(30);
        scopeConfig.EnableAutomaticCleanup = true;
        scopeConfig.CleanupInterval = TimeSpan.FromMinutes(5);
        scopeConfig.OnScopeCreated = scope => 
            logger.LogInformation("Created pool for scope: {Scope}", scope);
        scopeConfig.OnScopeDisposed = scope => 
            logger.LogInformation("Disposed pool for scope: {Scope}", scope);
    });
```

---

## ?? **Key Features**

### 1. Multiple Isolation Strategies
- **Tenant-based**: Separate pools per tenant
- **User-based**: Separate pools per user
- **Context-based**: Separate pools per request/session/custom context

### 2. Automatic Scope Management
- Automatic creation of pools for new scopes
- Automatic cleanup of inactive scopes
- Configurable idle timeout
- Maximum scope limits

### 3. Flexible Scope Resolution
- **HttpContext**: Extract from HTTP headers or claims
- **Ambient**: Use AsyncLocal context
- **DependencyInjection**: Integrate with scoped services
- **Custom**: Implement your own resolution logic

### 4. Statistics and Monitoring
- Per-scope statistics
- Global scoped pool statistics
- Access count tracking
- Idle time tracking

---

## ?? **Configuration Options**

### ScopedPoolConfiguration

```csharp
var scopingConfig = new ScopedPoolConfiguration
{
    // Resolution strategy
    ResolutionStrategy = ScopeResolutionStrategy.HttpContext,
    CustomScopeResolver = null,
    
    // Capacity limits
    MaxScopes = 100,                            // Max concurrent scopes
    
    // Cleanup settings
    ScopeIdleTimeout = TimeSpan.FromMinutes(30), // Idle before cleanup
    EnableAutomaticCleanup = true,              // Auto cleanup
    CleanupInterval = TimeSpan.FromMinutes(5),  // Cleanup frequency
    DisposePoolsOnCleanup = true,               // Dispose pools
    
    // HTTP/Claims settings
    TenantHeaderName = "X-Tenant-Id",           // HTTP header name
    TenantClaimType = "tenant_id",              // JWT claim type
    
    // Lifecycle callbacks
    OnScopeCreated = scope => { /* ... */ },
    OnScopeDisposed = scope => { /* ... */ }
};
```

---

## ?? **Statistics and Monitoring**

### Get Statistics for All Scopes

```csharp
var stats = scopedPoolManager.GetStatistics();

Console.WriteLine($"Active Scopes: {stats.ActiveScopes}");
Console.WriteLine($"Total Created: {stats.TotalScopesCreated}");
Console.WriteLine($"Peak Scopes: {stats.PeakScopes}");
Console.WriteLine($"Total Objects: {stats.TotalObjects}");
Console.WriteLine($"Avg Objects/Scope: {stats.AverageObjectsPerScope:F2}");

foreach (var kvp in stats.ScopeAccessCounts)
{
    Console.WriteLine($"Scope {kvp.Key}: {kvp.Value} accesses");
}
```

### Get Statistics for Specific Scope

```csharp
var scope = PoolScope.FromTenant("tenant1");
var scopeStats = scopedPoolManager.GetScopeStatistics(scope);

if (scopeStats != null)
{
    Console.WriteLine($"Scope ID: {scopeStats["scope_id"]}");
    Console.WriteLine($"Tenant ID: {scopeStats["tenant_id"]}");
    Console.WriteLine($"Last Access: {scopeStats["last_access"]}");
    Console.WriteLine($"Idle Seconds: {scopeStats["idle_seconds"]}");
}
```

### Manual Scope Management

```csharp
// Manually trigger cleanup
scopedPoolManager.TriggerCleanup();

// Remove specific scope
var removed = scopedPoolManager.RemoveScope(scope);

// Get all active scopes
var activeScopes = scopedPoolManager.GetActiveScopes();
foreach (var s in activeScopes)
{
    Console.WriteLine($"Active scope: {s}");
}
```

---

## ?? **Real-World Scenarios**

### Scenario 1: Multi-Tenant SaaS Application

```csharp
// Startup
builder.Services.AddTenantScopedObjectPool<DbConnection>(
    (sp, tenantId) => CreateTenantConnection(tenantId));

// Middleware to extract tenant
app.Use(async (context, next) =>
{
    var tenantId = context.Request.Headers["X-Tenant-Id"].FirstOrDefault()
                   ?? context.User.FindFirst("tenant_id")?.Value
                   ?? "default";
    
    using (AmbientPoolScope.BeginScope(PoolScope.FromTenant(tenantId)))
    {
        await next();
    }
});

// Service automatically uses correct tenant's pool
public class OrderService
{
    private readonly ScopedPoolManager<DbConnection> _poolManager;
    
    public async Task<Order> GetOrderAsync(int orderId)
    {
        using var connection = _poolManager.GetObject(); // Uses current tenant's pool
        // ... query order ...
    }
}
```

### Scenario 2: User-Specific Resource Pools

```csharp
// Register user-scoped pools
builder.Services.AddScopedObjectPool<UserSession>(
    (sp, scope) => new UserSession(scope.UserId));

// In your service
public class UserActivityTracker
{
    private readonly ScopedPoolManager<UserSession> _sessionManager;
    
    public async Task TrackActivityAsync(string userId, string activity)
    {
        var scope = PoolScope.FromUser(userId);
        using var session = _sessionManager.GetObjectForScope(scope);
        
        session.Unwrap().RecordActivity(activity);
    }
}
```

### Scenario 3: Request-Context Isolation

```csharp
// Register context-scoped pools
builder.Services.AddScopedObjectPool<RequestContext>(
    (sp, scope) => new RequestContext(scope.Id),
    configureScoping: config =>
    {
        config.ResolutionStrategy = ScopeResolutionStrategy.Custom;
        config.CustomScopeResolver = () =>
        {
            var requestId = Activity.Current?.Id ?? Guid.NewGuid().ToString();
            return PoolScope.FromContext(requestId);
        };
    });
```

---

## ?? **Testing Scoped Pools**

### Unit Testing with Explicit Scopes

```csharp
[Fact]
public void MultiTenant_EachTenantGetsOwnPool()
{
    // Arrange
    var manager = new ScopedPoolManager<Car>(
        scope => new DynamicObjectPool<Car>(
            () => new Car(scope.TenantId ?? "unknown", "Model")));
    
    var tenant1 = PoolScope.FromTenant("tenant1");
    var tenant2 = PoolScope.FromTenant("tenant2");
    
    // Act
    using var car1 = manager.GetObjectForScope(tenant1);
    using var car2 = manager.GetObjectForScope(tenant2);
    
    // Assert
    Assert.Equal("tenant1", car1.Unwrap().Make);
    Assert.Equal("tenant2", car2.Unwrap().Make);
}
```

### Integration Testing with Ambient Scope

```csharp
[Fact]
public async Task AmbientScope_IsolatesPoolsCorrectly()
{
    var manager = new ScopedPoolManager<HttpClient>(
        scope => new DynamicObjectPool<HttpClient>(() => new HttpClient()),
        new ScopedPoolConfiguration
        {
            ResolutionStrategy = ScopeResolutionStrategy.Ambient
        });
    
    using (AmbientPoolScope.BeginScope(PoolScope.FromTenant("tenant1")))
    {
        using var client1 = manager.GetObject();
        Assert.NotNull(client1);
    }
    
    using (AmbientPoolScope.BeginScope(PoolScope.FromTenant("tenant2")))
    {
        using var client2 = manager.GetObject();
        Assert.NotNull(client2);
    }
}
```

---

## ? **Performance Considerations**

1. **Scope Limits**: Set `MaxScopes` based on expected concurrent tenants/users
2. **Cleanup Interval**: Balance between memory usage and cleanup overhead
3. **Idle Timeout**: Set based on tenant activity patterns
4. **Pool Size Per Scope**: Configure appropriate `MaxPoolSize` per tenant needs

### Example Performance Tuning

```csharp
builder.Services.AddScopedObjectPool<DbConnection>(
    (sp, scope) => CreateConnection(scope.TenantId),
    configurePool: config =>
    {
        // Per-tenant pool configuration
        config.MaxPoolSize = 25;              // 25 connections per tenant
        config.MaxActiveObjects = 15;         // Max 15 concurrent per tenant
    },
    configureScoping: scopeConfig =>
    {
        // Multi-tenant configuration
        scopeConfig.MaxScopes = 1000;         // Support up to 1000 tenants
        scopeConfig.ScopeIdleTimeout = TimeSpan.FromHours(1); // Cleanup after 1hr idle
        scopeConfig.CleanupInterval = TimeSpan.FromMinutes(10); // Check every 10min
    });
```

---

## ?? **Best Practices**

1. **Use Ambient Scope** for request-scoped isolation
2. **Set Appropriate Timeouts** to prevent resource exhaustion
3. **Monitor Scope Statistics** to optimize capacity
4. **Configure Max Scopes** based on actual tenant count
5. **Enable Automatic Cleanup** for long-running applications
6. **Use Lifecycle Callbacks** for audit logging
7. **Test Scope Isolation** thoroughly in integration tests

---

## ?? **Troubleshooting**

### Issue: Too Many Scopes Created
**Solution**: Reduce `MaxScopes` or decrease `ScopeIdleTimeout`

### Issue: Scopes Not Cleaning Up
**Solution**: 
- Verify `EnableAutomaticCleanup = true`
- Check `ScopeIdleTimeout` isn't too long
- Manually call `TriggerCleanup()` for testing

### Issue: Wrong Pool Retrieved
**Solution**: 
- Verify scope resolution strategy
- Check custom scope resolver logic
- Ensure ambient scope is set correctly

---

## ?? **API Reference**

### PoolScope Factory Methods

```csharp
// From tenant ID
var scope = PoolScope.FromTenant("tenant123");

// From user ID
var scope = PoolScope.FromUser("user456", tenantId: "tenant123");

// From context
var scope = PoolScope.FromContext("request-abc-123", tenantId: "tenant123");

// Custom scope with metadata
var scope = new PoolScope("custom-id", "tenant123", "user456");
scope.Metadata["region"] = "us-east-1";
scope.Metadata["environment"] = "production";
```

### ScopedPoolManager Methods

```csharp
// Get pool for specific scope
IObjectPool<T> pool = manager.GetPoolForScope(scope);

// Get object from specific scope
using var obj = manager.GetObjectForScope(scope);

// Get pool for current scope (based on resolution strategy)
IObjectPool<T> pool = manager.GetPool();

// Get object from current scope
using var obj = manager.GetObject();

// Management
manager.RemoveScope(scope);
manager.TriggerCleanup();
var scopes = manager.GetActiveScopes();
var stats = manager.GetStatistics();
var scopeStats = manager.GetScopeStatistics(scope);
```

---

## ?? **Integration Examples**

### With ASP.NET Core Identity

```csharp
builder.Services.AddScopedObjectPool<UserContext>(
    (sp, scope) => new UserContext(scope.UserId),
    configureScoping: config =>
    {
        config.ResolutionStrategy = ScopeResolutionStrategy.Custom;
        config.CustomScopeResolver = () =>
        {
            var httpContext = GetHttpContext();
            var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var tenantId = httpContext.User.FindFirst("tenant_id")?.Value;
            return PoolScope.FromUser(userId ?? "anonymous", tenantId);
        };
    });
```

### With gRPC Services

```csharp
// In gRPC service
public class TenantGrpcService : TenantService.TenantServiceBase
{
    private readonly ScopedPoolManager<GrpcChannel> _channelManager;
    
    public override async Task<Response> GetData(Request request, ServerCallContext context)
    {
        var tenantId = context.RequestHeaders.GetValue("x-tenant-id");
        var scope = PoolScope.FromTenant(tenantId);
        
        using var channel = _channelManager.GetObjectForScope(scope);
        // Use tenant-specific channel
    }
}
```

---

## ? **Testing**

All 16 scoped pool tests verify:
- ? Scope creation and equality
- ? Pool isolation per scope
- ? Ambient scope context management
- ? Custom scope resolution
- ? Automatic cleanup of inactive scopes
- ? Statistics tracking
- ? DI integration
- ? Lifecycle callbacks
- ? Disposal and resource cleanup

---

**Version 4.0.0 - Production Ready** ??
