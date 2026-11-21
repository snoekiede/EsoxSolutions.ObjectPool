using EsoxSolutions.ObjectPool.CircuitBreaker;
using EsoxSolutions.ObjectPool.DependencyInjection;
using EsoxSolutions.ObjectPool.Eviction;
using EsoxSolutions.ObjectPool.Exceptions;
using EsoxSolutions.ObjectPool.HealthChecks;
using EsoxSolutions.ObjectPool.Interfaces;
using EsoxSolutions.ObjectPool.Lifecycle;
using EsoxSolutions.ObjectPool.Models;
using EsoxSolutions.ObjectPool.Policies;
using EsoxSolutions.ObjectPool.Pools;
using EsoxSolutions.ObjectPool.Scoping;
using EsoxSolutions.ObjectPool.Tests.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace EsoxSolutions.ObjectPool.Tests;

public class ReviewFixesTests
{
    [Fact]
    public void ObjectPool_MaxActiveObjects_ReleasesSlotsOnSynchronousReturn()
    {
        var config = new PoolConfiguration { MaxActiveObjects = 2 };
        var pool = new ObjectPool<int>([1, 2, 3], config);

        // Perform 10 sequential checkout/dispose cycles with MaxActiveObjects = 2
        for (int i = 0; i < 10; i++)
        {
            using var first = pool.GetObject();
            using var second = pool.GetObject();
            Assert.Equal(2, pool.Statistics.CurrentActiveObjects);
        }

        Assert.Equal(0, pool.Statistics.CurrentActiveObjects);
        Assert.Equal(3, pool.AvailableObjectCount);
    }

    [Fact]
    public void DynamicObjectPool_MaxActiveObjects_ReleasesSlotsOnSynchronousReturn()
    {
        var config = new PoolConfiguration { MaxActiveObjects = 2 };
        var pool = new DynamicObjectPool<Car>(() => new Car("Ford", "Focus"), config);

        for (int i = 0; i < 10; i++)
        {
            using var first = pool.GetObject();
            using var second = pool.GetObject();
            Assert.Equal(2, pool.Statistics.CurrentActiveObjects);
        }

        Assert.Equal(0, pool.Statistics.CurrentActiveObjects);
    }

    [Fact]
    public void QueryableObjectPool_FailedQuery_ReleasesActiveSlot_And_MatchesDefaultValueType()
    {
        var config = new PoolConfiguration { MaxActiveObjects = 1 };
        var pool = new QueryableObjectPool<int>([0, 1, 2], config);

        // Failed query must not leak the single active slot
        Assert.Throws<NoObjectsInPoolException>(() => pool.GetObject(x => x == 99));

        // Value type default(int) == 0 must be matchable and active slot must still be available
        using var zeroModel = pool.GetObject(x => x == 0);
        Assert.Equal(0, zeroModel.Unwrap());
    }

    [Fact]
    public void DynamicObjectPool_PoolModelDispose_InvokesLifecycleHooksAndEvictionReturn()
    {
        var returnHookCount = 0;
        var validationFailedHookCount = 0;
        var hooks = new LifecycleHooks<Car>
        {
            OnReturn = _ => returnHookCount++,
            OnValidationFailed = _ => validationFailedHookCount++
        };

        var config = new PoolConfiguration
        {
            LifecycleHooks = hooks,
            ValidateOnReturn = true,
            ValidationFunction = obj => ((Car)obj).Make == "Valid"
        };

        var pool = new DynamicObjectPool<Car>(() => new Car("Valid", "Car"), config);

        // Standard `using` disposes PoolModel<T> via IObjectPool<T>.ReturnObject
        using (var valid = pool.GetObject())
        {
            Assert.Equal("Valid", valid.Unwrap().Make);
        }

        Assert.Equal(1, returnHookCount);
        Assert.Equal(0, validationFailedHookCount);

        using (var invalid = pool.GetObject())
        {
            invalid.Unwrap().Make = "Invalid";
        }

        Assert.Equal(2, returnHookCount);
        Assert.Equal(1, validationFailedHookCount);
        Assert.Equal(0, pool.AvailableObjectCount);
    }

    [Fact]
    public async Task DynamicObjectPool_TryGetObjectAndGetObjectAsync_UseFactoryAndLifecycleHooks()
    {
        var createCount = 0;
        var acquireCount = 0;
        var returnCount = 0;

        var hooks = new LifecycleHooks<Car>
        {
            OnCreate = _ => Interlocked.Increment(ref createCount),
            OnAcquire = _ => Interlocked.Increment(ref acquireCount),
            OnReturnAsync = _ =>
            {
                Interlocked.Increment(ref returnCount);
                return Task.CompletedTask;
            }
        };

        var config = new PoolConfiguration { LifecycleHooks = hooks };
        var pool = new DynamicObjectPool<Car>(() => new Car("Dynamic", "Async"), config);

        // TryGetObject on empty dynamic pool should invoke factory
        Assert.True(pool.TryGetObject(out var first));
        Assert.NotNull(first);
        await first!.DisposeAsync();

        // GetObjectAsync should work seamlessly with await using
        await using (var second = await pool.GetObjectAsync())
        {
            Assert.Equal("Dynamic", second.Unwrap().Make);
        }

        Assert.Equal(1, createCount);
        Assert.Equal(2, acquireCount);
        Assert.Equal(2, returnCount);
    }

    [Fact]
    public void PoolingPolicies_AreIntegratedIntoObjectPool()
    {
        // FIFO policy
        var fifoPool = new ObjectPool<string>(
            ["first", "second", "third"],
            new PoolConfiguration { PoolingPolicyType = PoolingPolicyType.Fifo });

        using var f1 = fifoPool.GetObject();
        using var f2 = fifoPool.GetObject();
        using var f3 = fifoPool.GetObject();
        Assert.Equal("first", f1.Unwrap());
        Assert.Equal("second", f2.Unwrap());
        Assert.Equal("third", f3.Unwrap());

        // Priority policy
        var priorityConfig = new PoolConfiguration<int>
        {
            PoolingPolicyType = PoolingPolicyType.Priority,
            TypedPrioritySelector = x => x
        };
        var priorityPool = new ObjectPool<int>([10, 50, 30], priorityConfig);

        using var p1 = priorityPool.GetObject();
        using var p2 = priorityPool.GetObject();
        using var p3 = priorityPool.GetObject();
        Assert.Equal(50, p1.Unwrap());
        Assert.Equal(30, p2.Unwrap());
        Assert.Equal(10, p3.Unwrap());
    }

    [Fact]
    public void RoundRobinPoolingPolicy_RotatesEvenlyWhenReturnedOutOfOrder()
    {
        var pool = new ObjectPool<string>(
            ["A", "B", "C"],
            new PoolConfiguration { PoolingPolicyType = PoolingPolicyType.RoundRobin });

        // Checkout A and B
        var a = pool.GetObject();
        var b = pool.GetObject();
        Assert.Equal("A", a.Unwrap());
        Assert.Equal("B", b.Unwrap());

        // Return B before A
        b.Dispose();
        a.Dispose();

        // Next three checkouts should still follow circular round-robin turn order: C, A, B
        using var cNext = pool.GetObject();
        using var aNext = pool.GetObject();
        using var bNext = pool.GetObject();

        Assert.Equal("C", cNext.Unwrap());
        Assert.Equal("A", aNext.Unwrap());
        Assert.Equal("B", bNext.Unwrap());
    }

    [Fact]
    public async Task Eviction_EvictsAllExpiredObjectsNotJustTopOfStack()
    {
        var disposedCount = 0;
        var config = new PoolConfiguration
        {
            EvictionConfiguration = new EvictionConfiguration
            {
                Policy = EvictionPolicy.TimeToLive,
                TimeToLive = TimeSpan.FromMilliseconds(60),
                DisposeEvictedObjects = true,
                EnableBackgroundEviction = false
            }
        };

        var pool = new DynamicObjectPool<DisposableResource>(
            () => new DisposableResource(() => Interlocked.Increment(ref disposedCount)),
            config);

        await pool.WarmUpAsync(4);
        Assert.Equal(4, pool.AvailableObjectCount);

        await Task.Delay(120);

        // Add one fresh object on top of the LIFO stack so expired objects are buried underneath
        using (var fresh = new DynamicObjectPool<DisposableResource>(() => new DisposableResource(null)).GetObject())
        {
            // Keep existing 4 expired objects in the pool and trigger eviction
        }

        pool.TriggerEviction();

        Assert.Equal(0, pool.AvailableObjectCount);
        Assert.Equal(4, disposedCount);
    }

    [Fact]
    public async Task ObjectMetadata_IdleTime_IsMeasuredFromReturnNotCheckout()
    {
        var config = new PoolConfiguration
        {
            EvictionConfiguration = new EvictionConfiguration
            {
                Policy = EvictionPolicy.IdleTimeout,
                IdleTimeout = TimeSpan.FromMilliseconds(150),
                EnableBackgroundEviction = false
            }
        };

        var pool = new DynamicObjectPool<Car>(() => new Car("Ford", "Mustang"), config);

        // Checkout object and keep it active for 200ms (> IdleTimeout)
        var model = pool.GetObject();
        await Task.Delay(200);

        // Return object to pool; it has just been returned so idle time should reset to ~0
        model.Dispose();

        // Trigger eviction immediately after return - object must NOT be evicted
        pool.TriggerEviction();
        Assert.Equal(1, pool.AvailableObjectCount);

        // Now let it sit idle in the pool for 200ms
        await Task.Delay(200);
        pool.TriggerEviction();
        Assert.Equal(0, pool.AvailableObjectCount);
    }

    [Fact]
    public void DiscardedObjects_AreDisposedOnValidationFailureAndMaxPoolSize()
    {
        var disposedCount = 0;
        var config = new PoolConfiguration<DisposableResource>
        {
            MaxPoolSize = 1,
            ValidateOnReturn = true,
            TypedValidationFunction = r => r.IsValid
        };

        var pool = new DynamicObjectPool<DisposableResource>(
            () => new DisposableResource(() => Interlocked.Increment(ref disposedCount)),
            config);

        // 1. Validation failure should dispose discarded object
        var invalid = pool.GetObject();
        invalid.Unwrap().IsValid = false;
        invalid.Dispose();
        Assert.Equal(1, disposedCount);
        Assert.Equal(0, pool.AvailableObjectCount);

        // 2. Exceeding MaxPoolSize (1) should dispose the excess returned object
        var first = pool.GetObject();
        var second = pool.GetObject();
        first.Dispose();  // Fits in pool (count = 1)
        second.Dispose(); // Exceeds MaxPoolSize (1), should be disposed
        Assert.Equal(2, disposedCount);
        Assert.Equal(1, pool.AvailableObjectCount);
    }

    [Fact]
    public async Task CircuitBreaker_TimeUntilRetryAndFailureWindow_WorkCorrectly()
    {
        var config = new CircuitBreakerConfiguration
        {
            FailureThreshold = 100,
            MinimumThroughput = 4,
            FailurePercentageThreshold = 50.0,
            FailureWindow = TimeSpan.FromMilliseconds(100),
            OpenDuration = TimeSpan.FromSeconds(5),
            EnableAutomaticRecovery = false
        };

        var breaker = new EsoxSolutions.ObjectPool.CircuitBreaker.CircuitBreaker(config);

        // Record 2 failures that will expire outside FailureWindow
        for (int i = 0; i < 2; i++)
        {
            try { breaker.Execute<int>(() => throw new InvalidOperationException()); }
            catch { }
        }

        await Task.Delay(150);

        // Now record 3 successes and 1 failure inside the new window (25% failure rate < 50%)
        for (int i = 0; i < 3; i++)
        {
            breaker.Execute(() => 1);
        }
        try { breaker.Execute<int>(() => throw new InvalidOperationException()); }
        catch { }

        // Circuit should remain closed because the old failures aged out of the window
        Assert.Equal(CircuitState.Closed, breaker.GetStatistics().State);

        // Trip circuit and verify TimeUntilRetry reports remaining duration <= OpenDuration
        breaker.Trip();
        var remaining = breaker.GetStatistics().TimeUntilRetry;
        Assert.NotNull(remaining);
        Assert.True(remaining.Value > TimeSpan.FromSeconds(1) && remaining.Value <= TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ScopedPoolManager_GetStatisticsAndDisposeAsync_ReportAccurateObjectCounts()
    {
        var disposedCount = 0;
        await using var manager = new ScopedPoolManager<DisposableResource>(
            _ => new DynamicObjectPool<DisposableResource>(
                () => new DisposableResource(() => Interlocked.Increment(ref disposedCount))));

        var scope1 = PoolScope.FromTenant("t1");
        var scope2 = PoolScope.FromTenant("t2");

        using (manager.GetObjectForScope(scope1)) { }
        using (manager.GetObjectForScope(scope2)) { }

        var stats = manager.GetStatistics();
        Assert.Equal(2, stats.TotalObjects);
        Assert.Equal(1.0, stats.AverageObjectsPerScope);
    }

    [Fact]
    public async Task DependencyInjection_Extensions_ApplyEvictionHealthChecksAndScoping()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddObjectPool<Car>(builder => builder
            .WithFactory(() => new Car("Ford", "Focus"))
            .WithInitialObjects(new Car("Ford", "Initial"))
            .WithFifoPolicy()
            .WithHealthChecks()
            .WithCustomScoping(() => PoolScope.FromTenant("t1")));

        services.WithTimeToLive<Car>(TimeSpan.FromMinutes(5));

        var provider = services.BuildServiceProvider();
        var pool = provider.GetRequiredService<IObjectPool<Car>>();
        var scopedManager = provider.GetRequiredService<ScopedPoolManager<Car>>();
        var healthCheckService = provider.GetRequiredService<HealthCheckService>();

        Assert.NotNull(pool);
        Assert.NotNull(scopedManager);

        var dynamicPool = Assert.IsType<DynamicObjectPool<Car>>(pool);
        Assert.NotNull(dynamicPool.GetEvictionStatistics());

        var healthReport = await healthCheckService.CheckHealthAsync();
        Assert.Equal(HealthStatus.Healthy, healthReport.Status);
        Assert.Contains("objectpool_car", healthReport.Entries.Keys);
    }

    private sealed class DisposableResource(Action? onDispose) : IDisposable
    {
        public bool IsValid { get; set; } = true;
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                onDispose?.Invoke();
            }
        }
    }
}
