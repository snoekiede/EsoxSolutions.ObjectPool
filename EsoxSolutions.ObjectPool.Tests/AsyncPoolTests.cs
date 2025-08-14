using EsoxSolutions.ObjectPool.Pools;

namespace EsoxSolutions.ObjectPool.Tests
{
    public class AsyncPoolTests
    {
        [Fact]
        public async Task TestAsyncRetrieval()
        {
            // Arrange
            var initialObjects = new List<int> { 1, 2, 3 };
            var pool = new ObjectPool<int>(initialObjects);
            
            // Act
            var model = await pool.GetObjectAsync();
            
            // Assert
            Assert.NotNull(model);
            
            // Cleanup
            model.Dispose();
        }
        
        [Fact]
        public async Task TestAsyncWithTimeout()
        {
            // Arrange
            var initialObjects = new List<int> { 1 };
            var pool = new ObjectPool<int>(initialObjects);
            
            // Get the only object so the pool is empty
            using var obj = pool.GetObject();
            
            // Act & Assert
            await Assert.ThrowsAsync<TimeoutException>(async () => 
                await pool.GetObjectAsync(TimeSpan.FromMilliseconds(50)));
        }
        
        [Fact]
        public async Task TestAsyncCancellation()
        {
            // Arrange
            var initialObjects = new List<int> { 1 };
            var pool = new ObjectPool<int>(initialObjects);
            
            // Get the only object so the pool is empty
            using var obj = pool.GetObject();
            
            // Act & Assert
            var cts = new CancellationTokenSource();
            await cts.CancelAsync();
            await Assert.ThrowsAsync<OperationCanceledException>(async () => 
                await pool.GetObjectAsync(cancellationToken: cts.Token));
        }
    }
}