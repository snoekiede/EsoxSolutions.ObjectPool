using EsoxSolutions.ObjectPool.Lifecycle;
using EsoxSolutions.ObjectPool.Models;
using EsoxSolutions.ObjectPool.Pools;
using EsoxSolutions.ObjectPool.Interfaces;

namespace EsoxSolutions.ObjectPool.Tests;

public class ProductionReadinessTests
{
    [Fact]
    public void ConfigurationRejectsInvalidValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ObjectPool<Resource>([new Resource()], new PoolConfiguration { MaxPoolSize = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ObjectPool<Resource>([new Resource()], new PoolConfiguration { MaxActiveObjects = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ObjectPool<Resource>([new Resource()], new PoolConfiguration { DefaultTimeout = TimeSpan.Zero }));
    }

    [Fact]
    public void DynamicReturnUsesOverrideThroughInterface()
    {
        var returnCalls = 0;
        var configuration = new PoolConfiguration
        {
            LifecycleHooks = new LifecycleHooks<Resource>
            {
                OnReturn = _ => Interlocked.Increment(ref returnCalls)
            }
        };
        IObjectPool<Resource> pool = new DynamicObjectPool<Resource>(() => new Resource(), configuration);

        using (pool.GetObject())
        {
        }

        Assert.Equal(1, returnCalls);
    }

    [Fact]
    public void DynamicAcquireHookFailureReleasesCapacity()
    {
        var failFirstAcquire = 1;
        var configuration = new PoolConfiguration
        {
            MaxActiveObjects = 1,
            ContinueOnLifecycleHookError = false,
            LifecycleHooks = new LifecycleHooks<Resource>
            {
                OnAcquire = _ =>
                {
                    if (Interlocked.Exchange(ref failFirstAcquire, 0) == 1)
                    {
                        throw new InvalidOperationException("acquire failure");
                    }
                }
            }
        };
        var pool = new DynamicObjectPool<Resource>(() => new Resource(), configuration);

        Assert.Throws<InvalidOperationException>(() => pool.GetObject());
        using var model = pool.GetObject();
        Assert.NotNull(model.Unwrap());
    }

    private sealed class Resource
    {
    }
}
