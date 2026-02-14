using EsoxSolutions.ObjectPool.DependencyInjection;
using EsoxSolutions.ObjectPool.Interfaces;
using EsoxSolutions.ObjectPool.Models;
using EsoxSolutions.ObjectPool.Pools;
using Microsoft.Extensions.DependencyInjection;

namespace EsoxSolutions.ObjectPool.Tests.Async
{
    public class AsyncDisposalTests
    {
        private class AsyncDisposableResource : IAsyncDisposable
        {
            public bool IsDisposed { get; private set; }
            public bool WasDisposedAsync { get; private set; }

            public async ValueTask DisposeAsync()
            {
                await Task.Delay(1); // Simulate async work
                IsDisposed = true;
                WasDisposedAsync = true;
            }
        }

        private class SyncDisposableResource : IDisposable
        {
            public bool IsDisposed { get; private set; }

            public void Dispose()
            {
                IsDisposed = true;
            }
        }

        private class BothDisposableResource : IDisposable, IAsyncDisposable
        {
            public bool IsDisposed { get; private set; }
            public bool WasDisposedAsync { get; private set; }

            public void Dispose()
            {
                IsDisposed = true;
            }

            public async ValueTask DisposeAsync()
            {
                await Task.Delay(1);
                IsDisposed = true;
                WasDisposedAsync = true;
            }
        }

        [Fact]
        public async Task DisposeAsync_WithAsyncDisposableObjects_ShouldDisposeAsync()
        {
            // Arrange
            var resource1 = new AsyncDisposableResource();
            var resource2 = new AsyncDisposableResource();
            var pool = new ObjectPool<AsyncDisposableResource>(new List<AsyncDisposableResource> { resource1, resource2 });

            // Act
            await pool.DisposeAsync();

            // Assert
            Assert.True(resource1.IsDisposed);
            Assert.True(resource1.WasDisposedAsync);
            Assert.True(resource2.IsDisposed);
            Assert.True(resource2.WasDisposedAsync);
        }

        [Fact]
        public async Task DisposeAsync_WithSyncDisposableObjects_ShouldDispose()
        {
            // Arrange
            var resource1 = new SyncDisposableResource();
            var resource2 = new SyncDisposableResource();
            var pool = new ObjectPool<SyncDisposableResource>(new List<SyncDisposableResource> { resource1, resource2 });

            // Act
            await pool.DisposeAsync();

            // Assert
            Assert.True(resource1.IsDisposed);
            Assert.True(resource2.IsDisposed);
        }

        [Fact]
        public async Task DisposeAsync_WithBothInterfaces_ShouldPreferAsync()
        {
            // Arrange
            var resource = new BothDisposableResource();
            var pool = new ObjectPool<BothDisposableResource>(new List<BothDisposableResource> { resource });

            // Act
            await pool.DisposeAsync();

            // Assert
            Assert.True(resource.IsDisposed);
            Assert.True(resource.WasDisposedAsync);
        }

        [Fact]
        public async Task DisposeAsync_WithActiveObjects_ShouldDisposeAll()
        {
            // Arrange
            var resource1 = new AsyncDisposableResource();
            var resource2 = new AsyncDisposableResource();
            var pool = new ObjectPool<AsyncDisposableResource>(new List<AsyncDisposableResource> { resource1, resource2 });

            // Get one object (make it active)
            var pooled = pool.GetObject();

            // Act
            await pool.DisposeAsync();

            // Assert - Both available and active objects should be disposed
            Assert.True(resource1.IsDisposed);
            Assert.True(resource2.IsDisposed);
        }

        [Fact]
        public async Task DisposeAsync_CalledTwice_ShouldNotThrow()
        {
            // Arrange
            var resource = new AsyncDisposableResource();
            var pool = new ObjectPool<AsyncDisposableResource>(new List<AsyncDisposableResource> { resource });

            // Act
            await pool.DisposeAsync();
            await pool.DisposeAsync(); // Call again

            // Assert
            Assert.True(resource.IsDisposed);
        }

        [Fact]
        public async Task DynamicPool_DisposeAsync_ShouldDisposeAllObjects()
        {
            // Arrange
            var createdResources = new List<AsyncDisposableResource>();
            var pool = new DynamicObjectPool<AsyncDisposableResource>(
                () =>
                {
                    var resource = new AsyncDisposableResource();
                    createdResources.Add(resource);
                    return resource;
                },
                new PoolConfiguration { MaxPoolSize = 5 });

            // Create some objects
            var pooled1 = pool.GetObject();
            var pooled2 = pool.GetObject();
            pool.ReturnObject(pooled1);
            pool.ReturnObject(pooled2);

            // Act
            await pool.DisposeAsync();

            // Assert
            Assert.All(createdResources, r => Assert.True(r.IsDisposed));
            Assert.All(createdResources, r => Assert.True(r.WasDisposedAsync));
        }

        [Fact]
        public async Task DisposeAsync_WithDI_ShouldWork()
        {
            // Arrange
            var disposedResources = new List<AsyncDisposableResource>();
            var services = new ServiceCollection();

            services.AddObjectPool<AsyncDisposableResource>(builder => builder
                .WithFactory(() => new AsyncDisposableResource())
                .WithMaxSize(5));

            var provider = services.BuildServiceProvider();
            var pool = provider.GetRequiredService<IObjectPool<AsyncDisposableResource>>();

            // Get all objects to track them
            var pooled1 = pool.GetObject();
            var pooled2 = pool.GetObject();
            var pooled3 = pool.GetObject();
            
            disposedResources.Add(pooled1.Unwrap());
            disposedResources.Add(pooled2.Unwrap());
            disposedResources.Add(pooled3.Unwrap());

            pool.ReturnObject(pooled1);
            pool.ReturnObject(pooled2);
            pool.ReturnObject(pooled3);

            // Act
            await provider.DisposeAsync();

            // Assert
            Assert.All(disposedResources, r => Assert.True(r.IsDisposed));
        }
    }
}
