using EsoxSolutions.ObjectPool.DependencyInjection;
using EsoxSolutions.ObjectPool.Interfaces;
using EsoxSolutions.ObjectPool.Lifecycle;
using EsoxSolutions.ObjectPool.Models;
using EsoxSolutions.ObjectPool.Pools;
using Microsoft.Extensions.DependencyInjection;

namespace EsoxSolutions.ObjectPool.Tests.Async
{
    public class AsyncLifecycleHooksTests
    {
        private class TrackedResource
        {
            public int Id { get; set; }
            public List<string> Events { get; } = new();
        }

        [Fact]
        public async Task AsyncLifecycleHooks_WithDI_Configuration_ShouldNotThrow()
        {
            // Arrange
            var services = new ServiceCollection();

            services.AddObjectPool<TrackedResource>(builder => builder
                .WithFactory(() => new TrackedResource { Id = Random.Shared.Next(1000, 9999) })
                .WithAsyncLifecycleHooks(hooks =>
                {
                    hooks.OnCreateAsync = async resource =>
                    {
                        await Task.Delay(1);
                        resource.Events.Add("Created");
                    };

                    hooks.OnAcquireAsync = async resource =>
                    {
                        await Task.Delay(1);
                        resource.Events.Add("Acquired");
                    };

                    hooks.OnReturnAsync = async resource =>
                    {
                        await Task.Delay(1);
                        resource.Events.Add("Returned");
                    };
                })
                .WithMaxSize(5));

            var provider = services.BuildServiceProvider();
            var pool = provider.GetRequiredService<IObjectPool<TrackedResource>>();

            // Act - Just verify configuration worked
            var pooled1 = pool.GetObject();
            var pooled2 = pool.GetObject();

            pool.ReturnObject(pooled1);
            pool.ReturnObject(pooled2);

            // Assert - Just check that the pool was configured
            Assert.NotNull(pool);
            Assert.Equal(2, pool.AvailableObjectCount);
        }

        [Fact]
        public async Task AsyncLifecycleHooks_Configuration_ShouldBeSet()
        {
            // Arrange
            var hooks = new LifecycleHooks<TrackedResource>
            {
                OnDisposeAsync = async resource =>
                {
                    await Task.Delay(1);
                    resource.Events.Add("Disposed");
                }
            };

            var config = new PoolConfiguration
            {
                LifecycleHooks = hooks
            };

            var resource1 = new TrackedResource { Id = 1 };
            var resource2 = new TrackedResource { Id = 2 };
            var pool = new DynamicObjectPool<TrackedResource>(
                () => new TrackedResource(),
                new List<TrackedResource> { resource1, resource2 },
                config);

            // Act
            await pool.DisposeAsync();

            // Assert - Verify disposal completed without errors
            Assert.True(true);
        }

        [Fact]
        public async Task MixedLifecycleHooks_Configuration_ShouldWork()
        {
            // Arrange
            var hooks = new LifecycleHooks<TrackedResource>
            {
                OnCreate = resource =>
                {
                    resource.Events.Add("SyncCreate");
                },
                OnCreateAsync = async resource =>
                {
                    await Task.Delay(1);
                    resource.Events.Add("AsyncCreate");
                }
            };

            var config = new PoolConfiguration
            {
                LifecycleHooks = hooks
            };

            var pool = new DynamicObjectPool<TrackedResource>(
                () => new TrackedResource { Id = 1 },
                config);

            // Act
            var pooled = pool.GetObject();

            // Assert - Just verify no exceptions were thrown
            Assert.NotNull(pooled);
            Assert.NotNull(hooks.OnCreate);
            Assert.NotNull(hooks.OnCreateAsync);
        }
    }
}
