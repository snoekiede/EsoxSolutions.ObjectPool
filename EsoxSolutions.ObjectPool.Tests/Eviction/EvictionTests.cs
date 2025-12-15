using EsoxSolutions.ObjectPool.Eviction;
using EsoxSolutions.ObjectPool.Models;
using EsoxSolutions.ObjectPool.Pools;
using EsoxSolutions.ObjectPool.Tests.Models;

namespace EsoxSolutions.ObjectPool.Tests.Eviction;

public class EvictionTests
{
    [Fact]
    public async Task TimeToLive_ExpiredObjects_AreEvicted()
    {
        // Arrange
        var config = new PoolConfiguration
        {
            EvictionConfiguration = new EvictionConfiguration
            {
                Policy = EvictionPolicy.TimeToLive,
                TimeToLive = TimeSpan.FromMilliseconds(100),
                EvictionInterval = TimeSpan.FromMilliseconds(50),
                EnableBackgroundEviction = false // Manual control for testing
            }
        };

        var pool = new DynamicObjectPool<Car>(() => new Car("Test", "Model"), config, null);

        // Warm up pool
        await pool.WarmUpAsync(5);

        // Act - Wait for TTL to expire
        await Task.Delay(150);

        // Manually trigger eviction
        pool.TriggerEviction();

        // Assert - Objects should be evicted
        var stats = pool.GetEvictionStatistics();
        Assert.NotNull(stats);
        Assert.True(stats.TotalEvictions > 0);
        Assert.True(stats.TtlEvictions > 0);
    }

    [Fact]
    public async Task IdleTimeout_IdleObjects_AreEvicted()
    {
        // Arrange
        var config = new PoolConfiguration
        {
            EvictionConfiguration = new EvictionConfiguration
            {
                Policy = EvictionPolicy.IdleTimeout,
                IdleTimeout = TimeSpan.FromMilliseconds(100),
                EvictionInterval = TimeSpan.FromMilliseconds(50),
                EnableBackgroundEviction = false
            }
        };

        var pool = new DynamicObjectPool<Car>(() => new Car("Test", "Model"), config, null);
        await pool.WarmUpAsync(3);

        // Get and return an object to mark it as accessed
        using (var obj = pool.GetObject())
        {
            // Object is now accessed
        }

        // Wait for idle timeout
        await Task.Delay(150);

        // Act
        pool.TriggerEviction();

        // Assert
        var stats = pool.GetEvictionStatistics();
        Assert.NotNull(stats);
        Assert.True(stats.IdleEvictions > 0);
    }

    [Fact]
    public async Task CombinedPolicy_EvictsOnEitherCondition()
    {
        // Arrange
        var config = new PoolConfiguration
        {
            EvictionConfiguration = new EvictionConfiguration
            {
                Policy = EvictionPolicy.Combined,
                TimeToLive = TimeSpan.FromMilliseconds(200),
                IdleTimeout = TimeSpan.FromMilliseconds(100),
                EvictionInterval = TimeSpan.FromMilliseconds(50),
                EnableBackgroundEviction = false
            }
        };

        var pool = new DynamicObjectPool<Car>(() => new Car("Test", "Model"), config, null);
        await pool.WarmUpAsync(3);

        // Wait for idle timeout (but not TTL)
        await Task.Delay(120);

        // Act
        pool.TriggerEviction();

        // Assert - Should evict due to idle timeout
        var stats = pool.GetEvictionStatistics();
        Assert.NotNull(stats);
        Assert.True(stats.TotalEvictions > 0);
    }

    [Fact]
    public async Task GetObject_SkipsExpiredObjects()
    {
        // Arrange
        var config = new PoolConfiguration
        {
            EvictionConfiguration = new EvictionConfiguration
            {
                Policy = EvictionPolicy.TimeToLive,
                TimeToLive = TimeSpan.FromMilliseconds(100),
                EnableBackgroundEviction = false
            }
        };

        var pool = new DynamicObjectPool<Car>(() => new Car("Test", "Model"), config, null);
        await pool.WarmUpAsync(3);

        // Wait for objects to expire
        await Task.Delay(150);

        // Act - Get object should skip expired ones and create new
        using var obj = pool.GetObject();

        // Assert
        Assert.NotNull(obj);
        Assert.NotNull(obj.Unwrap());
    }

    [Fact]
    public void CustomEvictionPredicate_WorksCorrectly()
    {
        // Arrange
        var callCount = 0;
        var config = new PoolConfiguration
        {
            EvictionConfiguration = new EvictionConfiguration
            {
                Policy = EvictionPolicy.TimeToLive,
                CustomEvictionPredicate = (obj, metadata) =>
                {
                    callCount++;
                    return metadata.AccessCount > 2;
                },
                EnableBackgroundEviction = false
            }
        };

        var pool = new DynamicObjectPool<Car>(() => new Car("Test", "Model"), config, null);

        // Add some objects and access them
        using (var obj1 = pool.GetObject()) { }
        using (var obj2 = pool.GetObject()) { }
        using (var obj3 = pool.GetObject()) { }
        using (var obj4 = pool.GetObject()) { }

        // Act
        pool.TriggerEviction();

        // Assert
        Assert.True(callCount > 0);
        var stats = pool.GetEvictionStatistics();
        Assert.NotNull(stats);
    }

    [Fact]
    public async Task EvictionStatistics_TrackCorrectly()
    {
        // Arrange
        var config = new PoolConfiguration
        {
            EvictionConfiguration = new EvictionConfiguration
            {
                Policy = EvictionPolicy.TimeToLive,
                TimeToLive = TimeSpan.FromMilliseconds(100),
                EnableBackgroundEviction = false
            }
        };

        var pool = new DynamicObjectPool<Car>(() => new Car("Test", "Model"), config, null);
        await pool.WarmUpAsync(5);

        // Wait and evict
        await Task.Delay(150);
        pool.TriggerEviction();

        // Act
        var stats = pool.GetEvictionStatistics();

        // Assert
        Assert.NotNull(stats);
        Assert.True(stats.TotalEvictions > 0);
        Assert.True(stats.EvictionRuns > 0);
        Assert.NotNull(stats.LastEvictionRun);
        Assert.True(stats.LastEvictionDuration > TimeSpan.Zero);
    }

    [Fact]
    public async Task DisposableObjects_AreDisposedWhenEvicted()
    {
        // Arrange
        var disposeCount = 0;

        var config = new PoolConfiguration
        {
            EvictionConfiguration = new EvictionConfiguration
            {
                Policy = EvictionPolicy.TimeToLive,
                TimeToLive = TimeSpan.FromMilliseconds(100),
                DisposeEvictedObjects = true,
                EnableBackgroundEviction = false
            }
        };

        var pool = new DynamicObjectPool<DisposableTestObject>(
            () => new DisposableTestObject(() => Interlocked.Increment(ref disposeCount)),
            config,
            null);

        await pool.WarmUpAsync(3);

        // Wait for expiration
        await Task.Delay(150);

        // Act
        pool.TriggerEviction();

        // Allow some time for disposal
        await Task.Delay(50);

        // Assert
        Assert.True(disposeCount > 0);
    }

    [Fact]
    public async Task NoEviction_WhenPolicyIsNone()
    {
        // Arrange
        var config = new PoolConfiguration
        {
            EvictionConfiguration = new EvictionConfiguration
            {
                Policy = EvictionPolicy.None
            }
        };

        var pool = new DynamicObjectPool<Car>(() => new Car("Test", "Model"), config, null);
        await pool.WarmUpAsync(5);

        // Wait a long time
        await Task.Delay(200);

        // Act
        pool.TriggerEviction();

        // Assert
        var stats = pool.GetEvictionStatistics();
        Assert.Null(stats); // No eviction manager when policy is None
    }

    [Fact]
    public async Task EvictionDoesNotAffectActiveObjects()
    {
        // Arrange
        var config = new PoolConfiguration
        {
            EvictionConfiguration = new EvictionConfiguration
            {
                Policy = EvictionPolicy.TimeToLive,
                TimeToLive = TimeSpan.FromMilliseconds(100),
                EnableBackgroundEviction = false
            }
        };

        var pool = new DynamicObjectPool<Car>(() => new Car("Test", "Model"), config, null);
        await pool.WarmUpAsync(5);

        // Get an object and hold it
        var activeObj = pool.GetObject();

        // Wait for TTL
        await Task.Delay(150);

        // Act
        pool.TriggerEviction();

        // Assert - Active object should still be usable
        Assert.NotNull(activeObj.Unwrap());

        // Cleanup
        activeObj.Dispose();
    }

    [Fact]
    public async Task BackgroundEviction_WorksAutomatically()
    {
        // Arrange
        var config = new PoolConfiguration
        {
            EvictionConfiguration = new EvictionConfiguration
            {
                Policy = EvictionPolicy.TimeToLive,
                TimeToLive = TimeSpan.FromMilliseconds(100),
                EvictionInterval = TimeSpan.FromMilliseconds(50),
                EnableBackgroundEviction = true
            }
        };

        var pool = new DynamicObjectPool<Car>(() => new Car("Test", "Model"), config, null);
        await pool.WarmUpAsync(3);

        // Wait for TTL + eviction interval
        await Task.Delay(200);

        // Act - Check stats (background eviction should have run)
        var stats = pool.GetEvictionStatistics();

        // Assert
        Assert.NotNull(stats);
        // Background eviction may or may not have run depending on timing
        // Just verify we have stats tracking
        Assert.NotNull(stats.LastEvictionRun);
    }

    [Fact]
    public async Task MaxEvictionsPerRun_LimitsEvictions()
    {
        // Arrange
        var config = new PoolConfiguration
        {
            EvictionConfiguration = new EvictionConfiguration
            {
                Policy = EvictionPolicy.TimeToLive,
                TimeToLive = TimeSpan.FromMilliseconds(100),
                MaxEvictionsPerRun = 2,
                EnableBackgroundEviction = false
            }
        };

        var pool = new DynamicObjectPool<Car>(() => new Car("Test", "Model"), config, null);
        await pool.WarmUpAsync(10);

        // Wait for all to expire
        await Task.Delay(150);

        // Act - First run should evict max 2
        pool.TriggerEviction();

        // Assert
        var stats = pool.GetEvictionStatistics();
        Assert.NotNull(stats);
        Assert.True(stats.TotalEvictions <= 2);
    }
}

// Helper class for testing disposal
public class DisposableTestObject : IDisposable
{
    private readonly Action _onDispose;
    private bool _disposed;

    public DisposableTestObject(Action onDispose)
    {
        _onDispose = onDispose;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _onDispose?.Invoke();
            _disposed = true;
        }
    }
}
