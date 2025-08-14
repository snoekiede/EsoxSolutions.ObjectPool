using EsoxSolutions.ObjectPool.Exceptions;
using EsoxSolutions.ObjectPool.Models;
using EsoxSolutions.ObjectPool.Pools;
using EsoxSolutions.ObjectPool.Tests.Models;

namespace EsoxSolutions.ObjectPool.Tests
{
    public class ProductionFeaturesTests
    {
        [Fact]
        public void TestPoolConfiguration()
        {
            var config = new PoolConfiguration
            {
                MaxPoolSize = 5,
                MaxActiveObjects = 3,
                ValidateOnReturn = true,
                ValidationFunction = _ => true
            };

            var initialObjects = new List<int> { 1, 2, 3, 4, 5 };
            var pool = new ObjectPool<int>(initialObjects, config);

            Assert.Equal(5, pool.AvailableObjectCount);
        }

        [Fact]
        public void TestHealthMonitoring()
        {
            var initialObjects = new List<int> { 1, 2, 3 };
            var pool = new ObjectPool<int>(initialObjects);

            // Pool should be healthy initially
            Assert.True(pool.IsHealthy);

            var healthStatus = pool.GetHealthStatus();
            Assert.True(healthStatus.IsHealthy);
            Assert.True(healthStatus.WarningCount == 0);
            Assert.Contains("healthy", healthStatus.HealthMessage?.ToLower() ?? "");

            // Get all objects to make pool unhealthy
            var obj1 = pool.GetObject();
            var obj2 = pool.GetObject();
            var obj3 = pool.GetObject();

            // Pool should now be unhealthy (no available objects)
            Assert.False(pool.IsHealthy);

            var unhealthyStatus = pool.GetHealthStatus();
            Assert.False(unhealthyStatus.IsHealthy);
            Assert.True(unhealthyStatus.WarningCount > 0);

            // Clean up
            obj1.Dispose();
            obj2.Dispose();
            obj3.Dispose();
        }

        [Fact]
        public void TestMetricsExport()
        {
            var initialObjects = new List<int> { 1, 2, 3 };
            var pool = new ObjectPool<int>(initialObjects);

            // Perform some operations
            using var obj1 = pool.GetObject();
            using var obj2 = pool.GetObject();

            var metrics = pool.ExportMetrics();

            Assert.Contains("pool_objects_retrieved_total", metrics.Keys);
            Assert.Contains("pool_objects_active_current", metrics.Keys);
            Assert.Contains("pool_objects_available_current", metrics.Keys);
            Assert.Contains("pool_health_status", metrics.Keys);

            Assert.Equal(2L, metrics["pool_objects_retrieved_total"]);
            Assert.Equal(2, metrics["pool_objects_active_current"]);
            Assert.Equal(1, metrics["pool_objects_available_current"]);
        }

        

        [Fact]
        public void TestMetricsReset()
        {
            var initialObjects = new List<int> { 1, 2, 3 };
            var pool = new ObjectPool<int>(initialObjects);

            // Perform operations
            using var obj1 = pool.GetObject();
            using var obj2 = pool.GetObject();

            var metrics = pool.ExportMetrics();
            Assert.Equal(2L, metrics["pool_objects_retrieved_total"]);

            // Reset metrics
            pool.ResetMetrics();

            var resetMetrics = pool.ExportMetrics();
            Assert.Equal(0L, resetMetrics["pool_objects_retrieved_total"]);
        }

        [Fact]
        public void TestPoolConfigurationLimits()
        {
            var config = new PoolConfiguration
            {
                MaxActiveObjects = 2
            };

            var initialObjects = new List<int> { 1, 2, 3 };
            var pool = new ObjectPool<int>(initialObjects, config);

            // Should be able to get 2 objects
            var obj1 = pool.GetObject();
            var obj2 = pool.GetObject();

            // Third attempt should fail due to limit
            Assert.Throws<InvalidOperationException>(() => pool.GetObject());

            // Clean up
            obj1.Dispose();
            obj2.Dispose();
        }

        [Fact]
        public void TestTryGetObjectWithLimits()
        {
            var config = new PoolConfiguration
            {
                MaxActiveObjects = 1
            };

            var initialObjects = new List<int> { 1, 2 };
            var pool = new ObjectPool<int>(initialObjects, config);

            // First should succeed
            Assert.True(pool.TryGetObject(out var obj1));
            Assert.NotNull(obj1);

            // Second should fail due to limit
            Assert.False(pool.TryGetObject(out var obj2));
            Assert.Null(obj2);

            // Clean up
            obj1.Dispose();
        }

        [Fact]
        public void TestUtilizationPercentage()
        {
            var config = new PoolConfiguration
            {
                MaxActiveObjects = 4,
                MaxPoolSize = 4
            };

            var initialObjects = new List<int> { 1, 2, 3, 4 };
            var pool = new ObjectPool<int>(initialObjects, config);

            // Initially 0% utilization
            Assert.Equal(0.0, pool.UtilizationPercentage);

            // Get 2 objects = 50% utilization
            var obj1 = pool.GetObject();
            var obj2 = pool.GetObject();

            Assert.Equal(50.0, pool.UtilizationPercentage);

            // Clean up
            obj1.Dispose();
            obj2.Dispose();
        }

        [Fact]
        public void TestDisposal()
        {
            var initialObjects = new List<int> { 1, 2, 3 };
            var pool = new ObjectPool<int>(initialObjects);

            pool.Dispose();

            // Should throw after disposal
            Assert.Throws<ObjectDisposedException>(() => pool.GetObject());
            Assert.False(pool.TryGetObject(out _));
        }

        [Fact]
        public async Task TestAsyncTimeoutWithConfiguration()
        {
            var config = new PoolConfiguration
            {
                DefaultTimeout = TimeSpan.FromMilliseconds(100)
            };

            var initialObjects = new List<int> { 1 };
            var pool = new ObjectPool<int>(initialObjects, config);

            // Get the only object
            using var obj1 = pool.GetObject();

            // This should time out quickly due to configuration
            await Assert.ThrowsAsync<TimeoutException>(async () =>
            {
                await pool.GetObjectAsync(); // Uses default timeout from config
            });
        }

        [Fact]
        public void TestQueryablePoolEmptyResults()
        {
            // Test QueryableObjectPool returning no results
            var initialObjects = Car.GetInitialCars();
            var pool = new QueryableObjectPool<Car>(initialObjects);

            // Try to get a car that doesn't exist
            Assert.Throws<NoObjectsInPoolException>(() => 
                pool.GetObject(car => car.Make == "Toyota"));
        }

        [Fact]
        public void TestTryQueryablePoolEmptyResults()
        {
            // Test TryGetObject with query returning false
            var initialObjects = Car.GetInitialCars();
            var pool = new QueryableObjectPool<Car>(initialObjects);

            bool result = pool.TryGetObject(car => car.Make == "Toyota", out var model);
            
            Assert.False(result);
            Assert.Null(model);
        }

        [Fact]
        public void TestDynamicPoolFactoryCreation()
        {
            // Test that DynamicObjectPool creates objects when needed
            int factoryCallCount = 0;
            var factory = new Func<Car>(() => {
                factoryCallCount++;
                return new Car("Ford", "DynamicCar");
            });

            var pool = new DynamicObjectPool<Car>(factory);
            Assert.Equal(0, pool.AvailableObjectCount); // Empty at start

            // Getting an object should use the factory
            using var obj = pool.GetObject();
            Assert.Equal("Ford", obj.Unwrap().Make);
            Assert.Equal("DynamicCar", obj.Unwrap().Model);
            Assert.Equal(1, factoryCallCount);
        }

        [Fact]
        public void TestMaxPoolSizeLimit()
        {
            // Test MaxPoolSize limiting returned objects
            var config = new PoolConfiguration
            {
                MaxPoolSize = 2
            };

            // Create a pool with initial count = MaxPoolSize
            var initialObjects = new List<int> { 1, 2 };
            var pool = new ObjectPool<int>(initialObjects, config);
            
            // First, empty the pool
            var obj1 = pool.GetObject();
            var obj2 = pool.GetObject();
            
            // Pool should be empty now
            Assert.Equal(0, pool.AvailableObjectCount);
            
            // Return obj1 to the pool
            obj1.Dispose();
            
            // Pool should have 1 object now
            Assert.Equal(1, pool.AvailableObjectCount);
            
            // Get a new object from the pool
            var obj3 = pool.GetObject();
            
            // Pool should be empty again
            Assert.Equal(0, pool.AvailableObjectCount);
            
            // Return both objects
            obj2.Dispose();
            obj3.Dispose();
            
            // Pool should have MaxPoolSize objects
            Assert.Equal(2, pool.AvailableObjectCount);
            
            // Get and dispose objects multiple times to ensure pool size remains limited
            for (int i = 0; i < 5; i++)
            {
                var tempObj = pool.GetObject();
                tempObj.Dispose();
            }
            
            // Pool size should still be MaxPoolSize
            Assert.Equal(2, pool.AvailableObjectCount);
        }

        [Fact]
        public void TestValidationOnReturn()
        {
            // Test validation function on return
            var config = new PoolConfiguration
            {
                ValidateOnReturn = true,
                ValidationFunction = obj => (int)obj > 5 // Only allow values > 5
            };

            var initialObjects = new List<int> { 10 }; // Valid object
            var pool = new ObjectPool<int>(initialObjects, config);

            // Get the object and return it
            var obj = pool.GetObject();
            obj.Dispose();
            
            // Object should pass validation and be returned to pool
            Assert.Equal(1, pool.AvailableObjectCount);

            // Use GetObject to get a valid object from the pool
            var validObj = pool.GetObject();
            
            // Make sure we have the expected object
            Assert.Equal(10, validObj.Unwrap());
            
            // Return it
            validObj.Dispose();
            
            // Object should pass validation and be returned to pool
            Assert.Equal(1, pool.AvailableObjectCount);
        }

        [Fact]
        public void TestExportMetricsWithTags()
        {
            // Test exporting metrics with tags
            var pool = new ObjectPool<int>([1, 2, 3]);
            
            var tags = new Dictionary<string, string>
            {
                ["environment"] = "test",
                ["service"] = "unit-tests"
            };
            
            var metrics = pool.ExportMetrics(tags);
            
            // Check tags were included
            Assert.Equal("test", metrics["tag_environment"]);
            Assert.Equal("unit-tests", metrics["tag_service"]);
        }

        [Fact]
        public async Task TestAsyncCancellation()
        {
            // Test cancellation in GetObjectAsync
            var pool = new ObjectPool<int>([1]);
            
            // Get the only object to make pool empty
            using var obj = pool.GetObject();
            
            // Create a cancellation token and cancel it
            var cts = new CancellationTokenSource();
            await cts.CancelAsync();
            
            // This should throw due to cancellation
            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
                await pool.GetObjectAsync(cancellationToken: cts.Token));
        }

        [Fact]
        public async Task TestAsyncEventualSuccess()
        {
            // Test GetObjectAsync eventually succeeding
            var pool = new ObjectPool<int>([1]);
            
            // Get the only object
            var obj = pool.GetObject();
            
            // Start a task to get an object with enough timeout
            var getTask = Task.Run(async () => await pool.GetObjectAsync(TimeSpan.FromSeconds(2)));
            
            // Wait a bit then return the first object
            await Task.Delay(100);
            obj.Dispose();
            
            // The task should complete successfully now
            var result = await getTask;
            Assert.NotNull(result);
            result.Dispose();
        }

        [Fact]
        public void TestPoolHealthStatusDetails()
        {
            // Test health status diagnostic details
            var pool = new ObjectPool<int>([1, 2, 3]);
            
            // Get all objects to generate warnings
            var obj1 = pool.GetObject();
            var obj2 = pool.GetObject();
            var obj3 = pool.GetObject();
            
            var status = pool.GetHealthStatus();
            
            // Check diagnostics contains expected data
            Assert.Contains("CurrentActive", status.Diagnostics.Keys);
            Assert.Contains("CurrentAvailable", status.Diagnostics.Keys);
            Assert.Contains("TotalRetrieved", status.Diagnostics.Keys);
            
            // Check warnings are populated
            Assert.True(status.WarningCount > 0);
            Assert.NotEmpty(status.Warnings);
            
            // Clean up
            obj1.Dispose();
            obj2.Dispose();
            obj3.Dispose();
        }
    }
}
