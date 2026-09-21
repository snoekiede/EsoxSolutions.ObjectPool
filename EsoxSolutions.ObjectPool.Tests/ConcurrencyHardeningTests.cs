using EsoxSolutions.ObjectPool.Eviction;
using EsoxSolutions.ObjectPool.Models;
using EsoxSolutions.ObjectPool.Pools;

namespace EsoxSolutions.ObjectPool.Tests;

public class ConcurrencyHardeningTests
{
    [Fact]
    public async Task DynamicPool_EnforcesMaxActiveObjectsUnderConcurrency()
    {
        var configuration = new PoolConfiguration { MaxActiveObjects = 1 };
        var pool = new DynamicObjectPool<TestResource>(() => new TestResource(), configuration);
        var successes = 0;
        var tasks = Enumerable.Range(0, 32)
            .Select(_ => Task.Run(() =>
            {
                try
                {
                    using var model = pool.GetObject();
                    Interlocked.Increment(ref successes);
                    Thread.Sleep(5);
                }
                catch (InvalidOperationException)
                {
                }
            }))
            .ToArray();

        await Task.WhenAll(tasks);

        Assert.Equal(1, successes);
        Assert.Equal(0, pool.Statistics.CurrentActiveObjects);
    }

    [Fact]
    public void PoolTracksDistinctReferenceInstancesWithEqualValues()
    {
        var first = new EqualResource("same");
        var second = new EqualResource("same");
        var pool = new ObjectPool<EqualResource>([first, second], new PoolConfiguration { MaxActiveObjects = 2 });

        using var firstModel = pool.GetObject();
        using var secondModel = pool.GetObject();

        Assert.Equal(2, pool.Statistics.CurrentActiveObjects);
        Assert.NotSame(firstModel.Unwrap(), secondModel.Unwrap());
    }

    [Fact]
    public void PoolModelBecomesTerminalWhenReturnFails()
    {
        var pool = new ObjectPool<TestResource>([new TestResource()]);
        var model = pool.GetObject();
        pool.Dispose();

        Assert.Throws<ObjectDisposedException>(() => model.Dispose());
        Assert.Throws<ObjectDisposedException>(() => model.Unwrap());
        model.Dispose();
    }

    [Fact]
    public void EvictionMetadataUpdatesAreSafeUnderConcurrency()
    {
        var observedAccessCount = 0;
        var manager = new EvictionManager<TestResource>(new EvictionConfiguration
        {
            Policy = EvictionPolicy.IdleTimeout,
            IdleTimeout = TimeSpan.FromHours(1),
            CustomEvictionPredicate = (_, metadata) =>
            {
                observedAccessCount = metadata.AccessCount;
                return false;
            }
        });
        var resource = new TestResource();
        manager.TrackObject(resource);

        Parallel.For(0, 1000, _ =>
        {
            manager.RecordAccess(resource);
            manager.RecordReturn(resource);
        });

        Assert.False(manager.ShouldEvict(resource));
        Assert.Equal(1000, observedAccessCount);
    }

    private sealed class TestResource
    {
    }

    private sealed class EqualResource(string id)
    {
        public string Id { get; } = id;

        public override bool Equals(object? obj) => obj is EqualResource other && Id == other.Id;
        public override int GetHashCode() => Id.GetHashCode(StringComparison.Ordinal);
    }
}
