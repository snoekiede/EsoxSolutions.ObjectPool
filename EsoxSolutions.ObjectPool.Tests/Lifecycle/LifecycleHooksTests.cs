using EsoxSolutions.ObjectPool.Eviction;
using EsoxSolutions.ObjectPool.Lifecycle;
using EsoxSolutions.ObjectPool.Models;
using EsoxSolutions.ObjectPool.Pools;
using EsoxSolutions.ObjectPool.Tests.Models;

namespace EsoxSolutions.ObjectPool.Tests.Lifecycle;

public class LifecycleHooksTests
{
    [Fact]
    public void OnCreate_IsCalled_WhenObjectCreated()
    {
        // Arrange
        var createCalled = false;
        Car? createdObject = null;

        var hooks = new LifecycleHooks<Car>
        {
            OnCreate = obj =>
            {
                createCalled = true;
                createdObject = obj;
            }
        };

        var config = new PoolConfiguration { LifecycleHooks = hooks };
        var pool = new DynamicObjectPool<Car>(() => new Car("Test", "Model"), config);

        // Act
        using var obj = pool.GetObject();

        // Assert
        Assert.True(createCalled);
        Assert.NotNull(createdObject);
        Assert.Equal(obj.Unwrap(), createdObject);
    }

    [Fact]
    public void OnAcquire_IsCalled_WhenObjectRetrieved()
    {
        // Arrange
        var acquireCalled = false;
        var acquireCount = 0;

        var hooks = new LifecycleHooks<Car>
        {
            OnAcquire = obj => { acquireCalled = true; acquireCount++; }
        };

        var config = new PoolConfiguration { LifecycleHooks = hooks };
        var initialCars = new List<Car> { new("Test", "Model") };
        var pool = new DynamicObjectPool<Car>(() => new Car("New", "Model"), initialCars, config);

        // Act
        using (var obj1 = pool.GetObject()) { }
        using (var obj2 = pool.GetObject()) { }

        // Assert
        Assert.True(acquireCalled);
        Assert.Equal(2, acquireCount);
    }

    [Fact]
    public void OnReturn_IsCalled_WhenObjectReturned()
    {
        // Arrange
        var returnCalled = false;
        Car? returnedObject = null;

        var hooks = new LifecycleHooks<Car>
        {
            OnReturn = obj =>
            {
                returnCalled = true;
                returnedObject = obj;
            }
        };

        var config = new PoolConfiguration { LifecycleHooks = hooks };
        // Start with empty pool to force factory usage
        var pool = new DynamicObjectPool<Car>(() => new Car("Test", "Model"), [], config);

        // Act
        var obj = pool.GetObject();
        pool.ReturnObject(obj); // Explicitly call ReturnObject

        // Assert
        Assert.True(returnCalled);
        Assert.NotNull(returnedObject);
    }

    [Fact]
    public void OnDispose_IsCalled_WhenObjectDisposed()
    {
        // Arrange
        var disposeCalled = false;

        var hooks = new LifecycleHooks<DisposableTestObject>
        {
            OnDispose = obj => disposeCalled = true
        };

        var config = new PoolConfiguration
        {
            LifecycleHooks = hooks,
            EvictionConfiguration = new EvictionConfiguration
            {
                Policy = EvictionPolicy.TimeToLive,
                TimeToLive = TimeSpan.FromMilliseconds(10),
                DisposeEvictedObjects = true,
                EnableBackgroundEviction = false
            }
        };

        var pool = new DynamicObjectPool<DisposableTestObject>(
            () => new DisposableTestObject(),
            config);

        // Create and return object
        using (var obj = pool.GetObject()) { }

        // Wait for TTL
        Thread.Sleep(50);

        // Act - trigger eviction which will dispose
        pool.TriggerEviction();

        // Assert
        Assert.True(disposeCalled);
    }

    [Fact]
    public void OnEvict_IsCalled_WhenObjectEvicted()
    {
        // Arrange
        var evictCalled = false;
        EvictionReason? evictionReason = null;

        var hooks = new LifecycleHooks<Car>
        {
            OnEvict = (obj, reason) =>
            {
                evictCalled = true;
                evictionReason = reason;
            }
        };

        var config = new PoolConfiguration
        {
            LifecycleHooks = hooks,
            EvictionConfiguration = new EvictionConfiguration
            {
                Policy = EvictionPolicy.TimeToLive,
                TimeToLive = TimeSpan.FromMilliseconds(10),
                EnableBackgroundEviction = false
            }
        };

        var pool = new DynamicObjectPool<Car>(() => new Car("Test", "Model"), config);

        // Create object
        using (var obj = pool.GetObject()) { }

        // Wait for TTL
        Thread.Sleep(50);

        // Act
        pool.TriggerEviction();

        // Assert
        Assert.True(evictCalled);
        Assert.NotNull(evictionReason);
    }

    [Fact]
    public void LifecycleHookManager_TracksStatistics()
    {
        // Arrange
        var hooks = new LifecycleHooks<Car>
        {
            OnCreate = obj => { },
            OnAcquire = obj => { },
            OnReturn = obj => { }
        };

        var manager = new LifecycleHookManager<Car>(hooks);

        // Act
        var car = new Car("Test", "Model");
        manager.ExecuteOnCreate(car);
        manager.ExecuteOnAcquire(car);
        manager.ExecuteOnReturn(car);

        // Assert
        var stats = manager.GetStatistics();
        Assert.Equal(1, stats.CreateCalls);
        Assert.Equal(1, stats.AcquireCalls);
        Assert.Equal(1, stats.ReturnCalls);
    }

    [Fact]
    public void LifecycleHookManager_ContinuesOnError_WhenConfigured()
    {
        // Arrange
        var hooks = new LifecycleHooks<Car>
        {
            OnCreate = obj => throw new InvalidOperationException("Test error"),
            OnAcquire = obj => { } // Should still execute
        };

        var manager = new LifecycleHookManager<Car>(hooks, continueOnError: true);

        // Act - Should not throw
        var car = new Car("Test", "Model");
        manager.ExecuteOnCreate(car);
        manager.ExecuteOnAcquire(car);

        // Assert
        var stats = manager.GetStatistics();
        Assert.Equal(1, stats.ErrorCount);
        Assert.Equal(1, stats.AcquireCalls); // This should still have executed
    }

    [Fact]
    public void LifecycleHookManager_ThrowsOnError_WhenConfigured()
    {
        // Arrange
        var hooks = new LifecycleHooks<Car>
        {
            OnCreate = obj => throw new InvalidOperationException("Test error")
        };

        var manager = new LifecycleHookManager<Car>(hooks, continueOnError: false);

        // Act & Assert
        var car = new Car("Test", "Model");
        Assert.Throws<InvalidOperationException>(() => manager.ExecuteOnCreate(car));
    }

    [Fact]
    public async Task AsyncHooks_ExecuteCorrectly()
    {
        // Arrange
        var createCalled = false;
        var hooks = new LifecycleHooks<Car>
        {
            OnCreateAsync = async obj =>
            {
                await Task.Delay(10);
                createCalled = true;
            }
        };

        var manager = new LifecycleHookManager<Car>(hooks);

        // Act
        var car = new Car("Test", "Model");
        await manager.ExecuteOnCreateAsync(car);

        // Assert
        Assert.True(createCalled);
    }

    [Fact]
    public void GetLifecycleHookStatistics_ReturnsStats()
    {
        // Arrange
        var hooks = new LifecycleHooks<Car>
        {
            OnCreate = obj => { },
            OnAcquire = obj => { }
        };

        var config = new PoolConfiguration { LifecycleHooks = hooks };
        var pool = new DynamicObjectPool<Car>(() => new Car("Test", "Model"), config);

        // Act
        using (var obj = pool.GetObject()) { }

        // Assert
        var stats = pool.GetLifecycleHookStatistics();
        Assert.NotNull(stats);
        Assert.True(stats.CreateCalls > 0);
        Assert.True(stats.AcquireCalls > 0);
    }

    [Fact]
    public void MultipleHooks_ExecuteInOrder()
    {
        // Arrange
        var executionOrder = new List<string>();

        var hooks = new LifecycleHooks<Car>
        {
            OnCreate = obj => executionOrder.Add("Create"),
            OnAcquire = obj => executionOrder.Add("Acquire")
        };

        var config = new PoolConfiguration { LifecycleHooks = hooks };
        var pool = new DynamicObjectPool<Car>(() => new Car("Test", "Model"), config);

        // Act
        using (var obj = pool.GetObject()) { }

        // Assert
        Assert.Equal(2, executionOrder.Count);
        Assert.Equal("Create", executionOrder[0]);
        Assert.Equal("Acquire", executionOrder[1]);
    }

    [Fact]
    public void LifecycleHooks_WorkWithExistingObjects()
    {
        // Arrange
        var acquireCalled = false;
        var hooks = new LifecycleHooks<Car>
        {
            OnAcquire = obj => acquireCalled = true
        };

        var config = new PoolConfiguration { LifecycleHooks = hooks };
        var initialCars = new List<Car> { new("Existing", "Car") };
        var pool = new DynamicObjectPool<Car>(() => new Car("New", "Car"), initialCars, config);

        // Act
        using (var obj = pool.GetObject()) { }

        // Assert
        Assert.True(acquireCalled);
    }
}

public class DisposableTestObject : IDisposable
{
    private bool _disposed;

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}
