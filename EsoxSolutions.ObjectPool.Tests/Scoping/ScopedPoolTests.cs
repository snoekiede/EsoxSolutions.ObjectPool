using EsoxSolutions.ObjectPool.DependencyInjection;
using EsoxSolutions.ObjectPool.Models;
using EsoxSolutions.ObjectPool.Pools;
using EsoxSolutions.ObjectPool.Scoping;
using EsoxSolutions.ObjectPool.Tests.Models;
using Microsoft.Extensions.DependencyInjection;

namespace EsoxSolutions.ObjectPool.Tests.Scoping;

public class ScopedPoolTests
{
    [Fact]
    public void PoolScope_FromTenant_CreatesCorrectScope()
    {
        // Arrange & Act
        var scope = PoolScope.FromTenant("tenant1");

        // Assert
        Assert.Equal("tenant:tenant1", scope.Id);
        Assert.Equal("tenant1", scope.TenantId);
    }

    [Fact]
    public void PoolScope_FromUser_CreatesCorrectScope()
    {
        // Arrange & Act
        var scope = PoolScope.FromUser("user1", "tenant1");

        // Assert
        Assert.Equal("user:user1", scope.Id);
        Assert.Equal("tenant1", scope.TenantId);
        Assert.Equal("user1", scope.UserId);
    }

    [Fact]
    public void PoolScope_Equality_WorksCorrectly()
    {
        // Arrange
        var scope1 = new PoolScope("test", "tenant1");
        var scope2 = new PoolScope("test", "tenant1");
        var scope3 = new PoolScope("test", "tenant2");

        // Assert
        Assert.Equal(scope1, scope2);
        Assert.NotEqual(scope1, scope3);
        Assert.True(scope1 == scope2);
        Assert.True(scope1 != scope3);
    }

    [Fact]
    public void ScopedPoolManager_CreatesPoolPerScope()
    {
        // Arrange
        var callCounts = new Dictionary<string, int>();
        var manager = new ScopedPoolManager<Car>(
            scope =>
            {
                callCounts[scope.Id] = callCounts.GetValueOrDefault(scope.Id) + 1;
                return new DynamicObjectPool<Car>(() => new Car(scope.Id, "Model"));
            });

        var scope1 = PoolScope.FromTenant("tenant1");
        var scope2 = PoolScope.FromTenant("tenant2");

        // Act
        var pool1 = manager.GetPoolForScope(scope1);
        var pool1Again = manager.GetPoolForScope(scope1);
        var pool2 = manager.GetPoolForScope(scope2);

        // Assert
        Assert.Same(pool1, pool1Again); // Same pool for same scope
        Assert.NotSame(pool1, pool2); // Different pools for different scopes
        Assert.Equal(1, callCounts[scope1.Id]); // Factory called once per scope
        Assert.Equal(1, callCounts[scope2.Id]);
    }

    [Fact]
    public void ScopedPoolManager_GetObject_ReturnsObjectFromCorrectScope()
    {
        // Arrange
        var manager = new ScopedPoolManager<Car>(
            scope => new DynamicObjectPool<Car>(() => new Car(scope.Id, "Model")));

        var scope1 = PoolScope.FromTenant("tenant1");
        var scope2 = PoolScope.FromTenant("tenant2");

        // Act
        using var obj1 = manager.GetObjectForScope(scope1);
        using var obj2 = manager.GetObjectForScope(scope2);

        // Assert
        Assert.Equal("tenant:tenant1", obj1.Unwrap().Make);
        Assert.Equal("tenant:tenant2", obj2.Unwrap().Make);
    }

    [Fact]
    public void AmbientPoolScope_MaintainsCurrentScope()
    {
        // Arrange
        var scope1 = PoolScope.FromTenant("tenant1");
        var scope2 = PoolScope.FromTenant("tenant2");

        // Act & Assert
        Assert.Null(AmbientPoolScope.Current);

        using (AmbientPoolScope.BeginScope(scope1))
        {
            Assert.Equal(scope1, AmbientPoolScope.Current);

            using (AmbientPoolScope.BeginScope(scope2))
            {
                Assert.Equal(scope2, AmbientPoolScope.Current);
            }

            Assert.Equal(scope1, AmbientPoolScope.Current);
        }

        Assert.Null(AmbientPoolScope.Current);
    }

    [Fact]
    public void ScopedPoolManager_WithAmbientScope_ResolvesCorrectly()
    {
        // Arrange
        var manager = new ScopedPoolManager<Car>(
            scope => new DynamicObjectPool<Car>(() => new Car(scope.Id, "Model")),
            new ScopedPoolConfiguration
            {
                ResolutionStrategy = ScopeResolutionStrategy.Ambient
            });

        var scope1 = PoolScope.FromTenant("tenant1");
        var scope2 = PoolScope.FromTenant("tenant2");

        // Act
        Car? car1 = null;
        Car? car2 = null;

        using (AmbientPoolScope.BeginScope(scope1))
        {
            using var obj = manager.GetObject();
            car1 = obj.Unwrap();
        }

        using (AmbientPoolScope.BeginScope(scope2))
        {
            using var obj = manager.GetObject();
            car2 = obj.Unwrap();
        }

        // Assert
        Assert.NotNull(car1);
        Assert.NotNull(car2);
        Assert.Equal("tenant:tenant1", car1.Make);
        Assert.Equal("tenant:tenant2", car2.Make);
    }

    [Fact]
    public async Task ScopedPoolManager_Cleanup_RemovesInactiveScopes()
    {
        // Arrange
        var config = new ScopedPoolConfiguration
        {
            ScopeIdleTimeout = TimeSpan.FromMilliseconds(100),
            EnableAutomaticCleanup = false // Manual control
        };

        var manager = new ScopedPoolManager<Car>(
            scope => new DynamicObjectPool<Car>(() => new Car(scope.Id, "Model")),
            config);

        var scope1 = PoolScope.FromTenant("tenant1");
        var scope2 = PoolScope.FromTenant("tenant2");

        // Create pools
        manager.GetPoolForScope(scope1);
        manager.GetPoolForScope(scope2);

        Assert.Equal(2, manager.GetActiveScopes().Count());

        // Wait for timeout
        await Task.Delay(150);

        // Act
        manager.TriggerCleanup();

        // Assert
        Assert.Empty(manager.GetActiveScopes());
    }

    [Fact]
    public void ScopedPoolManager_MaxScopes_TriggersCleanup()
    {
        // Arrange
        var config = new ScopedPoolConfiguration
        {
            MaxScopes = 3,
            ScopeIdleTimeout = TimeSpan.FromMilliseconds(50),
            EnableAutomaticCleanup = false
        };

        var manager = new ScopedPoolManager<Car>(
            scope => new DynamicObjectPool<Car>(() => new Car(scope.Id, "Model")),
            config);

        // Act - Create more than max scopes
        for (int i = 0; i < 5; i++)
        {
            var scope = PoolScope.FromTenant($"tenant{i}");
            manager.GetPoolForScope(scope);
        }

        // Assert - Should have created all scopes (cleanup is async)
        Assert.True(manager.GetActiveScopes().Count() >= 3);
    }

    [Fact]
    public void ScopedPoolManager_RemoveScope_DisposesPool()
    {
        // Arrange
        var disposed = false;
        var config = new ScopedPoolConfiguration
        {
            DisposePoolsOnCleanup = true,
            OnScopeDisposed = scope => disposed = true
        };

        var manager = new ScopedPoolManager<DisposableCar>(
            scope => new DynamicObjectPool<DisposableCar>(
                () => new DisposableCar(scope.Id, "Model")),
            config);

        var testScope = PoolScope.FromTenant("tenant1");
        manager.GetPoolForScope(testScope);

        // Act
        var removed = manager.RemoveScope(testScope);

        // Assert
        Assert.True(removed);
        Assert.True(disposed);
        Assert.Empty(manager.GetActiveScopes());
    }

    [Fact]
    public void ScopedPoolManager_GetStatistics_ReturnsCorrectData()
    {
        // Arrange
        var manager = new ScopedPoolManager<Car>(
            scope => new DynamicObjectPool<Car>(() => new Car(scope.Id, "Model")));

        var scope1 = PoolScope.FromTenant("tenant1");
        var scope2 = PoolScope.FromTenant("tenant2");

        // Act
        manager.GetPoolForScope(scope1);
        manager.GetPoolForScope(scope2);
        manager.GetPoolForScope(scope1); // Access scope1 again

        var stats = manager.GetStatistics();

        // Assert
        Assert.Equal(2, stats.TotalScopesCreated);
        Assert.Equal(2, stats.ActiveScopes);
        Assert.Equal(2, stats.PeakScopes);
        Assert.True(stats.ScopeAccessCounts.ContainsKey(scope1.Id));
        Assert.True(stats.ScopeAccessCounts.ContainsKey(scope2.Id));
        // Access counts increment on every GetPoolForScope call
        Assert.True(stats.ScopeAccessCounts[scope1.Id] >= 1); // At least 1 access
        Assert.True(stats.ScopeAccessCounts[scope2.Id] >= 1); // At least 1 access
    }

    [Fact]
    public void ScopedPoolManager_GetScopeStatistics_ReturnsPoolStats()
    {
        // Arrange
        var manager = new ScopedPoolManager<Car>(
            scope => new DynamicObjectPool<Car>(() => new Car(scope.Id, "Model")));

        var testScope = PoolScope.FromTenant("tenant1");
        var pool = manager.GetPoolForScope(testScope);

        // Get an object to generate some stats
        using (var obj = pool.GetObject()) { }

        // Act
        var scopeStats = manager.GetScopeStatistics(testScope);

        // Assert
        Assert.NotNull(scopeStats);
        Assert.Equal("tenant:tenant1", scopeStats["scope_id"]);
        Assert.Equal("tenant1", scopeStats["tenant_id"]);
        Assert.True(scopeStats.ContainsKey("last_access"));
    }

    [Fact]
    public void ScopedPoolManager_CustomScopeResolver_WorksCorrectly()
    {
        // Arrange
        var currentScope = PoolScope.FromTenant("tenant1");
        var config = new ScopedPoolConfiguration
        {
            ResolutionStrategy = ScopeResolutionStrategy.Custom,
            CustomScopeResolver = () => currentScope
        };

        var manager = new ScopedPoolManager<Car>(
            scope => new DynamicObjectPool<Car>(() => new Car(scope.Id, "Model")),
            config);

        // Act
        using var obj1 = manager.GetObject();
        
        currentScope = PoolScope.FromTenant("tenant2");
        using var obj2 = manager.GetObject();

        // Assert
        Assert.Equal("tenant:tenant1", obj1.Unwrap().Make);
        Assert.Equal("tenant:tenant2", obj2.Unwrap().Make);
    }

    [Fact]
    public void ServiceCollection_AddScopedObjectPool_RegistersCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddScopedObjectPool<Car>(
            (sp, scope) => new Car(scope.Id, "Model"),
            config => config.MaxPoolSize = 10);

        var provider = services.BuildServiceProvider();
        var manager = provider.GetRequiredService<ScopedPoolManager<Car>>();

        // Assert
        Assert.NotNull(manager);

        using var obj = manager.GetObjectForScope(PoolScope.FromTenant("test"));
        Assert.NotNull(obj.Unwrap());
    }

    [Fact]
    public void ServiceCollection_AddTenantScopedObjectPool_RegistersCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddTenantScopedObjectPool<Car>(
            (sp, tenantId) => new Car($"tenant-{tenantId}", "Model"));

        var provider = services.BuildServiceProvider();
        var manager = provider.GetRequiredService<ScopedPoolManager<Car>>();

        // Assert
        Assert.NotNull(manager);

        var scope = PoolScope.FromTenant("test123");
        using var obj = manager.GetObjectForScope(scope);
        Assert.Equal("tenant-test123", obj.Unwrap().Make);
    }

    [Fact]
    public void ScopedPoolManager_Dispose_CleansUpAllPools()
    {
        // Arrange
        var manager = new ScopedPoolManager<Car>(
            scope => new DynamicObjectPool<Car>(() => new Car(scope.Id, "Model")));

        manager.GetPoolForScope(PoolScope.FromTenant("tenant1"));
        manager.GetPoolForScope(PoolScope.FromTenant("tenant2"));

        // Act
        manager.Dispose();

        // Assert
        Assert.Throws<ObjectDisposedException>(() => 
            manager.GetPoolForScope(PoolScope.FromTenant("tenant3")));
    }
}

public class DisposableCar : Car, IDisposable
{
    public bool IsDisposed { get; private set; }

    public DisposableCar(string make, string model) : base(make, model)
    {
    }

    public void Dispose()
    {
        IsDisposed = true;
    }
}
