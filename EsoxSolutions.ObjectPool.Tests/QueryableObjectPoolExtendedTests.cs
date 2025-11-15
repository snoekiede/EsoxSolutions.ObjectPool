using EsoxSolutions.ObjectPool.Exceptions;
using EsoxSolutions.ObjectPool.Models;
using EsoxSolutions.ObjectPool.Pools;
using EsoxSolutions.ObjectPool.Tests.Models;
using Microsoft.Extensions.Logging;
using Moq;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace EsoxSolutions.ObjectPool.Tests
{
    public class QueryableObjectPoolExtendedTests
    {
        [Fact]
        public void TestDisposalBehavior()
        {
            // Arrange
            var initialObjects = new List<Car>(Car.GetInitialCars());
            var pool = new QueryableObjectPool<Car>(initialObjects);
            
            // Act
            pool.Dispose();
            
            // Assert
            Assert.Throws<ObjectDisposedException>(() => pool.GetObject());
            Assert.Throws<ObjectDisposedException>(() => pool.GetObject(c => c.Make == "Ford"));
            Assert.False(pool.TryGetObject(out _));
            Assert.False(pool.TryGetObject(c => c.Make == "Ford", out _));
        }
        
        [Fact]
        public async Task TestAsyncOperationsWithDisposal()
        {
            // Arrange
            var initialObjects = new List<Car>(Car.GetInitialCars());
            var pool = new QueryableObjectPool<Car>(initialObjects);
            
            // Act
            pool.Dispose();
            
            // Assert
            await Assert.ThrowsAsync<ObjectDisposedException>(async () => 
                await pool.GetObjectAsync());
                
            await Assert.ThrowsAsync<ObjectDisposedException>(async () => 
                await pool.GetObjectAsync(c => c.Make == "Ford"));
        }
        
        [Fact]
        public void TestPoolHealthImplementation()
        {
            // Arrange
            var initialObjects = Car.GetInitialCars();
            
            // Create a configuration with higher max capacity to ensure health
            var config = new PoolConfiguration
            {
                MaxPoolSize = 100,        // Set high to avoid hitting pool size limit
                MaxActiveObjects = 100    // Set high to avoid hitting active object limit
            };
            
            var pool = new QueryableObjectPool<Car>(initialObjects, config);
            
            // Act - Check initial health
            var isHealthy = pool.IsHealthy;
            var utilization = pool.UtilizationPercentage;
            var healthStatus = pool.GetHealthStatus();
            
            // Assert
            Assert.True(isHealthy);
            Assert.Equal(0.0, utilization, 0.1); // No objects are active yet
            Assert.True(healthStatus.IsHealthy);
            Assert.Equal(0, healthStatus.WarningCount);
            Assert.Contains(healthStatus.Diagnostics, d => d.Key == "CurrentAvailable" && (int)d.Value == initialObjects.Count);
            
            // Act - Get some objects to change health metrics
            var models = new List<PoolModel<Car>>();
            for (int i = 0; i < 5; i++)
            {
                models.Add(pool.GetObject());
            }
            
            // Get updated health status
            utilization = pool.UtilizationPercentage;
            healthStatus = pool.GetHealthStatus();
            
            // With our high capacity settings, the pool should still be healthy
            // The default utilization threshold for unhealthy is 95%, and we're using 5 out of 100 (5%)
            Assert.True(pool.IsHealthy, "Pool should be healthy with low utilization");
            Assert.True(utilization > 0);
            Assert.True(utilization < 10.0, $"Utilization should be low (<10%) but was {utilization}%");
            Assert.Equal(5, healthStatus.Diagnostics["CurrentActive"]);
            Assert.Equal(2, healthStatus.Diagnostics["CurrentAvailable"]);
            
            // Clean up
            foreach (var model in models)
            {
                model.Dispose();
            }
        }
        
        [Fact]
        public void TestPoolHealthWarningConditions()
        {
            // Arrange - Create a pool with low capacity limits to trigger warnings
            var initialObjects = Car.GetInitialCars(); // 7 cars
            var config = new PoolConfiguration
            {
                MaxPoolSize = 7,        
                MaxActiveObjects = 7    
            };
            
            var pool = new QueryableObjectPool<Car>(initialObjects, config);
            
            // Act - Get 6 out of 7 objects to create high utilization
            var models = new List<PoolModel<Car>>();
            for (int i = 0; i < 6; i++)
            {
                models.Add(pool.GetObject());
            }
            
            // Get health status with high utilization
            var healthStatus = pool.GetHealthStatus();
            var utilization = pool.UtilizationPercentage;
            
            // Assert - Should have warnings but still be healthy
            Assert.True(utilization > 80.0, $"Utilization should be high (>80%) but was {utilization}%");
            Assert.True(healthStatus.WarningCount > 0, "Pool should have warnings at high utilization");
            Assert.Contains(healthStatus.Warnings, w => w.Contains("High utilization"));
            
            // The pool is still healthy because utilization is below 95% (critical threshold)
            Assert.True(pool.IsHealthy, "Pool should still be healthy at high but not critical utilization");
            
            // Clean up
            foreach (var model in models)
            {
                model.Dispose();
            }
        }
        
        [Fact]
        public void TestPoolMetricsImplementation()
        {
            // Arrange
            var initialObjects = Car.GetInitialCars();
            var pool = new QueryableObjectPool<Car>(initialObjects);
            
            // Act - Get initial metrics
            var initialMetrics = pool.ExportMetrics();
            
            // Assert initial state
            Assert.Equal(0L, initialMetrics["pool_objects_retrieved_total"]);
            Assert.Equal(0L, initialMetrics["pool_objects_returned_total"]);
            Assert.Equal(initialObjects.Count, initialMetrics["pool_objects_available_current"]);
            Assert.Equal(0, initialMetrics["pool_objects_active_current"]);
            Assert.Equal("queryable", initialMetrics["pool_type"]); // Check the custom metric
            
            // Act - Perform some operations to generate metrics
            var models = new List<PoolModel<Car>>();
            for (int i = 0; i < 3; i++)
            {
                models.Add(pool.GetObject());
            }
            
            models[0].Dispose(); // Return one object
            
            // Get updated metrics
            var updatedMetrics = pool.ExportMetrics();
            
            // Assert
            Assert.Equal(3L, updatedMetrics["pool_objects_retrieved_total"]);
            Assert.Equal(1L, updatedMetrics["pool_objects_returned_total"]);
            Assert.Equal(initialObjects.Count - 2, updatedMetrics["pool_objects_available_current"]);
            Assert.Equal(2, updatedMetrics["pool_objects_active_current"]);
            
            // Clean up
            foreach (var model in models.Skip(1)) // Skip the first one which was already disposed
            {
                model.Dispose();
            }
            
            // Act - Reset metrics
            pool.ResetMetrics();
            var resetMetrics = pool.ExportMetrics();
            
            // Assert reset state
            Assert.Equal(0L, resetMetrics["pool_objects_retrieved_total"]);
            Assert.Equal(0L, resetMetrics["pool_objects_returned_total"]);
        }
        
        [Fact]
        public void TestMetricsWithTags()
        {
            // Arrange
            var initialObjects = Car.GetInitialCars();
            var pool = new QueryableObjectPool<Car>(initialObjects);
            
            // Act - Export with tags
            var tags = new Dictionary<string, string>
            {
                ["service"] = "query-service",
                ["component"] = "car-pool"
            };
            
            var metrics = pool.ExportMetrics(tags);
            
            // Assert
            Assert.Equal("query-service", metrics["tag_service"]);
            Assert.Equal("car-pool", metrics["tag_component"]);
        }
        
        [Fact]
        public async Task TestQueryablePoolAsyncWithTimeoutAndCancellation()
        {
            // Arrange
            var initialObjects = new List<Car> { new("Ford", "Focus") };
            var pool = new QueryableObjectPool<Car>(initialObjects);
            
            // Get the only object so the pool is empty
            using var obj = pool.GetObject();
            
            // Act & Assert - Test with timeout
            var timeout = TimeSpan.FromMilliseconds(100);
            await Assert.ThrowsAsync<TimeoutException>(async () => 
                await pool.GetObjectAsync(timeout));
                
            // Test with cancellation
            var cts = new CancellationTokenSource();
            await cts.CancelAsync();
            await Assert.ThrowsAsync<OperationCanceledException>(async () => 
                await pool.GetObjectAsync(cancellationToken: cts.Token));
                
            // Test query-based async with timeout (use fresh token, not canceled one)
            await Assert.ThrowsAsync<TimeoutException>(async () => 
                await pool.GetObjectAsync(c => c.Make == "Ford", TimeSpan.FromMilliseconds(100)));
                
            // Test query-based async with cancellation (create new CancellationTokenSource)
            cts = new CancellationTokenSource();
            await cts.CancelAsync();
            await Assert.ThrowsAsync<OperationCanceledException>(async () => 
                await pool.GetObjectAsync(c => c.Make == "Ford", cancellationToken: cts.Token));
        }
        
        [Fact]
        public async Task TestQueryableAsyncEventualSuccess()
        {
            // Arrange - Create a pool with two cars
            var initialObjects = new List<Car> 
            { 
                new("Ford", "Focus"),
                new("Ford", "Mustang")
            };
            var pool = new QueryableObjectPool<Car>(initialObjects);
            
            // Get the first object
            var obj = pool.GetObject();
            
            // Start tasks to get objects (will wait)
            var getTask = Task.Run(async () => await pool.GetObjectAsync(TimeSpan.FromSeconds(5)));
            
            // Allow the getTask to complete by returning the first object
            await Task.Delay(100);
            obj.Dispose();
            
            // Wait for the first task to complete
            var result1 = await getTask;
            
            // Now the pool is empty again, start a task that uses a query
            var queryTask = Task.Run(async () => await pool.GetObjectAsync(c => c.Make == "Ford", TimeSpan.FromSeconds(5)));
            
            // Return the first result so the second task can complete
            await Task.Delay(100);
            result1.Dispose();
            
            // Get the result of the query task
            var result2 = await queryTask;
            
            // Assert
            Assert.NotNull(result1);
            Assert.NotNull(result2);
            Assert.Equal("Ford", result2.Unwrap().Make);
            
            // Clean up
            result2.Dispose();
        }
        
        [Fact]
        public void TestMassiveParallelAccess()
        {
            // Arrange - Create a large pool
            var initialObjects = new List<Car>();
            for (int i = 0; i < 100; i++)
            {
                initialObjects.Add(new Car($"Make{i % 10}", $"Model{i}"));
            }
            var pool = new QueryableObjectPool<Car>(initialObjects);
            
            // Act - Run many parallel tasks
            var tasks = new List<Task>();
            var exceptions = new ConcurrentBag<Exception>();
            var successCount = 0;
            var failureCount = 0;
            
            for (int i = 0; i < 500; i++)
            {
                int makeIndex = i % 10;
                tasks.Add(Task.Run(() =>
                {
                    try
                    {
                        // Try to get an object with the current make index
                        if (pool.TryGetObject(c => c.Make == $"Make{makeIndex}", out var model))
                        {
                            Interlocked.Increment(ref successCount);
                            // Simulate work
                            Thread.Sleep(5);
                            model?.Dispose();
                        }
                        else
                        {
                            Interlocked.Increment(ref failureCount);
                        }
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                }));
            }
            
            // Wait for all tasks to complete
            Task.WaitAll(tasks.ToArray());
            
            // Assert
            Assert.Empty(exceptions); // No exceptions should be thrown
            Assert.Equal(100, pool.AvailableObjectCount); // All objects should be returned
            Assert.True(successCount > 0); // Some tasks should succeed
            Assert.True(failureCount < 500); // Not all tasks should fail
            
            // The sum of success and failure should equal the total number of tasks
            Assert.Equal(500, successCount + failureCount);
            
            // Check health after massive usage
            var health = pool.GetHealthStatus();
            Assert.True(health.IsHealthy);
        }
        
        [Fact]
        public void TestLoggingInQueryableObjectPool()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<QueryableObjectPool<Car>>>();
            var initialObjects = Car.GetInitialCars();
            
            // Act
            var pool = new QueryableObjectPool<Car>(initialObjects, null, loggerMock.Object);
            using var obj = pool.GetObject();
            
            // Assert - Verify log was called for initialization
            loggerMock.Verify(
                x => x.Log(
                    It.IsAny<LogLevel>(),
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("ObjectPool created")),
                    It.IsAny<Exception?>(),
                    It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
                Times.Once);
                
            // Verify log was called for object retrieval
            loggerMock.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Attempting to get object from pool")),
                    It.IsAny<Exception?>(),
                    It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
                Times.Once);
        }
        
        [Fact]
        public void TestQueryablePoolWithValidation()
        {
            // Arrange
            var config = new PoolConfiguration
            {
                ValidateOnReturn = true,
                ValidationFunction = obj => ((Car)obj).Make != "Citroen" // Reject Citroen cars
            };
            
            // Create a custom list with a known count of each type of car
            var initialObjects = new List<Car>
            {
                new("Ford", "Focus"),    // 0 - Should be kept after validation
                new("Ford", "Mustang"),  // 1 - Should be kept after validation
                new("Ford", "Fiesta"),   // 2 - Should be kept after validation
                new("Citroen", "C1"),    // 3 - Should be rejected by validation
                new("Citroen", "C2"),    // 4 - Should be rejected by validation 
            };
            
            var pool = new QueryableObjectPool<Car>(initialObjects, config);
            
            // Act - Get a Citroen car and return it
            var citroen = pool.GetObject(c => c.Make == "Citroen");
            citroen.Dispose(); // This should validate and reject the car
            
            // Get a Ford car and return it
            var ford = pool.GetObject(c => c.Make == "Ford");
            ford.Dispose(); // This should validate and keep the car
            
            // Assert
            // We should have 3 Fords (kept after validation) and 1 remaining Citroen
            Assert.Equal(4, pool.AvailableObjectCount); // 3 Fords + 1 remaining Citroen
            
            // Try to get another Citroen (should still work because we only rejected one)
            var anotherCitroen = pool.GetObject(c => c.Make == "Citroen");
            anotherCitroen.Dispose(); // This will be rejected too
            
            // Now only the 3 Fords remain
            Assert.Equal(3, pool.AvailableObjectCount);
            
            // Now try to get another Citroen (should fail as they were all rejected)
            Assert.Throws<NoObjectsInPoolException>(() => 
                pool.GetObject(c => c.Make == "Citroen"));
        }
        
        [Fact]
        public void TestObjectDisposalDuringPoolDisposal()
        {
            // Arrange - Create a special Car class that tracks disposal
            var disposableCarCount = 0;
            
            var initialObjects = new List<DisposableCar>
            {
                new(() => Interlocked.Increment(ref disposableCarCount)),
                new(() => Interlocked.Increment(ref disposableCarCount)),
                new(() => Interlocked.Increment(ref disposableCarCount))
            };
            
            var pool = new QueryableObjectPool<DisposableCar>(initialObjects);
            
            // Act - Dispose the pool
            pool.Dispose();
            
            // Assert - All cars should have been disposed
            Assert.Equal(3, disposableCarCount);
        }
        
        [Fact]
        public void TestRaceConditionInQueryOperation()
        {
            // Arrange
            var initialObjects = new List<Car>();
            for (int i = 0; i < 5; i++)
            {
                initialObjects.Add(new Car("Ford", $"Model{i}"));
            }
            
            var pool = new QueryableObjectPool<Car>(initialObjects);
            
            // Act - Create a race condition by having multiple threads query for Ford cars
            var tasks = new List<Task<PoolModel<Car>?>>();
            for (int i = 0; i < 20; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    try
                    {
                        return pool.TryGetObject(c => c.Make == "Ford", out var model) ? model : null;
                    }
                    catch
                    {
                        return null;
                    }
                }));
            }
            
            // Wait for all tasks to complete
            Task.WaitAll(tasks.Select(t => t as Task).ToArray());
            
            // Get results
            var results = tasks.Select(t => t.Result).Where(r => r != null).ToList();
            
            // Assert
            Assert.Equal(5, results.Count); // Only 5 Ford cars should be retrieved
            Assert.Equal(0, pool.AvailableObjectCount); // All cars should be taken
            
            // Clean up
            foreach (var result in results)
            {
                result?.Dispose();
            }
            
            // After returning, all should be available again
            Assert.Equal(5, pool.AvailableObjectCount);
        }
        
        [Fact]
        public void TestQueryablePoolPerformance()
        {
            // Only run this in DEBUG mode - skip in release builds where JIT optimizations may affect timing
            if (!Debugger.IsAttached)
            {
                return;
            }
            
            // Arrange - Create a large pool
            const int poolSize = 10000;
            var random = new Random(42); // Fixed seed for reproducibility
            var initialObjects = new List<Car>();
            
            for (int i = 0; i < poolSize; i++)
            {
                var makeIndex = random.Next(0, 10);
                var modelIndex = random.Next(0, 100);
                initialObjects.Add(new Car($"Make{makeIndex}", $"Model{modelIndex}"));
            }
            
            var pool = new QueryableObjectPool<Car>(initialObjects);
            
            // Act - Measure time for different operations
            var stopwatch = Stopwatch.StartNew();
            
            // 1. Regular GetObject
            using (pool.GetObject())
            {
                // Just get and return
            }
            var getObjectTime = stopwatch.ElapsedTicks;
            stopwatch.Restart();
            
            // 2. Query by Make
            using (var obj = pool.GetObject(c => c.Make == "Make1"))
            {
                // Just get and return
            }
            var queryByMakeTime = stopwatch.ElapsedTicks;
            stopwatch.Restart();
            
            // 3. Query by Make and Model (more selective)
            using (var obj = pool.GetObject(c => c is { Make: "Make1", Model: "Model50" }))
            {
                // Just get and return (might fail if no such car exists, that's ok)
            }
            var complexQueryTime = stopwatch.ElapsedTicks;
            
            // Assert - Complex queries should take longer
            Assert.True(getObjectTime <= queryByMakeTime);
            Assert.True(queryByMakeTime <= complexQueryTime);
            
            // Also verify that the pool is still intact
            Assert.Equal(poolSize, pool.AvailableObjectCount);
        }
    }
    
    // Helper class for testing IDisposable implementation
    public class DisposableCar(Action onDispose) : IDisposable
    {
        public string Make { get; set; } = "TestMake";
        public string Model { get; set; } = "TestModel";
        private bool _disposed;

        public void Dispose()
        {
            if (!_disposed)
            {
                onDispose?.Invoke();
                _disposed = true;
            }
        }
    }
}