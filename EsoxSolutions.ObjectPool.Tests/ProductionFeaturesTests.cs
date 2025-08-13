using EsoxSolutions.ObjectPool.Models;
using EsoxSolutions.ObjectPool.Pools;

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
        public void TestPrometheusMetrics()
        {
            var initialObjects = new List<int> { 1, 2, 3 };
            var pool = new ObjectPool<int>(initialObjects);

            using var obj = pool.GetObject();

            var prometheusMetrics = pool.ExportPrometheusMetrics("test_pool");

            Assert.Contains("# HELP test_pool_pool_objects_retrieved_total", prometheusMetrics);
            Assert.Contains("# TYPE test_pool_pool_objects_retrieved_total counter", prometheusMetrics);
            Assert.Contains("test_pool_pool_objects_retrieved_total 1", prometheusMetrics);
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
    }
}
